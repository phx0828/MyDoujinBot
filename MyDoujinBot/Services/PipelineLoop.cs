using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    public class PipelineLoopSettings
    {
        public string Token { get; init; } = string.Empty;
        public List<PipelineStage> Stages { get; init; } = new();
        public double ExtraDelaySeconds { get; init; } = 2.0;
    }

    /// <summary>
    /// 階梯行動排程迴圈控制器。
    /// 依序執行階段隊列，達成指定條件後自動平滑切換至下一階段，直到全部完成。
    /// </summary>
    public class PipelineLoop
    {
        private readonly TrainingService _trainingService;
        private readonly BattleService _battleService;
        private readonly EventService _eventService;

        public Action<string, Color>? OnLog { get; set; }
        public Action<LoopStatus>? OnStatusChanged { get; set; }
        public Action<LoopStats>? OnStatsUpdated { get; set; }
        public Action<int, int, PipelineStage>? OnStageChanged { get; set; }

        public Func<PendingEvent, CancellationToken, Task<string?>>? OnManualEventSelect { get; set; }
        public Func<EventResult, CancellationToken, Task>? OnManualEventResult { get; set; }
        public Action? OnCloseManualUi { get; set; }
        public AutoEventStrategy? AutoStrategy { get; set; } = AutoEventStrategy.HighestSuccessRate;

        public LoopStats Stats { get; } = new();

        public PipelineLoop(TrainingService trainingService, BattleService battleService, EventService eventService)
        {
            _trainingService = trainingService;
            _battleService = battleService;
            _eventService = eventService;
        }

        public async Task RunAsync(PipelineLoopSettings settings, CancellationToken cancellationToken)
        {
            if (settings.Stages == null || settings.Stages.Count == 0)
            {
                Log("[排程錯誤] 排程清單為空，請先於「階梯排程」頁籤新增階段。", Color.FromArgb(220, 80, 80));
                OnStatusChanged?.Invoke(LoopStatus.Error);
                return;
            }

            Stats.RunCount = 0;
            Stats.SuccessCount = 0;
            Stats.FailCount = 0;
            Stats.EventCount = 0;
            Stats.EventSuccessCount = 0;
            Stats.EventFailCount = 0;
            Stats.TotalExp = 0;
            Stats.StartTime = DateTime.Now;
            Stats.GainedCharacters.Clear();

            OnStatusChanged?.Invoke(LoopStatus.Running);
            NotifyStats();

            int stageIndex = 0;

            try
            {
                while (stageIndex < settings.Stages.Count && !cancellationToken.IsCancellationRequested)
                {
                    var stage = settings.Stages[stageIndex];
                    int stageRunCount = 0;
                    DateTime stageStartTime = DateTime.Now;

                    OnStageChanged?.Invoke(stageIndex + 1, settings.Stages.Count, stage);
                    Log($"[排程] 進入第 {stageIndex + 1}/{settings.Stages.Count} 階段「{stage.Name}」：{stage.GetActionSummary()}，目標：{stage.GetConditionSummary()}",
                        Color.FromArgb(140, 200, 255));

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // ── 檢查階段完成條件 ──
                        bool isConditionMet = false;
                        switch (stage.ConditionType)
                        {
                            case StageConditionType.LevelReach:
                                if (Stats.CurrentLevel > 0 && Stats.CurrentLevel >= stage.ConditionValue)
                                    isConditionMet = true;
                                break;
                            case StageConditionType.RunCountReach:
                                if (stageRunCount >= stage.ConditionValue)
                                    isConditionMet = true;
                                break;
                            case StageConditionType.MinutesReach:
                                if ((DateTime.Now - stageStartTime).TotalMinutes >= stage.ConditionValue)
                                    isConditionMet = true;
                                break;
                        }

                        if (isConditionMet)
                        {
                            Log($"[排程] ✓ 第 {stageIndex + 1} 階段「{stage.Name}」已達成條件（{stage.GetConditionSummary()}）！",
                                Color.FromArgb(80, 220, 120));
                            stageIndex++;
                            break; // 跳出當前階段，進入下一階段
                        }

                        OnStatusChanged?.Invoke(LoopStatus.Running);

                        int cooldownMs = 3000;
                        PendingEvent? pendingEvent = null;

                        // ── 執行動作（訓練 or 戰鬥）──
                        if (stage.ActionType == StageActionType.Training)
                        {
                            var result = await _trainingService.ExecuteTrainingAsync(
                                settings.Token, stage.TrainingActionId, cancellationToken);

                            if (result.IsCancelled) return;

                            if (!result.IsSuccess)
                            {
                                Stats.FailCount++;
                                Log($"[失敗] {result.ErrorMessage}", Color.FromArgb(220, 80, 80));
                                NotifyStats();

                                if (result.IsUnauthorized)
                                {
                                    Log("[錯誤] Token 無效或已過期，排程終止。", Color.FromArgb(220, 80, 80));
                                    OnStatusChanged?.Invoke(LoopStatus.Error);
                                    return;
                                }

                                await WaitAsync(5000, cancellationToken);
                                continue;
                            }

                            Stats.RunCount++;
                            stageRunCount++;
                            Stats.SuccessCount++;

                            var resp = result.Response!;
                            cooldownMs = resp.CooldownMs;
                            pendingEvent = resp.PendingEvent;

                            if (resp.ActionResult != null)
                            {
                                var ar = resp.ActionResult;
                                Stats.TotalExp += ar.Exp;
                                if (ar.Level > 0) Stats.CurrentLevel = ar.Level;
                                Log($"[訓練] {ar.ActionName}：獲得 EXP {ar.Exp}，目前 Lv.{ar.Level}",
                                    Color.FromArgb(90, 215, 110));
                            }
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(stage.BattleTargetId))
                            {
                                Log($"[排程錯誤] 階段「{stage.Name}」未填寫戰鬥目標 ID，跳過此階段。", Color.FromArgb(220, 80, 80));
                                stageIndex++;
                                break;
                            }

                            var result = stage.BattleType == BattleType.Challenge
                                ? await _battleService.ChallengeAsync(stage.BattleTargetId, settings.Token, cancellationToken)
                                : await _battleService.ChadoAsync(stage.BattleTargetId, settings.Token, cancellationToken);

                            if (result.IsCancelled) return;

                            if (!result.IsSuccess)
                            {
                                Stats.FailCount++;
                                Log($"[失敗] {result.ErrorMessage}", Color.FromArgb(220, 80, 80));
                                NotifyStats();

                                if (result.IsUnauthorized)
                                {
                                    Log("[錯誤] Token 無效或已過期，排程終止。", Color.FromArgb(220, 80, 80));
                                    OnStatusChanged?.Invoke(LoopStatus.Error);
                                    return;
                                }

                                await WaitAsync(5000, cancellationToken);
                                continue;
                            }

                            Stats.RunCount++;
                            stageRunCount++;
                            var bResp = result.Data!;
                            cooldownMs = bResp.CooldownMs;
                            pendingEvent = bResp.PendingEvent;

                            bool isWin = bResp.Winner == "TEAM_A";
                            if (isWin) Stats.SuccessCount++;
                            else Stats.FailCount++;

                            Stats.TotalExp += bResp.ExpGained;
                            string bType = stage.BattleType == BattleType.Challenge ? "友好切磋" : "我要茶渡你";
                            string winStr = isWin ? "勝利" : "失敗";
                            Color logCol = isWin ? Color.FromArgb(80, 200, 120) : Color.FromArgb(220, 160, 80);
                            Log($"[{winStr}] {bType}：獲得 EXP {bResp.ExpGained}", logCol);
                        }

                        NotifyStats();

                        // ── 處理事件 ──
                        if (pendingEvent != null)
                        {
                            Stats.EventCount++;
                            NotifyStats();
                            OnStatusChanged?.Invoke(LoopStatus.WaitingEvent);

                            string? chosenOptionId = null;
                            if (AutoStrategy.HasValue)
                            {
                                chosenOptionId = AutoSelectOption(pendingEvent, AutoStrategy.Value);
                                var chosenOption = pendingEvent.Options.Find(o => o.Id == chosenOptionId);
                                Log($"[事件] {pendingEvent.Name} → 自動選擇「{chosenOption?.Name}」",
                                    Color.FromArgb(200, 160, 255));
                            }
                            else
                            {
                                if (OnManualEventSelect == null)
                                {
                                    chosenOptionId = AutoSelectOption(pendingEvent, AutoEventStrategy.HighestSuccessRate);
                                }
                                else
                                {
                                    chosenOptionId = await OnManualEventSelect(pendingEvent, cancellationToken);
                                }
                            }

                            if (cancellationToken.IsCancellationRequested) return;

                            if (!string.IsNullOrEmpty(chosenOptionId))
                            {
                                var (eventOk, eventResult) = await ProcessEventAsync(
                                    settings.Token, pendingEvent.EventId, chosenOptionId, cancellationToken);
                                if (!eventOk)
                                {
                                    OnCloseManualUi?.Invoke();
                                    return;
                                }

                                if (!AutoStrategy.HasValue && eventResult != null && OnManualEventResult != null)
                                {
                                    await OnManualEventResult(eventResult, cancellationToken);
                                }
                                else
                                {
                                    OnCloseManualUi?.Invoke();
                                }
                            }

                            if (cancellationToken.IsCancellationRequested) return;
                            OnStatusChanged?.Invoke(LoopStatus.Running);
                        }

                        // ── 冷卻倒數 ──
                        int actualDelay = CalculateActualDelay(cooldownMs, settings.ExtraDelaySeconds);
                        OnStatusChanged?.Invoke(LoopStatus.WaitingCooldown);
                        await WaitWithCountdownAsync(actualDelay, cancellationToken);

                        if (cancellationToken.IsCancellationRequested) return;
                    }
                }

                if (stageIndex >= settings.Stages.Count)
                {
                    Log("🎉 所有階梯排程已全數完成！", Color.FromArgb(140, 200, 255));
                    OnStatusChanged?.Invoke(LoopStatus.Completed);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                Stats.NextRunCountdownSeconds = 0;
                OnStatusChanged?.Invoke(LoopStatus.Stopped);
                NotifyStats();
            }
        }

        private async Task<(bool Success, EventResult? Result)> ProcessEventAsync(
            string token, string eventId, string optionId, CancellationToken cancellationToken)
        {
            var eventResultResp = await _eventService.SelectOptionAsync(token, optionId, cancellationToken);
            if (eventResultResp.IsCancelled) return (false, null);

            if (!eventResultResp.IsSuccess)
            {
                Stats.EventFailCount++;
                Log($"[事件失敗] {eventResultResp.ErrorMessage}", Color.FromArgb(220, 80, 80));
                NotifyStats();
                if (eventResultResp.IsUnauthorized)
                {
                    Log("[錯誤] Token 無效或已過期，終止運行。", Color.FromArgb(220, 80, 80));
                    OnStatusChanged?.Invoke(LoopStatus.Error);
                    return (false, null);
                }
                return (true, null);
            }

            var er = eventResultResp.Result!;
            if (er.IsSuccess)
            {
                Stats.EventSuccessCount++;
                Log($"[事件結果] 判定成功！{er.Text}", Color.FromArgb(80, 220, 120));
            }
            else
            {
                Stats.EventFailCount++;
                Log($"[事件結果] 判定失敗。{er.Text}", Color.FromArgb(220, 100, 100));
            }

            if (er.GainCharacters != null && er.GainCharacters.Count > 0)
            {
                foreach (var gc in er.GainCharacters)
                {
                    if (!Stats.GainedCharacters.Contains(gc.Name))
                    {
                        Stats.GainedCharacters.Add(gc.Name);
                        Log($"★ 獲得角色：【{gc.Name}】！", Color.Gold);
                    }
                }
            }

            NotifyStats();
            return (true, er);
        }

        private static string AutoSelectOption(PendingEvent pe, AutoEventStrategy strategy)
        {
            if (pe.Options.Count == 0) return string.Empty;
            if (strategy == AutoEventStrategy.Random)
            {
                return pe.Options[Random.Shared.Next(pe.Options.Count)].Id;
            }

            EventOption? best = null;
            foreach (var opt in pe.Options)
            {
                if (best == null) { best = opt; continue; }
                int optChance = opt.SuccessChance ?? 0;
                int bestChance = best.SuccessChance ?? 0;
                if (optChance > bestChance) best = opt;
            }
            return best?.Id ?? pe.Options[0].Id;
        }

        private static int CalculateActualDelay(int cooldownMs, double extraDelaySeconds)
        {
            int baseDelay = Math.Max(cooldownMs, 1000);
            int extraMaxMs = (int)(Math.Max(0, extraDelaySeconds) * 1000);
            int randomExtra = extraMaxMs > 0 ? Random.Shared.Next(0, extraMaxMs + 1) : 0;
            return baseDelay + randomExtra;
        }

        private async Task WaitWithCountdownAsync(int totalMs, CancellationToken cancellationToken)
        {
            int remainingMs = totalMs;
            const int intervalMs = 100;
            while (remainingMs > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Stats.NextRunCountdownSeconds = Math.Max(0, remainingMs / 1000.0);
                NotifyStats();
                int delay = Math.Min(remainingMs, intervalMs);
                await Task.Delay(delay, cancellationToken);
                remainingMs -= delay;
            }
            Stats.NextRunCountdownSeconds = 0;
            NotifyStats();
        }

        private static async Task WaitAsync(int ms, CancellationToken cancellationToken)
        {
            await Task.Delay(ms, cancellationToken);
        }

        private void NotifyStats() => OnStatsUpdated?.Invoke(Stats);
        private void Log(string message, Color color) => OnLog?.Invoke(message, color);
    }
}
