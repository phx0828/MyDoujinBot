using System;
using System.Text.Json.Serialization;
using MyDoujinBot.Services;

namespace MyDoujinBot.Models
{
    public enum StageActionType
    {
        Training,
        Battle
    }

    public enum StageConditionType
    {
        LevelReach,     // 角色等級達到 (例: 等級 >= 80)
        RunCountReach,  // 執行滿指定次數 (例: 執行滿 100 次)
        MinutesReach    // 執行滿指定時間 (分鐘)
    }

    /// <summary>
    /// 階梯行動排程中的單一階段設定。
    /// 由使用者自訂：執行動作、目標條件、以及完成後切換下一階段。
    /// </summary>
    public class PipelineStage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [JsonPropertyName("name")]
        public string Name { get; set; } = "階段 1";

        [JsonPropertyName("actionType")]
        public StageActionType ActionType { get; set; } = StageActionType.Training;

        // --- 若為訓練 ---
        [JsonPropertyName("trainingActionId")]
        public string TrainingActionId { get; set; } = "hunt";

        // --- 若為戰鬥 ---
        [JsonPropertyName("battleTargetId")]
        public string BattleTargetId { get; set; } = string.Empty;

        [JsonPropertyName("battleTargetName")]
        public string BattleTargetName { get; set; } = string.Empty;

        [JsonPropertyName("battleType")]
        public BattleType BattleType { get; set; } = BattleType.Challenge;

        // --- 切換至下一階段的條件 ---
        [JsonPropertyName("conditionType")]
        public StageConditionType ConditionType { get; set; } = StageConditionType.LevelReach;

        [JsonPropertyName("conditionValue")]
        public int ConditionValue { get; set; } = 80;

        /// <summary>格式化取得動作簡述文字</summary>
        public string GetActionSummary()
        {
            if (ActionType == StageActionType.Training)
            {
                var action = System.Linq.Enumerable.FirstOrDefault(TrainingActions.All, a => a.ActionId == TrainingActionId);
                return $"訓練：{action?.DisplayName ?? TrainingActionId}";
            }
            else
            {
                string bType = BattleType == BattleType.Challenge ? "友好切磋" : "我要茶渡你";
                string target = string.IsNullOrEmpty(BattleTargetName) ? BattleTargetId : BattleTargetName;
                return $"戰鬥：{bType} ({target})";
            }
        }

        /// <summary>格式化取得達成條件簡述文字</summary>
        public string GetConditionSummary()
        {
            return ConditionType switch
            {
                StageConditionType.LevelReach => $"角色等級 >= Lv.{ConditionValue}",
                StageConditionType.RunCountReach => $"執行滿 {ConditionValue} 次",
                StageConditionType.MinutesReach => $"運行滿 {ConditionValue} 分鐘",
                _ => $"達標值: {ConditionValue}"
            };
        }
    }
}
