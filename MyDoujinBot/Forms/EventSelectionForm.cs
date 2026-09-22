using System;
using System.Drawing;
using System.Windows.Forms;
using MyDoujinBot.Models;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 遭遇事件選擇對話框。
    /// 當 pendingEvent 出現（人工模式）時，彈出此視窗。
    /// 訓練循環透過 TaskCompletionSource 等待此視窗的使用者選擇。
    ///
    /// 使用方式：
    ///   var form = new EventSelectionForm(pendingEvent);
    ///   form.OptionSelected += optionId => tcs.SetResult(optionId);
    ///   form.FormClosed += (_, _) => tcs.TrySetResult(null);
    ///   form.Show(ownerForm);
    /// </summary>
    public class EventSelectionForm : Form
    {
        /// <summary>使用者點選選項時觸發，帶有 optionId</summary>
        public event Action<string>? OptionSelected;

        public EventSelectionForm(PendingEvent pendingEvent)
        {
            this.Text = $"⚡  遭遇事件：{pendingEvent.Name}";
            this.Width = 500;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(28, 28, 35);
            this.ForeColor = Color.FromArgb(220, 220, 230);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;

            BuildUI(pendingEvent);
        }

        private void BuildUI(PendingEvent pe)
        {
            this.SuspendLayout();
            int y = 16;
            const int pad = 16;
            const int innerWidth = 460;

            // --- 事件名稱 ---
            var lblName = new Label
            {
                Text = pe.Name,
                Location = new Point(pad, y),
                Width = innerWidth,
                Font = new Font("Microsoft JhengHei UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 220, 80),
                AutoSize = false,
                Height = 32
            };
            this.Controls.Add(lblName);
            y += 38;

            // --- 事件描述 ---
            if (!string.IsNullOrWhiteSpace(pe.Description))
            {
                var lblDesc = new Label
                {
                    Text = pe.Description,
                    Location = new Point(pad, y),
                    MaximumSize = new Size(innerWidth, 0),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(195, 195, 210),
                };
                this.Controls.Add(lblDesc);
                int preferredHeight = lblDesc.GetPreferredSize(new Size(innerWidth, 0)).Height;
                lblDesc.Size = new Size(innerWidth, Math.Max(preferredHeight, 20));
                y += lblDesc.Height + 12;
            }

            // --- 分隔線 ---
            var sep = new Label
            {
                Location = new Point(pad, y),
                Width = innerWidth,
                Height = 1,
                BackColor = Color.FromArgb(70, 70, 90),
                AutoSize = false,
                Text = ""
            };
            this.Controls.Add(sep);
            y += 10;

            // --- 選項標題 ---
            var lblChoose = new Label
            {
                Text = "請選擇一個選項：",
                Location = new Point(pad, y),
                ForeColor = Color.FromArgb(160, 160, 200),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f)
            };
            this.Controls.Add(lblChoose);
            y += 26;

            // --- 選項按鈕 ---
            foreach (var option in pe.Options)
            {
                string rateText = option.SuccessChance.HasValue ? $"{option.SuccessChance}%" : "未知";
                string mainText = option.Name;
                string subText = string.IsNullOrEmpty(option.CheckStat) 
                    ? " (直接行動)" 
                    : $" ({MyDoujinBot.Utilities.StatHelper.GetDisplayName(option.CheckStat)}：- / 成功率：{rateText})";

                // 按鈕顏色：依成功率給予視覺提示
                Color btnBack = option.SuccessChance.HasValue
                    ? (option.SuccessChance.Value >= 70
                        ? Color.FromArgb(40, 70, 50)   // 高成功率 → 偏綠
                        : option.SuccessChance.Value >= 40
                            ? Color.FromArgb(60, 55, 40) // 中 → 偏橘
                            : Color.FromArgb(70, 40, 40)) // 低 → 偏紅
                    : Color.FromArgb(50, 50, 70);  // 未知 → 中性

                Color btnBackHover = Color.FromArgb(
                    Math.Min(btnBack.R + 25, 255),
                    Math.Min(btnBack.G + 25, 255),
                    Math.Min(btnBack.B + 25, 255));

                var btn = new Button
                {
                    Location = new Point(pad, y),
                    Width = innerWidth,
                    Height = 46,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = btnBack,
                    Cursor = Cursors.Hand,
                    Tag = option.Id,
                    Text = ""
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(90, 85, 120);
                btn.FlatAppearance.BorderSize = 1;

                var mainFont = new Font("Microsoft JhengHei UI", 9.5f);
                var subFont = new Font("Microsoft JhengHei UI", 8.2f);

                btn.Paint += (s, e) =>
                {
                    int leftPad = 12;
                    
                    Size mainSize = TextRenderer.MeasureText(e.Graphics, mainText, mainFont, new Size(innerWidth, 46), TextFormatFlags.NoPadding);
                    Size subSize = TextRenderer.MeasureText(e.Graphics, subText, subFont, new Size(innerWidth, 46), TextFormatFlags.NoPadding);

                    int mainY = (btn.Height - mainSize.Height) / 2;
                    int subY = (btn.Height - subSize.Height) / 2;

                    TextRenderer.DrawText(e.Graphics, mainText, mainFont, new Point(leftPad, mainY), Color.FromArgb(235, 230, 255), TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(e.Graphics, subText, subFont, new Point(leftPad + mainSize.Width + 2, subY + 1), Color.FromArgb(170, 170, 190), TextFormatFlags.NoPadding);
                };

                btn.Disposed += (s, e) => 
                {
                    mainFont.Dispose();
                    subFont.Dispose();
                };

                var capturedOption = option;
                btn.MouseEnter += (_, _) => btn.BackColor = btnBackHover;
                btn.MouseLeave += (_, _) => btn.BackColor = btnBack;

                btn.Click += (_, _) =>
                {
                    OptionSelected?.Invoke(capturedOption.Id);
                    this.Close();
                };

                this.Controls.Add(btn);
                y += 52;
            }

            // 設定視窗高度以剛好容納所有選項
            y += 16; // 底部邊距
            this.ClientSize = new Size(500, y);

            this.ResumeLayout(false);
        }
    }
}
