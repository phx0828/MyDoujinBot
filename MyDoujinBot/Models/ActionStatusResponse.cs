using System;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 行動與即時狀態 API（GET /api/action/status）的回傳資料規格。
    /// </summary>
    public class ActionStatusResponse
    {
        [JsonPropertyName("lastActionAt")]
        public DateTimeOffset? LastActionAt { get; set; }

        [JsonPropertyName("serverTime")]
        public DateTimeOffset ServerTime { get; set; }

        [JsonPropertyName("cooldownRemainingMs")]
        public int CooldownRemainingMs { get; set; }

        [JsonPropertyName("cooldownMs")]
        public int CooldownMs { get; set; }

        [JsonPropertyName("floorReward")]
        public FloorRewardInfo? FloorReward { get; set; }

        [JsonPropertyName("pendingEvent")]
        public PendingEvent? PendingEvent { get; set; }

        [JsonPropertyName("isMaxLevel")]
        public bool IsMaxLevel { get; set; }

        [JsonPropertyName("trainingSpec")]
        public TrainingSpecInfo? TrainingSpec { get; set; }
    }

    /// <summary>
    /// 樓層獎勵狀態結構
    /// </summary>
    public class FloorRewardInfo
    {
        [JsonPropertyName("floor")]
        public int Floor { get; set; }

        [JsonPropertyName("exp")]
        public int Exp { get; set; }

        [JsonPropertyName("cooldownMs")]
        public long CooldownMs { get; set; }

        [JsonPropertyName("cooldownRemainingMs")]
        public long CooldownRemainingMs { get; set; }

        [JsonPropertyName("serverTime")]
        public DateTimeOffset ServerTime { get; set; }

        [JsonPropertyName("isDead")]
        public bool IsDead { get; set; }

        [JsonPropertyName("isMaxLevel")]
        public bool IsMaxLevel { get; set; }
    }

    /// <summary>
    /// 委託/長時間訓練設定規格
    /// </summary>
    public class TrainingSpecInfo
    {
        [JsonPropertyName("minMinutes")]
        public int MinMinutes { get; set; }

        [JsonPropertyName("maxMinutes")]
        public int MaxMinutes { get; set; }

        [JsonPropertyName("stepMinutes")]
        public int StepMinutes { get; set; }
    }
}
