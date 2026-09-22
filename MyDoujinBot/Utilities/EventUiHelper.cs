using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MyDoujinBot.Models;

namespace MyDoujinBot.Utilities
{
    public static class EventUiHelper
    {
        public static List<string> BuildBonusStatsParts(Dictionary<string, int>? bonusStats)
        {
            var parts = new List<string>();
            if (bonusStats == null || bonusStats.Count == 0) return parts;

            foreach (var kv in bonusStats)
            {
                if (kv.Value != 0)
                {
                    var statName = StatHelper.GetDisplayName(kv.Key);
                    parts.Add($"{statName} +{kv.Value}");
                }
            }
            return parts;
        }

        public static Panel CreateCheckResultPanel(EventResult er, int width)
        {
            var panel = new Panel
            {
                Width = width,
                BackColor = Color.FromArgb(32, 32, 44)
            };

            panel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(70, 70, 95), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            };

            int y = 8;
            const int padX = 12;
            int innerW = width - padX * 2;

            // 【判定結果】
            var lblTag = new Label
            {
                Text = "【判定結果】",
                Location = new Point(padX, y),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 180, 210)
            };
            panel.Controls.Add(lblTag);
            y += lblTag.Height + 4;

            // Line 1: Stat, Roll, Total
            var statName = !string.IsNullOrEmpty(er.Stat) ? StatHelper.GetDisplayName(er.Stat) : "";
            string line1Text = "";
            if (er.StatValue.HasValue && er.EffectiveStat.HasValue)
            {
                line1Text = $"{statName} ({er.StatValue} → {er.EffectiveStat})";
            }
            else if (!string.IsNullOrEmpty(statName))
            {
                line1Text = statName;
            }

            if (er.Roll.HasValue)
            {
                if (!string.IsNullOrEmpty(line1Text))
                    line1Text += $" + 骰出 {er.Roll}";
                else
                    line1Text = $"骰出 {er.Roll}";
            }

            if (er.Total.HasValue)
            {
                line1Text += $" = {er.Total}";
            }

            if (!string.IsNullOrEmpty(er.Critical))
            {
                line1Text += $"  【{er.Critical}】";
            }

            if (!string.IsNullOrWhiteSpace(line1Text))
            {
                var lblLine1 = new Label
                {
                    Text = line1Text,
                    Location = new Point(padX, y),
                    Width = innerW,
                    AutoSize = true,
                    Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(235, 235, 245)
                };
                panel.Controls.Add(lblLine1);
                y += lblLine1.Height + 4;
            }

            // Line 2: DC, Chance
            string line2Text = "";
            if (er.DC.HasValue && er.Chance.HasValue)
            {
                line2Text = $"(門檻 {er.DC} / 成功率 {er.Chance}%)";
            }
            else if (er.DC.HasValue)
            {
                line2Text = $"(門檻 {er.DC})";
            }
            else if (er.Chance.HasValue)
            {
                line2Text = $"(成功率 {er.Chance}%)";
            }

            if (!string.IsNullOrWhiteSpace(line2Text))
            {
                var lblLine2 = new Label
                {
                    Text = line2Text,
                    Location = new Point(padX, y),
                    Width = innerW,
                    AutoSize = true,
                    Font = new Font("Microsoft JhengHei UI", 9f),
                    ForeColor = Color.FromArgb(160, 160, 185)
                };
                panel.Controls.Add(lblLine2);
                y += lblLine2.Height + 8;
            }
            else
            {
                y += 6;
            }

            panel.Height = y;
            return panel;
        }
    }
}
