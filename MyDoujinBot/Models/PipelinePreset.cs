using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 階梯行動排程方案 (Pipeline Preset)。
    /// 封裝單套排程的所有階段與中繼資料。
    /// </summary>
    public class PipelinePreset : IPresetProfile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("name")]
        public string Name { get; set; } = "預設排程方案";

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("stages")]
        public List<PipelineStage> Stages { get; set; } = new();

        /// <summary>
        /// 深層複製此方案並賦予新名稱與獨立 ID。
        /// </summary>
        public PipelinePreset Clone(string newName)
        {
            var json = JsonSerializer.Serialize(this);
            var cloned = JsonSerializer.Deserialize<PipelinePreset>(json) ?? new PipelinePreset();
            cloned.Id = Guid.NewGuid().ToString("N");
            cloned.Name = newName;
            cloned.CreatedAt = DateTime.Now;
            cloned.UpdatedAt = DateTime.Now;

            // 為各階段產生新識別碼，避免參照重複
            foreach (var stage in cloned.Stages)
            {
                stage.Id = Guid.NewGuid().ToString("N");
            }

            return cloned;
        }

        public override string ToString() => $"{Name} ({Stages.Count} 階段)";
    }
}
