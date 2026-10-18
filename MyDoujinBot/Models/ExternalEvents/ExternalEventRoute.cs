using System.Text.Json.Serialization;

namespace MyDoujinBot.Models.ExternalEvents
{
    /// <summary>
    /// 第三方詳細事件路線檔案 (DefaultEventRoute.json) 根模型。
    /// 包含各角色線、全事件檢定門檻、能力期望值、前置條件圖譜與攻略建議。
    /// </summary>
    public class ExternalRouteFile
    {
        [JsonPropertyName("about")]
        public ExternalRouteAbout About { get; set; } = new();

        [JsonPropertyName("fields")]
        public Dictionary<string, string> Fields { get; set; } = new();

        [JsonPropertyName("weights")]
        public Dictionary<string, Dictionary<string, double>> Weights { get; set; } = new();

        [JsonPropertyName("chains")]
        public Dictionary<string, ExternalRouteChain> Chains { get; set; } = new();

        [JsonPropertyName("overview")]
        public Dictionary<string, ExternalRouteOverviewProfile> Overview { get; set; } = new();

        [JsonPropertyName("encounter")]
        public ExternalRouteEncounterAdvice Encounter { get; set; } = new();

        [JsonPropertyName("prerequisites")]
        public List<ExternalRoutePrerequisite> Prerequisites { get; set; } = new();
    }

    /// <summary>
    /// 路線檔中繼資料（網站、作者、資料日期、格式、版本等）。
    /// </summary>
    public class ExternalRouteAbout
    {
        [JsonPropertyName("site")]
        public string Site { get; set; } = string.Empty;

        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        [JsonPropertyName("dataDate")]
        public string DataDate { get; set; } = string.Empty;

        [JsonPropertyName("format")]
        public string Format { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("note")]
        public string Note { get; set; } = string.Empty;
    }

