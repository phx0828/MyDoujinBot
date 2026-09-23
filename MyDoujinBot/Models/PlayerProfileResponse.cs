using System;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    public class PlayerProfileResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [JsonPropertyName("character")]
        public CharacterInfo? Character { get; set; }
    }

    public class CharacterInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("level")]
        public int Level { get; set; }
    }
}
