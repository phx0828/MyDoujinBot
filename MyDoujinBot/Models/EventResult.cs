using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 事件選擇 API 的 response。
    /// 記錄骰子結果、是否成功、以及獲得的獎勵。
    /// </summary>
    public class EventResult
    {
        // 判定用的 stat 縮寫（例如 "int", "tec"），可能為 null（無判定選項）
        [JsonPropertyName("stat")]
        public string? Stat { get; set; }

        [JsonPropertyName("statValue")]
        public int? StatValue { get; set; }

        [JsonPropertyName("effectiveStat")]
        public int? EffectiveStat { get; set; }

        // roll：骰子點數
        [JsonPropertyName("roll")]
        public int? Roll { get; set; }

        // total：roll + effectiveStat
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        // dc：難度閾值（Difficulty Class）
        [JsonPropertyName("dc")]
        public int? DC { get; set; }

        [JsonPropertyName("chance")]
        public int? Chance { get; set; }

        [JsonPropertyName("critical")]
        public string? Critical { get; set; }

        // isSuccess：事件成功或失敗
        // 這是判斷「遊戲事件結果」的關鍵欄位
        // 注意：HTTP 200 只代表 API Request 成功，
        //       isSuccess 才代表遊戲內事件的成敗
        [JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; }

        // 事件結果的敘述文字
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        // rewards 可能為 null（無獎勵）
        [JsonPropertyName("rewards")]
        public EventRewards? Rewards { get; set; }

        [JsonPropertyName("battleResult")]
        public BattleResult? BattleResult { get; set; }

        [JsonPropertyName("gainCharacters")]
        public List<GainCharacter>? GainCharacters { get; set; }
    }

    /// <summary>
    /// 事件獎勵區塊。
    /// </summary>
    public class EventRewards
    {
        // bonusStats 可能為 null 或空物件 {}
        // 使用 Dictionary<string, int>? 可以同時處理：
        // 1. bonusStats 不存在 → null
        // 2. bonusStats: null  → null
        // 3. bonusStats: {}    → 空 Dictionary（Count == 0）
        // 4. bonusStats: { "int": 1 } → Dictionary { "int"->1 }
        [JsonPropertyName("bonusStats")]
        public Dictionary<string, int>? BonusStats { get; set; }
    }

    /// <summary>
    /// 事件中獲得的角色資訊
    /// </summary>
    public class GainCharacter
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("imagePath")]
        public string ImagePath { get; set; } = string.Empty;

        [JsonPropertyName("base")]
        public CharacterStats? Base { get; set; }

        [JsonPropertyName("normalAttack")]
        public string NormalAttack { get; set; } = string.Empty;

        [JsonPropertyName("skills")]
        public List<string> Skills { get; set; } = new();

        [JsonPropertyName("growth")]
        public CharacterStats? Growth { get; set; }
    }

    /// <summary>
    /// 角色的能力值結構 (Base / Growth)
    /// </summary>
    public class CharacterStats
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
    }

    /// <summary>
    /// 戰鬥結果
    /// </summary>
    public class BattleResult
    {
        [JsonPropertyName("winner")]
        public string? Winner { get; set; }

        [JsonPropertyName("logs")]
        public List<BattleLog>? Logs { get; set; }
    }

    /// <summary>
    /// 戰報單筆紀錄
    /// </summary>
    public class BattleLog
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("actorId")]
        public string? ActorId { get; set; }

        [JsonPropertyName("skillId")]
        public string? SkillId { get; set; }

        [JsonPropertyName("skillTier")]
        public string? SkillTier { get; set; }

        [JsonPropertyName("isNormalAttack")]
        public bool? IsNormalAttack { get; set; }

        [JsonPropertyName("targetId")]
        public string? TargetId { get; set; }

        [JsonPropertyName("value")]
        public int? Value { get; set; }

        [JsonPropertyName("actualDamage")]
        public int? ActualDamage { get; set; }

        [JsonPropertyName("isCrit")]
        public bool? IsCrit { get; set; }

        [JsonPropertyName("blocked")]
        public bool? Blocked { get; set; }

        [JsonPropertyName("maxHp")]
        public int? MaxHp { get; set; }

        [JsonPropertyName("isDead")]
        public bool? IsDead { get; set; }

        [JsonPropertyName("expGained")]
        public int? ExpGained { get; set; }

        [JsonPropertyName("tier")]
        public string? Tier { get; set; }
    }
}
