using System;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 領取樓層獎勵 API（POST /api/action/floor-reward）之回傳結構。
    /// </summary>
    public class FloorRewardClaimResponse
    {
        [JsonPropertyName("floor")]
        public int Floor { get; set; }

        [JsonPropertyName("exp")]
        public long Exp { get; set; }

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

        [JsonPropertyName("rewardResult")]
        public FloorRewardResultInfo? RewardResult { get; set; }
    }

    /// <summary>
    /// 樓層獎勵領取結果詳細資訊
    /// </summary>
    public class FloorRewardResultInfo
    {
        [JsonPropertyName("floor")]
        public int Floor { get; set; }

        [JsonPropertyName("exp")]
        public long Exp { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("levelUps")]
        public int LevelUps { get; set; }

        [JsonPropertyName("gains")]
        public FloorRewardGainsInfo? Gains { get; set; }
    }

    /// <summary>
    /// 樓層獎勵能力值成長增量
    /// </summary>
    public class FloorRewardGainsInfo
    {
        [JsonPropertyName("hp")]
        public int Hp { get; set; }

        [JsonPropertyName("atk")]
        public int Atk { get; set; }

        [JsonPropertyName("def")]
        public int Def { get; set; }

        [JsonPropertyName("sta")]
        public int Sta { get; set; }

        [JsonPropertyName("agi")]
        public int Agi { get; set; }

        [JsonPropertyName("spd")]
        public int Spd { get; set; }

        [JsonPropertyName("tec")]
        public int Tec { get; set; }

        [JsonPropertyName("int")]
        public int Int { get; set; }

        [JsonPropertyName("luk")]
        public int Luk { get; set; }
    }
}
