using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 事件自選方案 (Event Choice Preset)。
    /// 實作 IPresetProfile 介面，供 PresetManager 進行儲存與匯出匯入。
    /// </summary>
    public class EventChoicePreset : IPresetProfile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("name")]
        public string Name { get; set; } = "預設方案";

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Key: eventId (即 event.key，例如 "hiro_e001_the_list")
        /// Value: optionId (即 option.key，例如 "hiro_opt_e001_red")
        /// </summary>
        [JsonPropertyName("choices")]
        public Dictionary<string, string> Choices { get; set; } = new();

        /// <summary>
        /// 複製方案並賦予新名稱與獨立 ID。
        /// </summary>
        public EventChoicePreset Clone(string newName)
        {
            var json = JsonSerializer.Serialize(this);
            var cloned = JsonSerializer.Deserialize<EventChoicePreset>(json) ?? new EventChoicePreset();
            cloned.Id = Guid.NewGuid().ToString("N");
            cloned.Name = newName;
            cloned.CreatedAt = DateTime.Now;
            cloned.UpdatedAt = DateTime.Now;
            return cloned;
        }

        public override string ToString() => Name;
    }
}
