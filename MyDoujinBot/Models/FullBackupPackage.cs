using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MyDoujinBot.Services;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 全域資料一鍵匯出/匯入封裝包模型。
    /// 包含完整系統設定（含 Token）、所有排程方案與所有事件自選方案。
    /// </summary>
    public class FullBackupPackage
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("exportedAt")]
        public DateTime ExportedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("settings")]
        public AppSettings? Settings { get; set; }

        [JsonPropertyName("pipelinePresets")]
        public List<PipelinePreset> PipelinePresets { get; set; } = new();

        [JsonPropertyName("eventPresets")]
        public List<EventChoicePreset> EventPresets { get; set; } = new();
    }
}
