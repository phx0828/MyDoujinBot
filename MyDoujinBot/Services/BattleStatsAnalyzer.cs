using System;
using MyDoujinBot.Models;

namespace MyDoujinBot.Services
{
    /// <summary>一份戰報的分析結果（原始戰報 + 對照表 + 統計）。</summary>
    public class BattleStatsAnalysis
    {
        public BattleStatsAnalysis(BattleReportResponse report, BattleReportIndex index, BattleStatsResult stats)
        {
            Report = report;
            Index = index;
            Stats = stats;
        }

        public BattleReportResponse Report { get; }
        public BattleReportIndex Index { get; }
        public BattleStatsResult Stats { get; }
    }

    /// <summary>
    /// 分析流程：戰報 → 建立對照表（驗證格式）→ 統計。
    /// 取得資料（BattleService）與繪圖（BattleStatsChartBuilder）各自獨立，這裡只串「解析 + 統計」。
    /// </summary>
    public static class BattleStatsAnalyzer
    {
        /// <summary>
        /// 分析戰報。戰報結構異常（缺玩家清單、缺 logs 等）時回傳 false 並說明原因，不丟例外。
        /// </summary>
        public static bool TryAnalyze(BattleReportResponse? report, out BattleStatsAnalysis? analysis, out string error)
        {
            analysis = null;

            if (!BattleReportIndex.TryCreate(report, out var index, out error))
                return false;

            try
            {
                var stats = BattleStatsCalculator.Calculate(index, report!.Logs!);
                analysis = new BattleStatsAnalysis(report, index, stats);
                return true;
            }
            catch (Exception ex)
            {
                error = $"統計過程發生錯誤：{ex.GetType().Name} - {ex.Message}";
                return false;
            }
        }
    }
}
