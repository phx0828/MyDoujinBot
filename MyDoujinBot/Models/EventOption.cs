using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// pendingEvent.options 中的每一個選項。
    /// 注意：successChance 屬性在 JSON 中可能完全不存在，
    /// 也可能存在但為 null，必須用 int?（nullable）處理。
    /// </summary>
    public class EventOption
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        // checkStat 可能為 null（代表這個選項不需要判定）
        [JsonPropertyName("checkStat")]
        public string? CheckStat { get; set; }

        [JsonPropertyName("baseDC")]
        public int? BaseDC { get; set; }

        [JsonPropertyName("effectiveStat")]
        public int? EffectiveStat { get; set; }

        // successChance 說明：
        // int? 是 C# 的「可為 null 的值型別」（Nullable<int>）。
        // 一般的 int 不能是 null，但 int? 可以。
        //
        // 這個欄位在 JSON 中有三種情況：
        // 1. "successChance": 65     → 有值，int? = 65
        // 2. "successChance": null   → 存在但為 null，int? = null
        // 3. （欄位完全不存在）       → System.Text.Json 預設 int? = null
        //
        // 所以只要宣告成 int?，三種情況都能正確處理，不會 NullReferenceException。
        [JsonPropertyName("successChance")]
        public int? SuccessChance { get; set; }
    }
}
