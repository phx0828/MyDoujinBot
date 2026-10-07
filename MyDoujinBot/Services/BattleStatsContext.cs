using System.Collections.Generic;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    /// <summary>陣營（用來判斷是否為同陣營傷害）。</summary>
    public enum BattleTeam
    {
        Unknown,
        Player,
        Enemy
    }

    /// <summary>
    /// 統計過程中的共用狀態與基本操作（單位表、召喚者對照表、陣營判斷、累計傷害）。
    ///
    /// 事件處理函式（BattleEventHandlers）只負責「讀事件、決定怎麼記」，
    /// 實際累加一律透過這個類別的方法，避免各事件重複實作歸屬／同陣營規則。
    /// </summary>
    public class BattleStatsContext
    {
        // 防止召喚者對照表出現循環時無限迴圈
        private const int MaxSummonChainDepth = 8;

        private readonly Dictionary<string, string> _summonerOf = new();

        public BattleStatsContext(BattleReportIndex index)
        {
            Index = index;

            // 以 participants.enemies 的 summonerId 先補充召喚者對照表
            foreach (var pair in index.DeclaredSummoners)
                RegisterSummon(pair.Key, pair.Value);
        }

        public BattleReportIndex Index { get; }

        public BattleStatsResult Result { get; } = new();

        // ---------------------------------------------------------------------
        // 單位 / 召喚物
        // ---------------------------------------------------------------------

        /// <summary>取得單位數據；不在 participants 內的 ID（例如 summon_1）會建立臨時單位，不中斷。</summary>
        public UnitStats GetUnit(string id)
        {
            if (!Result.Units.TryGetValue(id, out var unit))
            {
                unit = new UnitStats(id);
                Result.Units[id] = unit;
            }
            return unit;
        }

        /// <summary>記錄召喚者對照表：summonId（召喚物）→ summonerId（召喚者）。</summary>
        public void RegisterSummon(string summonId, string summonerId)
        {
            if (string.IsNullOrWhiteSpace(summonId) || string.IsNullOrWhiteSpace(summonerId)) return;
            if (summonId == summonerId) return;
            _summonerOf[summonId] = summonerId;
        }

        /// <summary>
        /// 取得「主動攻擊」的歸屬者：召喚物歸召喚者本人（可多層），非召喚物回傳自己。
        /// 僅用於主動攻擊（DAMAGE / BLOCK / MISS / COUNTER 的 actor）；
        /// 召喚物承受的傷害、召喚物的治療不使用此方法，不算到召喚者身上。
        /// </summary>
        public string ResolveAttackOwnerId(string actorId)
        {
            var current = actorId;
            for (int depth = 0; depth < MaxSummonChainDepth; depth++)
            {
                if (!_summonerOf.TryGetValue(current, out var summoner)) break;
                current = summoner;
            }
            return current;
        }

        // ---------------------------------------------------------------------
        // 陣營
        // ---------------------------------------------------------------------

        /// <summary>
        /// 陣營判斷：participants.players 為玩家陣營、participants.enemies 為敵方陣營；
        /// 不在名單內的召喚物，視為與其召喚者同陣營。
        /// </summary>
        public BattleTeam GetTeam(string id)
        {
            var current = id;
            for (int depth = 0; depth <= MaxSummonChainDepth; depth++)
            {
                if (Index.IsPlayer(current)) return BattleTeam.Player;
                if (Index.IsEnemy(current)) return BattleTeam.Enemy;
                if (!_summonerOf.TryGetValue(current, out var summoner)) return BattleTeam.Unknown;
                current = summoner;
            }
            return BattleTeam.Unknown;
        }

        /// <summary>兩個單位是否同陣營（任一方陣營未知時視為不同陣營）。</summary>
        public bool IsSameTeam(string a, string b)
        {
            var teamA = GetTeam(a);
            return teamA != BattleTeam.Unknown && teamA == GetTeam(b);
        }

        // ---------------------------------------------------------------------
        // 累計
        // ---------------------------------------------------------------------

        /// <summary>
        /// 累計一次「攻擊造成的傷害」（DAMAGE / BLOCK / COUNTER 共用）。
        ///
        /// - target 承傷一律 ＋damage。
        /// - 同陣營傷害（含自傷、範圍技波及友軍）：只累計 target 承傷，
        ///   不累計 actor 輸出，也不算出手。
        /// - 跨陣營：輸出算在攻擊歸屬者（召喚物歸召喚者）；countAsShot 為 true 時
        ///   出手 ＋1，isCrit 為 true 再加爆擊 ＋1。
        /// </summary>
        public void ApplyAttackDamage(string actorId, string targetId, long damage, bool countAsShot, bool isCrit)
        {
            GetUnit(targetId).Taken += damage;

            if (IsSameTeam(actorId, targetId)) return;

            var owner = GetUnit(ResolveAttackOwnerId(actorId));
            owner.Output += damage;
            AddShot(owner, countAsShot, isCrit);
        }

        /// <summary>
        /// 累計一次落空的攻擊（MISS）：攻擊方出手 ＋1，不是爆擊、不累計傷害。
        /// 同陣營不算出手。
        /// </summary>
        public void ApplyMiss(string actorId, string? targetId)
        {
            if (!string.IsNullOrEmpty(targetId) && IsSameTeam(actorId, targetId)) return;

            AddShot(GetUnit(ResolveAttackOwnerId(actorId)), true, false);
        }

        private static void AddShot(UnitStats unit, bool countAsShot, bool isCrit)
        {
            if (!countAsShot) return;
            unit.Shots++;
            if (isCrit) unit.Crits++;
        }
    }
}
