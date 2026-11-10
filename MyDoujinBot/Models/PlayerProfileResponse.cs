using System;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 單一玩家詳細資訊 API (GET /api/players/{id}) 回傳結構。
    /// </summary>
    public class PlayerProfileResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [JsonPropertyName("character")]
        public CharacterInfo? Character { get; set; }
    }

    /// <summary>
    /// 玩家當前出戰角色詳細資訊（包含等級、經驗、本輪戰績、狀態、稱號、立繪路徑與九圍能力值）。
    /// </summary>
    public class CharacterInfo
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("characterId")]
        public string CharacterId { get; set; } = string.Empty;

        [JsonPropertyName("formId")]
        public string? FormId { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("exp")]
        public int Exp { get; set; }

        [JsonPropertyName("totalExp")]
        public int TotalExp { get; set; }

        [JsonPropertyName("combatStats")]
        public CombatStats? CombatStats { get; set; }

        [JsonPropertyName("isDead")]
        public bool IsDead { get; set; }

        [JsonPropertyName("isSoiled")]
        public bool IsSoiled { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("imagePath")]
        public string ImagePath { get; set; } = string.Empty;

        [JsonPropertyName("stats")]
        public CharacterStats? Stats { get; set; }
    }
}
