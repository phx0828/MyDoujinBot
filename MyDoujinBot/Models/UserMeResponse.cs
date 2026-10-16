using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 玩家自身基本資訊 API（GET /api/users/me）的回傳資料規格。
    /// </summary>
    public class UserMeResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("discordId")]
        public string DiscordId { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [JsonPropertyName("activeCharacterId")]
        public string ActiveCharacterId { get; set; } = string.Empty;

        [JsonPropertyName("personalStatus")]
        public string PersonalStatus { get; set; } = string.Empty;

        [JsonPropertyName("currentFloor")]
        public int CurrentFloor { get; set; }
    }
}
