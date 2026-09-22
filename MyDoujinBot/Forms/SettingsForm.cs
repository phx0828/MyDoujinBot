using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 設定對話框。
    /// 目前僅包含 Bearer Token 設定。
    /// 未來可在此加入其他設定項目。
    /// </summary>
    public class SettingsForm : Form
    {
        private TextBox txtToken = null!;

        /// <summary>儲存後的 Token 值（Save 按下後才有值）</summary>
        public string Token { get; private set; } = string.Empty;

        public SettingsForm(string currentToken = "")
        {
            this.Text = "設定";
            this.Size = new Size(480, 200);
            this.MinimumSize = new Size(480, 200);
            this.MaximumSize = new Size(480, 200);
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
            var lblToken = new Label
            {
                Text = "Bearer Token：",
                Location = new Point(16, 18),
                ForeColor = Color.FromArgb(160, 180, 255),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true
            };
            this.Controls.Add(lblToken);

            txtToken = new TextBox
            {
                Location = new Point(16, 42),
                Width = 430,
                UseSystemPasswordChar = true,
                BackColor = Color.FromArgb(45, 45, 55),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5f),
                PlaceholderText = "輸入 Bearer Token（輸入時自動遮蔽）",
                Text = currentToken
            };
            this.Controls.Add(txtToken);

            var lblNote = new Label
            {
                Text = "⚠ Token 僅儲存於本次執行記憶體，不寫入磁碟，不出現在 LOG。",
                Location = new Point(16, 74),
                Width = 430,
                ForeColor = Color.FromArgb(150, 150, 100),
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                AutoSize = false,
                Height = 18
            };
            this.Controls.Add(lblNote);

            // --- 儲存按鈕 ---
            var btnSave = new Button
            {
                Text = "儲存",
                Location = new Point(228, 112),
                Width = 104,
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
                Token = txtToken.Text;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(btnSave);

            // --- 取消按鈕 ---
            var btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(342, 112),
                Width = 104,
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

            // Enter 鍵觸發儲存，Escape 觸發取消
            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }
    }
}
