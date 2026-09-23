using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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

        [JsonPropertyName("logs")]
        public List<BattleLog>? Logs { get; set; }
    }
}
