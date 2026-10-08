using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MyDoujinBot.Models;
using MyDoujinBot.Services;

namespace MyDoujinBot.Utilities
{
    /// <summary>
    /// 文字片段的語意樣式（不含實際顏色，顏色由 UI 層決定，繪圖邏輯因此不依賴 WinForms）。
    /// </summary>
    public enum ChartStyle
    {
        /// <summary>預設樣式</summary>
        Normal,
        /// <summary>標題（戰報基本資訊）</summary>
        Heading,
        /// <summary>次要說明（略過筆數等）</summary>
        Muted,
        /// <summary>【倒下】橘色粗體</summary>
        Fallen,
        /// <summary>【死亡】紅色粗體</summary>
        Dead,
        /// <summary>輸出長條（紅）</summary>
        OutputBar,
        /// <summary>承傷長條（藍）</summary>
        TakenBar,
        /// <summary>治療長條（綠）</summary>
        HealBar
    }

    /// <summary>一段帶樣式的文字。</summary>
    public readonly record struct ChartSegment(string Text, ChartStyle Style = ChartStyle.Normal);

    /// <summary>
    /// 繪圖層：把統計結果轉成「一行行帶樣式的文字片段」，由 UI 層負責實際上色與顯示。
    /// 每個函式只做一件事（格式化數字、長條、標題行、出手爆擊行…），方便日後調整格式。
    /// </summary>
    public static class BattleStatsChartBuilder
    {
        /// <summary>最大值的長條格數（100%）。</summary>
        public const int MaxBarLength = 20;

        private const char BarChar = '█';

        /// <summary>
        /// 建立完整的圖表內容：戰報標題、玩家數據、分隔線、BOSS 與敵方數據、略過筆數。
        /// </summary>
        public static List<List<ChartSegment>> Build(BattleStatsAnalysis analysis)
        {
            var lines = new List<List<ChartSegment>>();
            var players = analysis.Index.Players;
            var enemies = analysis.Index.Enemies;
            var stats = analysis.Stats;

            lines.Add(BuildReportHeaderLine(analysis.Report));
            lines.Add(new List<ChartSegment>());

            // 全場統一最大值基準 (Global Scale)：涵蓋所有單位（玩家與敵方/BOSS）的最大數值
            long globalMax = stats.Units.Values.Count > 0
                ? stats.Units.Values.Max(u => Math.Max(u.Output, Math.Max(u.Taken, u.Heal)))
                : 0;

            // ── 玩家區塊 ──
            var playerStats = players.Select(p => stats.GetUnitOrEmpty(p.UserId!)).ToList();
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                var unit = playerStats[i];

                lines.Add(BuildTitleLine(player,
                    isFallen: stats.IsFallen(player.UserId!),
                    isDead: analysis.Index.IsDead(player.UserId!)));
                lines.Add(BuildMetricLine("輸出", unit.Output, globalMax, ChartStyle.OutputBar));
                lines.Add(BuildMetricLine("承傷", unit.Taken, globalMax, ChartStyle.TakenBar));
                lines.Add(BuildMetricLine("治療", unit.Heal, globalMax, ChartStyle.HealBar));
                lines.Add(BuildShotLine(unit));
                lines.Add(new List<ChartSegment>());
            }

            // ── 敵方 / BOSS / PVP 對手區塊 ──
            if (enemies.Count > 0)
            {
                bool isPvp = enemies.All(e => e.IsPlayerOpponent);
                lines.Add(new List<ChartSegment> { new("────────────────────────────────────────", ChartStyle.Muted) });
                lines.Add(new List<ChartSegment> { new(isPvp ? "【對手統計】" : "【敵方 / BOSS 統計】", ChartStyle.Heading) });
                lines.Add(new List<ChartSegment>());

                foreach (var enemy in enemies)
                {
                    var enemyId = enemy.Id;
                    if (string.IsNullOrWhiteSpace(enemyId)) continue;

                    var unit = stats.GetUnitOrEmpty(enemyId);
                    lines.Add(BuildEnemyTitleLine(enemy, isFallen: stats.IsFallen(enemyId)));
                    lines.Add(BuildMetricLine("輸出", unit.Output, globalMax, ChartStyle.OutputBar));
                    lines.Add(BuildMetricLine("承傷", unit.Taken, globalMax, ChartStyle.TakenBar));
                    lines.Add(BuildMetricLine("治療", unit.Heal, globalMax, ChartStyle.HealBar));
                    lines.Add(BuildShotLine(unit));
                    lines.Add(new List<ChartSegment>());
                }
            }

            if (stats.SkippedCount > 0)
                lines.Add(BuildSkippedLine(stats));

            return lines;
        }

        /// <summary>戰報基本資訊行，例：戰報 #118362｜BOSS｜三鍋臭媽媽｜勝利</summary>
        public static List<ChartSegment> BuildReportHeaderLine(BattleReportResponse report)
        {
            var parts = new List<string> { $"戰報 #{report.ReportId}" };
            if (!string.IsNullOrWhiteSpace(report.BattleType)) parts.Add(report.BattleType!);
            if (!string.IsNullOrWhiteSpace(report.Boss?.Name)) parts.Add(report.Boss!.Name!);
            parts.Add(FormatResult(report.Result));

            return new List<ChartSegment> { new(string.Join("｜", parts), ChartStyle.Heading) };
        }

