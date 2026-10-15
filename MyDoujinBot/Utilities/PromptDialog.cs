using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyDoujinBot.Utilities
{
    /// <summary>
    /// 精緻暗黑風格的文字輸入對話框，用於方案命名、重新命名等即時輸入情境。
    /// </summary>
    public sealed class PromptDialog : Form
    {
        private readonly TextBox _txtInput;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;

        public string InputText => _txtInput.Text.Trim();

        private PromptDialog(string title, string prompt, string defaultValue)
        {
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.ClientSize = new Size(380, 160);
            this.BackColor = Color.FromArgb(24, 24, 32);
            this.ForeColor = Color.FromArgb(230, 230, 240);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);

            var pnlMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(16, 14, 16, 14),
                BackColor = Color.Transparent
            };
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblPrompt = new Label
            {
                Text = prompt,
                ForeColor = Color.FromArgb(200, 200, 220),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };

            _txtInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = defaultValue,
                BackColor = Color.FromArgb(38, 38, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 16)
            };
            _txtInput.SelectAll();

            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };

            _btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Width = 78,
                Height = 30,
                BackColor = Color.FromArgb(50, 50, 65),
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 0)
            };
            _btnCancel.FlatAppearance.BorderSize = 0;

            _btnOk = new Button
            {
                Text = "確定",
                DialogResult = DialogResult.OK,
                Width = 78,
                Height = 30,
                BackColor = Color.FromArgb(42, 90, 160),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnOk.FlatAppearance.BorderSize = 0;

            pnlButtons.Controls.Add(_btnCancel);
            pnlButtons.Controls.Add(_btnOk);

            pnlMain.Controls.Add(lblPrompt, 0, 0);
            pnlMain.Controls.Add(_txtInput, 0, 1);
            pnlMain.Controls.Add(pnlButtons, 0, 2);

            this.Controls.Add(pnlMain);
            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;
        }

        /// <summary>
        /// 顯示輸入視窗並取得使用者輸入的字串。
        /// 若使用者點擊取消或關閉視窗，回傳 null。
        /// </summary>
        public static string? Show(IWin32Window? owner, string title, string prompt, string defaultValue = "")
        {
            using var dlg = new PromptDialog(title, prompt, defaultValue);
            if (owner != null)
            {
                return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.InputText : null;
            }
            return dlg.ShowDialog() == DialogResult.OK ? dlg.InputText : null;
        }
    }
}
