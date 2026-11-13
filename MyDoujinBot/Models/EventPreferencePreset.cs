using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 事件偏好方案 (Event Preference Preset)。
    /// 實作 IPresetProfile 介面，供 PresetManager 進行儲存與匯出匯入。
    /// </summary>
    public class EventPreferencePreset : IPresetProfile
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
        /// 勾選要降低事件出現機率的角色 ID 清單（對應 CharacterEntry.CharacterId）。
        /// </summary>
        [JsonPropertyName("deprioritizedCharacterIds")]
        public List<string> DeprioritizedCharacterIds { get; set; } = new();

        /// <summary>
        /// 複製方案並賦予新名稱與獨立 ID。
        /// </summary>
        public EventPreferencePreset Clone(string newName)
        {
            var json = JsonSerializer.Serialize(this);
            var cloned = JsonSerializer.Deserialize<EventPreferencePreset>(json) ?? new EventPreferencePreset();
            cloned.Id = Guid.NewGuid().ToString("N");
            cloned.Name = newName;
            cloned.CreatedAt = DateTime.Now;
            cloned.UpdatedAt = DateTime.Now;
            return cloned;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// PUT /api/characters/event-preferences 的請求與回應資料規格。
    /// </summary>
    public class EventPreferencesPayload
    {
        [JsonPropertyName("deprioritizedCharacterIds")]
        public List<string> DeprioritizedCharacterIds { get; set; } = new();
    }
}

