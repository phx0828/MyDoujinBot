using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 獨立圖示選擇對話框。
    /// 開啟時自動讀取 Resources 資料夾中的 .ico 檔案，以縮圖卡片供使用者挑選，
    /// 點擊儲存後套用變更。
    /// </summary>
    public class IconSelectionForm : Form
    {
        public string SelectedIconFileName { get; private set; }

        private string _tempSelectedFileName;
        private readonly FlowLayoutPanel _pnlCards;
        private readonly List<CardItem> _cards = new();

        private class CardItem
        {
            public string FileName { get; set; } = string.Empty;
            public TableLayoutPanel Panel { get; set; } = null!;
            public Label Badge { get; set; } = null!;
            public PictureBox Pic { get; set; } = null!;
            public Label NameLabel { get; set; } = null!;
        }

        public IconSelectionForm(string currentSelected)
        {
            _tempSelectedFileName = string.IsNullOrWhiteSpace(currentSelected) ? "app.ico" : currentSelected;
            SelectedIconFileName = _tempSelectedFileName;

            this.Text = "選擇應用程式圖示";
            this.Size = new Size(540, 440);
            this.MinimumSize = new Size(460, 360);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(24, 24, 32);
            this.ForeColor = Color.FromArgb(220, 220, 230);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.ShowIcon = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            var tblRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(ScaleDpi(16)),
                BackColor = Color.Transparent
            };
            tblRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.Controls.Add(tblRoot);

            // 頂部說明
            var lblHeader = new Label
            {
                Text = "請選擇要套用的應用程式圖示（自動讀取自 Resources 資料夾）：",
                ForeColor = Color.FromArgb(190, 190, 210),
                Font = new Font("Microsoft JhengHei UI", 9f),
                AutoSize = true,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };
            tblRoot.Controls.Add(lblHeader, 0, 0);

            // 中央卡片捲動區
            _pnlCards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.FromArgb(18, 18, 24),
                Padding = new Padding(ScaleDpi(10)),
                Margin = new Padding(0, 0, 0, ScaleDpi(12))
            };
            tblRoot.Controls.Add(_pnlCards, 0, 1);

            // 底部按鈕列
            var pnlBottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            var btnCancel = new Button
            {
                Text = "取消",
                AutoSize = true,
                BackColor = Color.FromArgb(45, 45, 58),
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Cursor = Cursors.Hand,
                Padding = new Padding(ScaleDpi(16), ScaleDpi(5), ScaleDpi(16), ScaleDpi(5)),
                Margin = new Padding(ScaleDpi(8), 0, 0, 0)
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 85);
            btnCancel.MouseEnter += (s, e) => btnCancel.BackColor = Color.FromArgb(58, 58, 72);
            btnCancel.MouseLeave += (s, e) => btnCancel.BackColor = Color.FromArgb(45, 45, 58);
            btnCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            pnlBottom.Controls.Add(btnCancel);

            var btnSave = new Button
            {
                Text = "儲存",
                AutoSize = true,
                BackColor = Color.FromArgb(40, 140, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Padding = new Padding(ScaleDpi(18), ScaleDpi(5), ScaleDpi(18), ScaleDpi(5)),
                Margin = Padding.Empty
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.MouseEnter += (s, e) => btnSave.BackColor = Color.FromArgb(50, 160, 95);
            btnSave.MouseLeave += (s, e) => btnSave.BackColor = Color.FromArgb(40, 140, 80);
            btnSave.Click += (s, e) =>
            {
                SelectedIconFileName = _tempSelectedFileName;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            pnlBottom.Controls.Add(btnSave);

            tblRoot.Controls.Add(pnlBottom, 0, 2);

            LoadIcons();
        }

        private void LoadIcons()
        {
            _pnlCards.SuspendLayout();
            _cards.Clear();

            var iconPaths = GetAvailableIconPaths();

            if (iconPaths.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "Resources 資料夾內目前未找到任何 .ico 圖示檔案。",
                    ForeColor = Color.FromArgb(160, 160, 180),
                    AutoSize = true,
                    Margin = new Padding(ScaleDpi(10))
                };
                _pnlCards.Controls.Add(lblEmpty);
                _pnlCards.ResumeLayout(true);
                return;
            }

            foreach (var iconPath in iconPaths)
            {
                string fileName = Path.GetFileName(iconPath);
                bool isSelected = string.Equals(fileName, _tempSelectedFileName, StringComparison.OrdinalIgnoreCase);

                var card = new TableLayoutPanel
                {
                    Width = ScaleDpi(115),
                    Height = ScaleDpi(125),
                    ColumnCount = 1,
                    RowCount = 3,
                    BackColor = isSelected ? Color.FromArgb(32, 45, 65) : Color.FromArgb(28, 28, 38),
                    Margin = new Padding(0, 0, ScaleDpi(10), ScaleDpi(10)),
                    Padding = new Padding(ScaleDpi(6)),
                    Cursor = Cursors.Hand
                };
                card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                card.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleDpi(54)));
                card.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleDpi(34)));
                card.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleDpi(20)));

                var picIcon = new PictureBox
                {
                    Size = new Size(ScaleDpi(48), ScaleDpi(48)),
                    SizeMode = PictureBoxSizeMode.CenterImage,
                    Anchor = AnchorStyles.None,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                try
                {
                    using var fs = new FileStream(iconPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var ico = new Icon(fs, new Size(48, 48));
                    picIcon.Image = ico.ToBitmap();
                }
                catch
                {
                    picIcon.Image = SystemIcons.Application.ToBitmap();
                }

                var lblName = new Label
                {
                    Text = fileName,
                    ForeColor = isSelected ? Color.FromArgb(240, 240, 255) : Color.FromArgb(180, 180, 195),
                    Font = new Font("Microsoft JhengHei UI", 8.5f, isSelected ? FontStyle.Bold : FontStyle.Regular),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                var lblBadge = new Label
                {
                    Text = isSelected ? "✓ 已選取" : "點擊選取",
                    ForeColor = isSelected ? Color.FromArgb(80, 220, 120) : Color.FromArgb(120, 120, 140),
                    Font = new Font("Microsoft JhengHei UI", 8f),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                card.Paint += (s, pe) =>
                {
                    bool sel = string.Equals(fileName, _tempSelectedFileName, StringComparison.OrdinalIgnoreCase);
                    var borderColor = sel ? Color.FromArgb(80, 160, 255) : Color.FromArgb(50, 50, 65);
                    int borderWidth = sel ? 2 : 1;
                    using var pen = new Pen(borderColor, borderWidth);
                    var rect = card.ClientRectangle;
                    rect.Width -= 1;
                    rect.Height -= 1;
                    pe.Graphics.DrawRectangle(pen, rect);
                };

                var item = new CardItem
                {
                    FileName = fileName,
                    Panel = card,
                    Badge = lblBadge,
                    Pic = picIcon,
                    NameLabel = lblName
                };
                _cards.Add(item);

                Action onSelect = () =>
                {
                    _tempSelectedFileName = fileName;
                    UpdateCardVisuals();
                };

                Action onConfirm = () =>
                {
                    _tempSelectedFileName = fileName;
                    SelectedIconFileName = _tempSelectedFileName;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                };

                card.Click += (s, e) => onSelect();
                picIcon.Click += (s, e) => onSelect();
                lblName.Click += (s, e) => onSelect();
                lblBadge.Click += (s, e) => onSelect();

                card.DoubleClick += (s, e) => onConfirm();
                picIcon.DoubleClick += (s, e) => onConfirm();
                lblName.DoubleClick += (s, e) => onConfirm();
                lblBadge.DoubleClick += (s, e) => onConfirm();

                EventHandler onEnter = (s, e) =>
                {
                    if (!string.Equals(fileName, _tempSelectedFileName, StringComparison.OrdinalIgnoreCase))
                        card.BackColor = Color.FromArgb(38, 38, 50);
                };
                EventHandler onLeave = (s, e) =>
                {
                    if (!string.Equals(fileName, _tempSelectedFileName, StringComparison.OrdinalIgnoreCase))
                        card.BackColor = Color.FromArgb(28, 28, 38);
                };

                card.MouseEnter += onEnter;
                card.MouseLeave += onLeave;
                picIcon.MouseEnter += onEnter;
                picIcon.MouseLeave += onLeave;
                lblName.MouseEnter += onEnter;
                lblName.MouseLeave += onLeave;
                lblBadge.MouseEnter += onEnter;
                lblBadge.MouseLeave += onLeave;

                card.Controls.Add(picIcon, 0, 0);
                card.Controls.Add(lblName, 0, 1);
                card.Controls.Add(lblBadge, 0, 2);

                _pnlCards.Controls.Add(card);
            }

            _pnlCards.ResumeLayout(true);
        }

        private void UpdateCardVisuals()
        {
            foreach (var card in _cards)
            {
                bool isSel = string.Equals(card.FileName, _tempSelectedFileName, StringComparison.OrdinalIgnoreCase);
                card.Panel.BackColor = isSel ? Color.FromArgb(32, 45, 65) : Color.FromArgb(28, 28, 38);
                card.Badge.Text = isSel ? "✓ 已選取" : "點擊選取";
                card.Badge.ForeColor = isSel ? Color.FromArgb(80, 220, 120) : Color.FromArgb(120, 120, 140);
                card.NameLabel.ForeColor = isSel ? Color.FromArgb(240, 240, 255) : Color.FromArgb(180, 180, 195);
                card.NameLabel.Font = new Font("Microsoft JhengHei UI", 8.5f, isSel ? FontStyle.Bold : FontStyle.Regular);
                card.Panel.Invalidate();
            }
        }

        public static List<string> GetAvailableIconPaths()
        {
            var iconMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var candidateDirs = new List<string>
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"),
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(Directory.GetCurrentDirectory(), "MyDoujinBot", "Resources"),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources"),
                Directory.GetCurrentDirectory()
            };

            try
            {
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                for (int i = 0; i < 4 && dir != null; i++)
                {
                    var sub = Path.Combine(dir.FullName, "Resources");
                    if (Directory.Exists(sub)) candidateDirs.Add(sub);
                    var projectSub = Path.Combine(dir.FullName, "MyDoujinBot", "Resources");
                    if (Directory.Exists(projectSub)) candidateDirs.Add(projectSub);
                    dir = dir.Parent;
                }
            }
            catch { }

            foreach (var dir in candidateDirs)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (var file in Directory.GetFiles(dir, "*.ico"))
                    {
                        string name = Path.GetFileName(file);
                        if (!iconMap.ContainsKey(name))
                        {
                            iconMap[name] = file;
                        }
                    }
                }
                catch { }
            }

            return iconMap.Values.OrderBy(p => Path.GetFileName(p)).ToList();
        }

        private int ScaleDpi(int value)
        {
            return (int)Math.Round(value * (this.DeviceDpi / 96F));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var c in _cards)
                {
                    if (c.Pic.Image != null)
                    {
                        var img = c.Pic.Image;
                        c.Pic.Image = null;
                        img.Dispose();
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}
