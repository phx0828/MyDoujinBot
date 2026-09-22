using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 訓練 API response 中的 actionResult 區塊。
    /// 記錄這次訓練的行動名稱、獲得的 EXP、等級資訊與能力成長。
    /// </summary>
    public class ActionResult
    {
        // JsonPropertyName 說明：
        // C# 屬性名稱習慣用 PascalCase（大寫開頭），
        // 但 API 的 JSON 是 camelCase（小寫開頭）。
        // [JsonPropertyName("actionId")] 告訴 System.Text.Json：
        // 序列化/反序列化時，這個屬性對應 JSON 的 "actionId" 欄位。

        [JsonPropertyName("actionId")]
        public string ActionId { get; set; } = string.Empty;

        [JsonPropertyName("actionName")]
        public string ActionName { get; set; } = string.Empty;

        [JsonPropertyName("exp")]
        public int Exp { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        // levelUps：本次訓練升了幾級（通常是 0，偶爾是 1 以上）
        [JsonPropertyName("levelUps")]
        public int LevelUps { get; set; }

        // gains：各能力的成長值
        // 使用 Dictionary<string, int> 而不是固定屬性，
        // 好處是：如果未來 API 新增能力，不需要修改這個 class
        // 例如 JSON: { "hp": 0, "atk": 1, "int": 2 }
        // 會變成 Dictionary: { "hp"->0, "atk"->1, "int"->2 }
        [JsonPropertyName("gains")]
        public Dictionary<string, int>? Gains { get; set; }
    }
}
