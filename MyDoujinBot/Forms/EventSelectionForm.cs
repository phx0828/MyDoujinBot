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
            this.Width = 520;
            this.AutoSize = true; this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.MinimumSize = new Size(400, 250);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(28, 28, 35);
            this.ForeColor = Color.FromArgb(220, 220, 230);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

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
                    foreach (Control ctrl in this.Controls)
                    {
                        ctrl.Enabled = false;
                    }
                    OptionSelected?.Invoke(capturedOption.Id);
                };

                this.Controls.Add(btn);
                y += 52;
            }

            // 設定視窗高度以剛好容納所有選項，並避免超出螢幕
            y += 16; // 底部邊距
            this.AutoScroll = true;
            int maxHeight = (Screen.PrimaryScreen?.WorkingArea.Height ?? 1080) - 100;
            this.ClientSize = new Size(500, Math.Min(y, maxHeight));

            this.ResumeLayout(false);
        }

        public System.Threading.Tasks.Task ShowResultAsync(EventResult er, System.Threading.CancellationToken ct)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var reg = ct.Register(() =>
            {
                this.BeginInvoke(() =>
                {
                    tcs.TrySetResult(false);
                    if (!this.IsDisposed) this.Close();
                });
            });

            Action buildAction = () =>
            {
                if (this.IsDisposed)
                {
                    reg.Dispose();
                    tcs.TrySetResult(true);
                    return;
                }

                this.SuspendLayout();
                this.Controls.Clear();
                this.Text = $"⚡  事件結果：{(er.IsSuccess ? "成功" : "失敗")}";

                int y = 16;
                const int pad = 16;
                const int innerWidth = 460;

                // --- 標題：成功 / 失敗 ---
                string resultTitle = er.IsSuccess ? "成功" : "失敗";
                Color titleColor = er.IsSuccess 
                    ? Color.FromArgb(80, 220, 120) 
                    : Color.FromArgb(240, 80, 80);

                var lblTitle = new Label
                {
                    Text = resultTitle,
                    Location = new Point(pad, y),
                    Width = innerWidth,
                    Font = new Font("Microsoft JhengHei UI", 16f, FontStyle.Bold),
                    ForeColor = titleColor,
                    AutoSize = false,
                    Height = 36
                };
                this.Controls.Add(lblTitle);
                y += 42;

                // --- 判定結果區塊 ---
                if (!string.IsNullOrEmpty(er.Stat) || er.Roll.HasValue)
                {
                    var pnlCheck = MyDoujinBot.Utilities.EventUiHelper.CreateCheckResultPanel(er, innerWidth);
                    pnlCheck.Location = new Point(pad, y);
                    this.Controls.Add(pnlCheck);
                    y += pnlCheck.Height + 14;
                }

                // --- 故事內文 ---
                if (!string.IsNullOrWhiteSpace(er.Text))
                {
                    var lblStory = new Label
                    {
                        Text = er.Text,
                        Location = new Point(pad, y),
                        MaximumSize = new Size(innerWidth, 0),
                        AutoSize = true,
                        ForeColor = Color.FromArgb(220, 220, 235),
                        Font = new Font("Microsoft JhengHei UI", 10.5f)
                    };
                    this.Controls.Add(lblStory);
                    int preferredHeight = lblStory.GetPreferredSize(new Size(innerWidth, 0)).Height;
                    lblStory.Size = new Size(innerWidth, Math.Max(preferredHeight, 20));
                    y += lblStory.Height + 14;
                }

                // --- 戰鬥結果 ---
                if (er.BattleResult != null)
                {
                    var btnBattle = new Button
                    {
                        Text = "⚔ 查看戰報",
                        Location = new Point(pad, y),
                        Width = 120,
                        Height = 36,
                        FlatStyle = FlatStyle.Flat,
                        BackColor = Color.FromArgb(50, 50, 70),
                        ForeColor = Color.White,
                        Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                        Cursor = Cursors.Hand
                    };
                    btnBattle.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 130);
                    btnBattle.Click += (_, _) =>
                    {
                        using var form = new BattleReportForm(er.BattleResult);
                        form.ShowDialog(this);
                    };
                    this.Controls.Add(btnBattle);
                    y += btnBattle.Height + 14;
                }

                // --- 獲得獎勵 ---
                var bonusParts = MyDoujinBot.Utilities.EventUiHelper.BuildBonusStatsParts(er.Rewards?.BonusStats);
                if (bonusParts.Count > 0)
                {
                    var lblReward = new Label
                    {
                        Text = $"獲得獎勵：{string.Join("、", bonusParts)}",
                        Location = new Point(pad, y),
                        Width = innerWidth,
                        AutoSize = true,
                        ForeColor = Color.FromArgb(255, 220, 80),
                        Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold)
                    };
                    this.Controls.Add(lblReward);
                    y += lblReward.Height + 16;
                }

                // --- 關閉按鈕 ---
                var btnClose = new Button
                {
                    Text = "關閉",
                    Location = new Point(pad + (innerWidth - 120) / 2, y),
                    Width = 120,
                    Height = 36,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(60, 60, 80),
                    ForeColor = Color.White,
                    Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnClose.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 130);
                btnClose.Click += (_, _) =>
                {
                    reg.Dispose();
                    tcs.TrySetResult(true);
                    this.Close();
                };
                this.Controls.Add(btnClose);
                y += 48;

                this.AutoScroll = true;
                int resultMaxHeight = (Screen.PrimaryScreen?.WorkingArea.Height ?? 1080) - 100;
                this.ClientSize = new Size(500, Math.Min(y, resultMaxHeight));
                this.ResumeLayout(true);

                this.FormClosed += (_, _) =>
                {
                    reg.Dispose();
                    tcs.TrySetResult(true);
                };

                this.BringToFront();
                this.Activate();
            };

            if (this.InvokeRequired)
                this.Invoke(buildAction);
            else
                buildAction();

            return tcs.Task;
        }
    }
}
