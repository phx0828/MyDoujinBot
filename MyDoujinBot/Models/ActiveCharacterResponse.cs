using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 當前出戰角色詳情 API（GET /api/characters/active）的回傳資料規格。
    /// </summary>
    public class ActiveCharacterResponse
    {
        [JsonPropertyName("character")]
        public ActiveCharacter? Character { get; set; }
    }
}
