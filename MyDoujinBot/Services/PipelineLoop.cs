using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using MyDoujinBot.Models;
using MyDoujinBot.Utilities;

namespace MyDoujinBot.Services
{
    public class PipelineLoopSettings
    {
        public string Token { get; init; } = string.Empty;
        public List<PipelineStage> Stages { get; init; } = new();
        public double ExtraDelaySeconds { get; init; } = 1.0;
    }

    /// <summary>
    /// 行動排程迴圈控制器。
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
        public Action<BattleResult>? OnLastBattleResultUpdated { get; set; }

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
                Log("[排程錯誤] 排程清單為空，請先於「行動排程」頁籤新增階段。", LoopLogHelper.ErrorRed);
                OnStatusChanged?.Invoke(LoopStatus.Error);
                return;
            }

            Stats.RunCount = 0;
            Stats.TrainingRunCount = 0;
            Stats.TrainingSuccessCount = 0;
            Stats.TrainingFailCount = 0;
            Stats.BattleRunCount = 0;
            Stats.BattleWinCount = 0;
            Stats.BattleLossCount = 0;
            Stats.CurrentStageIndex = 1;
            Stats.TotalStages = settings.Stages.Count;
            Stats.CurrentStage = settings.Stages[0];
            Stats.CurrentStageRunCount = 0;
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

                    Stats.CurrentStageIndex = stageIndex + 1;
                    Stats.TotalStages = settings.Stages.Count;
                    Stats.CurrentStage = stage;
                    Stats.CurrentStageRunCount = stageRunCount;

                    OnStageChanged?.Invoke(stageIndex + 1, settings.Stages.Count, stage);
                    NotifyStats();
                    Log(LoopLogHelper.FormatStageStart(stageIndex + 1, settings.Stages.Count, stage));

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
                            var nextStage = (stageIndex + 1 < settings.Stages.Count) ? settings.Stages[stageIndex + 1] : null;
                            Log(LoopLogHelper.FormatStageConditionMet(stageIndex + 1, settings.Stages.Count, stage, nextStage));
                            stageIndex++;
                            if (stageIndex < settings.Stages.Count)
                            {
                                Stats.CurrentStageIndex = stageIndex + 1;
                                Stats.CurrentStage = settings.Stages[stageIndex];
                                Stats.CurrentStageRunCount = 0;
                            }
                            NotifyStats();
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
                                Stats.TrainingFailCount++;
                                Log(LoopLogHelper.FormatError(result.ErrorMessage));
                                NotifyStats();

                                if (result.IsUnauthorized)
                                {
                                    Log(LoopLogHelper.FormatTokenExpired("排程"));
                                    OnStatusChanged?.Invoke(LoopStatus.Error);
                                    return;
                                }

                                await WaitAsync(5000, cancellationToken);
                                continue;
                            }

                            Stats.RunCount++;
                            stageRunCount++;
                            Stats.TrainingRunCount++;
                            Stats.TrainingSuccessCount++;
                            Stats.CurrentStageRunCount = stageRunCount;
                            Stats.SuccessCount++;

                            var resp = result.Response!;
                            cooldownMs = resp.CooldownMs;
                            pendingEvent = resp.PendingEvent;

                            if (resp.ActionResult != null)
                            {
                                var ar = resp.ActionResult;
                                Stats.TotalExp += ar.Exp;
                                if (ar.Level > 0)
                                {
                                    Stats.CurrentLevel = ar.Level;
                                    UserContext.UpdateLevel(ar.Level);
                                }
                                Log(LoopLogHelper.FormatTrainingResult(ar));
                            }
                            if (resp.ActiveCharacter != null && resp.ActiveCharacter.Level > 0)
                            {
                                Stats.CurrentLevel = resp.ActiveCharacter.Level;
                                UserContext.UpdateLevel(resp.ActiveCharacter.Level);
                            }
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(stage.BattleTargetId))
                            {
                                Log($"[排程錯誤] 階段「{stage.Name}」未填寫戰鬥目標 ID，跳過此階段。", LoopLogHelper.ErrorRed);
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
                                Stats.BattleLossCount++;
                                Log(LoopLogHelper.FormatError(result.ErrorMessage));
                                NotifyStats();

                                if (result.IsUnauthorized)
                                {
                                    Log(LoopLogHelper.FormatTokenExpired("排程"));
                                    OnStatusChanged?.Invoke(LoopStatus.Error);
                                    return;
                                }

                                await WaitAsync(5000, cancellationToken);
                                continue;
                            }

                            Stats.RunCount++;
                            stageRunCount++;
                            Stats.BattleRunCount++;
                            Stats.CurrentStageRunCount = stageRunCount;
                            var bResp = result.Data!;
                            cooldownMs = bResp.CooldownMs;
                            pendingEvent = bResp.PendingEvent;

                            bool isWin = bResp.Winner == "TEAM_A";
                            if (isWin)
                            {
                                Stats.SuccessCount++;
                                Stats.BattleWinCount++;
                            }
                            else
                            {
                                Stats.FailCount++;
                                Stats.BattleLossCount++;
                            }

                            Stats.TotalExp += bResp.ExpGained;
                            if (bResp.Level > 0)
                            {
                                Stats.CurrentLevel = bResp.Level;
                                UserContext.UpdateLevel(bResp.Level);
                            }

                            Log(LoopLogHelper.FormatBattleResult(stage.BattleType, isWin, bResp.ExpGained));

                            if (stage.BattleType == BattleType.Chado && bResp.IsPlayerChadoSuccess())
                            {
                                Stats.ChadoSuccessCount++;
                                Log(LoopLogHelper.FormatChadoSuccess());
                            }

                            var battleResult = new BattleResult
                            {
                                Winner = bResp.Winner,
                                Logs = bResp.Logs
                            };
                            OnLastBattleResultUpdated?.Invoke(battleResult);
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
                                chosenOptionId = LoopLogHelper.AutoSelectOption(pendingEvent, AutoStrategy.Value);
                                Log(LoopLogHelper.FormatEventSelection(pendingEvent, chosenOptionId));
                            }
                            else
                            {
                                if (OnManualEventSelect == null)
                                {
                                    Log(LoopLogHelper.FormatManualEventFallback(pendingEvent));
                                    chosenOptionId = LoopLogHelper.AutoSelectOption(pendingEvent, AutoEventStrategy.HighestSuccessRate);
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

                        // ── 計算並等待 Cooldown ──
                        int actualDelay = LoopLogHelper.CalculateActualDelay(cooldownMs, settings.ExtraDelaySeconds);
                        Log(LoopLogHelper.FormatCooldown(cooldownMs, actualDelay));

                        OnStatusChanged?.Invoke(LoopStatus.WaitingCooldown);
                        await WaitWithCountdownAsync(actualDelay, cancellationToken);

                        if (cancellationToken.IsCancellationRequested) return;
                    }
                }

                if (stageIndex >= settings.Stages.Count)
                {
                    Log("已達成所有排程階段條件，行動排程結束。", LoopLogHelper.LoopCompletedBlue);
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
                Log(LoopLogHelper.FormatEventApiError(eventResultResp.ErrorMessage));
                NotifyStats();
                if (eventResultResp.IsUnauthorized)
                {
                    Log(LoopLogHelper.FormatTokenExpired("排程"));
                    OnStatusChanged?.Invoke(LoopStatus.Error);
                    return (false, null);
                }
                return (true, null);
            }

            var er = eventResultResp.Result!;
            ProcessEventResult(er);
            NotifyStats();
            return (true, er);
        }

        private void ProcessEventResult(EventResult er)
        {
            if (er.IsSuccess)
                Stats.EventSuccessCount++;
            else
                Stats.EventFailCount++;

            Log(LoopLogHelper.FormatEventResult(er));

            if (er.GainCharacters != null && er.GainCharacters.Count > 0)
            {
                foreach (var character in er.GainCharacters)
                {
                    if (!string.IsNullOrEmpty(character.Name))
                    {
                        if (!Stats.GainedCharacters.Contains(character.Name))
                            Stats.GainedCharacters.Add(character.Name);
                        Log(LoopLogHelper.FormatGainCharacter(character.Name));
                    }
                }
            }
        }

        private async Task WaitWithCountdownAsync(int totalMs, CancellationToken cancellationToken)
        {
            var deadline = DateTime.Now.AddMilliseconds(totalMs);
            const int tickMs = 200;

            while (!cancellationToken.IsCancellationRequested)
            {
                var remaining = (deadline - DateTime.Now).TotalSeconds;
                if (remaining <= 0) break;

                Stats.NextRunCountdownSeconds = remaining;
                NotifyStats();

                var waitMs = Math.Min(tickMs, (int)((deadline - DateTime.Now).TotalMilliseconds));
                if (waitMs <= 0) break;

                try
                {
                    await Task.Delay(waitMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            Stats.NextRunCountdownSeconds = 0;
            NotifyStats();
        }

        private static async Task WaitAsync(int ms, CancellationToken cancellationToken)
        {
            try { await Task.Delay(ms, cancellationToken); }
            catch (OperationCanceledException) { }
        }

        private void NotifyStats() => OnStatsUpdated?.Invoke(Stats);
        private void Log(string message, Color color) => OnLog?.Invoke(message, color);
        private void Log((string Message, Color Color) entry) => OnLog?.Invoke(entry.Message, entry.Color);
    }
}
