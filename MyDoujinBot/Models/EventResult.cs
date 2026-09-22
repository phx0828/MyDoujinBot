using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 事件選擇 API 的 response。
    /// 記錄骰子結果、是否成功、以及獲得的獎勵。
    /// </summary>
    public class EventResult
    {
        // 判定用的 stat 縮寫（例如 "int", "tec"），可能為 null（無判定選項）
        [JsonPropertyName("stat")]
        public string? Stat { get; set; }

        [JsonPropertyName("statValue")]
        public int? StatValue { get; set; }

        [JsonPropertyName("effectiveStat")]
        public int? EffectiveStat { get; set; }

        // roll：骰子點數
        [JsonPropertyName("roll")]
        public int? Roll { get; set; }

        // total：roll + effectiveStat
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        // dc：難度閾值（Difficulty Class）
        [JsonPropertyName("dc")]
        public int? DC { get; set; }

        [JsonPropertyName("chance")]
        public int? Chance { get; set; }

        [JsonPropertyName("critical")]
        public string? Critical { get; set; }

        // isSuccess：事件成功或失敗
        // 這是判斷「遊戲事件結果」的關鍵欄位
        // 注意：HTTP 200 只代表 API Request 成功，
        //       isSuccess 才代表遊戲內事件的成敗
        [JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; }

        // 事件結果的敘述文字
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        // rewards 可能為 null（無獎勵）
        [JsonPropertyName("rewards")]
        public EventRewards? Rewards { get; set; }

        [JsonPropertyName("battleResult")]
        public object? BattleResult { get; set; }
    }

    /// <summary>
    /// 事件獎勵區塊。
    /// </summary>
    public class EventRewards
    {
        // bonusStats 可能為 null 或空物件 {}
        // 使用 Dictionary<string, int>? 可以同時處理：
        // 1. bonusStats 不存在 → null
        // 2. bonusStats: null  → null
        // 3. bonusStats: {}    → 空 Dictionary（Count == 0）
        // 4. bonusStats: { "int": 1 } → Dictionary { "int"->1 }
        [JsonPropertyName("bonusStats")]
        public Dictionary<string, int>? BonusStats { get; set; }
    }
}
