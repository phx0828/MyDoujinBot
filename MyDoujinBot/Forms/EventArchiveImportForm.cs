using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using FontAwesome.Sharp;
using MyDoujinBot.Services;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 手動匯入更新事件庫對話視窗（作為線上自動更新之備援機制）。
    /// 支援選取本機 JSON 路線檔或直接自剪貼簿貼上路線檔 JSON 文字。
    /// </summary>
    public sealed class EventArchiveImportForm : Form
    {
        private const int PreviewCharLimit = 8000;

        private readonly EventArchiveService _archiveService;
        private string _fullJsonContent = string.Empty;
        private bool _isTruncatedPreview = false;
        private bool _isSuppressTextChange = false;

        private IconButton _btnSelectFile = null!;
        private IconButton _btnPasteClipboard = null!;
        private IconButton _btnClear = null!;
        private IconButton _btnOpenSite = null!;
        private TextBox _txtJson = null!;
        private Label _lblStatus = null!;
        private Button _btnImport = null!;
        private Button _btnCancel = null!;

        public EventArchiveUpdateResult? UpdateResult { get; private set; }

        private float DpiScale => (float)this.DeviceDpi / 96f;
        private int ScaleDpi(int value) => (int)Math.Round(value * DpiScale);

        public EventArchiveImportForm(EventArchiveService archiveService)
        {
            _archiveService = archiveService;

            this.Text = "手動匯入更新事件庫（備援）";
            this.Size = new Size(ScaleDpi(680), ScaleDpi(500));
            this.MinimumSize = new Size(ScaleDpi(560), ScaleDpi(400));
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(24, 24, 32);
            this.ForeColor = Color.FromArgb(225, 225, 238);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            BuildUI();
        }

        private void BuildUI()
        {
            var tblMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(ScaleDpi(16), ScaleDpi(14), ScaleDpi(16), ScaleDpi(14)),
                BackColor = Color.Transparent
            };
            tblMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tblMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Row 0: 說明文字
            tblMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Row 1: 操作按鈕列
            tblMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // Row 2: JSON 文字框
            tblMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Row 3: 狀態列
            tblMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Row 4: 底部確認/取消按鈕

            // ── Row 0: 說明文字 ──
            var lblHint = new Label
            {
                Text = "當線上自動更新因第三方網站改版而異常時，可前往【我的同人攻略圖鑑網】點擊上方「匯出路線檔」按鈕，再選擇下載的 JSON 檔案，或直接貼上複製的路線檔 JSON 文字：",
                ForeColor = Color.FromArgb(195, 205, 225),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };
            this.Resize += (_, _) =>
            {
                int maxW = Math.Max(200, tblMain.ClientSize.Width - tblMain.Padding.Horizontal);
                lblHint.MaximumSize = new Size(maxW, 0);
            };
            this.Load += (_, _) =>
            {
                int maxW = Math.Max(200, tblMain.ClientSize.Width - tblMain.Padding.Horizontal);
                lblHint.MaximumSize = new Size(maxW, 0);
            };

            // ── Row 1: 操作工具列 ──
            var pnlToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, ScaleDpi(8)),
                Padding = Padding.Empty
            };

            _btnSelectFile = CreateToolButton("選擇 JSON 檔案...", Color.FromArgb(45, 95, 150), IconChar.FolderOpen);
            _btnSelectFile.Click += OnSelectFileClicked;

            _btnPasteClipboard = CreateToolButton("從剪貼簿貼上", Color.FromArgb(42, 110, 85), IconChar.Paste);
            _btnPasteClipboard.Click += (_, _) => PasteFromClipboard();

            _btnClear = CreateToolButton("清空", Color.FromArgb(75, 50, 55), IconChar.Eraser);
            _btnClear.Click += (_, _) => ClearContent();

            _btnOpenSite = CreateToolButton("開啟攻略圖鑑網", Color.FromArgb(55, 65, 90), IconChar.ArrowUpRightFromSquare);
            _btnOpenSite.Click += (_, _) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo(EventArchiveService.DefaultRemoteUrl) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"無法開啟瀏覽器：\n{ex.Message}", "開啟失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            pnlToolbar.Controls.Add(_btnSelectFile);
            pnlToolbar.Controls.Add(_btnPasteClipboard);
            pnlToolbar.Controls.Add(_btnClear);
            pnlToolbar.Controls.Add(_btnOpenSite);

            // ── Row 2: JSON 輸入與預覽框 ──
            _txtJson = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                MaxLength = 0,
                WordWrap = false,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(32, 34, 44),
                ForeColor = Color.FromArgb(220, 225, 235),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5f),
                PlaceholderText = "請點擊上方「選擇 JSON 檔案...」或「從剪貼簿貼上」，亦可直接在此處按 Ctrl+V 貼上「匯出路線檔」之 JSON 內容...",
                Margin = new Padding(0, 0, 0, ScaleDpi(8))
            };
            _txtJson.KeyDown += OnTxtJsonKeyDown;
            _txtJson.TextChanged += OnTxtJsonTextChanged;

            // 自訂右鍵選單，確保右鍵貼上大檔案時亦走快速載入流程
            var ctxMenu = new ContextMenuStrip();
            var miPaste = new ToolStripMenuItem("從剪貼簿貼上 (Ctrl+V)");
            miPaste.Click += (_, _) => PasteFromClipboard();
            var miClear = new ToolStripMenuItem("清空內容");
            miClear.Click += (_, _) => ClearContent();
            ctxMenu.Items.Add(miPaste);
            ctxMenu.Items.Add(new ToolStripSeparator());
            ctxMenu.Items.Add(miClear);
            _txtJson.ContextMenuStrip = ctxMenu;

            // ── Row 3: 狀態提示列 ──
            _lblStatus = new Label
            {
                Text = "尚未載入路線檔資料。",
                ForeColor = Color.FromArgb(145, 155, 175),
                Font = new Font("Microsoft JhengHei UI", 9.2f),
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };

            // ── Row 4: 底部按鈕列 ──
            var pnlBottom = new FlowLayoutPanel
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
                Size = new Size(ScaleDpi(88), ScaleDpi(32)),
                BackColor = Color.FromArgb(55, 55, 70),
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Margin = Padding.Empty
            };
            _btnCancel.FlatAppearance.BorderSize = 0;

            _btnImport = new Button
            {
                Text = "確認匯入",
                Size = new Size(ScaleDpi(104), ScaleDpi(32)),
                BackColor = Color.FromArgb(40, 140, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, ScaleDpi(10), 0)
            };
            _btnImport.FlatAppearance.BorderSize = 0;
            _btnImport.Click += OnImportClicked;

            pnlBottom.Controls.Add(_btnCancel);
            pnlBottom.Controls.Add(_btnImport);

            tblMain.Controls.Add(lblHint, 0, 0);
            tblMain.Controls.Add(pnlToolbar, 0, 1);
            tblMain.Controls.Add(_txtJson, 0, 2);
            tblMain.Controls.Add(_lblStatus, 0, 3);
            tblMain.Controls.Add(pnlBottom, 0, 4);

            this.Controls.Add(tblMain);
            this.CancelButton = _btnCancel;
        }

        private IconButton CreateToolButton(string text, Color backColor, IconChar icon)
        {
            var btn = new IconButton
            {
                Text = $" {text}",
                AutoSize = true,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.2f),
                Cursor = Cursors.Hand,
                IconChar = icon,
                IconColor = Color.White,
                IconSize = ScaleDpi(16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(8), ScaleDpi(4), ScaleDpi(6), ScaleDpi(4)),
                Margin = new Padding(0, 0, ScaleDpi(8), ScaleDpi(4))
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void OnTxtJsonKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                PasteFromClipboard();
            }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                _txtJson.SelectAll();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }

        private void OnTxtJsonTextChanged(object? sender, EventArgs e)
        {
            if (_isSuppressTextChange) return;

            _isTruncatedPreview = false;
            _fullJsonContent = _txtJson.Text;

            if (string.IsNullOrWhiteSpace(_fullJsonContent))
            {
                _lblStatus.Text = "尚未載入路線檔資料。";
                _lblStatus.ForeColor = Color.FromArgb(145, 155, 175);
            }
            else
            {
                _lblStatus.Text = $"已輸入文字（共 {_fullJsonContent.Length:N0} 字元）";
                _lblStatus.ForeColor = Color.FromArgb(140, 200, 160);
            }
        }

        private void OnSelectFileClicked(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "選擇匯出的事件路線檔 JSON",
                Filter = "JSON 檔案 (*.json)|*.json|所有檔案 (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string content = File.ReadAllText(ofd.FileName);
                SetJsonContent(content, $"檔案 [{Path.GetFileName(ofd.FileName)}]");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"讀取檔案失敗：\n{ex.Message}", "讀取失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PasteFromClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText())
                {
                    MessageBox.Show(this, "目前剪貼簿中沒有文字內容。", "剪貼簿為空", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text))
                {
                    MessageBox.Show(this, "目前剪貼簿中的文字為空白。", "剪貼簿為空", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                SetJsonContent(text, "剪貼簿貼上");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"讀取剪貼簿失敗：\n{ex.Message}", "貼上失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearContent()
        {
            _isSuppressTextChange = true;
            try
            {
                _fullJsonContent = string.Empty;
                _isTruncatedPreview = false;
                _txtJson.ReadOnly = false;
                _txtJson.Text = string.Empty;
                _lblStatus.Text = "尚未載入路線檔資料。";
                _lblStatus.ForeColor = Color.FromArgb(145, 155, 175);
            }
            finally
            {
                _isSuppressTextChange = false;
            }
        }

        private void SetJsonContent(string content, string sourceName)
        {
            _isSuppressTextChange = true;
            try
            {
                _fullJsonContent = content.Trim();
                if (_fullJsonContent.Length > PreviewCharLimit)
                {
                    _isTruncatedPreview = true;
                    _txtJson.ReadOnly = true;
                    _txtJson.Text = _fullJsonContent.Substring(0, PreviewCharLimit) +
                                    $"\r\n\r\n... [內容較長，此處僅預覽前 {PreviewCharLimit:N0} 字元；完整資料共 {_fullJsonContent.Length:N0} 字元已載入記憶體，請直接點擊右下角「確認匯入」]";
                }
                else
                {
                    _isTruncatedPreview = false;
                    _txtJson.ReadOnly = false;
                    _txtJson.Text = _fullJsonContent;
                }

                _txtJson.SelectionStart = 0;
                _txtJson.ScrollToCaret();

                _lblStatus.Text = $"✔ 已載入來源：{sourceName}（共 {_fullJsonContent.Length:N0} 字元）";
                _lblStatus.ForeColor = Color.FromArgb(110, 215, 150);
            }
            finally
            {
                _isSuppressTextChange = false;
            }
        }

        private async void OnImportClicked(object? sender, EventArgs e)
        {
            string jsonToImport = _isTruncatedPreview ? _fullJsonContent : _txtJson.Text.Trim();
            if (string.IsNullOrWhiteSpace(jsonToImport))
            {
                MessageBox.Show(this, "請先選擇路線檔 (.json) 或貼上路線檔 JSON 文字。", "內容為空", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetBusy(true, "⏳ 正在驗證路線檔結構並更新本機事件庫...");

            try
            {
                var result = await Task.Run(() => _archiveService.ImportFromRouteJson(jsonToImport));
                UpdateResult = result;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                SetBusy(false, $"❌ 匯入失敗：{ex.Message}", isError: true);
                MessageBox.Show(this, $"匯入事件路線檔失敗：\n{ex.Message}", "匯入失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetBusy(bool busy, string statusText, bool isError = false)
        {
            _btnSelectFile.Enabled = !busy;
            _btnPasteClipboard.Enabled = !busy;
            _btnClear.Enabled = !busy;
            _btnOpenSite.Enabled = !busy;
            _txtJson.Enabled = !busy;
            _btnImport.Enabled = !busy;
            _btnCancel.Enabled = !busy;
            this.ControlBox = !busy;
            this.Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

            _lblStatus.Text = statusText;
            _lblStatus.ForeColor = busy
                ? Color.FromArgb(235, 185, 80)
                : isError
                    ? Color.FromArgb(235, 95, 95)
                    : Color.FromArgb(110, 215, 150);
        }
    }
}

