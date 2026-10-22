using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    /// 統計過程中的共用狀態與基本操作（單位表、召喚者對照表、陣營判斷、累計傷害、BUFF 歸屬四層判定）。
    ///
    /// 事件處理函式（BattleEventHandlers）只負責「讀事件、決定怎麼記」，
    /// 實際累加一律透過這個類別的方法，避免各事件重複實作歸屬／同陣營規則。
    /// </summary>
    public class BattleStatsContext
    {
        // 防止召喚者對照表出現循環時無限迴圈
        private const int MaxSummonChainDepth = 8;

        private static readonly Regex BuffExtractRegex = new(@"受到\s+(?<name>.+?)\s+影響", RegexOptions.Compiled);

        private readonly Dictionary<string, string> _summonerOf = new();
        private readonly Dictionary<(string TargetId, string BuffKey), string> _activeBuffs = new();

        // 動態前綴與技能施放者映射（完全不寫死角色名稱，依本場出戰敵方與實際施放日誌動態學習）
        private readonly Dictionary<string, HashSet<string>> _prefixToActors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _skillToActors = new(StringComparer.OrdinalIgnoreCase);

        public BattleStatsContext(BattleReportIndex index, IReadOnlyList<BattleReportLog?>? logs = null)
        {
            Index = index;
            Logs = logs;

            // 以 participants.enemies 的 summonerId 先補充召喚者對照表
            foreach (var pair in index.DeclaredSummoners)
                RegisterSummon(pair.Key, pair.Value);

            InitializeDynamicPrefixesAndSkills();
        }

        public BattleReportIndex Index { get; }

        public IReadOnlyList<BattleReportLog?>? Logs { get; }

        /// <summary>目前處理中的 LOG 索引（供 Tier 3 回溯使用）</summary>
        public int CurrentLogIndex { get; set; }

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

        public string? GetUnitDisplayName(string unitId)
        {
            foreach (var p in Index.Players)
            {
                if (p.UserId == unitId)
                    return p.CharacterName ?? p.Name;
            }
            foreach (var e in Index.Enemies)
            {
                if (e.Id == unitId)
                    return e.CharacterName ?? e.Name;
            }
            return null;
        }

        // ---------------------------------------------------------------------
        // BUFF 追蹤與歸屬判定 (四階層邏輯)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Tier 1 主動註冊：記錄 BUFF 上去的時機與對象，建立 (targetId, buffKey) → casterId 映射。
        /// </summary>
        public void RegisterActiveBuff(string targetId, string? buffKey, string casterId)
        {
            if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(buffKey) || string.IsNullOrWhiteSpace(casterId))
                return;

            _activeBuffs[(targetId, buffKey)] = casterId;
        }

        /// <summary>
        /// 解析 BUFF_EFFECT 的施放者（四階層判定）：
        /// Tier 1: 運行時主動註冊表 (Active Buff Mapping)
        /// Tier 2: 角色專屬 buffId 前綴表 (Character Prefix Mapping)
        /// Tier 3: 深度回溯至起點 (Deep Backwards Tracing to Index 0)
        /// Tier 4: 無法判定（回傳 null，由呼叫端記錄至 UnattributedBuffs）
        /// </summary>
        public string? ResolveBuffCaster(BattleReportLog log, int currentIndex)
        {
            string targetId = log.ActorId ?? string.Empty;
            string? buffId = log.BuffId;
            string? skillId = log.SkillId;
            string? buffName = null;

            if (!string.IsNullOrEmpty(log.Message))
            {
                var match = BuffExtractRegex.Match(log.Message);
                if (match.Success)
                    buffName = match.Groups["name"].Value.Trim();
            }

            // Tier 1: 運行時主動註冊表
            if (!string.IsNullOrEmpty(buffId) && _activeBuffs.TryGetValue((targetId, buffId), out var caster1))
                return caster1;
            if (!string.IsNullOrEmpty(buffName) && _activeBuffs.TryGetValue((targetId, buffName), out var caster2))
                return caster2;
            if (!string.IsNullOrEmpty(skillId) && _activeBuffs.TryGetValue((targetId, skillId), out var caster3))
                return caster3;

            // Tier 2: 動態技能與前綴對照 (Dynamic Skill & Prefix Mapping，不寫死任何角色名稱)
            // 2a: 精確匹配本場出戰角色曾施放之 skillId（特別適用於「同人」等借用他人技能之角色）
            if (!string.IsNullOrEmpty(buffId) && _skillToActors.TryGetValue(buffId, out var skillActors) && skillActors.Count == 1)
            {
                foreach (var caster in skillActors)
                {
                    RegisterActiveBuff(targetId, buffId, caster);
                    if (!string.IsNullOrEmpty(buffName)) RegisterActiveBuff(targetId, buffName, caster);
                    return caster;
                }
            }

            // 2b: 動態前綴比對（自敵方 monsterId / entityId 及出戰日誌中學習到的技能前綴）
            if (!string.IsNullOrEmpty(buffId))
            {
                string? prefix = ExtractPrefix(buffId);
                if (!string.IsNullOrEmpty(prefix) && _prefixToActors.TryGetValue(prefix, out var matchedActors) && matchedActors.Count == 1)
                {
                    foreach (var caster in matchedActors)
                    {
                        RegisterActiveBuff(targetId, buffId, caster);
                        if (!string.IsNullOrEmpty(buffName)) RegisterActiveBuff(targetId, buffName, caster);
                        return caster;
                    }
                }
            }

            // Tier 3: 深度回溯至起點 (回溯到底，至 index 0)
            if (Logs != null && (!string.IsNullOrEmpty(buffName) || !string.IsNullOrEmpty(buffId)))
            {
                string? targetName = GetUnitDisplayName(targetId);

                for (int j = currentIndex - 1; j >= 0; j--)
                {
                    var prev = Logs[j];
                    if (prev == null || string.IsNullOrWhiteSpace(prev.ActorId)) continue;
                    // BUFF_EFFECT 是狀態結算事件（actorId 為承受效果之受害者而非施放者），不可作為施放來源
                    if (string.Equals(prev.Type, "BUFF_EFFECT", StringComparison.OrdinalIgnoreCase)) continue;

                    string prevMsg = prev.Message ?? string.Empty;
                    bool buffMatches = (!string.IsNullOrEmpty(buffName) && prevMsg.Contains(buffName, StringComparison.Ordinal))
                                    || (!string.IsNullOrEmpty(buffId) && (string.Equals(prev.BuffId, buffId, StringComparison.OrdinalIgnoreCase) || string.Equals(prev.SkillId, buffId, StringComparison.OrdinalIgnoreCase)));

                    if (buffMatches)
                    {
                        bool targetMatches = string.Equals(prev.TargetId, targetId, StringComparison.Ordinal)
                                          || (!string.IsNullOrEmpty(targetName) && prevMsg.Contains(targetName, StringComparison.Ordinal));

                        if (targetMatches)
                        {
                            string caster = prev.ActorId!;
                            if (!string.IsNullOrEmpty(buffName)) RegisterActiveBuff(targetId, buffName, caster);
                            if (!string.IsNullOrEmpty(buffId)) RegisterActiveBuff(targetId, buffId, caster);
                            return caster;
                        }
                    }
                }
            }

            // Tier 4: 無法判定施放者
            return null;
        }

        private void InitializeDynamicPrefixesAndSkills()
        {
            // 1. 動態從敵方 entityId / monsterId 建立前綴對照
            foreach (var e in Index.Enemies)
            {
                string? enemyId = e.Id;
                if (string.IsNullOrEmpty(enemyId)) continue;

                if (!string.IsNullOrWhiteSpace(e.MonsterId))
                {
                    AddPrefixMapping(e.MonsterId.Trim().ToLowerInvariant() + "_", enemyId);
                }

                if (!string.IsNullOrWhiteSpace(e.EntityId))
                {
                    var parts = e.EntityId.Split('_');
                    if (parts.Length >= 3 && parts[0].Equals("mob", StringComparison.OrdinalIgnoreCase))
                    {
                        AddPrefixMapping(parts[2].Trim().ToLowerInvariant() + "_", enemyId);
                    }
                }
            }

            // 2. 動態從出戰日誌中，學習每位角色施放的技能 skillId 與其前綴
            if (Logs != null)
            {
                var prefixUsage = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

                foreach (var log in Logs)
                {
                    if (log == null || string.IsNullOrWhiteSpace(log.ActorId) || string.IsNullOrWhiteSpace(log.SkillId))
                        continue;

                    string actorId = log.ActorId!;
                    string skillId = log.SkillId!.Trim();

                    if (!_skillToActors.TryGetValue(skillId, out var actors))
                    {
                        actors = new HashSet<string>(StringComparer.Ordinal);
                        _skillToActors[skillId] = actors;
                    }
                    actors.Add(actorId);

                    string? prefix = ExtractPrefix(skillId);
                    if (!string.IsNullOrEmpty(prefix) &&
                        !prefix.Equals("basic_", StringComparison.OrdinalIgnoreCase) &&
                        !prefix.Equals("three_", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!prefixUsage.TryGetValue(prefix, out var usageMap))
                        {
                            usageMap = new Dictionary<string, int>(StringComparer.Ordinal);
                            prefixUsage[prefix] = usageMap;
                        }
                        usageMap[actorId] = usageMap.TryGetValue(actorId, out int count) ? count + 1 : 1;
                    }
                }

                // 若該前綴在日誌中主要由某位特定角色施放，建立 prefix -> actorId
                foreach (var (prefix, usageMap) in prefixUsage)
                {
                    if (_prefixToActors.ContainsKey(prefix)) continue; // 敵方 monsterId 明確指定者優先

                    string? primaryActor = null;
                    int maxCount = 0;
                    foreach (var (actorId, count) in usageMap)
                    {
                        if (count > maxCount)
                        {
                            maxCount = count;
                            primaryActor = actorId;
                        }
                    }
                    if (primaryActor != null)
                    {
                        AddPrefixMapping(prefix, primaryActor);
                    }
                }
            }
        }

        private void AddPrefixMapping(string prefix, string actorId)
        {
            if (!_prefixToActors.TryGetValue(prefix, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                _prefixToActors[prefix] = set;
            }
            set.Add(actorId);
        }

        private static string? ExtractPrefix(string id)
        {
            int idx = id.IndexOf('_');
            return idx > 0 ? id.Substring(0, idx + 1).ToLowerInvariant() : null;
        }

        /// <summary>記錄無法歸屬的 BUFF_EFFECT（Tier 4）供戰報底部呈現</summary>
        public void RecordUnattributedBuff(string targetId, BattleReportLog log, long value)
        {
            string? buffName = null;
            if (!string.IsNullOrEmpty(log.Message))
            {
                var match = BuffExtractRegex.Match(log.Message);
                if (match.Success)
                    buffName = match.Groups["name"].Value.Trim();
            }

            string buffId = log.BuffId ?? string.Empty;
            string buffType = log.BuffType ?? "DOT";
            string key = !string.IsNullOrWhiteSpace(buffId) ? buffId : (!string.IsNullOrWhiteSpace(buffName) ? buffName : "unknown");

            if (!Result.UnattributedBuffs.TryGetValue(key, out var record))
            {
                record = new UnattributedBuffRecord
                {
                    BuffId = buffId,
                    BuffName = buffName ?? string.Empty,
                    BuffType = buffType
                };
                Result.UnattributedBuffs[key] = record;
            }

            record.TotalValue += value;
            record.Count++;

            string targetName = GetUnitDisplayName(targetId) ?? targetId;
            record.ValueByTarget[targetName] = record.ValueByTarget.TryGetValue(targetName, out var v) ? v + value : value;
        }

        // ---------------------------------------------------------------------
        // 累計
        // ---------------------------------------------------------------------

        /// <summary>
        /// 累計一次「攻擊造成的傷害」（DAMAGE / BLOCK / COUNTER 共用）。
        ///
        /// - target 承傷一律 ＋damage。
        /// - 同陣營傷害（倒戈誤傷／自傷／波及友軍）：
        ///   target 累計 Taken 與 FriendlyDamageTaken；
        ///   actor 累計 FriendlyDamageDealt；若 countAsShot 為 true 累計 FriendlyShots。
        /// - 跨陣營：輸出算在攻擊歸屬者（召喚物歸召喚者）；countAsShot 為 true 時
        ///   出手 ＋1，isCrit 為 true 再加爆擊 ＋1。
        /// </summary>
        public void ApplyAttackDamage(string actorId, string targetId, long damage, bool countAsShot, bool isCrit)
        {
            GetUnit(targetId).Taken += damage;

            if (IsSameTeam(actorId, targetId))
            {
                GetUnit(targetId).FriendlyDamageTaken += damage;
                var friendlyOwner = GetUnit(ResolveAttackOwnerId(actorId));
                friendlyOwner.FriendlyDamageDealt += damage;
                if (countAsShot) friendlyOwner.FriendlyShots++;
                return;
            }

            var owner = GetUnit(ResolveAttackOwnerId(actorId));
            owner.Output += damage;
            AddShot(owner, countAsShot, isCrit);
        }

        /// <summary>
        /// 累計一次落空的攻擊（MISS）：
        /// - 同陣營：累計 FriendlyShots。
        /// - 跨陣營：攻擊方出手 ＋1，不算命中、不是爆擊、不累計傷害。
        /// </summary>
        public void ApplyMiss(string actorId, string? targetId)
        {
            if (!string.IsNullOrEmpty(targetId) && IsSameTeam(actorId, targetId))
            {
                var friendlyOwner = GetUnit(ResolveAttackOwnerId(actorId));
                friendlyOwner.FriendlyShots++;
                return;
            }

            AddShot(GetUnit(ResolveAttackOwnerId(actorId)), countAsShot: true, isCrit: false, isHit: false);
        }

        private static void AddShot(UnitStats unit, bool countAsShot, bool isCrit, bool isHit = true)
        {
            if (!countAsShot) return;
            unit.Shots++;
            if (!isHit) return;
            unit.Hits++;
            if (isCrit) unit.Crits++;
        }
    }
}
