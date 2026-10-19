using System;
using System.Collections.Generic;
using System.Linq;

namespace MyDoujinBot.Models
{
    /// <summary>
    /// 單一分類的能力點統計（例如：升級獲得、事件獲得、總計獲得）。
    /// </summary>
    public class StatPointsCategory
    {
        public Dictionary<string, int> Stats { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>所有能力點總加總</summary>
        public int TotalPoints => Stats.Values.Sum();

        /// <summary>取得指定能力代號之點數，若不存在回傳 0</summary>
        public int GetStat(string key) => Stats.TryGetValue(key, out var val) ? val : 0;

        /// <summary>累加單一能力點數</summary>
        public void Add(string key, int value)
        {
            if (string.IsNullOrWhiteSpace(key) || value == 0) return;
            if (Stats.TryGetValue(key, out var cur))
                Stats[key] = cur + value;
            else
                Stats[key] = value;
        }

        /// <summary>批次累加能力點數字典</summary>
        public void AddRange(Dictionary<string, int>? source)
        {
            if (source == null) return;
            foreach (var kv in source)
            {
                Add(kv.Key, kv.Value);
            }
        }

        public void Clear() => Stats.Clear();

        public StatPointsCategory Clone()
        {
            var clone = new StatPointsCategory();
            clone.AddRange(this.Stats);
            return clone;
        }
    }

    /// <summary>
    /// 能力點數紀錄模型。
    /// 包含：升級獲得、事件獲得，以及兩者自動加總的總計獲得。
    /// </summary>
    public class StatPointsRecord
    {
        /// <summary>升級獲得的能力點</summary>
        public StatPointsCategory LevelUp { get; } = new();

        /// <summary>事件獲得的能力點</summary>
        public StatPointsCategory Event { get; } = new();

        /// <summary>總計獲得的能力點（升級 + 事件）</summary>
        public StatPointsCategory Total
        {
            get
            {
                var total = new StatPointsCategory();
                total.AddRange(LevelUp.Stats);
                total.AddRange(Event.Stats);
                return total;
            }
        }

        public void Clear()
        {
            LevelUp.Clear();
            Event.Clear();
        }

        public StatPointsRecord Clone()
        {
            var clone = new StatPointsRecord();
            clone.LevelUp.AddRange(LevelUp.Stats);
            clone.Event.AddRange(Event.Stats);
            return clone;
        }
    }

    /// <summary>
    /// 排程各階段專屬能力點紀錄模型。
    /// </summary>
    public class StageStatPointsRecord
    {
        public int StageIndex { get; set; }
        public string StageName { get; set; } = string.Empty;
        public string StageSummary { get; set; } = string.Empty;
        public StageActionType ActionType { get; set; }
        public MyDoujinBot.Services.BattleType BattleType { get; set; }
        public int ChadoSuccessCount { get; set; }
        public StatPointsRecord Record { get; } = new();
    }
}
