using System;
using System.Text.RegularExpressions;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 各事件類型的處理函式（一種事件一個函式）。
    ///
    /// 回傳值：true = 已處理；false = 缺少必要欄位（呼叫端會略過並計數）。
    /// 新增事件類型時：在這裡加一個 Handle 函式，再到 BattleStatsCalculator 的處理表註冊即可。
    ///
    /// 共同原則：
    /// - 一律使用結構化欄位，不解析 message（唯一例外：SP_RECOVER，見 TryParseHpRecoveredFromSpRecover）。
    /// - 傷害使用 actualDamage，不用 value；只有 COUNTER、LUCK_EVENT、BUFF_EFFECT 沒有 actualDamage，才用 value。
    /// - 部位破壞欄位（partId / partName / partDamage / partDurability）一律不使用，
    ///   partDamage 只是扣在部位耐久上的量，不是額外傷害。
    /// </summary>
    public static class BattleEventHandlers
    {
        // =====================================================================
        // 攻擊類：DAMAGE / BLOCK / MISS / COUNTER
        // =====================================================================

        /// <summary>
        /// DAMAGE：
        /// - isCounter 為 true 的 DAMAGE 與前一筆 COUNTER 是同一次傷害，略過避免重複。
        /// - actor 輸出 ＋actualDamage，target 承傷 ＋actualDamage（同陣營規則見 ApplyAttackDamage）。
        /// - 有 isCrit 欄位才算一次出手；沒有 isCrit 的 DAMAGE（REFLECTION 反射碎片、DIRECT 被動直接傷害、
        ///   three_brothers_guard 替身承受）只累計傷害，不算出手。
        /// </summary>
        public static bool HandleDamage(BattleStatsContext ctx, BattleReportLog log)
        {
            if (log.IsCounter == true) return true;

            if (IsBlank(log.ActorId) || IsBlank(log.TargetId) || !log.ActualDamage.HasValue) return false;

            bool hasCritField = log.IsCrit.HasValue;
            ctx.ApplyAttackDamage(log.ActorId!, log.TargetId!, ToLong(log.ActualDamage.Value),
                countAsShot: hasCritField, isCrit: log.IsCrit == true);
            return true;
        }

        /// <summary>
        /// BLOCK：actor（攻擊方）輸出、target（被攻擊方）承傷，使用 actualDamage。
        /// 每筆 BLOCK 都算一次出手（isCrit 為 true 則爆擊 ＋1）。
        /// actualDamage 為 0 代表被完全擋下（替身承受／BOSS 無敵）：不累計傷害，但仍算一次出手。
        /// 方向不可相反：actor 一定是攻擊方，target 一定是被攻擊方。
        /// </summary>
        public static bool HandleBlock(BattleStatsContext ctx, BattleReportLog log)
        {
            if (IsBlank(log.ActorId) || IsBlank(log.TargetId) || !log.ActualDamage.HasValue) return false;

            ctx.ApplyAttackDamage(log.ActorId!, log.TargetId!, ToLong(log.ActualDamage.Value),
                countAsShot: true, isCrit: log.IsCrit == true);
            return true;
        }

        /// <summary>
        /// MISS（攻擊落空）：actor 是攻擊方，target 是閃避方。
        /// actor 出手 ＋1（不是爆擊，不累計傷害）。
        /// </summary>
        public static bool HandleMiss(BattleStatsContext ctx, BattleReportLog log)
        {
            if (IsBlank(log.ActorId)) return false;

            ctx.ApplyMiss(log.ActorId!, log.TargetId);
            return true;
        }

        /// <summary>
        /// COUNTER（反擊）：actor 輸出 ＋value，target 承傷 ＋value。
        /// 不算出手、不算爆擊；partHits 欄位不使用，一律用 value。
        /// </summary>
        public static bool HandleCounter(BattleStatsContext ctx, BattleReportLog log)
        {
            if (IsBlank(log.ActorId) || IsBlank(log.TargetId) || !log.Value.HasValue) return false;

            ctx.ApplyAttackDamage(log.ActorId!, log.TargetId!, ToLong(log.Value.Value),
                countAsShot: false, isCrit: false);
            return true;
        }

        // =====================================================================
        // 其他造成傷害 / 治療的事件
        // =====================================================================

        /// <summary>
        /// LUCK_EVENT：actor 承傷 ＋value；opponentId（對立陣營）輸出 ＋value。不算出手。
        /// value 可能為 0。
        /// </summary>
        public static bool HandleLuckEvent(BattleStatsContext ctx, BattleReportLog log)
        {
            if (IsBlank(log.ActorId) || IsBlank(log.OpponentId) || !log.Value.HasValue) return false;

            long value = ToLong(log.Value.Value);
            ctx.GetUnit(log.ActorId!).Taken += value;
            ctx.GetUnit(log.OpponentId!).Output += value;
            return true;
        }

        /// <summary>HEAL：actor 治療 ＋value（算在施放者）。actor 與 target 可相同（自我治療）。</summary>
        public static bool HandleHeal(BattleStatsContext ctx, BattleReportLog log)
        {
            if (IsBlank(log.ActorId) || !log.Value.HasValue) return false;

            ctx.GetUnit(log.ActorId!).Heal += ToLong(log.Value.Value);
            return true;
        }

        /// <summary>
        /// BUFF_EFFECT：
        /// - HOT：治療 ＋value，算在受益者本人（actorId）。
        /// - DOT：受害者（actorId）承傷 ＋value，不歸屬任何人的輸出。
        /// - 沒有 buffType（只有敘述）：略過（視為已辨識、不計入無法辨識）。
        /// </summary>
        public static bool HandleBuffEffect(BattleStatsContext ctx, BattleReportLog log)
        {
            if (string.IsNullOrWhiteSpace(log.BuffType)) return true;

            if (IsBlank(log.ActorId) || !log.Value.HasValue) return false;

            long value = ToLong(log.Value.Value);
            switch (log.BuffType!.ToUpperInvariant())
            {
                case "HOT":
                    ctx.GetUnit(log.ActorId!).Heal += value;
                    return true;
                case "DOT":
                    ctx.GetUnit(log.ActorId!).Taken += value;
                    return true;
                default:
                    return false; // 未知的 buffType
            }
        }

        /// <summary>
        /// SP_RECOVER：value 是 SP，不是治療，不可累計。
        /// 但部分 message 含「回復了 N 點體力與 M 點生命值」（例：喝熱湯），M 是真實的 HP 回復，
        /// 此時 actor 治療 ＋M（M 為 0 或不符合格式就不加）。
        /// </summary>
        public static bool HandleSpRecover(BattleStatsContext ctx, BattleReportLog log)
        {
            if (IsBlank(log.ActorId)) return false;

            if (TryParseHpRecoveredFromSpRecover(log.Message, out long hp) && hp > 0)
                ctx.GetUnit(log.ActorId!).Heal += hp;
            return true;
        }

        // 「與 (\d+) 點生命值」：SP_RECOVER 文字中 HP 回復量的格式
        private static readonly Regex SpRecoverHpPattern = new(@"與\s*(\d+)\s*點生命值", RegexOptions.Compiled);

        /// <summary>
        /// 【整份規格中唯一需要解析 message 的地方】
        /// SP_RECOVER 的結構化欄位只有 SP（value），HP 回復量只存在於 message 文字中。
        /// 若日後 LOG 改版（例如新增結構化的 hpRecovered 欄位），只需修改這個函式。
        /// </summary>
        private static bool TryParseHpRecoveredFromSpRecover(string? message, out long hp)
        {
            hp = 0;
            if (string.IsNullOrEmpty(message)) return false;

            var match = SpRecoverHpPattern.Match(message);
            return match.Success && long.TryParse(match.Groups[1].Value, out hp);
        }

        // =====================================================================
        // 狀態類：DEATH / SUMMON
        // =====================================================================

        /// <summary>DEATH：記錄倒下的單位（玩家的「倒下」標記由此判斷）。</summary>
        public static bool HandleDeath(BattleStatsContext ctx, BattleReportLog log)
        {
            if (IsBlank(log.ActorId)) return false;

            ctx.Result.FallenIds.Add(log.ActorId!);
            return true;
        }

        /// <summary>SUMMON：只用來建立召喚者對照表（targetId 召喚物 → actorId 召喚者），不影響數據。</summary>
        public static bool HandleSummon(BattleStatsContext ctx, BattleReportLog log)
        {
            if (IsBlank(log.ActorId) || IsBlank(log.TargetId)) return false;

            ctx.RegisterSummon(log.TargetId!, log.ActorId!);
            return true;
        }

        // =====================================================================
        // 工具
        // =====================================================================

        private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

        private static long ToLong(double value) => (long)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
