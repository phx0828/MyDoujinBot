using System;
using System.Drawing;
using System.Windows.Forms;
using MyDoujinBot.Services;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 應用程式設定對話框。
    /// 包含連線設定（Token）與通知設定。
    /// 所有設定儲存後會持久化至磁碟（%AppData%\MyDoujinBot\settings.json）。
    /// </summary>
    public class SettingsForm : Form
    {
        private TextBox txtToken = null!;
        private CheckBox chkNotify = null!;
        private CheckBox chkNotifySound = null!;

        /// <summary>儲存後的 Token 值，供 MainForm 更新記憶體中的 _currentToken</summary>
        public string Token { get; private set; } = string.Empty;

        public SettingsForm(string currentToken = "")
        {
            this.Text = "設定";
            this.Size = new Size(600, 420);
            this.MinimumSize = new Size(480, 320);
            // MaximumSize 移除：它會阻止 AutoScaleMode.Dpi 對視窗尺寸之縮放
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(28, 28, 35);
            this.ForeColor = Color.FromArgb(220, 220, 230);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // DPI 自動縮放
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            BuildUI(currentToken);
        }

        private void BuildUI(string currentToken)
        {
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1, RowCount = 0,
                AutoScroll = true,
                Padding = new Padding(16)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            this.Controls.Add(tbl);

            void AddRow(Control c)
            {
                tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                tbl.Controls.Add(c);
            }

            // ═══════════════════════════════════════════════════════════════════
            // 區塊：連線設定
            // ═══════════════════════════════════════════════════════════════════
            AddRow(new Label
            {
                Text = "連線設定",
                ForeColor = Color.FromArgb(130, 170, 255),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true, Margin = new Padding(0, 0, 0, 4)
            });

            AddRow(new Label
            {
                Text = "Bearer Token：",
                ForeColor = Color.FromArgb(160, 180, 255),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true, Margin = new Padding(0, 4, 0, 2)
            });

            txtToken = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 55),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5f),
                PlaceholderText = "請輸入 Bearer Token",
                Text = currentToken,
                Margin = new Padding(0, 0, 0, 4)
            };
            AddRow(txtToken);

            AddRow(new Label
            {
                Text = "⚠ Token 將儲存至磁碟，下次啟動後自動載入。請勿在公共電腦上使用。",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(210, 90, 90),
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                AutoSize = true, Margin = new Padding(0, 0, 0, 10)
            });

            // 分隔線
            AddRow(new Label
            {
                Dock = DockStyle.Fill, Height = 1,
                BackColor = Color.FromArgb(65, 65, 85),
                AutoSize = false, Text = "",
                Margin = new Padding(0, 0, 0, 10)
            });

            // ═══════════════════════════════════════════════════════════════════
            // 區塊：通知設定
            // ═══════════════════════════════════════════════════════════════════
            AddRow(new Label
            {
                Text = "通知設定",
                ForeColor = Color.FromArgb(130, 170, 255),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true, Margin = new Padding(0, 0, 0, 4)
            });

            chkNotify = new CheckBox
            {
                Text = "啟用系統通知（手動內嵌模式下，事件觸發時右下角氣泡提示）",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(210, 210, 225),
                BackColor = Color.Transparent,
                AutoSize = true,
                Checked = AppSettingsManager.Current.EnableSystemNotify,
                Margin = new Padding(0, 2, 0, 4)
            };
            AddRow(chkNotify);

            AddRow(new Label
            {
                Text = "僅於「手動選擇」+「內嵌於主畫面」模式有效。",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(120, 120, 145),
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                AutoSize = true,
                Margin = new Padding(20, 0, 0, 4)
            });

            chkNotifySound = new CheckBox
            {
                Text = "開啟通知音效",
                ForeColor = Color.FromArgb(210, 210, 225),
                BackColor = Color.Transparent,
                AutoSize = true,
                Checked = AppSettingsManager.Current.EnableSystemNotifySound,
                Margin = new Padding(20, 0, 0, 12)
            };
            AddRow(chkNotifySound);

            // ═══════════════════════════════════════════════════════════════════
            // 按鈕列
            // ═══════════════════════════════════════════════════════════════════
            var pnlBtns = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3, RowCount = 1,
                Margin = new Padding(0, 12, 0, 0)
            };
            pnlBtns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Spacer
            pnlBtns.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlBtns.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var btnCancel = new Button
            {
                Text = "取消",
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(16, 4, 16, 4),
                BackColor = Color.FromArgb(70, 70, 90),
                ForeColor = Color.FromArgb(200, 200, 210),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (_, _) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            var btnSave = new Button
            {
                Text = "儲存",
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(16, 4, 16, 4),
                BackColor = Color.FromArgb(40, 140, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand, Margin = Padding.Empty
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (_, _) =>
            {
                Token = txtToken.Text.Trim();

                AppSettingsManager.Current.Token = Token;
                AppSettingsManager.Current.EnableSystemNotify = chkNotify.Checked;
                AppSettingsManager.Current.EnableSystemNotifySound = chkNotifySound.Checked;
                AppSettingsManager.Save();

                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            pnlBtns.Controls.Add(new Label { Text = "", AutoSize = false }, 0, 0); // Spacer
            pnlBtns.Controls.Add(btnCancel, 1, 0);
            pnlBtns.Controls.Add(btnSave, 2, 0);
            AddRow(pnlBtns);

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

    }
}
