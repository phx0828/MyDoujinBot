using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FontAwesome.Sharp;
using MyDoujinBot.Models;
using MyDoujinBot.Services;
using MyDoujinBot.Utilities;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 玩家詳細資訊確認視窗（呼叫 GET /api/players/{id} 並顯示頭像、ID 複製、角色稱號、九圍數值與戰績）。
    /// </summary>
    public sealed class PlayerDetailForm : Form
    {
        private static readonly Color DeadNameColor               = Color.FromArgb(245, 85, 85);
        private static readonly Color SoiledNameColor             = Color.FromArgb(195, 135, 75);
        private static readonly Color NormalNameColor             = Color.FromArgb(240, 240, 250);
        private static readonly Color UniformStatValueColor       = Color.FromArgb(235, 238, 248);
        private static readonly Color HighestStatValueColor       = Color.FromArgb(255, 215, 70);  // 金黃色（除 HP 外最高）
        private static readonly Color SecondHighestStatValueColor = Color.FromArgb(242, 232, 185); // 偏白淺暖黃（除 HP 外次高）

        private readonly BattleService _battleService;
        private readonly string _playerId;
        private readonly string _token;
        private readonly PlayerSummaryItem? _summaryHint;
        private readonly CancellationTokenSource _cts = new();

        private string _currentPlayerId;
        private PictureBox _picAvatar = null!;
        private Label _lblNickname = null!;
        private Label _lblDeadTag = null!;
        private Label _lblCharacterTitle = null!;
        private Label _lblLevelFloor = null!;
        private IconPictureBox _picCopyIdIcon = null!;
        private Label _lblPlayerIdValue = null!;
        private Label _lblCopyFeedback = null!;
        private Label _lblLoadingStatus = null!;

        // 九圍數值標籤
        private Label _valHp = null!;
        private Label _valAtk = null!;
        private Label _valDef = null!;
        private Label _valSta = null!;
        private Label _valAgi = null!;
        private Label _valSpd = null!;
        private Label _valTec = null!;
        private Label _valInt = null!;
        private Label _valLuk = null!;

        // 戰績標籤
        private Label _valWins = null!;
        private Label _valLosses = null!;
        private Label _valActiveThrows = null!;
        private Label _valDefenseThrows = null!;
        private Label _valAssaultSoiled = null!;
        private Label _valCounterSoiled = null!;

        private float DpiScale => (float)this.DeviceDpi / 96f;
        private int ScaleDpi(int value) => (int)Math.Round(value * DpiScale);

        public PlayerDetailForm(
            BattleService battleService,
            string playerId,
            string token,
            PlayerSummaryItem? summaryHint = null)
        {
            _battleService = battleService;
            _playerId = playerId;
            _currentPlayerId = playerId;
            _token = token;
            _summaryHint = summaryHint;

            string initName = summaryHint?.Nickname ?? playerId;
            this.Text = $"玩家詳細資訊 - {initName}";
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(22, 22, 30);
            this.ForeColor = Color.FromArgb(225, 225, 238);
            this.Font = new Font("Microsoft JhengHei UI", 10.5f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            BuildUI();

            this.Shown += async (_, _) => await LoadDetailAsync();
            this.FormClosing += (_, _) =>
            {
                _cts.Cancel();
                _cts.Dispose();
            };
        }

        private void BuildUI()
        {
            var tblRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = ScaleDpi(540),
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(ScaleDpi(14), ScaleDpi(12), ScaleDpi(14), ScaleDpi(12)),
                BackColor = Color.Transparent
            };
            tblRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 0: 頂部角色與玩家基本資料卡
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 1: 載入狀態提示
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 2: 雙欄區塊（左：玩家角色數值直行，右：本輪戰績統計直行）
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 3: 底部關閉按鈕

            // ═══════════════════════════════════════════════════════════════════
            // Row 0: 頂部玩家與角色卡片
            // ═══════════════════════════════════════════════════════════════════
            var pnlHeaderCard = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(30, 32, 44),
                Padding = new Padding(ScaleDpi(14)),
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };
            pnlHeaderCard.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlHeaderCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            _picAvatar = new HighQualityAvatarBox
            {
                Size = new Size(ScaleDpi(84), ScaleDpi(84)),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(40, 42, 56),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(12), 0),
                Image = _battleService.TryGetCachedAvatarBitmap(_summaryHint?.ImagePath, 0)
            };
            pnlHeaderCard.Controls.Add(_picAvatar, 0, 0);

            var tblInfo = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };
            tblInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // 1. 暱稱 (死亡標記紅色字體「(死亡)」)
            var pnlNameRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, ScaleDpi(2))
            };

            bool initDead = _summaryHint?.IsDead ?? false;
            bool initSoiled = _summaryHint?.IsSoiled ?? false;

            _lblNickname = new Label
            {
                Text = _summaryHint?.Nickname ?? "載入中...",
                Font = new Font("Microsoft JhengHei UI", 13.5f, FontStyle.Bold),
                ForeColor = GetNicknameColor(initDead, initSoiled),
                AutoSize = true,
                Margin = new Padding(0, 0, ScaleDpi(6), 0)
            };

            _lblDeadTag = new Label
            {
                Text = "(死亡)",
                Font = new Font("Microsoft JhengHei UI", 11.5f, FontStyle.Bold),
                ForeColor = DeadNameColor,
                AutoSize = true,
                Visible = initDead,
                Margin = new Padding(0, ScaleDpi(2), 0, 0)
            };

            pnlNameRow.Controls.Add(_lblNickname);
            pnlNameRow.Controls.Add(_lblDeadTag);
            tblInfo.Controls.Add(pnlNameRow, 0, 0);

            // 2. 角色（稱號）
            _lblCharacterTitle = new Label
            {
                Text = _summaryHint != null
                    ? FormatCharacterTitle(_summaryHint.CharacterName, _summaryHint.CharacterTitle)
                    : "載入中...",
                Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 195, 255),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(2))
            };
            tblInfo.Controls.Add(_lblCharacterTitle, 0, 1);

            // 3. 等級：Lv.88｜樓層：1F / 80F
            _lblLevelFloor = new Label
            {
                Text = FormatLevelAndFloor(_summaryHint?.Level ?? 0, _summaryHint?.Floor ?? 0, _summaryHint?.HighestFloor ?? 0),
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(215, 220, 235),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(3))
            };
            tblInfo.Controls.Add(_lblLevelFloor, 0, 2);

            // 4. 玩家ID：[複製icon] xxxxxxx (點圖示或字串複製)
            var pnlIdRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };

            var lblIdPrefix = new Label
            {
                Text = "玩家ID：",
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                ForeColor = Color.FromArgb(165, 175, 195),
                AutoSize = true,
                Margin = new Padding(0, ScaleDpi(1), 0, 0)
            };

            _lblCopyFeedback = new Label
            {
                Text = "（已複製！）",
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(95, 225, 135),
                AutoSize = true,
                Visible = false,
                Margin = new Padding(0, ScaleDpi(1), ScaleDpi(4), 0)
            };

            _lblPlayerIdValue = new Label
            {
                Text = _currentPlayerId,
                Font = new Font("Consolas", 10.5f, FontStyle.Underline),
                ForeColor = Color.FromArgb(120, 200, 255),
                AutoSize = true,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, ScaleDpi(1), ScaleDpi(4), 0)
            };
            _lblPlayerIdValue.Click += (_, _) => CopyPlayerIdToClipboard();

            _picCopyIdIcon = new IconPictureBox
            {
                IconChar = IconChar.Copy,
                IconColor = Color.FromArgb(120, 200, 255),
                IconSize = ScaleDpi(15),
                Size = new Size(ScaleDpi(16), ScaleDpi(16)),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, ScaleDpi(2), 0, 0)
            };
            _picCopyIdIcon.Click += (_, _) => CopyPlayerIdToClipboard();

            pnlIdRow.Controls.Add(lblIdPrefix);
            pnlIdRow.Controls.Add(_picCopyIdIcon);
            pnlIdRow.Controls.Add(_lblPlayerIdValue);
            pnlIdRow.Controls.Add(_lblCopyFeedback);
            tblInfo.Controls.Add(pnlIdRow, 0, 3);

            pnlHeaderCard.Controls.Add(tblInfo, 1, 0);
            tblRoot.Controls.Add(pnlHeaderCard, 0, 0);

            // ═══════════════════════════════════════════════════════════════════
            // Row 1: 狀態提示（僅於載入中或錯誤時顯示，載入完成後自動隱藏）
            // ═══════════════════════════════════════════════════════════════════
            _lblLoadingStatus = new Label
            {
                Text = "⏳ 正在讀取玩家詳細數值...",
                ForeColor = Color.FromArgb(235, 185, 80),
                Font = new Font("Microsoft JhengHei UI", 10f),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(8))
            };
            tblRoot.Controls.Add(_lblLoadingStatus, 0, 1);

            // ═══════════════════════════════════════════════════════════════════
            // Row 2: 左右雙欄佈局（左側：玩家角色數值直行 1x9，右側：本輪戰績統計直行 1x6）
            // ═══════════════════════════════════════════════════════════════════
            var tblColumns = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, ScaleDpi(8))
            };
            tblColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tblColumns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tblColumns.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // 左側：玩家角色數值表格 (直式單欄 1x9)
            var grpStats = CreateSectionCard("玩家角色數值", out var tblStatsGrid, columns: 1, rows: 9);
            grpStats.Margin = new Padding(0, 0, ScaleDpi(5), 0);
            _valHp  = AddStatCell(tblStatsGrid, 0, 0, "HP",       UniformStatValueColor);
            _valAtk = AddStatCell(tblStatsGrid, 0, 1, "攻擊",     UniformStatValueColor);
            _valDef = AddStatCell(tblStatsGrid, 0, 2, "防禦",     UniformStatValueColor);
            _valSta = AddStatCell(tblStatsGrid, 0, 3, "體力",     UniformStatValueColor);
            _valAgi = AddStatCell(tblStatsGrid, 0, 4, "敏捷",     UniformStatValueColor);
            _valSpd = AddStatCell(tblStatsGrid, 0, 5, "反應速度", UniformStatValueColor);
            _valTec = AddStatCell(tblStatsGrid, 0, 6, "技巧",     UniformStatValueColor);
            _valInt = AddStatCell(tblStatsGrid, 0, 7, "智力",     UniformStatValueColor);
            _valLuk = AddStatCell(tblStatsGrid, 0, 8, "幸運",     UniformStatValueColor);
            tblColumns.Controls.Add(grpStats, 0, 0);

            // 右側：本輪戰績統計表格 (直式單欄 1x6)
            var grpCombat = CreateSectionCard("本輪戰績統計", out var tblCombatGrid, columns: 1, rows: 6);
            grpCombat.Margin = new Padding(ScaleDpi(5), 0, 0, 0);
            _valWins          = AddStatCell(tblCombatGrid, 0, 0, "勝場",       Color.FromArgb(100, 225, 140));
            _valLosses        = AddStatCell(tblCombatGrid, 0, 1, "敗場",       Color.FromArgb(235, 115, 115));
            _valActiveThrows  = AddStatCell(tblCombatGrid, 0, 2, "主動扔屎",   Color.FromArgb(215, 175, 115));
            _valDefenseThrows = AddStatCell(tblCombatGrid, 0, 3, "防衛扔屎",   Color.FromArgb(215, 175, 115));
            _valAssaultSoiled = AddStatCell(tblCombatGrid, 0, 4, "遭襲沾屎",   Color.FromArgb(195, 140, 85));
            _valCounterSoiled = AddStatCell(tblCombatGrid, 0, 5, "遭反殺沾屎", Color.FromArgb(195, 140, 85));
            tblColumns.Controls.Add(grpCombat, 1, 0);

            tblRoot.Controls.Add(tblColumns, 0, 2);

            // ═══════════════════════════════════════════════════════════════════
            // Row 3: 底部關閉按鈕
            // ═══════════════════════════════════════════════════════════════════
            var pnlBottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, ScaleDpi(8), 0, 0)
            };

            var btnClose = new Button
            {
                Text = "關閉",
                DialogResult = DialogResult.Cancel,
                Size = new Size(ScaleDpi(96), ScaleDpi(34)),
                BackColor = Color.FromArgb(55, 58, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 10.5f)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (_, _) => this.Close();
            pnlBottom.Controls.Add(btnClose);

            tblRoot.Controls.Add(pnlBottom, 0, 3);
            this.Controls.Add(tblRoot);
            this.CancelButton = btnClose;
        }

        private Panel CreateSectionCard(string title, out TableLayoutPanel grid, int columns, int rows)
        {
            var card = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = Color.FromArgb(30, 32, 44),
                Padding = new Padding(ScaleDpi(12), ScaleDpi(8), ScaleDpi(12), ScaleDpi(10)),
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };

            var tblWrapper = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };
            tblWrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var lblTitle = new Label
            {
                Text = $"▎{title}",
                Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(145, 180, 255),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(6))
            };
            tblWrapper.Controls.Add(lblTitle, 0, 0);

            grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = columns,
                RowCount = rows,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                BackColor = Color.FromArgb(38, 40, 54),
                Margin = Padding.Empty
            };
            for (int c = 0; c < columns; c++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            }
            for (int r = 0; r < rows; r++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            tblWrapper.Controls.Add(grid, 0, 1);
            card.Controls.Add(tblWrapper);
            return card;
        }

        private Label AddStatCell(TableLayoutPanel grid, int col, int row, string labelText, Color valueColor)
        {
            var pnlCell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(26, 28, 38),
                Padding = new Padding(ScaleDpi(8), ScaleDpi(5), ScaleDpi(8), ScaleDpi(5)),
                Margin = Padding.Empty
            };
            pnlCell.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlCell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var lblKey = new Label
            {
                Text = labelText,
                ForeColor = Color.FromArgb(175, 182, 202),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };

            var lblVal = new Label
            {
                Text = "—",
                ForeColor = valueColor,
                Font = new Font("Consolas", 11f, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Right
            };

            pnlCell.Controls.Add(lblKey, 0, 0);
            pnlCell.Controls.Add(lblVal, 1, 0);
            grid.Controls.Add(pnlCell, col, row);
            return lblVal;
        }

        private void CopyPlayerIdToClipboard()
        {
            if (string.IsNullOrWhiteSpace(_currentPlayerId)) return;
            try
            {
                Clipboard.SetText(_currentPlayerId);
                _lblCopyFeedback.Visible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"複製失敗：\n{ex.Message}", "複製錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task LoadDetailAsync()
        {
            try
            {
                var profileTask = _battleService.GetPlayerProfileAsync(_playerId, _token, _cts.Token);

                // 若已有列表帶入的頭像路徑，可同步開始抓頭像
                string? initialImgPath = _summaryHint?.ImagePath;
                Task<Bitmap?>? avatarTask = !string.IsNullOrWhiteSpace(initialImgPath)
                    ? _battleService.FetchAvatarBitmapAsync(initialImgPath, 0, _cts.Token)
                    : null;

                var profileRes = await profileTask;
                if (_cts.IsCancellationRequested || this.IsDisposed) return;

                if (!profileRes.IsSuccess || profileRes.Data == null)
                {
                    _lblLoadingStatus.Text = $"❌ 無法取得詳細資料：{profileRes.ErrorMessage ?? "未知錯誤"}";
                    _lblLoadingStatus.ForeColor = Color.FromArgb(235, 95, 95);
                    _lblLoadingStatus.Visible = true;
                    return;
                }

                var data = profileRes.Data;
                var ch = data.Character;

                this.Text = $"玩家詳細資訊 - {data.Nickname}";
                _currentPlayerId = !string.IsNullOrWhiteSpace(data.Id) ? data.Id : _playerId;
                _lblPlayerIdValue.Text = _currentPlayerId;

                bool isDead = ch?.IsDead ?? _summaryHint?.IsDead ?? false;
                bool isSoiled = ch?.IsSoiled ?? _summaryHint?.IsSoiled ?? false;

                _lblNickname.Text = data.Nickname;
                _lblNickname.ForeColor = GetNicknameColor(isDead, isSoiled);
                _lblDeadTag.Visible = isDead;

                if (ch != null)
                {
                    _lblCharacterTitle.Text = FormatCharacterTitle(ch.Name, ch.Title);
                    _lblLevelFloor.Text = FormatLevelAndFloor(
                        ch.Level,
                        _summaryHint?.Floor ?? 0,
                        _summaryHint?.HighestFloor ?? 0);

                    if (ch.Stats != null)
                    {
                        _valHp.Text  = ch.Stats.HP.ToString("N0");
                        _valAtk.Text = ch.Stats.Atk.ToString("N0");
                        _valDef.Text = ch.Stats.Def.ToString("N0");
                        _valSta.Text = ch.Stats.Sta.ToString("N0");
                        _valAgi.Text = ch.Stats.Agi.ToString("N0");
                        _valSpd.Text = ch.Stats.Spd.ToString("N0");
                        _valTec.Text = ch.Stats.Tec.ToString("N0");
                        _valInt.Text = ch.Stats.Int.ToString("N0");
                        _valLuk.Text = ch.Stats.Luk.ToString("N0");

                        ApplyStatValueHighlightColors(ch.Stats);
                    }

                    if (ch.CombatStats != null)
                    {
                        _valWins.Text          = ch.CombatStats.Wins.ToString("N0");
                        _valLosses.Text        = ch.CombatStats.Losses.ToString("N0");
                        _valActiveThrows.Text  = ch.CombatStats.ActiveThrows.ToString("N0");
                        _valDefenseThrows.Text = ch.CombatStats.DefenseThrows.ToString("N0");
                        _valAssaultSoiled.Text = ch.CombatStats.AssaultSoiled.ToString("N0");
                        _valCounterSoiled.Text = ch.CombatStats.CounterSoiled.ToString("N0");
                    }

                    // 若先前未抓頭像，或詳細 API 回傳了新的 imagePath，則抓取頭像
                    if (avatarTask == null && !string.IsNullOrWhiteSpace(ch.ImagePath))
                    {
                        avatarTask = _battleService.FetchAvatarBitmapAsync(ch.ImagePath, 0, _cts.Token);
                    }
                }

                // 載入完成後隱藏提示文字
                _lblLoadingStatus.Visible = false;

                if (avatarTask != null)
                {
                    var bmp = await avatarTask;
                    if (!_cts.IsCancellationRequested && !this.IsDisposed && bmp != null)
                    {
                        _picAvatar.Image = bmp;
                    }
                }
            }
            catch (Exception ex) when (!_cts.IsCancellationRequested)
            {
                if (!this.IsDisposed)
                {
                    _lblLoadingStatus.Text = $"❌ 載入發生例外：{ex.Message}";
                    _lblLoadingStatus.ForeColor = Color.FromArgb(235, 95, 95);
                    _lblLoadingStatus.Visible = true;
                }
            }
        }

        /// <summary>
        /// 角色數值（除 HP 外）最高的一項以金黃色標示，次高（比最高低一階）的一項以亮青藍色標示，其餘維持統一淺白色。
        /// </summary>
        private void ApplyStatValueHighlightColors(CharacterStats stats)
        {
            _valHp.ForeColor = UniformStatValueColor;

            var nonHpStats = new (Label Label, int Value)[]
            {
                (_valAtk, stats.Atk),
                (_valDef, stats.Def),
                (_valSta, stats.Sta),
                (_valAgi, stats.Agi),
                (_valSpd, stats.Spd),
                (_valTec, stats.Tec),
                (_valInt, stats.Int),
                (_valLuk, stats.Luk)
            };

            var distinctRanks = nonHpStats
                .Select(s => s.Value)
                .Where(v => v > 0)
                .Distinct()
                .OrderByDescending(v => v)
                .ToList();

            int? highestVal = distinctRanks.Count > 0 ? distinctRanks[0] : null;
            int? secondVal  = distinctRanks.Count > 1 ? distinctRanks[1] : null;

            foreach (var item in nonHpStats)
            {
                if (highestVal.HasValue && item.Value == highestVal.Value)
                {
                    item.Label.ForeColor = HighestStatValueColor;
                }
                else if (secondVal.HasValue && item.Value == secondVal.Value)
                {
                    item.Label.ForeColor = SecondHighestStatValueColor;
                }
                else
                {
                    item.Label.ForeColor = UniformStatValueColor;
                }
            }
        }

        private static string FormatCharacterTitle(string? characterName, string? characterTitle)
        {
            string name = characterName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(characterTitle))
            {
                return $"{name}（{characterTitle}）";
            }
            return !string.IsNullOrWhiteSpace(name) ? name : "—";
        }

        private static string FormatLevelAndFloor(int level, int floor, int highestFloor)
        {
            string levelPart = level > 0 ? $"Lv.{level}" : "Lv.—";
            string floorPart = (floor > 0 || highestFloor > 0)
                ? $"{StatHelper.ConvertFloor(floor)}F / {StatHelper.ConvertFloor(highestFloor)}F"
                : "—F / —F";
            return $"等級：{levelPart}｜樓層：{floorPart}";
        }

        private static Color GetNicknameColor(bool isDead, bool isSoiled)
        {
            if (isDead) return DeadNameColor;
            if (isSoiled) return SoiledNameColor;
            return NormalNameColor;
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

