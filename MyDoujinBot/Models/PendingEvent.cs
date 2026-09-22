using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 訓練 API response 中的 pendingEvent 區塊。
    /// 這整個物件在 JSON 中可能為 null（代表沒有觸發事件），
    /// 所以在 TrainingResponse 中宣告為 PendingEvent?（nullable）。
    /// </summary>
    public class PendingEvent
    {
        [JsonPropertyName("eventId")]
        public string EventId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        // List<EventOption> 對應 JSON 的 array
        // 例如："options": [ {...}, {...}, {...} ]
        [JsonPropertyName("options")]
        public List<EventOption> Options { get; set; } = new();
    }
}
