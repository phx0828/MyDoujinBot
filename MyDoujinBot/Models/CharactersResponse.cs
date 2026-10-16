using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 全角色清單與轉生配點庫 API（GET /api/characters）的回傳資料規格。
    /// </summary>
    public class CharactersResponse
    {
        [JsonPropertyName("deprioritizedCharacterIds")]
        public List<string> DeprioritizedCharacterIds { get; set; } = new();

        [JsonPropertyName("jumpMaxFloor")]
        public int JumpMaxFloor { get; set; }

        [JsonPropertyName("characters")]
        public List<CharacterEntry> Characters { get; set; } = new();

        [JsonPropertyName("reincarnation")]
        public ReincarnationInfo? Reincarnation { get; set; }
    }

    /// <summary>
    /// 角色清單中的單一角色項目（可為一般角色或自訂同人角色）
    /// </summary>
    public class CharacterEntry
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
        public string? NormalAttackId { get; set; }

        [JsonPropertyName("skillIds")]
        public List<string>? SkillIds { get; set; }

        [JsonPropertyName("appearance")]
        public CharacterAppearance? Appearance { get; set; }

        [JsonPropertyName("reincarnationIdentity")]
        public ReincarnationIdentity? ReincarnationIdentity { get; set; }

        [JsonPropertyName("legacyLevel")]
        public int LegacyLevel { get; set; }

        [JsonPropertyName("reincarnationStats")]
        public CharacterStats? ReincarnationStats { get; set; }
    }

    /// <summary>
    /// 轉生識別外觀資訊
    /// </summary>
    public class ReincarnationIdentity
    {
        [JsonPropertyName("formId")]
        public string? FormId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("imagePath")]
        public string ImagePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// 轉生點數與層數解鎖狀態
    /// </summary>
    public class ReincarnationInfo
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("spent")]
        public int Spent { get; set; }

        [JsonPropertyName("available")]
        public int Available { get; set; }

        [JsonPropertyName("allocated")]
        public ReincarnationAllocated? Allocated { get; set; }

        [JsonPropertyName("maxPoints")]
        public int MaxPoints { get; set; }

        [JsonPropertyName("advancedUnlocked")]
        public bool AdvancedUnlocked { get; set; }

        [JsonPropertyName("advancedUnlockFloor")]
        public int AdvancedUnlockFloor { get; set; }

        [JsonPropertyName("utilityUnlocked")]
        public bool UtilityUnlocked { get; set; }

        [JsonPropertyName("utilityUnlockFloor")]
        public int UtilityUnlockFloor { get; set; }

        [JsonPropertyName("legacyUnlocked")]
        public bool LegacyUnlocked { get; set; }

        [JsonPropertyName("legacyUnlockFloor")]
        public int LegacyUnlockFloor { get; set; }
    }

    /// <summary>
    /// 轉生配點分配項目
    /// </summary>
    public class ReincarnationAllocated
    {
        [JsonPropertyName("hp")]
        public int HP { get; set; }

        [JsonPropertyName("atk")]
        public int Atk { get; set; }

        [JsonPropertyName("def")]
        public int Def { get; set; }

        [JsonPropertyName("sta")]
        public int Sta { get; set; }

        [JsonPropertyName("agi")]
        public int Agi { get; set; }

        [JsonPropertyName("spd")]
        public int Spd { get; set; }

        [JsonPropertyName("tec")]
        public int Tec { get; set; }

        [JsonPropertyName("int")]
        public int Int { get; set; }

        [JsonPropertyName("luk")]
        public int Luk { get; set; }

        [JsonPropertyName("expBonus")]
        public int ExpBonus { get; set; }

        [JsonPropertyName("eventBonus")]
        public int EventBonus { get; set; }

        [JsonPropertyName("effortBonus")]
        public int EffortBonus { get; set; }

        [JsonPropertyName("aggroIncrease")]
        public int AggroIncrease { get; set; }

        [JsonPropertyName("aggroDecrease")]
        public int AggroDecrease { get; set; }
    }
}
