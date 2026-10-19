using System.Collections.Generic;
using System.Linq;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 事件記憶體索引模型，供 UI 即時搜尋、篩選與自繪渲染使用。
    /// </summary>
    public class EventRegistryItem
    {
        public string EventId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ChainId { get; set; } = string.Empty;
        public string ChainName { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<EventRegistryOption> Options { get; set; } = new();
    }

    /// <summary>
    /// 事件選項記憶體模型。
    /// </summary>
    public class EventRegistryOption
    {
        public string OptionId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string GuideKind { get; set; } = string.Empty;
        public string GuideWhy { get; set; } = string.Empty;
        public bool HasEncounter { get; set; }
        public Dictionary<string, int> OnSuccess { get; set; } = new();
        public string StatsSummary { get; set; } = string.Empty;

        /// <summary>
        /// 依據 Guide.kind 取得推薦文字標記。
        /// </summary>
        public string GuideTag => GuideKind switch
        {
            "route" => "【推薦】",
            "always" => "【必選】",
            "never" => "【別選】",
            _ => string.Empty
        };

        /// <summary>
        /// 將能力屬性字典格式化為顯示字串。
        /// </summary>
        public static string FormatStatsSummary(Dictionary<string, int>? stats)
        {
            if (stats == null || stats.Count == 0) return string.Empty;
            var list = new List<string>();
            string[] order = { "hp", "攻", "防", "體", "敏", "反", "技", "智", "幸" };
            foreach (var key in order)
            {
                if (stats.TryGetValue(key, out int val) && val != 0)
                {
                    string name = key switch
                    {
                        "hp" => "HP",
                        "攻" => "攻擊",
                        "防" => "防禦",
                        "體" => "體力",
                        "敏" => "敏捷",
                        "反" => "反應速度",
                        "技" => "技巧",
                        "智" => "智力",
                        "幸" => "幸運",
                        _ => key
                    };
                    string prefix = val > 0 ? "+" : "";
                    list.Add($"{name}{prefix}{val}");
                }
            }
            foreach (var kvp in stats)
            {
                if (!order.Contains(kvp.Key) && kvp.Value != 0)
                {
                    string prefix = kvp.Value > 0 ? "+" : "";
                    list.Add($"{kvp.Key}{prefix}{kvp.Value}");
                }
            }
            return string.Join(" ", list);
        }
    }
}