    /// <summary>
    /// 單一角色線資料。
    /// </summary>
    public class ExternalRouteChain
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("sub")]
        public string Sub { get; set; } = string.Empty;

        [JsonPropertyName("forced")]
        public bool Forced { get; set; }

        [JsonPropertyName("perfect")]
        public List<ExternalRoutePerfectCondition> Perfect { get; set; } = new();

        [JsonPropertyName("events")]
        public List<ExternalRouteEvent> Events { get; set; } = new();
    }

    /// <summary>
    /// 角色線完美結局條件項目。
    /// </summary>
    public class ExternalRoutePerfectCondition
    {
        [JsonPropertyName("eventKey")]
        public string EventKey { get; set; } = string.Empty;

        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("option")]
        public string Option { get; set; } = string.Empty;

        [JsonPropertyName("confirmed")]
        public bool Confirmed { get; set; }
    }

    /// <summary>
    /// 路線檔中的單一事件節點。
    /// </summary>
    public class ExternalRouteEvent
    {
        [JsonPropertyName("order")]
        public int Order { get; set; }

        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("requires")]
        public List<ExternalRouteEventRequirement> Requires { get; set; } = new();

        [JsonPropertyName("options")]
        public List<ExternalRouteOption> Options { get; set; } = new();

        [JsonPropertyName("atk")]
        public ExternalRouteEventRecommendation? Atk { get; set; }

        [JsonPropertyName("int")]
        public ExternalRouteEventRecommendation? Int { get; set; }

        [JsonPropertyName("route")]
        public ExternalRouteGuidance? Route { get; set; }

        [JsonPropertyName("avoid")]
        public List<ExternalRouteGuidance> Avoid { get; set; } = new();
    }

    /// <summary>
    /// 事件前置條件要求。
    /// </summary>
    public class ExternalRouteEventRequirement
    {
        [JsonPropertyName("eventKey")]
        public string EventKey { get; set; } = string.Empty;

        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("option")]
        public string? Option { get; set; }

        [JsonPropertyName("notOption")]
        public string? NotOption { get; set; }
    }

    /// <summary>
    /// 路線檔中的單一選項資料。
    /// </summary>
    public class ExternalRouteOption
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("check")]
        public string? Check { get; set; }

        [JsonPropertyName("dc")]
        public int? Dc { get; set; }

        [JsonPropertyName("tries")]
        public int Tries { get; set; }

        [JsonPropertyName("success")]
        public int Success { get; set; }

        [JsonPropertyName("rate")]
        public double Rate { get; set; }

        [JsonPropertyName("onSuccess")]
        public Dictionary<string, int> OnSuccess { get; set; } = new();

        [JsonPropertyName("onFail")]
        public Dictionary<string, int>? OnFail { get; set; }

        [JsonPropertyName("encounter")]
        [JsonConverter(typeof(ExternalRouteEncounterConverter))]
        public ExternalRouteEncounter Encounter { get; set; } = new();

        [JsonPropertyName("unlock")]
        public string? Unlock { get; set; }

        [JsonPropertyName("flags")]
        public ExternalRouteOptionFlags? Flags { get; set; }
    }

    /// <summary>
    /// 遭遇戰型態（解析 false / true / "success"）。
    /// </summary>
    public class ExternalRouteEncounter
    {
        /// <summary>
        /// 是否可能進入遭遇戰。
        /// </summary>
        public bool HasEncounter { get; set; }

        /// <summary>
        /// 是否僅在判定成功時才進入遭遇戰。
        /// </summary>
        public bool SuccessOnly { get; set; }

        /// <summary>
        /// 原始值 ("false", "true", "success")。
        /// </summary>
        public string RawValue { get; set; } = "false";

        /// <summary>
        /// 是否為無條件遭遇戰。
        /// </summary>
        [JsonIgnore]
        public bool IsAlways => HasEncounter && !SuccessOnly;

        /// <summary>
        /// 是否為僅在成功時遭遇。
        /// </summary>
        [JsonIgnore]
        public bool IsSuccessOnly => SuccessOnly;

        /// <summary>
        /// 是否不進遭遇戰。
        /// </summary>
        [JsonIgnore]
        public bool IsNone => !HasEncounter;
    }

    /// <summary>
    /// 選項特殊標記集合。
    /// </summary>
    public class ExternalRouteOptionFlags
    {
        [JsonPropertyName("perfect")]
        public bool? Perfect { get; set; }

        [JsonPropertyName("opens")]
        public List<string>? Opens { get; set; }

        [JsonPropertyName("gate")]
        public List<ExternalRouteGateItem>? Gate { get; set; }

        [JsonPropertyName("breaks")]
        public string? Breaks { get; set; }

        [JsonPropertyName("once")]
        public bool? Once { get; set; }

        [JsonPropertyName("guide")]
        public ExternalRouteGuide? Guide { get; set; }
    }

    /// <summary>
    /// 開啟後續選項的閘道項目。
    /// </summary>
    public class ExternalRouteGateItem
    {
        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("option")]
        public string Option { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>
    /// 玩家攻略建議。
    /// </summary>
    public class ExternalRouteGuide
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("why")]
        public string Why { get; set; } = string.Empty;
    }

    /// <summary>
    /// 屬性導向推薦選項（包含攻擊型 atk、智力型 int）。
    /// </summary>
    public class ExternalRouteEventRecommendation
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("option")]
        public string Option { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; set; }
    }

    /// <summary>
    /// 路線指引與理由（包含路線建議 route、迴避選項 avoid）。
    /// </summary>
    public class ExternalRouteGuidance
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("option")]
        public string Option { get; set; } = string.Empty;

        [JsonPropertyName("why")]
        public string Why { get; set; } = string.Empty;
    }

    /// <summary>
    /// 總路線總覽設定檔（如 atk、int）。
    /// </summary>
    public class ExternalRouteOverviewProfile
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("order")]
        public string Order { get; set; } = string.Empty;

        [JsonPropertyName("median")]
        public double Median { get; set; }

        [JsonPropertyName("lines")]
        public List<ExternalRouteOverviewLine> Lines { get; set; } = new();

        [JsonPropertyName("recommend")]
        public List<string> Recommend { get; set; } = new();

        [JsonPropertyName("untick")]
        public List<string> Untick { get; set; } = new();
    }

    /// <summary>
    /// 角色線期望值排行項目。
    /// </summary>
    public class ExternalRouteOverviewLine
    {
        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("sub")]
        public string Sub { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("events")]
        public int Events { get; set; }

        [JsonPropertyName("seen")]
        public int Seen { get; set; }

        [JsonPropertyName("tier")]
        public string Tier { get; set; } = string.Empty;

        [JsonPropertyName("forced")]
        public bool Forced { get; set; }
    }

    /// <summary>
    /// 遭遇戰處理建議方針。
    /// </summary>
    public class ExternalRouteEncounterAdvice
    {
        [JsonPropertyName("atk")]
        public string Atk { get; set; } = string.Empty;

        [JsonPropertyName("int")]
        public string Int { get; set; } = string.Empty;

        [JsonPropertyName("minExp")]
        public int MinExp { get; set; }

        [JsonPropertyName("note")]
        public string Note { get; set; } = string.Empty;
    }

    /// <summary>
    /// 前置條件總表項目。
    /// </summary>
    public class ExternalRoutePrerequisite
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("chain")]
        public string Chain { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("option")]
        public string? Option { get; set; }

        [JsonPropertyName("needEvent")]
        public string? NeedEvent { get; set; }

        [JsonPropertyName("needOption")]
        public string? NeedOption { get; set; }

        [JsonPropertyName("alsoNeed")]
        public List<ExternalRoutePrerequisiteRequirement>? AlsoNeed { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    /// <summary>
    /// 同時需要符合的前置要求（alsoNeed 陣列項）。
    /// </summary>
    public class ExternalRoutePrerequisiteRequirement
    {
        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("option")]
        public string Option { get; set; } = string.Empty;
    }
}
