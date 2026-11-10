using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 關注群友儲存項目（記錄玩家 ID 與暱稱）。
    /// </summary>
    public class FollowedPlayerEntry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;
    }
}

