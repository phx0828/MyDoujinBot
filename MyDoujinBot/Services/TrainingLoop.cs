using System;
using System.Threading;
using System.Threading.Tasks;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 執行模式：次數或時間
    /// </summary>
    public enum ExecutionMode
    {
        Count,  // 執行指定次數
        Time    // 執行指定時間（分鐘）
    }

    /// <summary>
    /// 訓練循環的參數設定。
    /// 由 Form 填入，傳給 TrainingLoop 使用。
    /// 用獨立 class 傳參數，而不是一堆 method parameter，
    /// 未來增加設定只需要改這個 class，method signature 不需要改。
    /// </summary>
    public class LoopSettings
    {
        public string Token { get; init; } = string.Empty;
        public string ActionId { get; init; } = string.Empty;
        public ExecutionMode Mode { get; init; }
        public int CountLimit { get; init; }        // 次數模式：最多執行幾次
        public int TimeLimitMinutes { get; init; }  // 時間模式：執行幾分鐘
        public double ExtraDelaySeconds { get; init; }  // 額外延遲上限（秒）
    }

    /// <summary>
    /// 訓練循環執行的統計數據。
    /// 由 TrainingLoop 持有，Form 定期讀取更新 UI。
    /// </summary>
    public class LoopStats
    {
        public int RunCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public int EventCount { get; set; }
        public int EventSuccessCount { get; set; }
        public int EventFailCount { get; set; }
        public int CurrentLevel { get; set; }
        public int TotalExp { get; set; }
        public double NextRunCountdownSeconds { get; set; }  // 倒數秒數，供 UI 顯示
        public DateTime StartTime { get; set; }
        public System.Collections.Generic.List<string> GainedCharacters { get; set; } = new();
    }

    /// <summary>
    /// 訓練循環的狀態
    /// </summary>
    public enum LoopStatus
    {
        Stopped,
        Running,
        WaitingCooldown,
        WaitingEvent,       // 等待使用者選擇事件（人工模式）
        Completed,
        Error
    }

    /// <summary>
    /// 訓練主循環控制器。
    /// 負責：循環排程、冷卻計時、停止條件判斷。
    /// 不直接操作 UI，所有 UI 更新透過 callback（Action delegate）通知 Form。
    ///
    /// 為什麼用 callback 而不是直接引用 Form？
    /// Service 層不應該依賴 UI 層（單向依賴原則）。
    /// 用 Action/Func delegate 讓 Service 可以在不知道 Form 存在的情況下
    /// 把訊息傳給 Form，降低耦合。
    /// </summary>
    public class TrainingLoop
    {
        private readonly TrainingService _trainingService;
        private readonly EventService _eventService;

        // Callbacks — Form 設定這些，TrainingLoop 在需要時呼叫
        public Action<string, System.Drawing.Color>? OnLog { get; set; }
        public Action<LoopStatus>? OnStatusChanged { get; set; }
        public Action<LoopStats>? OnStatsUpdated { get; set; }

        // 人工模式下，等待 Form 顯示事件面板並回傳選擇的 optionId
        // 如果是自動模式，TrainingLoop 自己選擇，不呼叫這個 callback
        public Func<PendingEvent, CancellationToken, Task<string?>>? OnManualEventSelect { get; set; }

        // 人工模式下，當事件選擇結果回傳時，呼叫 Form 顯示結果 UI 並等待使用者關閉
        public Func<EventResult, CancellationToken, Task>? OnManualEventResult { get; set; }

        // 人工模式下，當 API 失敗或需要清理手動 UI 時呼叫
        public Action? OnCloseManualUi { get; set; }

        public LoopStats Stats { get; } = new();

        public TrainingLoop(TrainingService trainingService, EventService eventService)
        {
            _trainingService = trainingService;
            _eventService = eventService;
        }

        // =====================================================================
        // 啟動訓練循環
        // async Task：非同步執行，不阻塞 UI Thread
        // CancellationToken：使用者按停止時取消
        // =====================================================================
        public async Task RunAsync(LoopSettings settings, CancellationToken cancellationToken)
        {
            // 初始化統計
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

            // 計算時間模式的結束時間
            // DateTime? 說明：DateTime? 是 nullable DateTime，
            // 次數模式不需要結束時間，所以設為 null
            DateTime? endTime = settings.Mode == ExecutionMode.Time
                ? DateTime.Now.AddMinutes(settings.TimeLimitMinutes)
                : null;



            try
            {
                // 主循環
                while (!cancellationToken.IsCancellationRequested)
                {
                    // ── 停止條件 1：次數模式達到上限 ──
                    if (settings.Mode == ExecutionMode.Count &&
                        Stats.RunCount >= settings.CountLimit)
                    {
                        Log("已完成指定次數，訓練結束。", System.Drawing.Color.FromArgb(180, 220, 255));
                        OnStatusChanged?.Invoke(LoopStatus.Completed);
                        return;
                    }

                    // ── 停止條件 2：時間模式超過時限 ──
                    if (settings.Mode == ExecutionMode.Time &&
                        endTime.HasValue && DateTime.Now >= endTime.Value)
                    {
                        Log("已達指定時間，訓練結束。", System.Drawing.Color.FromArgb(180, 220, 255));
                        OnStatusChanged?.Invoke(LoopStatus.Completed);
                        return;
                    }

                    // ── 執行一次訓練 ──
                    OnStatusChanged?.Invoke(LoopStatus.Running);
                    var result = await _trainingService.ExecuteTrainingAsync(
                        settings.Token, settings.ActionId, cancellationToken);

                    // 使用者取消
                    if (result.IsCancelled) return;

                    // HTTP 或網路錯誤
                    if (!result.IsSuccess)
                    {
                        Stats.FailCount++;
                        Log($"[失敗] {result.ErrorMessage}", System.Drawing.Color.FromArgb(220, 80, 80));
                        NotifyStats();

                        // 401 Unauthorized：Token 無效或過期，直接終止訓練
                        if (result.IsUnauthorized)
                        {
                            Log("[錯誤] Token 無效或已過期，訓練終止。請點擊「設定 Token」重新輸入。",
                                System.Drawing.Color.FromArgb(220, 80, 80));
                            OnStatusChanged?.Invoke(LoopStatus.Error);
                            return;
                        }

                        // 其他網路錯誤：等待 5 秒再重試（避免連續打爆 API）
                        await WaitAsync(5000, cancellationToken);
                        continue;
                    }

                    // ── 處理成功 Response ──
                    Stats.RunCount++;
                    var response = result.Response!;

                    // 處理 actionResult LOG
                    if (response.ActionResult != null)
                    {
                        Stats.SuccessCount++;
                        ProcessActionResult(response.ActionResult);
                    }

                    NotifyStats();

                    // ── 處理 pendingEvent ──
                    if (response.PendingEvent != null)
                    {
                        Stats.EventCount++;
                        NotifyStats();
                        OnStatusChanged?.Invoke(LoopStatus.WaitingEvent);

                        var pe = response.PendingEvent;
                        string? chosenOptionId;

                        if (IsAutoEventMode)
                        {
                            // 自動模式：在 TrainingLoop 內部自己選擇最高 successChance
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
                            // 人工模式：等待 Form 的使用者選擇
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

                        // 呼叫事件選擇 API
                        if (!string.IsNullOrEmpty(chosenOptionId))
                        {
                            var (eventOk, eventResult) = await ProcessEventAsync(
                                settings.Token, pe.EventId, chosenOptionId, cancellationToken);
                            if (!eventOk)
                            {
                                OnCloseManualUi?.Invoke();
                                return; // 若 401 授權失敗或已取消，終止循環
                            }

                            // 手動選擇模式下，若有結果且有配置 OnManualEventResult，則 await 結果展示（等待使用者按下關閉）
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

                    // ── 計算並等待 Cooldown ──
                    var cooldownMs = response.CooldownMs;
                    var actualDelay = CalculateActualDelay(cooldownMs, settings.ExtraDelaySeconds);

                    Log($"冷卻等待：{actualDelay / 1000.0:F1} 秒（基礎 {cooldownMs / 1000.0:F0}s + 額外隨機）",
                        System.Drawing.Color.FromArgb(140, 140, 160));

                    OnStatusChanged?.Invoke(LoopStatus.WaitingCooldown);
                    await WaitWithCountdownAsync(actualDelay, cancellationToken);

                    if (cancellationToken.IsCancellationRequested) return;
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不報錯
            }
            finally
            {
                // 無論正常結束、取消、或例外，都把狀態改回 Stopped
                Stats.NextRunCountdownSeconds = 0;
                OnStatusChanged?.Invoke(LoopStatus.Stopped);
                NotifyStats();
            }
        }

        // =====================================================================
        // 計算實際等待時間
        //
        // 規則：
        // actualDelay = cooldownMs + randomDelay
        // randomDelay：大於 0，小於等於 extraDelaySeconds（毫秒精度）
        //
        // Random.Shared 說明：
        // .NET 6 引入的執行緒安全靜態 Random 實例，
        // 不需要自己 new Random()，避免多 Thread 同時使用同一個 Random 的問題。
        // =====================================================================
        private static int CalculateActualDelay(int cooldownMs, double extraDelaySeconds)
        {
            if (extraDelaySeconds <= 0)
            {
                // 沒有額外延遲：加上最小隨機值（100ms），確保不會剛好等於 cooldownMs
                var minRandom = Random.Shared.Next(100, 500);
                return cooldownMs + minRandom;
            }

            // 毫秒精度的隨機延遲：
            // 例如 extraDelaySeconds = 2.0 → extraDelayMs = 2000
            // 隨機範圍：1ms ～ 2000ms（確保大於 0）
            var extraDelayMs = (int)(extraDelaySeconds * 1000);

            // NextDouble() 回傳 [0.0, 1.0) 的浮點數
            // 乘以 extraDelayMs 再取整，得到毫秒精度的隨機數
            // + 1 確保大於 0
            var randomMs = (int)(Random.Shared.NextDouble() * extraDelayMs) + 1;

            return cooldownMs + randomMs;
        }

        // =====================================================================
        // 等待指定毫秒，期間每 200ms 更新一次倒數秒數
        //
        // Task.Delay 說明：
        // Task.Delay 是非同步的等待，不會阻塞 UI Thread。
        // 傳入 CancellationToken，使用者按停止時立即拋出 OperationCanceledException。
        //
        // 為什麼不用 Thread.Sleep？
        // Thread.Sleep 會阻塞整個 Thread（包括 UI Thread），
        // 導致視窗假死、無法回應使用者操作。
        // Task.Delay + await 只是「暫停這個 async 方法」，
        // UI Thread 可以繼續處理其他事件（例如按鈕點擊）。
        // =====================================================================
        private async Task WaitWithCountdownAsync(int totalMs, CancellationToken cancellationToken)
        {
            var deadline = DateTime.Now.AddMilliseconds(totalMs);
            const int tickMs = 200;  // 每 200ms 更新一次倒數

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
                    return;  // 使用者取消，直接返回
                }
            }

            Stats.NextRunCountdownSeconds = 0;
            NotifyStats();
        }

        // =====================================================================
        // 等待固定毫秒（錯誤重試用，不需要倒數）
        // =====================================================================
        private static async Task WaitAsync(int ms, CancellationToken cancellationToken)
        {
            try { await Task.Delay(ms, cancellationToken); }
            catch (OperationCanceledException) { }
        }

        // =====================================================================
        // 處理 actionResult，更新統計並輸出 LOG
        // =====================================================================
        private void ProcessActionResult(ActionResult ar)
        {
            // 更新等級與 EXP
            Stats.CurrentLevel = ar.Level;
            Stats.TotalExp += ar.Exp;

            // 組合 LOG 訊息
            var msg = $"[成功] 訓練 {ar.ActionName}：獲得 EXP {ar.Exp}";

            if (ar.LevelUps > 0)
                msg += $"，升級 +{ar.LevelUps}，目前等級 {ar.Level}";

            // 找出 gains 中非 0 的能力（LINQ）
            // LINQ 說明：
            // Where() 是過濾：保留 value != 0 的項目
            // 這裡用的是 Dictionary 的 LINQ，kv 是 KeyValuePair<string, int>
            if (ar.Gains != null)
            {
                var nonZeroGains = new System.Collections.Generic.List<string>();
                foreach (var kv in ar.Gains)
                {
                    if (kv.Value != 0)
                    {
                        var statName = Utilities.StatHelper.GetDisplayName(kv.Key);
                        nonZeroGains.Add($"{statName} +{kv.Value}");
                    }
                }

                if (nonZeroGains.Count > 0)
                    msg += $"，獲得能力：{string.Join("、", nonZeroGains)}";
            }

            Log(msg, System.Drawing.Color.FromArgb(80, 200, 120));
        }

        // =====================================================================
        // 輔助：通知 Form 更新統計
        // =====================================================================
        private void NotifyStats() => OnStatsUpdated?.Invoke(Stats);

        // =====================================================================
        // 輔助：輸出 LOG
        // =====================================================================
        private void Log(string message, System.Drawing.Color color)
            => OnLog?.Invoke(message, color);

        // =====================================================================
        // 自動選擇 pendingEvent 的 option
        //
        // 規則：
        // 1. 找出有 SuccessChance 的 option
        // 2. 選 SuccessChance 最高的
        // 3. 如果所有 option 都沒有 SuccessChance，隨機選一個
        //
        // LINQ 解釋：
        // Where(o => o.SuccessChance.HasValue)  → 過濾出有 successChance 的選項
        // OrderByDescending(o => o.SuccessChance!.Value)  → 由高到低排序
        // FirstOrDefault()  → 取第一個（最高的），如果空集合則回傳 null
        // =====================================================================
        private static string? AutoSelectOption(PendingEvent pe)
        {
            if (pe.Options == null || pe.Options.Count == 0)
                return null;

            // 找出有 SuccessChance 的 options
            var optionsWithChance = pe.Options
                .Where(o => o.SuccessChance.HasValue)
                .ToList();

            if (optionsWithChance.Count > 0)
            {
                // 選 SuccessChance 最高的
                // MaxBy 說明：找出使 SuccessChance 最大的元素（.NET 6+）
                var best = optionsWithChance.MaxBy(o => o.SuccessChance!.Value);
                return best?.Id;
            }

            // 所有 option 都沒有 SuccessChance → 隨機選一個
            var randomIndex = Random.Shared.Next(pe.Options.Count);
            return pe.Options[randomIndex].Id;
        }

        // =====================================================================
        // 呼叫事件選擇 API 並處理結果
        // 回傳 bool：true 代表事件處理正常，false 代表 401 授權失敗或取消，需終止循環
        // =====================================================================
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
                    Log("[錯誤] Token 無效或已過期，訓練終止。請點擊「設定 Token」重新輸入。",
                        System.Drawing.Color.FromArgb(220, 80, 80));
                    OnStatusChanged?.Invoke(LoopStatus.Error);
                    return (false, null);
                }

                return (true, null);
            }

            // 處理事件結果
            ProcessEventResult(eventActionResult.Result!);
            NotifyStats();
            return (true, eventActionResult.Result);
        }

        // =====================================================================
        // 處理事件選擇結果，輸出 LOG
        //
        // 注意：isSuccess 判斷的是「遊戲事件」的成敗，
        //       而不是 HTTP Request 的成敗（HTTP 成功已在上面確認）
        // =====================================================================
        private void ProcessEventResult(EventResult er)
        {
            if (er.IsSuccess)
            {
                Stats.EventSuccessCount++;
                var msg = "[事件成功]";

                // 顯示 bonusStats（如果有）
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

            // 處理獲得角色
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

        // =====================================================================
        // 把 bonusStats Dictionary 轉換成顯示字串列表
        // 例如：{ "int": 1, "atk": 2 } → ["智力 +1", "攻擊 +2"]
        //
        // 如果 bonusStats 是 null、空物件，回傳空列表（不顯示）
        // =====================================================================
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

        public bool IsAutoEventMode { get; set; } = true;
    }
}
