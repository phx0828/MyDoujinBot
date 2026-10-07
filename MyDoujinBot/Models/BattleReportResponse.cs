using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// GET /api/battle-reports/{reportId} 的回傳資料（戰報）。
    ///
    /// 設計重點：
    /// 1. 只宣告「戰鬥數據」功能用得到的欄位，其餘欄位（_id、userIds、imagePath…）一律不宣告，
    ///    System.Text.Json 會自動忽略，API 日後新增欄位也不會壞。
    /// 2. 頂層 userIds 不一定包含所有玩家，不可作為玩家清單，所以刻意不宣告。
    /// 3. 玩家以 userId、敵方以 entityId 識別，players 內的 _id 不是 userId，不可使用。
    /// </summary>
    public class BattleReportResponse
    {
        [JsonPropertyName("reportId")]
        public long ReportId { get; set; }

        [JsonPropertyName("battleType")]
        public string? BattleType { get; set; }

        /// <summary>注意：boss 是物件，不是字串。</summary>
        [JsonPropertyName("boss")]
        public BattleReportBoss? Boss { get; set; }

        /// <summary>WIN / LOSE</summary>
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("createdAt")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("participants")]
        public BattleReportParticipants? Participants { get; set; }

        /// <summary>戰後結果。可能缺少某位玩家，缺少時 isDead 視為 false。</summary>
        [JsonPropertyName("outcomes")]
        public List<BattleReportOutcome>? Outcomes { get; set; }

        [JsonPropertyName("logs")]
        public List<BattleReportLog>? Logs { get; set; }
    }

    public class BattleReportBoss
    {
        [JsonPropertyName("floorNumber")]
        public int? FloorNumber { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }

    public class BattleReportParticipants
    {
        [JsonPropertyName("players")]
        public List<BattleReportPlayer>? Players { get; set; }

        [JsonPropertyName("enemies")]
        public List<BattleReportEnemy>? Enemies { get; set; }
    }

    /// <summary>參戰玩家。UserId 為唯一識別，不可用名稱比對。</summary>
    public class BattleReportPlayer
    {
        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        /// <summary>玩家名稱</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("characterName")]
        public string? CharacterName { get; set; }

        [JsonPropertyName("characterId")]
        public string? CharacterId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("level")]
        public int? Level { get; set; }

        /// <summary>hp, atk, def, sta, agi, spd, tec, int, luk（用字典以容許日後新增能力值）。</summary>
        [JsonPropertyName("stats")]
        public Dictionary<string, double>? Stats { get; set; }
    }

    /// <summary>
    /// 敵方單位。EntityId 為唯一識別（MonsterId 不可作為唯一識別）。
    /// 召喚物也可能出現在此清單，帶有 SummonerId，且 Stats 可能為 null。
    /// </summary>
    public class BattleReportEnemy
    {
        [JsonPropertyName("entityId")]
        public string? EntityId { get; set; }

        [JsonPropertyName("monsterId")]
        public string? MonsterId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("floor")]
        public int? Floor { get; set; }

        [JsonPropertyName("level")]
        public int? Level { get; set; }

        /// <summary>召喚者的 userId 或 entityId。</summary>
        [JsonPropertyName("summonerId")]
        public string? SummonerId { get; set; }

        [JsonPropertyName("stats")]
        public Dictionary<string, double>? Stats { get; set; }
    }

    public class BattleReportOutcome
    {
        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        /// <summary>注意：此處是角色名稱，不是玩家名稱，不可用來對應玩家。</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("expGained")]
        public int? ExpGained { get; set; }

        [JsonPropertyName("newFloor")]
        public int? NewFloor { get; set; }

        [JsonPropertyName("isDead")]
        public bool? IsDead { get; set; }
    }

    /// <summary>
    /// 單筆戰鬥 LOG 事件。只保留統計會用到的結構化欄位（不解析 message，SP_RECOVER 除外）。
    ///
    /// Value / ActualDamage / IsCrit / IsCounter 使用「寬鬆轉換器」：
    /// 某些事件類型（例如 SANITY_CHANGE）可能在同名欄位放入非預期型別，
    /// 寬鬆轉換器遇到型別不符時回傳 null，避免整份戰報因單筆事件而解析失敗。
    /// </summary>
    public class BattleReportLog
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("actorId")]
        public string? ActorId { get; set; }

        [JsonPropertyName("targetId")]
        public string? TargetId { get; set; }

        /// <summary>LUCK_EVENT 的對立陣營單位。</summary>
        [JsonPropertyName("opponentId")]
        public string? OpponentId { get; set; }

        [JsonPropertyName("skillId")]
        public string? SkillId { get; set; }

        /// <summary>BUFF_EFFECT：HOT / DOT。</summary>
        [JsonPropertyName("buffType")]
        public string? BuffType { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("value")]
        [JsonConverter(typeof(LenientNumberConverter))]
        public double? Value { get; set; }

        /// <summary>實際扣血量（已扣除致命一擊溢出）。傷害一律使用此欄位，不用 Value。</summary>
        [JsonPropertyName("actualDamage")]
        [JsonConverter(typeof(LenientNumberConverter))]
        public double? ActualDamage { get; set; }

        /// <summary>有此欄位（非 null）才算一次「出手」。</summary>
        [JsonPropertyName("isCrit")]
        [JsonConverter(typeof(LenientBoolConverter))]
        public bool? IsCrit { get; set; }

        /// <summary>true 代表此 DAMAGE 與前一筆 COUNTER 是同一次傷害（重複）。</summary>
        [JsonPropertyName("isCounter")]
        [JsonConverter(typeof(LenientBoolConverter))]
        public bool? IsCounter { get; set; }
    }

    /// <summary>數字欄位的寬鬆轉換器：非數字（或無法轉換的字串）一律回傳 null 而不丟例外。</summary>
    public class LenientNumberConverter : JsonConverter<double?>
    {
        public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    return reader.TryGetDouble(out var number) ? number : null;
                case JsonTokenType.String:
                    return double.TryParse(reader.GetString(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
        {
            if (value.HasValue) writer.WriteNumberValue(value.Value);
            else writer.WriteNullValue();
        }
    }

    /// <summary>布林欄位的寬鬆轉換器：非布林一律回傳 null 而不丟例外。</summary>
    public class LenientBoolConverter : JsonConverter<bool?>
    {
        public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True: return true;
                case JsonTokenType.False: return false;
                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
        {
            if (value.HasValue) writer.WriteBooleanValue(value.Value);
            else writer.WriteNullValue();
        }
    }
}
