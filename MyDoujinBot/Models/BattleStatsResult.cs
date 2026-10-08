using System;
using System.Collections.Generic;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 單一單位（玩家 userId／敵方 entityId／臨時召喚物）的累計數據。
    /// 玩家與敵方都累計；畫面目前只顯示玩家，敵方資料保留供日後擴充。
    /// </summary>
    public class UnitStats
    {
        public UnitStats(string id)
        {
            Id = id;
        }

        /// <summary>userId 或 entityId（不在 participants 內的 ID 會建立臨時單位）。</summary>
        public string Id { get; }

        /// <summary>輸出（造成的傷害）</summary>
        public long Output { get; set; }

        /// <summary>承傷（受到的傷害）</summary>
        public long Taken { get; set; }

        /// <summary>治療（回復的 HP）</summary>
        public long Heal { get; set; }

        /// <summary>出手次數（含落空 MISS 與被完全擋下的 BLOCK，反擊不計入）</summary>
        public long Shots { get; set; }

        /// <summary>命中次數（出手中扣除落空 MISS；被完全擋下的 BLOCK 仍算命中）</summary>
        public long Hits { get; set; }

        /// <summary>爆擊次數</summary>
        public long Crits { get; set; }

        /// <summary>
        /// 命中率（百分比，四捨五入到小數點後一位）＝ 命中次數 ÷ 出手次數 × 100。
        /// 出手次數為 0 時為 0，不會發生除以零。
        /// </summary>
        public decimal HitRatePercent => ToPercent(Hits, Shots);

        /// <summary>
        /// 爆擊率（百分比，四捨五入到小數點後一位）＝ 爆擊次數 ÷ 命中次數 × 100。
        /// 命中次數為 0 時為 0，不會發生除以零。
        /// </summary>
        public decimal CritRatePercent => ToPercent(Crits, Hits);

        private static decimal ToPercent(long numerator, long denominator) =>
            denominator <= 0
                ? 0m
                : decimal.Round((decimal)numerator * 100m / denominator, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 一份戰報的統計結果（由 BattleStatsCalculator 產生）。
    /// 只放資料，不含任何顯示邏輯。
    /// </summary>
    public class BattleStatsResult
    {
        /// <summary>各單位累計數據（key = userId / entityId / 臨時召喚物 ID）。</summary>
        public Dictionary<string, UnitStats> Units { get; } = new();

        /// <summary>logs 中出現過 DEATH 事件的單位 ID（玩家的「倒下」狀態由此判斷）。</summary>
        public HashSet<string> FallenIds { get; } = new();

        /// <summary>被略過的事件總數（type 無法辨識、或缺少必要欄位）。</summary>
        public int SkippedCount { get; set; }

        /// <summary>被略過的事件依 type 分組的筆數（供結束時回報）。</summary>
        public SortedDictionary<string, int> SkippedByType { get; } = new(StringComparer.Ordinal);

        /// <summary>取得單位數據；單位從未出現在 LOG 時回傳全 0 的空資料。</summary>
        public UnitStats GetUnitOrEmpty(string id) =>
            Units.TryGetValue(id, out var unit) ? unit : new UnitStats(id);

        public bool IsFallen(string id) => FallenIds.Contains(id);
    }
}
