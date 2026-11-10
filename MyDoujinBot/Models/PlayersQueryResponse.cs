using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 玩家列表查詢 API (GET /api/players) 回傳結構。
    /// </summary>
    public class PlayersQueryResponse
    {
        [JsonPropertyName("players")]
        public List<PlayerSummaryItem> Players { get; set; } = new();

        [JsonPropertyName("pagination")]
        public PlayersPaginationInfo? Pagination { get; set; }

        [JsonPropertyName("sort")]
        public PlayersSortInfo? Sort { get; set; }
    }

    /// <summary>
    /// 玩家列表單筆玩家摘要資訊。
    /// </summary>
    public class PlayerSummaryItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("imagePath")]
        public string ImagePath { get; set; } = string.Empty;

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [JsonPropertyName("personalStatus")]
        public string PersonalStatus { get; set; } = string.Empty;

        [JsonPropertyName("characterName")]
        public string CharacterName { get; set; } = string.Empty;

        [JsonPropertyName("characterTitle")]
        public string CharacterTitle { get; set; } = string.Empty;

        [JsonPropertyName("formId")]
        public string? FormId { get; set; }

        [JsonPropertyName("floor")]
        public int Floor { get; set; }

        [JsonPropertyName("highestFloor")]
        public int HighestFloor { get; set; }

        [JsonPropertyName("isDead")]
        public bool IsDead { get; set; }

        [JsonPropertyName("isSoiled")]
        public bool IsSoiled { get; set; }
    }

    /// <summary>
    /// 玩家列表分頁資訊。
    /// </summary>
    public class PlayersPaginationInfo
    {
        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("totalPages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("hasNextPage")]
        public bool HasNextPage { get; set; }
    }

    /// <summary>
    /// 玩家列表排序資訊。
    /// </summary>
    public class PlayersSortInfo
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("order")]
        public string Order { get; set; } = string.Empty;
    }
}

