using System;
using System.Collections.Generic;
using System.Linq;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 戰報對照表（索引層）。
    ///
    /// 職責：把 participants.players / participants.enemies / outcomes 完整保存，
    /// 並以 userId / entityId 建立對照表，供後續查詢「誰（來源）對誰（目標）做了什麼」。
    ///
    /// 規則：
    /// - 玩家清單以 participants.players 為準（頂層 userIds 不可作為玩家清單）。
    /// - 一律以 userId / entityId 對應，不比對名稱字串（不同玩家可能有相近名稱，例：洛奇希、洛奇希2）。
    /// - outcomes 以 userId 對應；缺少某位玩家時 isDead 視為 false。
    /// </summary>
    public class BattleReportIndex
    {
        private readonly Dictionary<string, BattleReportPlayer> _players = new();
        private readonly Dictionary<string, BattleReportEnemy> _enemies = new();
        private readonly Dictionary<string, BattleReportOutcome> _outcomes = new();
        private readonly List<BattleReportPlayer> _playerList = new();
        private readonly List<BattleReportEnemy> _enemyList = new();

        private BattleReportIndex() { }

        /// <summary>參戰玩家（依 API 順序）。</summary>
        public IReadOnlyList<BattleReportPlayer> Players => _playerList;

        /// <summary>敵方與 BOSS 單位（依 API 順序）。</summary>
        public IReadOnlyList<BattleReportEnemy> Enemies => _enemyList;

        /// <summary>userId → 玩家</summary>
        public IReadOnlyDictionary<string, BattleReportPlayer> PlayerById => _players;

        /// <summary>entityId → 敵方單位（含 API 列在 enemies 內的召喚物）</summary>
        public IReadOnlyDictionary<string, BattleReportEnemy> EnemyById => _enemies;

        public bool IsPlayer(string id) => _players.ContainsKey(id);

        public bool IsEnemy(string id) => _enemies.ContainsKey(id);

        /// <summary>
        /// 取得 participants.enemies 內標註的召喚者（summonerId）。
        /// 敵方單位不是召喚物（沒有 summonerId）時回傳 null。
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> DeclaredSummoners =>
            _enemies
                .Where(e => !string.IsNullOrWhiteSpace(e.Value.SummonerId))
                .Select(e => new KeyValuePair<string, string>(e.Key, e.Value.SummonerId!));

        /// <summary>
        /// 戰後是否真正死亡。outcomes 缺少該玩家（或沒有 isDead 欄位）時視為 false，不報錯。
        /// </summary>
        public bool IsDead(string userId) =>
            _outcomes.TryGetValue(userId, out var outcome) && outcome.IsDead == true;

        /// <summary>
        /// ID → 顯示名稱（玩家取角色名稱、敵方取敵方名稱，查無則回傳 ID 本身）。
        /// LOG 中的 ID 一律透過此方法轉成名稱，不直接比對名稱字串。
        /// </summary>
        public string GetDisplayName(string id)
        {
            if (_players.TryGetValue(id, out var player))
                return string.IsNullOrWhiteSpace(player.CharacterName) ? (player.Name ?? id) : player.CharacterName!;
            if (_enemies.TryGetValue(id, out var enemy))
                return string.IsNullOrWhiteSpace(enemy.Name) ? id : enemy.Name!;
            return id;
        }

        /// <summary>
        /// 由戰報建立對照表。戰報缺少必要結構（回傳格式異常）時回傳 false 並說明原因。
        /// </summary>
        public static bool TryCreate(BattleReportResponse? report, out BattleReportIndex index, out string error)
        {
            index = new BattleReportIndex();
            error = string.Empty;

            if (report == null)
            {
                error = "戰報內容為空。";
                return false;
            }

            var players = report.Participants?.Players;
            if (players == null || players.Count == 0)
            {
                error = "找不到玩家清單（participants.players）。";
                return false;
            }

            if (report.Logs == null)
            {
                error = "找不到戰鬥 LOG（logs）。";
                return false;
            }

            foreach (var player in players)
            {
                if (string.IsNullOrWhiteSpace(player?.UserId)) continue;
                index._players[player.UserId!] = player;
                index._playerList.Add(player);
            }

            if (index._playerList.Count == 0)
            {
                error = "玩家清單內沒有任何有效的 userId。";
                return false;
            }

            foreach (var enemy in report.Participants?.Enemies ?? new List<BattleReportEnemy>())
            {
                if (string.IsNullOrWhiteSpace(enemy?.EntityId)) continue;
                index._enemies[enemy.EntityId!] = enemy;
                index._enemyList.Add(enemy);
            }

            foreach (var outcome in report.Outcomes ?? new List<BattleReportOutcome>())
            {
                if (string.IsNullOrWhiteSpace(outcome?.UserId)) continue;
                index._outcomes[outcome.UserId!] = outcome;
            }

            return true;
        }
    }
}
