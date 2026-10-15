using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyDoujinBot.Services
{
    /// <summary>
    /// 應用程式設定的持久化管理器。
    ///
    /// 儲存位置：與 EXE 同一資料夾下的 settings.json
    /// </summary>
    public static class AppSettingsManager
    {
        private static readonly string _settingsDir = AppDomain.CurrentDomain.BaseDirectory;

        private static readonly string _settingsPath =
            Path.Combine(_settingsDir, "settings.json");

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>目前載入的設定（單例）</summary>
        public static AppSettings Current { get; private set; } = new();

        /// <summary>
        /// 從磁碟讀取設定。
        /// 若檔案不存在或格式損壞，靜默使用預設值。
        /// 應在應用程式啟動時呼叫一次。
        /// </summary>
        public static void Load()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    Current = new AppSettings();
                    return;
                }

                var json = File.ReadAllText(_settingsPath);
                Current = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            }
            catch
            {
                Current = new AppSettings();
            }
        }

        /// <summary>
        /// 將目前設定寫入磁碟。
        /// 每次使用者修改設定後呼叫。
        /// </summary>
        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(_settingsDir);
                var json = JsonSerializer.Serialize(Current, _jsonOptions);
                File.WriteAllText(_settingsPath, json);
            }
            catch
            {
                // 寫入失敗時靜默忽略
            }
        }
    }

    /// <summary>
    /// 應用程式設定資料類別。
    ///
    /// EventMode 值：
    ///   "auto"        → 自動選擇（最高成功率）
    ///   "auto-random" → 自動選擇（全隨機）
    ///   "popup"       → 手動選擇 + 彈出視窗
    ///   "inline"      → 手動選擇 + 內嵌於主畫面
    ///
    /// ManualDisplayMode 值：
    ///   "popup"  → 彈出視窗（奪取焦點）
    ///   "inline" → 內嵌於主畫面（零干擾）
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Bearer Token（使用者同意儲存，非高度敏感資料）。
        /// </summary>
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// 遭遇事件應對模式。
        /// "auto" = 自動（最高成功率）；"auto-random" = 自動（全隨機）；"popup" = 手動+彈窗；"inline" = 手動+內嵌
        /// </summary>
        [JsonPropertyName("eventMode")]
        public string EventMode { get; set; } = "auto";

        /// <summary>
        /// 手動選擇時的顯示模式（popup / inline）。
        /// 預設：彈出視窗。
        /// </summary>
        [JsonPropertyName("manualDisplayMode")]
        public string ManualDisplayMode { get; set; } = "popup";

        /// <summary>
        /// 是否啟用 Windows 系統氣泡通知（右下角 BalloonTip）。
        /// 僅在手動+內嵌模式有效。
        /// 預設：關閉。
        /// </summary>
        [JsonPropertyName("enableSystemNotify")]
        public bool EnableSystemNotify { get; set; } = false;

        /// <summary>
        /// 是否啟用 Windows 系統通知的音效。
        /// 預設：關閉。
        /// </summary>
        [JsonPropertyName("enableSystemNotifySound")]
        public bool EnableSystemNotifySound { get; set; } = false;

        /// <summary>
        /// 最後一次戰鬥的目標玩家 ID。
        /// </summary>
        [JsonPropertyName("lastBattleTargetId")]
        public string LastBattleTargetId { get; set; } = string.Empty;

        /// <summary>
        /// 預設的戰鬥模式（Challenge 或 Chado）。
        /// </summary>
        [JsonPropertyName("battleMode")]
        public string BattleMode { get; set; } = "Challenge";

        /// <summary>
        /// 訓練項目的輪流清單
        /// </summary>
        [JsonPropertyName("trainingSequence")]
        public System.Collections.Generic.List<string> TrainingSequence { get; set; } = new();

        /// <summary>
        /// 主執行模式："training"（自動訓練）或 "battle"（自動戰鬥）。預設："training"。
        /// </summary>
        [JsonPropertyName("mainMode")]
        public string MainMode { get; set; } = "training";

        /// <summary>
        /// 執行限制模式："count"（指定次數）或 "time"（指定時間）。預設："time"。
        /// </summary>
        [JsonPropertyName("executionMode")]
        public string ExecutionMode { get; set; } = "time";

        /// <summary>
        /// 指定執行次數。預設：500。
        /// </summary>
        [JsonPropertyName("targetCount")]
        public int TargetCount { get; set; } = 500;

        /// <summary>
        /// 指定執行時間（分鐘）。預設：1440（24 小時）。
        /// </summary>
        [JsonPropertyName("targetMinutes")]
        public int TargetMinutes { get; set; } = 1440;

        /// <summary>
        /// 額外冷卻延遲（秒）。預設：1。
        /// </summary>
        [JsonPropertyName("extraDelay")]
        public int ExtraDelay { get; set; } = 1;

        /// <summary>
        /// 戰鬥執行限制模式："count"（指定次數）或 "time"（指定時間）。預設："time"。
        /// </summary>
        [JsonPropertyName("battleExecutionMode")]
        public string BattleExecutionMode { get; set; } = "time";

        /// <summary>
        /// 戰鬥指定執行次數。預設：500。
        /// </summary>
        [JsonPropertyName("battleTargetCount")]
        public int BattleTargetCount { get; set; } = 500;

        /// <summary>
        /// 戰鬥指定執行時間（分鐘）。預設：1440（24 小時）。
        /// </summary>
        [JsonPropertyName("battleTargetMinutes")]
        public int BattleTargetMinutes { get; set; } = 1440;

        /// <summary>
        /// 戰鬥額外冷卻延遲（秒）。預設：1。
        /// </summary>
        [JsonPropertyName("battleExtraDelay")]
        public int BattleExtraDelay { get; set; } = 1;

        /// <summary>
        /// 階梯排程額外冷卻延遲（秒）。預設：1。
        /// </summary>
        [JsonPropertyName("pipelineExtraDelay")]
        public int PipelineExtraDelay { get; set; } = 1;

        /// <summary>
        /// 最後一次選擇的訓練動作 ID。
        /// </summary>
        [JsonPropertyName("lastSelectedActionId")]
        public string LastSelectedActionId { get; set; } = string.Empty;

        /// <summary>
        /// 階梯行動排程清單
        /// </summary>
        [JsonPropertyName("pipelineStages")]
        public System.Collections.Generic.List<MyDoujinBot.Models.PipelineStage> PipelineStages { get; set; } = new();

        /// <summary>
        /// 當前啟用的階梯排程方案 ID。
        /// </summary>
        [JsonPropertyName("activePipelinePresetId")]
        public string ActivePipelinePresetId { get; set; } = string.Empty;

        /// <summary>
        /// 當前選取的導航分頁
        /// </summary>
        [JsonPropertyName("activeTab")]
        public string ActiveTab { get; set; } = "training";
    }
}
