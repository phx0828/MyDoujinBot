using System;
using System.Drawing;
using System.Windows.Forms;
using MyDoujinBot.Models;

namespace MyDoujinBot.Forms
{
    public class BattleReportForm : Form
    {
        private RichTextBox rtbLogs = null!;

        public BattleReportForm(BattleResult battleResult)
        {
            this.Text = "戰鬥報告";
            this.Size = new Size(600, 500);
            this.MinimumSize = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(28, 28, 35);
            this.ForeColor = Color.FromArgb(220, 220, 230);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.ShowIcon = false;
            this.MinimizeBox = false;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            BuildUI(battleResult);
        }

        private void BuildUI(BattleResult battleResult)
        {
            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };
            this.Controls.Add(pnlMain);

            string winStr = battleResult.Winner == "TEAM_A" ? "勝利" : "失敗";
            Color winColor = battleResult.Winner == "TEAM_A" ? Color.FromArgb(80, 220, 120) : Color.FromArgb(220, 80, 80);
            
            var lblWinner = new Label
            {
                Dock = DockStyle.Top,
                Text = winStr,
                Font = new Font("Microsoft JhengHei UI", 16f, FontStyle.Bold),
                ForeColor = winColor,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnlMain.Controls.Add(lblWinner);

            rtbLogs = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 16, 22),
                ForeColor = Color.FromArgb(217, 255, 255), // fallback white
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10.5f),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true,
                Margin = new Padding(0, 10, 0, 0)
            };

            var pnlLogs = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 0)
            };
            pnlLogs.Controls.Add(rtbLogs);
            pnlMain.Controls.Add(pnlLogs);
            pnlLogs.BringToFront();

            if (battleResult.Logs != null)
            {
                for (int i = 0; i < battleResult.Logs.Count; i++)
                {
                    var log = battleResult.Logs[i];
                    Color logColor = GetLogColor(log);
                    string lineText = $"{i + 1} {log.Message}\n";
                    AppendLog(lineText, logColor);
                }
            }
        }

        private Color GetLogColor(BattleLog log)
        {
            string type = log.Type?.ToUpperInvariant() ?? "";

            // Red overrides
            if (type == "DAMAGE" && log.IsCrit == true)
                return Color.FromArgb(246, 135, 139); // #F6878B
            if (type == "DEATH" || type == "PLAYER_DEATH")
                return Color.FromArgb(246, 135, 139);
            if (type == "LUCK_EVENT" && log.Tier == "RED")
                return Color.FromArgb(246, 135, 139);

            // Aqua
            if (type == "DAMAGE" && log.IsNormalAttack == false)
                return Color.FromArgb(79, 209, 197); // #4FD1C5
            if (type == "SKILL_TEXT" || type == "BUFF_APPLY" || type == "SUMMON")
                return Color.FromArgb(79, 209, 197);

            // Yellow / Orange
            if (type == "BATTLE_START" || type == "FATIGUE" || type == "COUNTER" || 
                type == "FATIGUE_SKIP" || type == "HP_REMAINING" || type == "BLOCK")
                return Color.FromArgb(226, 200, 151); // #E2C897

            // White (Slightly transparent -> fully opaque white or off-white)
            if (type == "DAMAGE" && log.IsNormalAttack == true)
                return Color.FromArgb(245, 245, 245); // #FFFFFFD9 roughly corresponds to light white
            if (type == "REST" || type == "TEXT" || type == "EXP_GAIN")
                return Color.FromArgb(245, 245, 245);

            // Gray
            if (type == "MISS" || type == "CHADO_MISS")
                return Color.FromArgb(161, 161, 170); // #A1A1AA

            // Brown
            if (type == "CHADO")
                return Color.FromArgb(200, 155, 106); // #C89B6A

            // Light Purple
            if (type == "LUCK_EVENT" && log.Tier == "PURPLE")
                return Color.FromArgb(192, 132, 252); // #C084FC

            if (type == "HEAL")
                return Color.FromArgb(123, 213, 166); // #7BD5A6

            return Color.White;
        }

        private void AppendLog(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;

            rtbLogs.SelectionStart = rtbLogs.TextLength;
            rtbLogs.SelectionLength = 0;
            rtbLogs.SelectionColor = color;
            rtbLogs.AppendText(text);
            rtbLogs.SelectionColor = rtbLogs.ForeColor; // reset
        }
    }
}