        /// <summary>
        /// 標題行：玩家名稱 (角色名稱) 【倒下】 【死亡】
        /// 兩個標記獨立判斷、可同時出現；都不符合時不顯示標記，也不留多餘空格。
        /// </summary>
        public static List<ChartSegment> BuildTitleLine(BattleReportPlayer player, bool isFallen, bool isDead)
        {
            var line = new List<ChartSegment>
            {
                new($"{player.Name} ({player.CharacterName})")
            };

            if (isFallen)
            {
                line.Add(new ChartSegment(" "));
                line.Add(new ChartSegment("【倒下】", ChartStyle.Fallen));
            }

            if (isDead)
            {
                line.Add(new ChartSegment(" "));
                line.Add(new ChartSegment("【死亡】", ChartStyle.Dead));
            }

            return line;
        }

        /// <summary>
        /// 敵方標題行：敵方名稱 (稱號) 【倒下】
        /// PVP 對手則跟玩家一樣顯示：玩家名稱 (角色名稱) 【倒下】
        /// </summary>
        public static List<ChartSegment> BuildEnemyTitleLine(BattleReportEnemy enemy, bool isFallen)
        {
            string subtitle = !string.IsNullOrWhiteSpace(enemy.CharacterName) ? enemy.CharacterName! : enemy.Title ?? string.Empty;
            string titlePart = string.IsNullOrWhiteSpace(subtitle) ? string.Empty : $" ({subtitle})";
            var line = new List<ChartSegment>
            {
                new($"{enemy.Name}{titlePart}")
            };

            if (isFallen)
            {
                line.Add(new ChartSegment(" "));
                line.Add(new ChartSegment("【倒下】", ChartStyle.Fallen));
            }

            return line;
        }

        /// <summary>長條行，例：輸出 ███████████ 125,000（長條上色，文字與數值為預設樣式）。</summary>
        public static List<ChartSegment> BuildMetricLine(string label, long value, long max, ChartStyle barStyle)
        {
            string bar = BuildBar(value, max);

            var line = new List<ChartSegment> { new($"{label} ") };
            if (bar.Length > 0)
            {
                line.Add(new ChartSegment(bar, barStyle));
                line.Add(new ChartSegment(" "));
            }
            line.Add(new ChartSegment(FormatNumber(value)));
            return line;
        }

        /// <summary>
        /// 出手與爆擊行：出手 N 次｜命中 N 次｜命中率 N.N%｜爆擊 N 次｜爆擊率 N.N%
        /// 數值直接取自統計欄位（不另外重算）；純文字、預設顏色、不畫長條。
        /// </summary>
        public static List<ChartSegment> BuildShotLine(UnitStats unit)
        {
            string text = $"出手 {FormatNumber(unit.Shots)} 次｜命中 {FormatNumber(unit.Hits)} 次｜命中率 {FormatPercent(unit.HitRatePercent)}"
                        + $"｜爆擊 {FormatNumber(unit.Crits)} 次｜爆擊率 {FormatPercent(unit.CritRatePercent)}";
            return new List<ChartSegment> { new(text) };
        }

        /// <summary>略過筆數回報，例：略過 2 筆無法辨識或缺少必要欄位的事件（DAMAGE ×1、WEIRD ×1）</summary>
        public static List<ChartSegment> BuildSkippedLine(BattleStatsResult stats)
        {
            var detail = string.Join("、", stats.SkippedByType.Select(p => $"{p.Key} ×{p.Value}"));
            return new List<ChartSegment>
            {
                new($"略過 {stats.SkippedCount} 筆無法辨識或缺少必要欄位的事件（{detail}）", ChartStyle.Muted)
            };
        }

        /// <summary>
        /// 長條字串：以 max 為 100% 等比例縮放成最多 MaxBarLength 格；
        /// 非零數值至少顯示 1 格（避免長條消失）；0 或 max 為 0 時回傳空字串。
        /// </summary>
        public static string BuildBar(long value, long max)
        {
            if (value <= 0 || max <= 0) return string.Empty;

            int length = (int)Math.Round((double)value / max * MaxBarLength, MidpointRounding.AwayFromZero);
            length = Math.Clamp(length, 1, MaxBarLength);
            return new string(BarChar, length);
        }

        /// <summary>千分位逗號（固定使用 InvariantCulture，避免受系統地區設定影響）。</summary>
        public static string FormatNumber(long value) =>
            value.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>百分比：一位小數 + %，例：42.9%。</summary>
        public static string FormatPercent(decimal percent) =>
            percent.ToString("F1", CultureInfo.InvariantCulture) + "%";

        private static string FormatResult(string? result) => result?.ToUpperInvariant() switch
        {
            "WIN" => "勝利",
            "LOSE" => "失敗",
            null or "" => "結果未知",
            _ => result
        };

        /// <summary>把一組行轉成純文字（供測試與除錯使用，不含樣式）。</summary>
        public static string ToPlainText(IEnumerable<List<ChartSegment>> lines)
        {
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                foreach (var segment in line) sb.Append(segment.Text);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
