using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyDoujinBot.Utilities
{
    public enum PromptDialogResultType
    {
        Ok,
        Cancel,
        SecondaryAction
    }

    public class PromptDialogResult
    {
        public PromptDialogResultType Type { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// 精緻暗黑風格的文字輸入對話框，用於方案命名、重新命名、Token 輸入等即時輸入情境。
    /// 支援載入進度提示（黃色進度文字）、元件鎖定與 High-DPI 自適應對齊。
    /// </summary>
    public sealed class PromptDialog : Form
    {
        private readonly TextBox _txtInput;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;
        private readonly Button? _btnSecondary;
        private readonly Label _lblPrompt;
        private readonly TableLayoutPanel _pnlMain;
        private readonly Label _lblStatus;
        private readonly Func<PromptDialog, Task<bool>>? _onSecondaryActionAsync;

        public string InputText
        {
            get => _txtInput.Text.Trim();
            set
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(() => { _txtInput.Text = value ?? string.Empty; });
                }
                else
                {
                    _txtInput.Text = value ?? string.Empty;
                }
            }
        }
        public PromptDialogResultType ResultType { get; private set; } = PromptDialogResultType.Cancel;

        private PromptDialog(
            string title,
            string prompt,
            string defaultValue,
            string placeholder = "",
            string? secondaryActionText = null,
            Func<PromptDialog, Task<bool>>? onSecondaryActionAsync = null)
        {
            _onSecondaryActionAsync = onSecondaryActionAsync;

            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.ShowIcon = false;
            this.ClientSize = new Size(secondaryActionText != null ? 480 : 420, 120);
            this.MinimumSize = new Size(360, 100);
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
                RowCount = 4,
                Padding = new Padding(20, 14, 20, 14),
                BackColor = Color.Transparent
            };
            _pnlMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _pnlMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Row 0: 提示文字
            _lblPrompt = new Label
            {
                Text = prompt,
                ForeColor = Color.FromArgb(215, 220, 235),
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 6)
            };

            // Row 1: 輸入框
            _txtInput = new TextBox
            {
                Dock = DockStyle.Top,
                Text = defaultValue,
                PlaceholderText = placeholder,
                BackColor = Color.FromArgb(38, 38, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 10f),
                Margin = new Padding(0, 0, 0, 10)
            };

            // Row 2: 載入與驗證進度狀態文字（黃色文字，預設隱藏）
            _lblStatus = new Label
            {
                Text = string.Empty,
                ForeColor = Color.FromArgb(235, 185, 80),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Dock = DockStyle.Top,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8),
                Visible = false
            };

            // Row 3: 按鈕列
            var tblButtonsRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 1,
                Height = 32,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblButtonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblButtonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var pnlRightButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Size = new Size(82, 32),
                BackColor = Color.FromArgb(50, 50, 65),
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = Padding.Empty,
                Anchor = AnchorStyles.Top
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => { ResultType = PromptDialogResultType.Cancel; };

            _btnOk = new Button
            {
                Text = "確定",
                DialogResult = DialogResult.OK,
                Size = new Size(82, 32),
                BackColor = Color.FromArgb(42, 90, 160),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 0, 10, 0),
                Anchor = AnchorStyles.Top
            };
            _btnOk.FlatAppearance.BorderSize = 0;
            _btnOk.Click += (s, e) => { ResultType = PromptDialogResultType.Ok; };

            pnlRightButtons.Controls.Add(_btnCancel);
            pnlRightButtons.Controls.Add(_btnOk);

            if (!string.IsNullOrWhiteSpace(secondaryActionText))
            {
                _btnSecondary = new Button
                {
                    Text = secondaryActionText,
                    Size = new Size(116, 32),
                    BackColor = Color.FromArgb(48, 72, 108),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new Font("Microsoft JhengHei UI", 9.5f),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = Padding.Empty,
                    Anchor = AnchorStyles.Left | AnchorStyles.Top
                };
                _btnSecondary.FlatAppearance.BorderSize = 0;
                _btnSecondary.Click += async (s, e) =>
                {
                    if (_onSecondaryActionAsync != null)
                    {
                        try
                        {
                            bool success = await _onSecondaryActionAsync(this);
                            if (success)
                            {
                                ResultType = PromptDialogResultType.SecondaryAction;
                                this.DialogResult = DialogResult.OK;
                                this.Close();
                            }
                            else
                            {
                                SetLoading(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            SetLoading(false);
                            MessageBox.Show(this, $"執行失敗：\n{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        ResultType = PromptDialogResultType.SecondaryAction;
                        this.DialogResult = DialogResult.Yes;
                        this.Close();
                    }
                };
                tblButtonsRow.Controls.Add(_btnSecondary, 0, 0);
            }

            tblButtonsRow.Controls.Add(pnlRightButtons, 1, 0);

            _pnlMain.Controls.Add(_lblPrompt, 0, 0);
            _pnlMain.Controls.Add(_txtInput, 0, 1);
            _pnlMain.Controls.Add(_lblStatus, 0, 2);
            _pnlMain.Controls.Add(tblButtonsRow, 0, 3);

            this.Controls.Add(_pnlMain);
            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;

            // 確保 Label 自動換行並依據實際內容貼齊視窗高度，消除多餘底部空白
            this.Load += (_, _) => UpdateDialogSize();
            this.Resize += (_, _) => UpdateDialogSize();
            UpdateDialogSize();
        }

        /// <summary>
        /// 設定視窗的 Loading 狀態與黃色進度文字，並鎖定/解鎖所有輸入元件。
        /// </summary>
        public void SetLoading(bool isLoading, string statusText = "")
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(() => SetLoading(isLoading, statusText));
                return;
            }

            _txtInput.Enabled = !isLoading;
            _btnOk.Enabled = !isLoading;
            _btnCancel.Enabled = !isLoading;
            if (_btnSecondary != null) _btnSecondary.Enabled = !isLoading;

            if (isLoading && !string.IsNullOrWhiteSpace(statusText))
            {
                _lblStatus.Text = statusText.StartsWith("⏳") ? statusText : $"⏳ {statusText}";
                _lblStatus.ForeColor = Color.FromArgb(235, 185, 80);
                _lblStatus.Visible = true;
            }
            else
            {
                _lblStatus.Text = string.Empty;
                _lblStatus.Visible = false;
            }

            UpdateDialogSize();
            this.ControlBox = !isLoading;
            this.Cursor = isLoading ? Cursors.WaitCursor : Cursors.Default;
            this.Refresh();
        }

        private void UpdatePromptWrap()
        {
            if (_pnlMain != null && _lblPrompt != null)
            {
                int maxW = Math.Max(100, _pnlMain.ClientSize.Width - _pnlMain.Padding.Horizontal);
                _lblPrompt.MaximumSize = new Size(maxW, 0);
            }
        }

        private void UpdateDialogSize()
        {
            if (_pnlMain == null) return;
            UpdatePromptWrap();
            int preferredHeight = _pnlMain.GetPreferredSize(new Size(this.ClientSize.Width, 0)).Height;
            if (preferredHeight > 50 && this.ClientSize.Height != preferredHeight)
            {
                this.ClientSize = new Size(this.ClientSize.Width, preferredHeight);
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

        /// <summary>
        /// 顯示具備次要操作按鈕（如「匯入備份資料」）的擴充輸入視窗。
        /// 支援傳入非同步回呼函式，在同一視窗內呈現黃色進度狀態文字並鎖定元件。
        /// </summary>
        public static PromptDialogResult ShowExtended(
            IWin32Window? owner,
            string title,
            string prompt,
            string? secondaryActionText = null,
            string defaultValue = "",
            string placeholder = "",
            Func<PromptDialog, Task<bool>>? onSecondaryActionAsync = null)
        {
            using var dlg = new PromptDialog(title, prompt, defaultValue, placeholder, secondaryActionText, onSecondaryActionAsync);
            var res = dlg.ShowDialog(owner);
            if (dlg.ResultType == PromptDialogResultType.SecondaryAction || res == DialogResult.Yes)
            {
                return new PromptDialogResult { Type = PromptDialogResultType.SecondaryAction, Text = dlg.InputText };
            }
            if (res == DialogResult.OK)
            {
                return new PromptDialogResult { Type = PromptDialogResultType.Ok, Text = dlg.InputText };
            }
            return new PromptDialogResult { Type = PromptDialogResultType.Cancel, Text = dlg.InputText };
        }
    }
}
