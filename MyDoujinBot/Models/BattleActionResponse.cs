using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MyDoujinBot.Models
{
    public class BattleActionResponse
    {
        [JsonPropertyName("cooldownMs")]
        public int CooldownMs { get; set; }

        [JsonPropertyName("cooldownRemainingMs")]
        public int CooldownRemainingMs { get; set; }

        [JsonPropertyName("expGained")]
        public int ExpGained { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("winner")]
        public string? Winner { get; set; }

        [JsonPropertyName("pendingEvent")]
        public PendingEvent? PendingEvent { get; set; }

        [JsonPropertyName("participants")]
        public BattleReportParticipants? Participants { get; set; }

        [JsonPropertyName("logs")]
        public List<BattleLog>? Logs { get; set; }

        [JsonPropertyName("levelUps")]
        public int LevelUps { get; set; }

        [JsonPropertyName("gains")]
        public Dictionary<string, int>? Gains { get; set; }

        [JsonPropertyName("actionResult")]
        public ActionResult? ActionResult { get; set; }

        /// <summary>
        /// 判斷是否為我方玩家成功對敵方丟下小零食並螺旋下指（嚴格排除敵方對我方潑屎）
        /// 格式範例："{players.characterName} 對 {enemies.characterName} 丟了小零食並螺旋下指"
        /// </summary>
        public bool IsPlayerChadoSuccess()
        {
            if (Logs == null || Logs.Count == 0) return false;

            var playerNames = Participants?.Players?
                .SelectMany(p => new[] { p.CharacterName, p.Name })
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList() ?? new List<string?>();

            var enemyNames = Participants?.Enemies?
                .SelectMany(e => new[] { e.CharacterName, e.Name })
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList() ?? new List<string?>();

            int checkCount = Math.Min(10, Logs.Count);
            for (int i = 1; i <= checkCount; i++)
            {
                var logEntry = Logs[Logs.Count - i];
                if (logEntry.Type == "CHADO_MISS")
                {
                    return false;
                }

                if (logEntry.Type == "CHADO")
                {
                    string msg = logEntry.Message ?? string.Empty;

                    // 若有 participants 資訊，以 正則 解析「A 對 B ... (丟了小零食/螺旋下指)」
                    if (playerNames.Count > 0 && enemyNames.Count > 0)
                    {
                        var match = Regex.Match(msg, @"(?<actor>.+?)\s*對\s*(?<target>.+?)\s*(?:丟|螺旋)");
                        if (match.Success)
                        {
                            string actor = match.Groups["actor"].Value.Trim();
                            string target = match.Groups["target"].Value.Trim();

                            bool isPlayerActor = playerNames.Any(p => !string.IsNullOrEmpty(p) && actor.Contains(p, StringComparison.OrdinalIgnoreCase));
                            bool isEnemyTarget = enemyNames.Any(e => !string.IsNullOrEmpty(e) && target.Contains(e, StringComparison.OrdinalIgnoreCase));

                            if (isPlayerActor && isEnemyTarget)
                            {
                                return true;
                            }

                            bool isEnemyActor = enemyNames.Any(e => !string.IsNullOrEmpty(e) && actor.Contains(e, StringComparison.OrdinalIgnoreCase));
                            bool isPlayerTarget = playerNames.Any(p => !string.IsNullOrEmpty(p) && target.Contains(p, StringComparison.OrdinalIgnoreCase));
                            if (isEnemyActor && isPlayerTarget)
                            {
                                return false;
                            }
                        }
                    }
                    else
                    {
                        // 若無 participants 資訊，退回檢查關鍵字
                        if (msg.Contains("丟了小零食並螺旋下指") || msg.Contains("螺旋下指"))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
