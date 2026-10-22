using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 訓練 API（POST /api/action/explore）的完整 response 規格。
    /// </summary>
    public class TrainingResponse
    {
        // 上一次行動的時間（ISO 8601 格式字串）
        [JsonPropertyName("lastActionAt")]
        public DateTimeOffset? LastActionAt { get; set; }

        [JsonPropertyName("serverTime")]
        public DateTimeOffset ServerTime { get; set; }

        // cooldownRemainingMs：目前剩餘冷卻時間（ms）
        // 用於顯示「還要等多久」
        [JsonPropertyName("cooldownRemainingMs")]
        public int CooldownRemainingMs { get; set; }

        // cooldownMs：這個行動的完整冷卻時間（ms）
        // 下一次執行前需要等待的基礎時間
        [JsonPropertyName("cooldownMs")]
        public int CooldownMs { get; set; }

        // pendingEvent 說明：
        // PendingEvent? 代表這個屬性可能為 null（未觸發事件時為 null）。
        // 當 JSON 中 "pendingEvent": null 或欄位不存在時，
        // 這個屬性就是 null，不會拋出例外。
        // 程式邏輯：if (response.PendingEvent != null) { 處理事件 }
        [JsonPropertyName("pendingEvent")]
        public PendingEvent? PendingEvent { get; set; }

        // isMaxLevel：當前出戰角色是否已達等級上限
        [JsonPropertyName("isMaxLevel")]
        public bool IsMaxLevel { get; set; }

        // activeCharacter：當前出戰角色即時資料物件
        [JsonPropertyName("activeCharacter")]
        public ActiveCharacter? ActiveCharacter { get; set; }

        // actionResult 也可能在某些錯誤情況下不存在，所以用 nullable
        [JsonPropertyName("actionResult")]
        public ActionResult? ActionResult { get; set; }
    }

    /// <summary>
    /// 出戰角色即時資訊（explore response 內 activeCharacter 欄位）
    /// </summary>
    public class ActiveCharacter
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("characterId")]
        public string CharacterId { get; set; } = string.Empty;

        [JsonPropertyName("formId")]
        public string? FormId { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("exp")]
        public int Exp { get; set; }

        [JsonPropertyName("totalExp")]
        public int TotalExp { get; set; }

        [JsonPropertyName("combatStats")]
        public CombatStats? CombatStats { get; set; }

        [JsonPropertyName("isDead")]
        public bool IsDead { get; set; }

        [JsonPropertyName("isSoiled")]
        public bool IsSoiled { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("imagePath")]
        public string ImagePath { get; set; } = string.Empty;

        [JsonPropertyName("stats")]
        public CharacterStats? Stats { get; set; }

        [JsonPropertyName("normalAttackId")]
        public string NormalAttackId { get; set; } = string.Empty;

        [JsonPropertyName("skillIds")]
        public List<string> SkillIds { get; set; } = new();

        [JsonPropertyName("appearance")]
        public CharacterAppearance? Appearance { get; set; }

        [JsonPropertyName("totalCombatStats")]
        public CombatStats? TotalCombatStats { get; set; }

        [JsonPropertyName("reincarnationCount")]
        public int ReincarnationCount { get; set; }

        [JsonPropertyName("doujinOwned")]
        public bool? DoujinOwned { get; set; }
    }

    /// <summary>
    /// 戰績統計資訊（本輪 combatStats 與生涯 totalCombatStats）
    /// </summary>
    public class CombatStats
    {
        [JsonPropertyName("activeThrows")]
        public int ActiveThrows { get; set; }

        [JsonPropertyName("defenseThrows")]
        public int DefenseThrows { get; set; }

        [JsonPropertyName("assaultSoiled")]
        public int AssaultSoiled { get; set; }

        [JsonPropertyName("counterSoiled")]
        public int CounterSoiled { get; set; }

        [JsonPropertyName("wins")]
        public int Wins { get; set; }

        [JsonPropertyName("losses")]
        public int Losses { get; set; }

        [JsonPropertyName("tricks")]
        public int Tricks { get; set; }
    }

    /// <summary>
    /// 角色紙娃娃外觀設定
    /// </summary>
    public class CharacterAppearance
    {
        [JsonPropertyName("frontHair")]
        public string FrontHair { get; set; } = string.Empty;

        [JsonPropertyName("backHair")]
        public string BackHair { get; set; } = string.Empty;

        [JsonPropertyName("brows")]
        public string Brows { get; set; } = string.Empty;

        [JsonPropertyName("eyes")]
        public string Eyes { get; set; } = string.Empty;

        [JsonPropertyName("skin")]
        public string Skin { get; set; } = string.Empty;

        [JsonPropertyName("outfit")]
        public string Outfit { get; set; } = string.Empty;

        [JsonPropertyName("frontHairStyle")]
        public string FrontHairStyle { get; set; } = string.Empty;

        [JsonPropertyName("backHairStyle")]
        public string BackHairStyle { get; set; } = string.Empty;

        [JsonPropertyName("browStyle")]
        public string BrowStyle { get; set; } = string.Empty;

        [JsonPropertyName("eyeStyle")]
        public string EyeStyle { get; set; } = string.Empty;

        [JsonPropertyName("outfitStyle")]
        public string OutfitStyle { get; set; } = string.Empty;
    }
}
