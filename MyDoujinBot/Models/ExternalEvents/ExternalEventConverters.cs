using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Models.ExternalEvents
{
    /// <summary>
    /// 解鎖欄位轉換器。
    /// 在第三方事件簡要檔中：
    /// - 若無解鎖，值為 boolean false
    /// - 若有解鎖，值為角色名稱字串 (例如 "二階堂希羅")
    /// 本轉換器將 false 與 null 轉為 null，將字串轉為角色名稱。
    /// </summary>
    public class ExternalEventUnlockConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.False => null,
                JsonTokenType.True => "true",
                JsonTokenType.String => reader.GetString(),
                _ => throw new JsonException($"非預期的解鎖欄位型別: {reader.TokenType}")
            };
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteBooleanValue(false);
            }
            else
            {
                writer.WriteStringValue(value);
            }
        }
    }

    /// <summary>
    /// 遭遇戰欄位轉換器。
    /// 在第三方事件路線檔中：
    /// - false: 不進遭遇戰
    /// - true: 進入遭遇戰
    /// - "success": 僅在檢定成功時才進入遭遇戰
    /// </summary>
    public class ExternalRouteEncounterConverter : JsonConverter<ExternalRouteEncounter>
    {
        public override ExternalRouteEncounter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.False => new ExternalRouteEncounter
                {
                    HasEncounter = false,
                    SuccessOnly = false,
                    RawValue = "false"
                },
                JsonTokenType.True => new ExternalRouteEncounter
                {
                    HasEncounter = true,
                    SuccessOnly = false,
                    RawValue = "true"
                },
                JsonTokenType.String => reader.GetString() switch
                {
                    "success" => new ExternalRouteEncounter
                    {
                        HasEncounter = true,
                        SuccessOnly = true,
                        RawValue = "success"
                    },
                    var s => new ExternalRouteEncounter
                    {
                        HasEncounter = !string.IsNullOrEmpty(s),
                        SuccessOnly = false,
                        RawValue = s ?? string.Empty
                    }
                },
                _ => new ExternalRouteEncounter()
            };
        }

        public override void Write(Utf8JsonWriter writer, ExternalRouteEncounter value, JsonSerializerOptions options)
        {
            if (value.SuccessOnly)
            {
                writer.WriteStringValue("success");
            }
            else
            {
                writer.WriteBooleanValue(value.HasEncounter);
            }
        }
    }
}
