using System;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 訓練 API（POST /api/action/explore）的完整 response。
    /// </summary>
    public class TrainingResponse
    {
        // 上一次行動的時間（ISO 8601 格式字串）
        [JsonPropertyName("lastActionAt")]
        public DateTimeOffset LastActionAt { get; set; }

        [JsonPropertyName("serverTime")]
        public DateTimeOffset ServerTime { get; set; }

        // cooldownRemainingMs：目前剩餘冷卻時間（ms）
        // 用於顯示「還要等多久」
        [JsonPropertyName("cooldownRemainingMs")]
        public int CooldownRemainingMs { get; set; }

        // cooldownMs：這個行動的完整冷卻時間（ms）
        // 下一次執行前需要等待的基礎時間
        [JsonPropertyName("cooldownMs")]
        public int CooldownMs { get; set; }

        // pendingEvent 說明：
        // PendingEvent? 代表這個屬性可能為 null。
        // 當 JSON 中 "pendingEvent": null 或欄位不存在時，
        // 這個屬性就是 null，不會拋出例外。
        // 程式邏輯：if (response.PendingEvent != null) { 處理事件 }
        [JsonPropertyName("pendingEvent")]
        public PendingEvent? PendingEvent { get; set; }

        // actionResult 也可能在某些錯誤情況下不存在，所以用 nullable
        [JsonPropertyName("actionResult")]
        public ActionResult? ActionResult { get; set; }
    }
}
