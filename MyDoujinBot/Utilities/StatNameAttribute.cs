using System;

namespace MyDoujinBot.Utilities
{
    /// <summary>
    /// 自訂 Attribute，附加在 StatType enum 的每個值上，
    /// 記錄對應的 API 縮寫（apiKey）和 UI 顯示的中文名稱（displayName）。
    ///
    /// Attribute 是什麼？
    /// C# 的 Attribute 是一種「標記」，寫在 [中括號] 裡，
    /// 可以附加在 class、method、property、enum 值等程式碼元素上。
    /// 它本身不執行任何邏輯，只是儲存額外資訊（元資料）。
    /// 其他程式碼可以在 runtime 透過「反射（Reflection）」讀取這些資訊。
    ///
    /// 範例：
    /// [StatName("int", "智力")]
    /// Int,
    ///
    /// 之後可以用 Reflection 讀取：
    /// var attr = GetAttribute(StatType.Int);
    /// attr.ApiKey      → "int"
    /// attr.DisplayName → "智力"
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class StatNameAttribute : Attribute
    {
        // AttributeUsage 說明：
        // 這個 Attribute 只能用在 Field（即 enum 的每個值）上
        // AllowMultiple = false 代表每個 Field 只能標注一次

        /// <summary>API 使用的英文縮寫，例如 "int"、"atk"</summary>
        public string ApiKey { get; }

        /// <summary>UI / LOG 顯示的中文名稱，例如 "智力"、"攻擊"</summary>
        public string DisplayName { get; }

        public StatNameAttribute(string apiKey, string displayName)
        {
            ApiKey = apiKey;
            DisplayName = displayName;
        }
    }
}
