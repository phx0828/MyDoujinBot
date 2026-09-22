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
            this.Size = new Size(510, 360);
            this.MinimumSize = new Size(510, 360);
            this.MaximumSize = new Size(510, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(28, 28, 35);
            this.ForeColor = Color.FromArgb(220, 220, 230);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            BuildUI(currentToken);
        }

        private void BuildUI(string currentToken)
        {
            int y = 16;
            const int x = 16;
            const int w = 460;

            // =====================================================================
            // 區塊：連線設定
            // =====================================================================
            AddSectionHeader(this, "連線設定", y, x);
            y += 32;

            // Token 輸入框
            var lblToken = new Label
            {
                Text = "Bearer Token：",
                Location = new Point(x, y),
                ForeColor = Color.FromArgb(160, 180, 255),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true
            };
            this.Controls.Add(lblToken);
            y += 22;

            txtToken = new TextBox
            {
                Location = new Point(x, y),
                Width = w,
                // 不使用密碼遮蔽 — 使用者確認此為低敏感資料
                BackColor = Color.FromArgb(45, 45, 55),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5f),
                PlaceholderText = "請輸入 Bearer Token",
                Text = currentToken
            };
            this.Controls.Add(txtToken);
            y += 28;

            // 紅字提醒：將會儲存至磁碟
            var lblTokenNote = new Label
            {
                Text = "⚠ Token 將儲存至磁碟，下次啟動後自動載入。請勿在公共電腦上使用。",
                Location = new Point(x, y),
                Width = w,
                ForeColor = Color.FromArgb(210, 90, 90),
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                AutoSize = false,
                Height = 18
            };
            this.Controls.Add(lblTokenNote);
            y += 28;

            // =====================================================================
            // 分隔線
            // =====================================================================
            this.Controls.Add(new Label
            {
                Location = new Point(x, y),
                Width = w,
                Height = 1,
                BackColor = Color.FromArgb(65, 65, 85),
                AutoSize = false,
                Text = ""
            });
            y += 12;

            // =====================================================================
            // 區塊：通知設定
            // =====================================================================
            AddSectionHeader(this, "通知設定", y, x);
            y += 32;

            chkNotify = new CheckBox
            {
                Text = "啟用系統通知（手動內嵌模式下，事件觸發時右下角氣泡提示）",
                Location = new Point(x, y),
                Width = w,
                ForeColor = Color.FromArgb(210, 210, 225),
                BackColor = Color.Transparent,
                AutoSize = false,
                Height = 22,
                Checked = AppSettingsManager.Current.EnableSystemNotify
            };
            this.Controls.Add(chkNotify);
            y += 28;

            var lblNotifyNote = new Label
            {
                Text = "僅於「手動選擇」+「內嵌於主畫面」模式有效。",
                Location = new Point(x + 20, y),
                Width = w - 20,
                ForeColor = Color.FromArgb(120, 120, 145),
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                AutoSize = false,
                Height = 18
            };
            this.Controls.Add(lblNotifyNote);
            y += 28;

            chkNotifySound = new CheckBox
            {
                Text = "開啟通知音效",
                Location = new Point(x + 20, y),
                Width = w - 20,
                ForeColor = Color.FromArgb(210, 210, 225),
                BackColor = Color.Transparent,
                AutoSize = false,
                Height = 22,
                Checked = AppSettingsManager.Current.EnableSystemNotifySound
            };
            this.Controls.Add(chkNotifySound);
            y += 32;

            // =====================================================================
            // 按鈕列
            // =====================================================================
            var btnSave = new Button
            {
                Text = "儲存",
                Location = new Point(268, y),
                Width = 100,
                Height = 34,
                BackColor = Color.FromArgb(40, 140, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (_, _) =>
            {
                Token = txtToken.Text.Trim();

                // 持久化所有設定
                AppSettingsManager.Current.Token = Token;
                AppSettingsManager.Current.EnableSystemNotify = chkNotify.Checked;
                AppSettingsManager.Current.EnableSystemNotifySound = chkNotifySound.Checked;
                AppSettingsManager.Save();

                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(btnSave);

            var btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(378, y),
                Width = 100,
                Height = 34,
                BackColor = Color.FromArgb(70, 70, 90),
                ForeColor = Color.FromArgb(200, 200, 210),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (_, _) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private static void AddSectionHeader(Control parent, string text, int y, int x)
        {
            parent.Controls.Add(new Label
            {
                Location = new Point(x, y),
                Width = parent.Width - x * 2,
                Text = text,
                ForeColor = Color.FromArgb(130, 170, 255),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = false,
                Height = 22
            });
            parent.Controls.Add(new Label
            {
                Location = new Point(x, y + 22),
                Width = parent.Width - x * 2,
                Height = 1,
                BackColor = Color.FromArgb(65, 65, 85),
                AutoSize = false,
                Text = ""
            });
        }
    }
}
