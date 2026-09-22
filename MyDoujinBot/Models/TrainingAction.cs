using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 代表一種訓練行動。
    /// 封裝了 UI 顯示名稱（DisplayName）和 API 使用的 actionId。
    /// </summary>
    public class TrainingAction
    {
        /// <summary>UI / LOG 顯示的中文名稱，例如「外出野餐」</summary>
        public string DisplayName { get; }

        /// <summary>送給 API 的 actionId，例如 "picnic"</summary>
        public string ActionId { get; }

        public TrainingAction(string displayName, string actionId)
        {
            DisplayName = displayName;
            ActionId = actionId;
        }

        // 讓 ComboBox 顯示 DisplayName
        // ToString() 是 C# 所有物件都有的方法。
        // ComboBox 在顯示 Items 時會呼叫 .ToString()，
        // 所以覆寫這個方法讓 ComboBox 直接顯示中文名稱，
        // 而不是預設的 "MyDoujinBot.Models.TrainingAction"。
        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// 所有訓練行動的統一定義。
    /// actionId 只在這裡出現，不散落在程式各處。
    ///
    /// 注意：目前 actionId 使用佔位值，你拿到實際 7 個 actionId 後
    /// 只需要修改這個 class，其他程式碼完全不需要改動。
    /// </summary>
    public static class TrainingActions
    {
        // ReadOnlyCollection 說明：
        // 使用 ReadOnlyCollection 而不是 List，
        // 確保外部程式碼無法新增/刪除這個集合的內容，
        // 保持資料的一致性（只有這個 class 能定義行動列表）
        public static readonly ReadOnlyCollection<TrainingAction> All
            = new ReadOnlyCollection<TrainingAction>(new List<TrainingAction>
        {
            // ================================================================
            // 在這裡定義所有訓練行動
            // 格式：new TrainingAction("UI顯示名稱", "actionId")
            //
            // 你拿到實際 actionId 後，只需要修改這裡的 "actionId" 字串
            // ================================================================
            new TrainingAction("狩獵", "hunt"),
            new TrainingAction("自主訓練", "training"),
            new TrainingAction("外出野餐",   "picnic"),
            new TrainingAction("汁妹", "flirt"),
            new TrainingAction("做善事", "charity"),
            new TrainingAction("坐下休息", "rest"),
            new TrainingAction("釣魚", "fishing"),
        });
    }
}
