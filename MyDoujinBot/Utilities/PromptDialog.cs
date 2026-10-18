using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyDoujinBot.Utilities
{
    /// <summary>
    /// 精緻暗黑風格的文字輸入對話框，用於方案命名、重新命名、Token 輸入等即時輸入情境。
    /// 完整支援 High-DPI 縮放與邊界自動換行，杜絕視窗跑版與內容超出邊界。
    /// </summary>
    public sealed class PromptDialog : Form
    {
        private readonly TextBox _txtInput;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;
        private readonly Label _lblPrompt;
        private readonly TableLayoutPanel _pnlMain;

        public string InputText => _txtInput.Text.Trim();

        private PromptDialog(string title, string prompt, string defaultValue, string placeholder = "")
        {
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.ShowIcon = false;
            this.ClientSize = new Size(460, 165);
            this.MinimumSize = new Size(380, 155);
            this.BackColor = Color.FromArgb(24, 24, 32);
            this.ForeColor = Color.FromArgb(230, 230, 240);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);

            // DPI 自動縮放
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            _pnlMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(20, 16, 20, 16),
                BackColor = Color.Transparent
            };
            _pnlMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _pnlMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _lblPrompt = new Label
            {
                Text = prompt,
                ForeColor = Color.FromArgb(210, 215, 230),
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 10)
            };

            _txtInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = defaultValue,
                PlaceholderText = placeholder,
                BackColor = Color.FromArgb(38, 38, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 16)
            };

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
                Width = 82,
                Height = 32,
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
                Width = 82,
                Height = 32,
                BackColor = Color.FromArgb(42, 90, 160),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 10, 0)
            };
            _btnOk.FlatAppearance.BorderSize = 0;

            pnlButtons.Controls.Add(_btnCancel);
            pnlButtons.Controls.Add(_btnOk);

            _pnlMain.Controls.Add(_lblPrompt, 0, 0);
            _pnlMain.Controls.Add(_txtInput, 0, 1);
            _pnlMain.Controls.Add(pnlButtons, 0, 2);

            this.Controls.Add(_pnlMain);
            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;

            // 確保 Label 自動換行，絕不超出視窗寬度
            this.Load += (_, _) => UpdatePromptWrap();
            this.Resize += (_, _) => UpdatePromptWrap();
        }

        private void UpdatePromptWrap()
        {
            if (_pnlMain != null && _lblPrompt != null)
            {
                int maxW = Math.Max(100, _pnlMain.ClientSize.Width - _pnlMain.Padding.Horizontal);
                _lblPrompt.MaximumSize = new Size(maxW, 0);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _txtInput.Focus();
            _txtInput.SelectAll();
        }

        /// <summary>
        /// 顯示輸入視窗並取得使用者輸入的字串。
        /// 若使用者點擊取消或關閉視窗，回傳 null。
        /// </summary>
        public static string? Show(IWin32Window? owner, string title, string prompt, string defaultValue = "", string placeholder = "")
        {
            using var dlg = new PromptDialog(title, prompt, defaultValue, placeholder);
            return dlg.ShowDialog(owner) == DialogResult.OK ? dlg.InputText : null;
        }
    }
}
