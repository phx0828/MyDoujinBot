using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FontAwesome.Sharp;
using MyDoujinBot.Models;
using MyDoujinBot.Services;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 【我的角色】分頁主面板。
    /// 顯示目前玩家暱稱（含死亡紅字／沾屎褐字狀態著色）、出戰角色、稱號、高解析度頭像、
    /// 等級、經驗值（總經驗值）、九圍能力值、轉生次數，以及本輪與生涯戰績統計。
    /// </summary>
    public sealed class MyCharacterPanel : Panel
    {
        private static readonly Color DeadNameColor   = Color.FromArgb(245, 85, 85);
        private static readonly Color SoiledNameColor = Color.FromArgb(195, 135, 75);
        private static readonly Color NormalNameColor = Color.FromArgb(238, 242, 250);

        private static readonly Color GridBorderColor = Color.FromArgb(46, 58, 76);
        private static readonly Color KeyCellBack     = Color.FromArgb(27, 36, 48);
        private static readonly Color ValCellBack     = Color.FromArgb(19, 25, 34);
        private static readonly Color KeyTextColor    = Color.FromArgb(195, 205, 220);
        private static readonly Color ValTextColor    = Color.FromArgb(238, 242, 250);

        private readonly BattleService _battleService;
        private readonly UserService _userService;
        private readonly Func<string> _tokenProvider;

        private Panel _pnlScrollOuter = null!;
        private TableLayoutPanel _tblMainLayout = null!;
        private IconButton _btnReincarnation = null!;
        private IconButton _btnRefresh = null!;
        private Label _lblStatus = null!;

        private HighQualityAvatarBox _picAvatar = null!;
        private string? _lastAvatarPath;
        private CancellationTokenSource? _avatarCts;

        // 頂部 3 列欄位
        private Label _valNickname = null!;
        private Label _valCharacter = null!;
        private Label _valTitle = null!;

        // 左側數值欄位
        private Label _valLevel = null!;
        private Label _valHp = null!;
        private Label _valAtk = null!;
        private Label _valDef = null!;
        private Label _valSta = null!;
        private Label _valAgi = null!;
        private Label _valSpd = null!;
        private Label _valTec = null!;
        private Label _valInt = null!;
        private Label _valLuk = null!;
        private Label _valReincarnation = null!;

        // 右側經驗與戰績欄位
        private Label _valExp = null!;
        private Label _valActiveThrows = null!;
        private Label _valDefenseThrows = null!;
        private Label _valTotalActiveThrows = null!;
        private Label _valTotalDefenseThrows = null!;
        private Label _valAssaultSoiled = null!;
        private Label _valCounterSoiled = null!;
        private Label _valWins = null!;
        private Label _valLosses = null!;
        private Label _valTotalWins = null!;
        private Label _valTotalLosses = null!;

        private bool _isRefreshing;

        private float DpiScale => (float)this.DeviceDpi / 96f;
        private int ScaleDpi(int value) => (int)Math.Round(value * DpiScale);

        public MyCharacterPanel(
            BattleService battleService,
            UserService userService,
            Func<string> tokenProvider)
        {
            _battleService = battleService;
            _userService = userService;
            _tokenProvider = tokenProvider;

            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(24, 24, 30);
            this.Padding = Padding.Empty;
            this.Margin = Padding.Empty;

            BuildUI();

            UserContext.OnProfileUpdated += OnUserContextProfileUpdated;
            this.Resize += (_, _) => UpdateResponsivePadding();

            RefreshFromUserContext();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UserContext.OnProfileUpdated -= OnUserContextProfileUpdated;
                _avatarCts?.Cancel();
                _avatarCts?.Dispose();
            }
            base.Dispose(disposing);
        }

        public void OnTabActivated()
        {
            UpdateResponsivePadding();
            RefreshFromUserContext();
        }

        private void OnUserContextProfileUpdated()
        {
            if (this.IsDisposed) return;
            if (this.InvokeRequired)
            {
                try { this.BeginInvoke(new Action(RefreshFromUserContext)); }
                catch { }
                return;
            }
            RefreshFromUserContext();
        }

        private void UpdateResponsivePadding()
        {
            if (_tblMainLayout == null || _pnlScrollOuter == null || _pnlScrollOuter.ClientSize.Width <= 0) return;

            int maxContentWidth = ScaleDpi(620);
            int minPadX = ScaleDpi(20);
            int padY = ScaleDpi(14);

            int extraWidth = _pnlScrollOuter.ClientSize.Width - maxContentWidth;
            int sidePad = extraWidth > minPadX * 2 ? extraWidth / 2 : minPadX;

            var targetPadding = new Padding(sidePad, padY, sidePad, padY);
            if (_tblMainLayout.Padding != targetPadding)
            {
                _tblMainLayout.Padding = targetPadding;
            }
        }

        private void BuildUI()
        {
            _pnlScrollOuter = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(24, 24, 30),
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };

            _tblMainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(ScaleDpi(20), ScaleDpi(16), ScaleDpi(20), ScaleDpi(16)),
                BackColor = Color.Transparent
            };
            _tblMainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _tblMainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 0: 頂部標題與重新整理按鈕
            _tblMainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 1: 角色資訊大表格

            // ═══════════════════════════════════════════════════════════════════
            // Row 0: 頂部標題與重新整理列
            // ═══════════════════════════════════════════════════════════════════
            var pnlHeaderBar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };
            pnlHeaderBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlHeaderBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pnlHeaderBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlHeaderBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lblTitle = new Label
            {
                Text = "我的角色",
                Font = new Font("Microsoft JhengHei UI", 13f, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 238, 248),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, ScaleDpi(12), 0)
            };

            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                ForeColor = Color.FromArgb(145, 195, 250),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = Padding.Empty
            };

            _btnReincarnation = new IconButton
            {
                Text = " 檢視轉生配點",
                AutoSize = true,
                Height = ScaleDpi(32),
                BackColor = Color.FromArgb(78, 62, 22),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Cursor = Cursors.Hand,
                IconChar = IconChar.Sliders,
                IconColor = Color.FromArgb(235, 200, 105),
                IconSize = ScaleDpi(15),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(8), 0, ScaleDpi(10), 0),
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, 0, ScaleDpi(8), 0)
            };
            _btnReincarnation.FlatAppearance.BorderSize = 0;
            _btnReincarnation.Click += (_, _) => OpenReincarnationDetailDialog();

            _btnRefresh = new IconButton
            {
                Text = " 重新整理",
                AutoSize = true,
                Height = ScaleDpi(32),
                BackColor = Color.FromArgb(45, 58, 82),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Cursor = Cursors.Hand,
                IconChar = IconChar.ArrowsRotate,
                IconColor = Color.FromArgb(135, 200, 255),
                IconSize = ScaleDpi(15),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(8), 0, ScaleDpi(10), 0),
                Anchor = AnchorStyles.Right,
                Margin = Padding.Empty
            };
            _btnRefresh.FlatAppearance.BorderSize = 0;
            _btnRefresh.Click += async (_, _) => await ManualRefreshAsync();

            pnlHeaderBar.Controls.Add(lblTitle, 0, 0);
            pnlHeaderBar.Controls.Add(_lblStatus, 1, 0);
            pnlHeaderBar.Controls.Add(_btnReincarnation, 2, 0);
            pnlHeaderBar.Controls.Add(_btnRefresh, 3, 0);
            _tblMainLayout.Controls.Add(pnlHeaderBar, 0, 0);

            // ═══════════════════════════════════════════════════════════════════
            // Row 1: 角色完整屬性與戰績表格 (4 欄 x 14 列)
            // ═══════════════════════════════════════════════════════════════════
            var tblGrid = new BufferedTableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 14,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                BackColor = GridBorderColor,
                Margin = Padding.Empty
            };

            tblGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21f));
            tblGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 29f));
            tblGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            tblGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

            // 前 3 列：左側為 暱稱 / 角色 / 稱號，右側跨 2 欄 x 3 列放頭像
            tblGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleDpi(34))); // Row 0: 暱稱
            tblGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleDpi(34))); // Row 1: 角色
            tblGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleDpi(68))); // Row 2: 稱號

            for (int r = 3; r < 14; r++)
            {
                tblGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleDpi(32)));
            }

            // ── Top Left (Rows 0..2, Cols 0..1) ──
            _valNickname  = AddPairCells(tblGrid, 0, 0, "暱稱");
            _valCharacter = AddPairCells(tblGrid, 0, 1, "角色");
            _valTitle     = AddPairCells(tblGrid, 0, 2, "稱號");

            // ── Top Right Avatar (Rows 0..2, Cols 2..3) ──
            var pnlAvatarCell = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ValCellBack,
                Margin = Padding.Empty,
                Padding = new Padding(ScaleDpi(6))
            };

            _picAvatar = new HighQualityAvatarBox
            {
                Size = new Size(ScaleDpi(124), ScaleDpi(124)),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                BorderStyle = BorderStyle.None
            };
            pnlAvatarCell.Controls.Add(_picAvatar);
            pnlAvatarCell.Resize += (_, _) =>
            {
                _picAvatar.Left = Math.Max(0, (pnlAvatarCell.ClientSize.Width - _picAvatar.Width) / 2);
                _picAvatar.Top  = Math.Max(0, (pnlAvatarCell.ClientSize.Height - _picAvatar.Height) / 2);
            };

            tblGrid.Controls.Add(pnlAvatarCell, 2, 0);
            tblGrid.SetColumnSpan(pnlAvatarCell, 2);
            tblGrid.SetRowSpan(pnlAvatarCell, 3);

            // ── Rows 3..13: 左欄 (等級、九圍、轉生次數) + 右欄 (經驗值、本輪與總戰績) ──
            _valLevel         = AddPairCells(tblGrid, 0, 3,  "等級");
            _valExp           = AddPairCells(tblGrid, 2, 3,  "經驗值 (總經驗值)");

            _valHp            = AddPairCells(tblGrid, 0, 4,  "HP");
            _valActiveThrows  = AddPairCells(tblGrid, 2, 4,  "主動扔屎");

            _valAtk           = AddPairCells(tblGrid, 0, 5,  "攻擊");
            _valDefenseThrows = AddPairCells(tblGrid, 2, 5,  "防衛扔屎");

            _valDef                = AddPairCells(tblGrid, 0, 6,  "防禦");
            _valTotalActiveThrows  = AddPairCells(tblGrid, 2, 6,  "總主動扔屎");

            _valSta                = AddPairCells(tblGrid, 0, 7,  "體力");
            _valTotalDefenseThrows = AddPairCells(tblGrid, 2, 7,  "總防衛扔屎");

            _valAgi           = AddPairCells(tblGrid, 0, 8,  "敏捷");
            _valAssaultSoiled = AddPairCells(tblGrid, 2, 8,  "遭襲沾屎");

            _valSpd           = AddPairCells(tblGrid, 0, 9,  "反應速度");
            _valCounterSoiled = AddPairCells(tblGrid, 2, 9,  "遭反殺沾屎");

            _valTec           = AddPairCells(tblGrid, 0, 10, "技巧");
            _valWins          = AddPairCells(tblGrid, 2, 10, "勝場");

            _valInt           = AddPairCells(tblGrid, 0, 11, "智力");
            _valLosses        = AddPairCells(tblGrid, 2, 11, "敗場");

            _valLuk           = AddPairCells(tblGrid, 0, 12, "幸運");
            _valTotalWins     = AddPairCells(tblGrid, 2, 12, "總勝場");

            _valReincarnation = AddPairCells(tblGrid, 0, 13, "轉生次數");
            _valTotalLosses   = AddPairCells(tblGrid, 2, 13, "總敗場");

            _tblMainLayout.Controls.Add(tblGrid, 0, 1);
            _pnlScrollOuter.Controls.Add(_tblMainLayout);
            this.Controls.Add(_pnlScrollOuter);
        }

        private Label AddPairCells(TableLayoutPanel grid, int startCol, int row, string keyText)
        {
            var pnlKey = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = KeyCellBack,
                Margin = Padding.Empty,
                Padding = new Padding(ScaleDpi(10), 0, ScaleDpi(6), 0)
            };

            var lblKey = new Label
            {
                Text = keyText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft JhengHei UI", 11.5f, FontStyle.Bold),
                ForeColor = KeyTextColor,
                AutoEllipsis = true
            };
            pnlKey.Controls.Add(lblKey);

            var pnlVal = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ValCellBack,
                Margin = Padding.Empty,
                Padding = new Padding(ScaleDpi(10), 0, ScaleDpi(10), 0)
            };

            var lblVal = new Label
            {
                Text = "—",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft JhengHei UI", 11.5f),
                ForeColor = ValTextColor,
                AutoEllipsis = true
            };
            pnlVal.Controls.Add(lblVal);

            grid.Controls.Add(pnlKey, startCol, row);
            grid.Controls.Add(pnlVal, startCol + 1, row);
            return lblVal;
        }

        public void RefreshFromUserContext()
        {
            var ctx = UserContext.Current;
            var me = ctx.UserMe;
            var ch = ctx.ActiveCharacter;

            if (!ctx.IsAuthenticated || ch == null)
            {
                _valNickname.Text = "—";
                _valNickname.ForeColor = NormalNameColor;
                _valCharacter.Text = "—";
                _valTitle.Text = "—";
                _valLevel.Text = "—";
                _valExp.Text = "—";
                _valHp.Text = "—";
                _valAtk.Text = "—";
                _valDef.Text = "—";
                _valSta.Text = "—";
                _valAgi.Text = "—";
                _valSpd.Text = "—";
                _valTec.Text = "—";
                _valInt.Text = "—";
                _valLuk.Text = "—";
                _valReincarnation.Text = "—";
                _valActiveThrows.Text = "—";
                _valDefenseThrows.Text = "—";
                _valTotalActiveThrows.Text = "—";
                _valTotalDefenseThrows.Text = "—";
                _valAssaultSoiled.Text = "—";
                _valCounterSoiled.Text = "—";
                _valWins.Text = "—";
                _valLosses.Text = "—";
                _valTotalWins.Text = "—";
                _valTotalLosses.Text = "—";
                _picAvatar.Image = null;
                _lastAvatarPath = null;
                return;
            }

            string nickname = !string.IsNullOrWhiteSpace(me?.Nickname)
                ? me.Nickname
                : (!string.IsNullOrWhiteSpace(me?.Username) ? me.Username : "—");

            _valNickname.Text = nickname;
            _valNickname.ForeColor = ch.IsDead
                ? DeadNameColor
                : (ch.IsSoiled ? SoiledNameColor : NormalNameColor);

            _valCharacter.Text = !string.IsNullOrWhiteSpace(ch.Name) ? ch.Name : "—";
            _valTitle.Text = !string.IsNullOrWhiteSpace(ch.Title) ? ch.Title : "—";

            _valLevel.Text = ch.Level.ToString();
            _valExp.Text = $"{ch.Exp} ({ch.TotalExp})";

            var st = ch.Stats;
            _valHp.Text  = (st?.HP ?? 0).ToString();
            _valAtk.Text = (st?.Atk ?? 0).ToString();
            _valDef.Text = (st?.Def ?? 0).ToString();
            _valSta.Text = (st?.Sta ?? 0).ToString();
            _valAgi.Text = (st?.Agi ?? 0).ToString();
            _valSpd.Text = (st?.Spd ?? 0).ToString();
            _valTec.Text = (st?.Tec ?? 0).ToString();
            _valInt.Text = (st?.Int ?? 0).ToString();
            _valLuk.Text = (st?.Luk ?? 0).ToString();

            _valReincarnation.Text = ch.ReincarnationCount.ToString();

            var cs = ch.CombatStats;
            var tcs = ch.TotalCombatStats;

            _valActiveThrows.Text       = (cs?.ActiveThrows ?? 0).ToString();
            _valDefenseThrows.Text      = (cs?.DefenseThrows ?? 0).ToString();
            _valTotalActiveThrows.Text  = (tcs?.ActiveThrows ?? 0).ToString();
            _valTotalDefenseThrows.Text = (tcs?.DefenseThrows ?? 0).ToString();
            _valAssaultSoiled.Text      = Math.Max(cs?.AssaultSoiled ?? 0, tcs?.AssaultSoiled ?? 0).ToString();
            _valCounterSoiled.Text      = Math.Max(cs?.CounterSoiled ?? 0, tcs?.CounterSoiled ?? 0).ToString();
            _valWins.Text               = (cs?.Wins ?? 0).ToString();
            _valLosses.Text             = (cs?.Losses ?? 0).ToString();
            _valTotalWins.Text          = (tcs?.Wins ?? 0).ToString();
            _valTotalLosses.Text        = (tcs?.Losses ?? 0).ToString();

            _ = EnsureAvatarLoadedAsync(ch.ImagePath);
        }

        private async Task EnsureAvatarLoadedAsync(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                _picAvatar.Image = null;
                _lastAvatarPath = null;
                return;
            }

            var cached = _battleService.TryGetCachedAvatarBitmap(imagePath, 0);
            if (cached != null)
            {
                _picAvatar.Image = cached;
                _lastAvatarPath = imagePath;
                return;
            }

            if (string.Equals(_lastAvatarPath, imagePath, StringComparison.OrdinalIgnoreCase) && _picAvatar.Image != null)
            {
                return;
            }

            _lastAvatarPath = imagePath;
            _avatarCts?.Cancel();
            _avatarCts?.Dispose();
            _avatarCts = new CancellationTokenSource();
            var ct = _avatarCts.Token;

            var bmp = await _battleService.FetchAvatarBitmapAsync(imagePath, 0, ct);
            if (!ct.IsCancellationRequested && !this.IsDisposed && bmp != null)
            {
                _picAvatar.Image = bmp;
            }
        }

        private void OpenReincarnationDetailDialog()
        {
            string token = _tokenProvider();
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(this.FindForm(), "請先至「設定」分頁設定 Bearer Token。", "未設定 Token", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new ReincarnationDetailForm(_userService, token);
            dlg.ShowDialog(this.FindForm());
        }

        private async Task ManualRefreshAsync()
        {
            if (_isRefreshing) return;

            string token = _tokenProvider();
            if (string.IsNullOrWhiteSpace(token))
            {
                _lblStatus.Text = "⚠ 請先於「設定」設定 Bearer Token。";
                _lblStatus.ForeColor = Color.FromArgb(235, 175, 70);
                return;
            }

            _isRefreshing = true;
            _btnRefresh.Enabled = false;
            _lblStatus.Text = "⏳ 正在同步角色資料...";
            _lblStatus.ForeColor = Color.FromArgb(145, 195, 250);

            try
            {
                var res = await _userService.ValidateAndFetchProfileAsync(token);
                if (this.IsDisposed) return;

                if (res.IsSuccess)
                {
                    _lblStatus.Text = string.Empty;
                    RefreshFromUserContext();
                }
                else
                {
                    _lblStatus.Text = $"❌ 同步失敗：{res.ErrorMessage}";
                    _lblStatus.ForeColor = Color.FromArgb(235, 95, 95);
                }
            }
            catch (Exception ex)
            {
                if (!this.IsDisposed)
                {
                    _lblStatus.Text = $"❌ 發生例外：{ex.Message}";
                    _lblStatus.ForeColor = Color.FromArgb(235, 95, 95);
                }
            }
            finally
            {
                if (!this.IsDisposed)
                {
                    _isRefreshing = false;
                    _btnRefresh.Enabled = true;
                }
            }
        }

        private sealed class BufferedTableLayoutPanel : TableLayoutPanel
        {
            public BufferedTableLayoutPanel()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            }
        }

        private sealed class HighQualityAvatarBox : PictureBox
        {
            protected override void OnPaint(PaintEventArgs pe)
            {
                pe.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                pe.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                pe.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                pe.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                base.OnPaint(pe);
            }
        }
    }
}

