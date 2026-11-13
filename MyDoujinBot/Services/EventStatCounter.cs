using System;
using System.Collections.Generic;
using MyDoujinBot.Models.ExternalEvents;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 單一角色線在某屬性上的事件計數。
    /// </summary>
    public class StatEventCount
    {
        /// <summary>至少有一個選項在成功或失敗時該屬性 ≠ 0 的事件數。</summary>
        public int AnyCount { get; set; }

        /// <summary>至少有一個選項成功時該屬性 ≠ 0 的事件數。</summary>
        public int SuccessCount { get; set; }

        /// <summary>至少有一個選項失敗時該屬性 ≠ 0 的事件數。</summary>
        public int FailCount { get; set; }

        /// <summary>至少有一個選項（成功或失敗）該屬性為負數的事件數。</summary>
        public int NegativeCount { get; set; }

        /// <summary>僅成功時才給點（失敗時沒有）。供 UI 標註用。</summary>
        public bool OnlyOnSuccess => AnyCount > 0 && FailCount == 0;
    }

    /// <summary>
    /// 單一角色線的事件計數結果。
    /// </summary>
    public class ChainStatCounts
    {
        public string ChainId { get; set; } = string.Empty;
        public string ChainName { get; set; } = string.Empty;
        public string ChainSub { get; set; } = string.Empty;
        public int TotalEvents { get; set; }
        public bool Forced { get; set; }

        /// <summary>Key：屬性鍵（hp 攻 防 體 敏 反 技 智 幸）。</summary>
        public Dictionary<string, StatEventCount> Stats { get; set; } = new();

        public double Ratio(string statKey) =>
            TotalEvents > 0 && Stats.TryGetValue(statKey, out var c) ? (double)c.AnyCount / TotalEvents : 0;
    }

    /// <summary>
    /// 統計各角色線「有多少事件會給某屬性」。資料來源為事件庫 EventRoute（onSuccess / onFail）。
    /// 以事件為單位計數：同一事件有多個選項給同一屬性仍只算 1。
    /// </summary>
    public static class EventStatCounter
    {
        public static readonly string[] StatKeys = { "hp", "攻", "防", "體", "敏", "反", "技", "智", "幸" };

        /// <summary>非角色線，不納入偏好選擇。</summary>
        public static bool IsNonCharacterChain(string chainId) => chainId.StartsWith('#');

        public static List<ChainStatCounts> Count(ExternalRouteFile route)
        {
            var result = new List<ChainStatCounts>();

            foreach (var kvp in route.Chains)
            {
                if (IsNonCharacterChain(kvp.Key)) continue;

                var chain = kvp.Value;
                var counts = new ChainStatCounts
                {
                    ChainId = kvp.Key,
                    ChainName = chain.Name,
                    ChainSub = chain.Sub,
                    TotalEvents = chain.Events.Count,
                    Forced = chain.Forced
                };
                foreach (var key in StatKeys) counts.Stats[key] = new StatEventCount();

                foreach (var ev in chain.Events)
                {
                    foreach (var key in StatKeys)
                    {
                        bool success = false, fail = false, negative = false;
                        foreach (var opt in ev.Options)
                        {
                            if (opt.OnSuccess != null && opt.OnSuccess.TryGetValue(key, out int s) && s != 0)
                            {
                                success = true;
                                if (s < 0) negative = true;
                            }
                            if (opt.OnFail != null && opt.OnFail.TryGetValue(key, out int f) && f != 0)
                            {
                                fail = true;
                                if (f < 0) negative = true;
                            }
                        }

                        var c = counts.Stats[key];
                        if (success) c.SuccessCount++;
                        if (fail) c.FailCount++;
                        if (success || fail) c.AnyCount++;
                        if (negative) c.NegativeCount++;
                    }
                }

                result.Add(counts);
            }

            return result;
        }
    }
}

