using System.Text.Json.Serialization;

namespace MyDoujinBot.Models.ExternalEvents
{
    /// <summary>
    /// 第三方事件簡要檔案 (DefaultEventBrief.json) 根模型。
    /// 結構為 Dictionary&lt;事件名稱, Dictionary&lt;選項文字, 選項結果&gt;&gt;。
    /// </summary>
    public class ExternalEventBriefFile : Dictionary<string, Dictionary<string, ExternalEventBriefOption>>
    {
    }

    /// <summary>
    /// 簡要事件中的單一選項資料。
    /// 包含 success（成功結果）與可選的 failed（失敗結果）。
    /// </summary>
    public class ExternalEventBriefOption
    {
        /// <summary>
        /// 成功時之結果與能力變化。
        /// </summary>
        [JsonPropertyName("success")]
        public ExternalEventBriefOutcome? Success { get; set; }

        /// <summary>
        /// 失敗時之結果與能力變化（若無判定或不會失敗則為 null）。
        /// </summary>
        [JsonPropertyName("failed")]
        public ExternalEventBriefOutcome? Failed { get; set; }
    }

    /// <summary>
    /// 單一結果分支之細節（成功或失敗）。
    /// </summary>
    public class ExternalEventBriefOutcome
    {
        /// <summary>
        /// 能力值增減字典。
        /// 鍵名簡寫：hp、攻、防、體、敏、反、技、智、幸。數值為整數。
        /// </summary>
        [JsonPropertyName("stats")]
        public Dictionary<string, int> Stats { get; set; } = new();

        /// <summary>
        /// 是否進入遭遇戰。
        /// </summary>
        [JsonPropertyName("encounter")]
        public bool Encounter { get; set; }

        /// <summary>
        /// 解鎖角色名稱。
        /// 原文無解鎖時為 false，解鎖時為角色名稱字串（例如 "二階堂希羅"）。
        /// 透過 <see cref="ExternalEventUnlockConverter"/> 轉換為 string?（無解鎖時為 null）。
        /// </summary>
        [JsonPropertyName("unlock")]
        [JsonConverter(typeof(ExternalEventUnlockConverter))]
        public string? Unlock { get; set; }

        /// <summary>
        /// 前置條件說明文字（可選，存在時記錄該選項出現的條件描述）。
        /// </summary>
        [JsonPropertyName("requires")]
        public string? Requires { get; set; }
    }
}
