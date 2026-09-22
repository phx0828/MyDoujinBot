using System;
using System.Collections.Generic;
using System.Reflection;

namespace MyDoujinBot.Utilities
{
    /// <summary>
    /// 能力種類的列舉（enum）。
    ///
    /// enum 是什麼？
    /// enum 是一組具名整數常數。例如 StatType.Hp 底層是整數 0，
    /// StatType.Atk 是整數 1，以此類推。
    /// 用 enum 而不是直接寫字串 "hp"、"atk" 的好處是：
    /// 1. 編譯期就能發現錯誤（拼錯字編譯器會報錯）
    /// 2. IDE 可以自動完成
    /// 3. 可以做 switch/match 判斷
    ///
    /// 每個 enum 值都標注了 [StatName(apiKey, displayName)]：
    /// apiKey      → API JSON 中使用的縮寫
    /// displayName → LOG / UI 顯示的中文名稱
    /// </summary>
    public enum StatType
    {
        [StatName("hp",  "HP")] Hp,
        [StatName("atk", "攻擊")] Atk,
        [StatName("def", "防禦")] Def,
        [StatName("sta", "體力")] Sta,
        [StatName("agi", "敏捷")] Agi,
        [StatName("spd", "反應速度")] Spd,
        [StatName("tec", "技巧")] Tec,
        [StatName("int", "智力")] Int,
        [StatName("luk", "幸運")] Luk,
    }

    /// <summary>
    /// StatType 的輔助工具。
    /// 提供「API 縮寫 → 中文名稱」的查詢功能。
    /// </summary>
    public static class StatHelper
    {
        // 靜態快取：程式啟動時只建立一次，之後所有查詢都從這個 Dictionary 取
        // key = API 縮寫（例如 "int"）
        // value = 中文顯示名稱（例如 "智力"）
        //
        // Lazy<T> 說明：
        // Lazy<T> 是「延遲初始化」。只有第一次有人呼叫 .Value 時，
        // 才會執行 BuildLookup()。之後每次都直接回傳同一個 Dictionary。
        // 這樣即使 StatHelper 被引用但沒用到，也不會浪費時間建立 Dictionary。
        private static readonly Lazy<Dictionary<string, string>> _apiKeyToDisplay
            = new(BuildLookup);

        /// <summary>
        /// 從 API 縮寫取得 UI 中文顯示名稱。
        /// 例如：GetDisplayName("int") → "智力"
        /// 如果找不到對應（例如 API 新增了還沒加到 enum 的能力），
        /// 直接回傳原始縮寫，而不是拋出例外。
        /// </summary>
        public static string GetDisplayName(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) return apiKey;
            return _apiKeyToDisplay.Value.TryGetValue(apiKey, out var name) ? name : apiKey;
        }

        /// <summary>
        /// 從 StatType enum 值取得 UI 中文顯示名稱。
        /// 例如：GetDisplayName(StatType.Int) → "智力"
        ///
        /// Extension Method 說明：
        /// 這個方法的第一個參數是 "this StatType stat"，
        /// 代表可以直接寫成 StatType.Int.GetDisplayName()，
        /// 而不是 StatHelper.GetDisplayName(StatType.Int)。
        /// Extension Method 讓現有的 type 增加新方法，不需要修改原始 class。
        /// </summary>
        public static string GetDisplayName(this StatType stat)
        {
            var field = typeof(StatType).GetField(stat.ToString());
            var attr = field?.GetCustomAttribute<StatNameAttribute>();
            return attr?.DisplayName ?? stat.ToString();
        }

        /// <summary>
        /// 取得 API 縮寫。例如：StatType.Int.GetApiKey() → "int"
        /// </summary>
        public static string GetApiKey(this StatType stat)
        {
            var field = typeof(StatType).GetField(stat.ToString());
            var attr = field?.GetCustomAttribute<StatNameAttribute>();
            return attr?.ApiKey ?? stat.ToString().ToLower();
        }

        // =====================================================================
        // 建立 API 縮寫 → 中文名稱的查詢 Dictionary
        // 用 Reflection 讀取 enum 每個值上的 [StatName] Attribute
        //
        // Reflection 說明：
        // Reflection（反射）是 C# 在 runtime 查詢型別資訊的機制。
        // typeof(StatType).GetFields() 可以取得 enum 的所有欄位（每個值）。
        // field.GetCustomAttribute<StatNameAttribute>() 則讀取標注在欄位上的 Attribute。
        // =====================================================================
        private static Dictionary<string, string> BuildLookup()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // 取得 StatType enum 的所有欄位
            foreach (var field in typeof(StatType).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                // 讀取每個欄位上的 [StatName] Attribute
                var attr = field.GetCustomAttribute<StatNameAttribute>();
                if (attr != null)
                {
                    // 加入 Dictionary：API 縮寫 → 中文名稱
                    dict[attr.ApiKey] = attr.DisplayName;
                }
            }

            return dict;
        }
    }
}
