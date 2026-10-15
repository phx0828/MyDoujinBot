using System;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 定義可儲存、命名、匯入匯出之方案 (Preset Profile) 的通用介面。
    /// 適用於：階梯行動排程 (Pipeline)、遭遇事件應對規則 (Event Rules) 等。
    /// </summary>
    public interface IPresetProfile
    {
        string Id { get; set; }
        string Name { get; set; }
        DateTime CreatedAt { get; set; }
        DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 通用方案匯出封裝包，包含中繼資料與分類標籤，便於匯入時檢驗分類與向下相容。
    /// </summary>
    public class PresetPackage<T> where T : class, IPresetProfile
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("exportedAt")]
        public DateTime ExportedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("preset")]
        public T? Preset { get; set; }
    }
}
