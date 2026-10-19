using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDoujinBot.Models;
using MyDoujinBot.Utilities;

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
        public AutoEventStrategy? AutoStrategy { get; set; } = AutoEventStrategy.HighestSuccessRate;

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
                        Log(LoopLogHelper.FormatError(result.ErrorMessage));
                        NotifyStats();

                        if (result.IsUnauthorized)
                        {
                            Log(LoopLogHelper.FormatTokenExpired("戰鬥"));
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
                    
                    Log(LoopLogHelper.FormatBattleResult(settings.Type, isWin, response.ExpGained));
                    
                    if (response.Level > 0)
                    {
                        Stats.CurrentLevel = response.Level;
                        UserContext.UpdateLevel(response.Level);
                    }

                    NotifyStats();

                    if (settings.Type == BattleType.Chado && response.IsPlayerChadoSuccess())
                    {
                        Stats.ChadoSuccessCount++;
                        Log(LoopLogHelper.FormatChadoSuccess());
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
                        string? chosenOptionId = null;
                        bool isCustomSelected = false;

                        // 優先級 1：比對使用者自選方案 (依 eventId 查詢 optionId)
                        if (CustomEventChoiceResolver != null)
                        {
                            var targetOptionId = CustomEventChoiceResolver(pe.EventId);
                            if (!string.IsNullOrEmpty(targetOptionId) && pe.Options.Any(o => o.Id == targetOptionId))
                            {
                                chosenOptionId = targetOptionId;
                                isCustomSelected = true;
                                var targetOpt = pe.Options.FirstOrDefault(o => o.Id == targetOptionId);
                                Log($"[事件自選] 依方案選擇：{pe.Name} ➜ {targetOpt?.Name ?? targetOptionId}", System.Drawing.Color.FromArgb(140, 210, 255));
                            }
                        }

                        // 優先級 2：未命中自選方案，退回既有自動/手動流程
                        if (string.IsNullOrEmpty(chosenOptionId))
                        {
                            if (AutoStrategy.HasValue)
                            {
                                chosenOptionId = LoopLogHelper.AutoSelectOption(pe, AutoStrategy.Value);
                                Log(LoopLogHelper.FormatEventSelection(pe, chosenOptionId));
                            }
                            else
                            {
                                if (OnManualEventSelect == null)
                                {
                                    Log(LoopLogHelper.FormatManualEventFallback(pe));
                                    chosenOptionId = LoopLogHelper.AutoSelectOption(pe, AutoEventStrategy.HighestSuccessRate);
                                }
                                else
                                {
                                    chosenOptionId = await OnManualEventSelect(pe, cancellationToken);
                                }
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

                            if (!AutoStrategy.HasValue && !isCustomSelected)
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
                            else
                            {
                                OnCloseManualUi?.Invoke();
                            }
                        }

                        if (cancellationToken.IsCancellationRequested) return;
                        OnStatusChanged?.Invoke(LoopStatus.Running);
                    }

                    var cooldownMs = response.CooldownMs;
                    var actualDelay = LoopLogHelper.CalculateActualDelay(cooldownMs, settings.ExtraDelaySeconds);

                    Log(LoopLogHelper.FormatCooldown(cooldownMs, actualDelay));

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

        private static int CalculateActualDelay(int cooldownMs, double extraDelaySeconds)
            => LoopLogHelper.CalculateActualDelay(cooldownMs, extraDelaySeconds);

        private static string? AutoSelectOption(PendingEvent pe, AutoEventStrategy strategy)
            => LoopLogHelper.AutoSelectOption(pe, strategy);

        private async Task<(bool ok, EventResult? result)> ProcessEventAsync(
            string token, string eventId, string optionId, CancellationToken cancellationToken)
        {
            var eventActionResult = await _eventService.SelectOptionAsync(
                token, optionId, cancellationToken);

            if (eventActionResult.IsCancelled) return (false, null);

            if (!eventActionResult.IsSuccess)
            {
                Log(LoopLogHelper.FormatEventApiError(eventActionResult.ErrorMessage));
                Stats.EventFailCount++;
                NotifyStats();

                if (eventActionResult.IsUnauthorized)
                {
                    Log(LoopLogHelper.FormatTokenExpired("戰鬥"));
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

        private static System.Collections.Generic.List<string> BuildBonusStatsParts(
            System.Collections.Generic.Dictionary<string, int>? bonusStats)
            => LoopLogHelper.BuildBonusStatsParts(bonusStats);

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
        private void Log((string Message, System.Drawing.Color Color) entry) => OnLog?.Invoke(entry.Message, entry.Color);

        /// <summary>
        /// 事件自選方案解析委派：傳入 eventId，回傳目標 optionId。若無設定則回傳 null。
        /// </summary>
        public Func<string, string?>? CustomEventChoiceResolver { get; set; }
    }
}
