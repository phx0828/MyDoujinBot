using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    public enum BattleType
    {
        Challenge,
        Chado
    }

    public class BattleLoopSettings
    {
        public string Token { get; init; } = string.Empty;
        public string TargetPlayerId { get; init; } = string.Empty;
        public string TargetPlayerName { get; init; } = string.Empty;
        public BattleType Type { get; init; }
        public ExecutionMode Mode { get; init; }
        public int CountLimit { get; init; }
        public int TimeLimitMinutes { get; init; }
        public double ExtraDelaySeconds { get; init; }
    }

    public class BattleStats
    {
        public int RunCount { get; set; }
        public int WinCount { get; set; }
        public int LossCount { get; set; }
        public int FailCount { get; set; }
        public int EventCount { get; set; }
        public int EventSuccessCount { get; set; }
        public int EventFailCount { get; set; }
        public int CurrentLevel { get; set; }
        public int TotalExp { get; set; }
        public double NextRunCountdownSeconds { get; set; }
        public DateTime StartTime { get; set; }
        public int ChadoSuccessCount { get; set; }
        public System.Collections.Generic.List<string> GainedCharacters { get; set; } = new();
    }

    public class BattleLoop
    {
        private readonly BattleService _battleService;
        private readonly EventService _eventService;

        public Action<string, System.Drawing.Color>? OnLog { get; set; }
        public Action<LoopStatus>? OnStatusChanged { get; set; }
        public Action<BattleStats>? OnStatsUpdated { get; set; }
        public Action<BattleResult>? OnLastBattleResultUpdated { get; set; }

        public Func<PendingEvent, CancellationToken, Task<string?>>? OnManualEventSelect { get; set; }
        public Func<EventResult, CancellationToken, Task>? OnManualEventResult { get; set; }
        public Action? OnCloseManualUi { get; set; }
        public bool IsAutoEventMode { get; set; } = true;

        public BattleStats Stats { get; } = new();

        public BattleLoop(BattleService battleService, EventService eventService)
        {
            _battleService = battleService;
            _eventService = eventService;
        }

        public async Task RunAsync(BattleLoopSettings settings, CancellationToken cancellationToken)
        {
            Stats.RunCount = 0;
            Stats.WinCount = 0;
            Stats.LossCount = 0;
            Stats.FailCount = 0;
            Stats.EventCount = 0;
            Stats.EventSuccessCount = 0;
            Stats.EventFailCount = 0;
            Stats.TotalExp = 0;
            Stats.StartTime = DateTime.Now;
            Stats.GainedCharacters.Clear();

            OnStatusChanged?.Invoke(LoopStatus.Running);
            NotifyStats();

            DateTime? endTime = settings.Mode == ExecutionMode.Time
                ? DateTime.Now.AddMinutes(settings.TimeLimitMinutes)
                : null;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (settings.Mode == ExecutionMode.Count && Stats.RunCount >= settings.CountLimit)
                    {
                        Log("已完成指定次數，戰鬥結束。", System.Drawing.Color.FromArgb(180, 220, 255));
                        OnStatusChanged?.Invoke(LoopStatus.Completed);
                        return;
                    }

                    if (settings.Mode == ExecutionMode.Time && endTime.HasValue && DateTime.Now >= endTime.Value)
                    {
                        Log("已達指定時間，戰鬥結束。", System.Drawing.Color.FromArgb(180, 220, 255));
                        OnStatusChanged?.Invoke(LoopStatus.Completed);
                        return;
                    }

                    OnStatusChanged?.Invoke(LoopStatus.Running);
                    
                    var result = settings.Type == BattleType.Challenge
                        ? await _battleService.ChallengeAsync(settings.TargetPlayerId, settings.Token, cancellationToken)
                        : await _battleService.ChadoAsync(settings.TargetPlayerId, settings.Token, cancellationToken);

                    if (result.IsCancelled) return;

                    if (!result.IsSuccess)
                    {
                        Stats.FailCount++;
                        Log($"[失敗] {result.ErrorMessage}", System.Drawing.Color.FromArgb(220, 80, 80));
                        NotifyStats();

                        if (result.IsUnauthorized)
                        {
                            Log("[錯誤] Token 無效或已過期，戰鬥終止。", System.Drawing.Color.FromArgb(220, 80, 80));
                            OnStatusChanged?.Invoke(LoopStatus.Error);
                            return;
                        }

                        await WaitAsync(5000, cancellationToken);
                        continue;
                    }

                    Stats.RunCount++;
                    var response = result.Data!;
                    
                    bool isWin = response.Winner == "TEAM_A";
                    if (isWin) Stats.WinCount++;
                    else Stats.LossCount++;

                    Stats.TotalExp += response.ExpGained;
                    
                    string typeStr = settings.Type == BattleType.Challenge ? "友好切磋" : "我要茶渡你";
                    string winStr = isWin ? "勝利" : "失敗";
                    System.Drawing.Color logColor = isWin ? System.Drawing.Color.FromArgb(80, 200, 120) : System.Drawing.Color.FromArgb(220, 160, 80);
                    
                    Log($"[{winStr}] {typeStr}：獲得 EXP {response.ExpGained}", logColor);
                    
                    NotifyStats();

                    if (settings.Type == BattleType.Chado && response.Logs != null && response.Logs.Count > 0)
                    {
                        int checkCount = Math.Min(10, response.Logs.Count);
                        bool isChadoSuccess = false;
                        for (int i = 1; i <= checkCount; i++)
                        {
                            var logEntry = response.Logs[response.Logs.Count - i];
                            if (logEntry.Type == "CHADO")
                            {
                                isChadoSuccess = true;
                                break;
                            }
                            if (logEntry.Type == "CHADO_MISS")
                            {
                                break;
                            }
                        }

                        if (isChadoSuccess)
                        {
                            Stats.ChadoSuccessCount++;
                            Log("茶渡潑屎成功！", System.Drawing.Color.FromArgb(200, 155, 106));
                        }
                    }

                    var battleResult = new BattleResult
                    {
                        Winner = response.Winner,
                        Logs = response.Logs
                    };
                    OnLastBattleResultUpdated?.Invoke(battleResult);

                    if (response.PendingEvent != null)
                    {
                        Stats.EventCount++;
                        NotifyStats();
                        OnStatusChanged?.Invoke(LoopStatus.WaitingEvent);

                        var pe = response.PendingEvent;
                        string? chosenOptionId;

                        if (IsAutoEventMode)
                        {
                            chosenOptionId = AutoSelectOption(pe);
                            var chosenOption = pe.Options.Find(o => o.Id == chosenOptionId);
                            var chanceTxt = chosenOption?.SuccessChance.HasValue == true
                                ? $"（成功率 {chosenOption.SuccessChance}%）"
                                : "（無判定）";
                            Log($"[事件] {pe.Name} → 自動選擇「{chosenOption?.Name}」{chanceTxt}",
                                System.Drawing.Color.FromArgb(200, 160, 255));
                        }
                        else
                        {
                            if (OnManualEventSelect == null)
                            {
                                Log($"[事件] {pe.Name} — 人工選擇未配置，自動切換為自動選擇。",
                                    System.Drawing.Color.FromArgb(200, 160, 255));
                                chosenOptionId = AutoSelectOption(pe);
                            }
                            else
                            {
                                chosenOptionId = await OnManualEventSelect(pe, cancellationToken);
                            }
                        }

                        if (cancellationToken.IsCancellationRequested) return;

                        if (!string.IsNullOrEmpty(chosenOptionId))
                        {
                            var (eventOk, eventResult) = await ProcessEventAsync(
                                settings.Token, pe.EventId, chosenOptionId, cancellationToken);
                            if (!eventOk)
                            {
                                OnCloseManualUi?.Invoke();
                                return;
                            }

                            if (!IsAutoEventMode)
                            {
                                if (eventResult != null && OnManualEventResult != null)
                                {
                                    await OnManualEventResult(eventResult, cancellationToken);
                                }
                                else
                                {
                                    OnCloseManualUi?.Invoke();
                                }
                            }
                        }

                        if (cancellationToken.IsCancellationRequested) return;
                        OnStatusChanged?.Invoke(LoopStatus.Running);
                    }

                    var cooldownMs = response.CooldownMs;
                    var actualDelay = CalculateActualDelay(cooldownMs, settings.ExtraDelaySeconds);

                    Log($"冷卻等待：{actualDelay / 1000.0:F1} 秒", System.Drawing.Color.FromArgb(140, 140, 160));

                    OnStatusChanged?.Invoke(LoopStatus.WaitingCooldown);
                    await WaitWithCountdownAsync(actualDelay, cancellationToken);

                    if (cancellationToken.IsCancellationRequested) return;
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

        private int CalculateActualDelay(int cooldownMs, double extraDelaySeconds)
        {
            if (extraDelaySeconds <= 0) return cooldownMs;
            var maxExtraMs = (int)(extraDelaySeconds * 1000);
            var randomDelay = Random.Shared.Next(1, maxExtraMs + 1);
            return cooldownMs + randomDelay;
        }

        private static string? AutoSelectOption(PendingEvent pe)
        {
            if (pe.Options == null || pe.Options.Count == 0)
                return null;

            var optionsWithChance = pe.Options
                .Where(o => o.SuccessChance.HasValue)
                .ToList();

            if (optionsWithChance.Count > 0)
            {
                var best = optionsWithChance.MaxBy(o => o.SuccessChance!.Value);
                return best?.Id;
            }

            var randomIndex = Random.Shared.Next(pe.Options.Count);
            return pe.Options[randomIndex].Id;
        }

        private async Task<(bool ok, EventResult? result)> ProcessEventAsync(
            string token, string eventId, string optionId, CancellationToken cancellationToken)
        {
            var eventActionResult = await _eventService.SelectOptionAsync(
                token, optionId, cancellationToken);

            if (eventActionResult.IsCancelled) return (false, null);

            if (!eventActionResult.IsSuccess)
            {
                Log($"[事件失敗] API 錯誤：{eventActionResult.ErrorMessage}",
                    System.Drawing.Color.FromArgb(220, 80, 80));
                Stats.EventFailCount++;
                NotifyStats();

                if (eventActionResult.IsUnauthorized)
                {
                    Log("[錯誤] Token 無效或已過期，戰鬥終止。",
                        System.Drawing.Color.FromArgb(220, 80, 80));
                    OnStatusChanged?.Invoke(LoopStatus.Error);
                    return (false, null);
                }

                return (true, null);
            }

            ProcessEventResult(eventActionResult.Result!);
            NotifyStats();
            return (true, eventActionResult.Result);
        }

        private void ProcessEventResult(EventResult er)
        {
            if (er.IsSuccess)
            {
                Stats.EventSuccessCount++;
                var msg = "[事件成功]";

                var bonusParts = BuildBonusStatsParts(er.Rewards?.BonusStats);
                if (bonusParts.Count > 0)
                    msg += $" 獲得 {string.Join("、", bonusParts)}";

                Log(msg, System.Drawing.Color.FromArgb(80, 200, 120));
            }
            else
            {
                Stats.EventFailCount++;
                var msg = "[事件失敗]";

                var bonusParts = BuildBonusStatsParts(er.Rewards?.BonusStats);
                if (bonusParts.Count > 0)
                    msg += $" 獲得 {string.Join("、", bonusParts)}";

                Log(msg, System.Drawing.Color.FromArgb(220, 80, 80));
            }

            if (er.GainCharacters != null && er.GainCharacters.Count > 0)
            {
                foreach (var character in er.GainCharacters)
                {
                    if (!string.IsNullOrEmpty(character.Name))
                    {
                        Stats.GainedCharacters.Add(character.Name);
                        Log($"[事件] 獲得角色：{character.Name}", System.Drawing.Color.Gold);
                    }
                }
            }
        }

        private static System.Collections.Generic.List<string> BuildBonusStatsParts(
            System.Collections.Generic.Dictionary<string, int>? bonusStats)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (bonusStats == null || bonusStats.Count == 0) return parts;

            foreach (var kv in bonusStats)
            {
                if (kv.Value != 0)
                {
                    var statName = Utilities.StatHelper.GetDisplayName(kv.Key);
                    parts.Add($"{statName} +{kv.Value}");
                }
            }
            return parts;
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
        private void Log(string message, System.Drawing.Color color) => OnLog?.Invoke(message, color);
    }
}
