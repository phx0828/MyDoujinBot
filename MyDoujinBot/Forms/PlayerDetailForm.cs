using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FontAwesome.Sharp;
using MyDoujinBot.Models;
using MyDoujinBot.Services;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 玩家詳細資訊確認視窗（呼叫 GET /api/players/{id} 並顯示頭像、ID 複製、角色稱號、九圍數值與戰績）。
    /// </summary>
    public sealed class PlayerDetailForm : Form
    {
        private static readonly Color DeadNameColor   = Color.FromArgb(245, 85, 85);
        private static readonly Color SoiledNameColor = Color.FromArgb(195, 135, 75);
        private static readonly Color NormalNameColor = Color.FromArgb(240, 240, 250);

        private readonly BattleService _battleService;
        private readonly string _playerId;
        private readonly string _token;
        private readonly PlayerSummaryItem? _summaryHint;
        private readonly CancellationTokenSource _cts = new();

        private PictureBox _picAvatar = null!;
        private Bitmap? _loadedAvatar;
        private Label _lblNickname = null!;
        private Label _lblStatusTags = null!;
        private Label _lblCharacterTitle = null!;
        private Label _lblLevelExp = null!;
        private Label _lblFloorStatus = null!;
        private Label _lblPlayerIdValue = null!;
        private IconButton _btnCopyId = null!;
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
            _token = token;
            _summaryHint = summaryHint;

            string initName = summaryHint?.Nickname ?? playerId;
            this.Text = $"玩家詳細資訊 - {initName}";
            this.Size = new Size(ScaleDpi(600), ScaleDpi(540));
            this.MinimumSize = new Size(ScaleDpi(540), ScaleDpi(480));
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(22, 22, 30);
            this.ForeColor = Color.FromArgb(225, 225, 238);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            BuildUI();

            this.Shown += async (_, _) => await LoadDetailAsync();
            this.FormClosing += (_, _) =>
            {
                _cts.Cancel();
                _cts.Dispose();
                _loadedAvatar?.Dispose();
            };
        }

        private void BuildUI()
        {
            var tblRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(ScaleDpi(16), ScaleDpi(14), ScaleDpi(16), ScaleDpi(14)),
                BackColor = Color.Transparent
            };
            tblRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Row 0: 頂部角色與玩家基本資料卡
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Row 1: 載入狀態提示
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Row 2: 九圍能力值區塊
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Row 3: 本輪戰績區塊
            tblRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // Row 4: 底部關閉按鈕

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
                Padding = new Padding(ScaleDpi(12)),
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };
            pnlHeaderCard.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlHeaderCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            _picAvatar = new PictureBox
            {
                Size = new Size(ScaleDpi(76), ScaleDpi(76)),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(40, 42, 56),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(14), 0)
            };
            pnlHeaderCard.Controls.Add(_picAvatar, 0, 0);

            var tblInfo = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };
            tblInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // 暱稱 + 狀態標記列
            var pnlNameRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, ScaleDpi(2))
            };

            _lblNickname = new Label
            {
                Text = _summaryHint?.Nickname ?? "載入中...",
                Font = new Font("Microsoft JhengHei UI", 13f, FontStyle.Bold),
                ForeColor = GetNicknameColor(_summaryHint?.IsDead ?? false, _summaryHint?.IsSoiled ?? false),
                AutoSize = true,
                Margin = new Padding(0, 0, ScaleDpi(8), 0)
            };

            _lblStatusTags = new Label
            {
                Text = FormatStatusBadgeText(_summaryHint?.IsDead ?? false, _summaryHint?.IsSoiled ?? false),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                ForeColor = (_summaryHint?.IsDead ?? false) ? DeadNameColor : SoiledNameColor,
                AutoSize = true,
                Margin = new Padding(0, ScaleDpi(4), 0, 0)
            };

            pnlNameRow.Controls.Add(_lblNickname);
            pnlNameRow.Controls.Add(_lblStatusTags);
            tblInfo.Controls.Add(pnlNameRow, 0, 0);

            // 角色與稱號
            _lblCharacterTitle = new Label
            {
                Text = _summaryHint != null
                    ? $"角色：{_summaryHint.CharacterName}（{_summaryHint.CharacterTitle}）"
                    : "角色：載入中...",
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 195, 255),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(2))
            };
            tblInfo.Controls.Add(_lblCharacterTitle, 0, 1);

            // 等級與經驗值
            _lblLevelExp = new Label
            {
                Text = _summaryHint != null ? $"等級：Lv.{_summaryHint.Level}" : "等級：—",
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                ForeColor = Color.FromArgb(205, 210, 225),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(2))
            };
            tblInfo.Controls.Add(_lblLevelExp, 0, 2);

            // 樓層與個人狀態（若有）
            _lblFloorStatus = new Label
            {
                Text = _summaryHint != null
                    ? $"層數：{_summaryHint.Floor} / {_summaryHint.HighestFloor} F" +
                      (!string.IsNullOrWhiteSpace(_summaryHint.PersonalStatus) ? $" ｜ 狀態：{_summaryHint.PersonalStatus}" : "")
                    : string.Empty,
                Font = new Font("Microsoft JhengHei UI", 9f),
                ForeColor = Color.FromArgb(165, 175, 195),
                AutoSize = true,
                Visible = _summaryHint != null,
                Margin = new Padding(0, 0, 0, ScaleDpi(4))
            };
            tblInfo.Controls.Add(_lblFloorStatus, 0, 3);

            // 玩家 ID 列（點擊直接複製）
            var pnlIdRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };

            var lblIdPrefix = new Label
            {
                Text = "玩家 ID：",
                Font = new Font("Microsoft JhengHei UI", 9f),
                ForeColor = Color.FromArgb(150, 160, 180),
                AutoSize = true,
                Margin = new Padding(0, ScaleDpi(4), 0, 0)
            };

            _lblPlayerIdValue = new Label
            {
                Text = _playerId,
                Font = new Font("Consolas", 9.5f, FontStyle.Underline),
                ForeColor = Color.FromArgb(120, 200, 255),
                AutoSize = true,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, ScaleDpi(4), ScaleDpi(8), 0)
            };
            _lblPlayerIdValue.Click += (_, _) => CopyPlayerIdToClipboard();

            _btnCopyId = new IconButton
            {
                Text = " 複製 ID",
                AutoSize = true,
                BackColor = Color.FromArgb(48, 72, 108),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 8.8f),
                Cursor = Cursors.Hand,
                IconChar = IconChar.Copy,
                IconColor = Color.White,
                IconSize = ScaleDpi(14),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(6), ScaleDpi(2), ScaleDpi(6), ScaleDpi(2)),
                Margin = new Padding(0, 0, ScaleDpi(6), 0)
            };
            _btnCopyId.FlatAppearance.BorderSize = 0;
            _btnCopyId.Click += (_, _) => CopyPlayerIdToClipboard();

            _lblCopyFeedback = new Label
            {
                Text = "✓ 已複製！",
                Font = new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(95, 225, 135),
                AutoSize = true,
                Visible = false,
                Margin = new Padding(0, ScaleDpi(4), 0, 0)
            };

            pnlIdRow.Controls.Add(lblIdPrefix);
            pnlIdRow.Controls.Add(_lblPlayerIdValue);
            pnlIdRow.Controls.Add(_btnCopyId);
            pnlIdRow.Controls.Add(_lblCopyFeedback);
            tblInfo.Controls.Add(pnlIdRow, 0, 4);

            pnlHeaderCard.Controls.Add(tblInfo, 1, 0);
            tblRoot.Controls.Add(pnlHeaderCard, 0, 0);

            // ═══════════════════════════════════════════════════════════════════
            // Row 1: 狀態提示
            // ═══════════════════════════════════════════════════════════════════
            _lblLoadingStatus = new Label
            {
                Text = "⏳ 正在讀取玩家詳細數值...",
                ForeColor = Color.FromArgb(235, 185, 80),
                Font = new Font("Microsoft JhengHei UI", 9.2f),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(8))
            };
            tblRoot.Controls.Add(_lblLoadingStatus, 0, 1);

            // ═══════════════════════════════════════════════════════════════════
            // Row 2: 九圍戰鬥屬性表格
            // ═══════════════════════════════════════════════════════════════════
            var grpStats = CreateSectionCard("九圍戰鬥屬性", out var tblStatsGrid, columns: 3, rows: 3);
            _valHp  = AddStatCell(tblStatsGrid, 0, 0, "HP", Color.FromArgb(110, 225, 145));
            _valAtk = AddStatCell(tblStatsGrid, 1, 0, "攻擊", Color.FromArgb(245, 130, 120));
            _valDef = AddStatCell(tblStatsGrid, 2, 0, "防禦", Color.FromArgb(130, 185, 245));
            _valSta = AddStatCell(tblStatsGrid, 0, 1, "體力", Color.FromArgb(235, 190, 105));
            _valAgi = AddStatCell(tblStatsGrid, 1, 1, "敏捷", Color.FromArgb(130, 220, 210));
            _valSpd = AddStatCell(tblStatsGrid, 2, 1, "反應速度", Color.FromArgb(165, 205, 250));
            _valTec = AddStatCell(tblStatsGrid, 0, 2, "技巧", Color.FromArgb(215, 165, 245));
            _valInt = AddStatCell(tblStatsGrid, 1, 2, "智力", Color.FromArgb(120, 200, 255));
            _valLuk = AddStatCell(tblStatsGrid, 2, 2, "幸運", Color.FromArgb(245, 215, 110));
            tblRoot.Controls.Add(grpStats, 0, 2);

            // ═══════════════════════════════════════════════════════════════════
            // Row 3: 本輪戰績統計表格
            // ═══════════════════════════════════════════════════════════════════
            var grpCombat = CreateSectionCard("本輪戰績統計", out var tblCombatGrid, columns: 3, rows: 2);
            _valWins          = AddStatCell(tblCombatGrid, 0, 0, "勝場", Color.FromArgb(100, 225, 140));
            _valLosses        = AddStatCell(tblCombatGrid, 1, 0, "敗場", Color.FromArgb(235, 115, 115));
            _valActiveThrows  = AddStatCell(tblCombatGrid, 2, 0, "主動扔屎", Color.FromArgb(215, 175, 115));
            _valDefenseThrows = AddStatCell(tblCombatGrid, 0, 1, "防衛扔屎", Color.FromArgb(215, 175, 115));
            _valAssaultSoiled = AddStatCell(tblCombatGrid, 1, 1, "遭襲沾屎", Color.FromArgb(195, 140, 85));
            _valCounterSoiled = AddStatCell(tblCombatGrid, 2, 1, "遭反殺沾屎", Color.FromArgb(195, 140, 85));
            tblRoot.Controls.Add(grpCombat, 0, 3);

            // ═══════════════════════════════════════════════════════════════════
            // Row 4: 底部關閉按鈕
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
                Size = new Size(ScaleDpi(90), ScaleDpi(32)),
                BackColor = Color.FromArgb(55, 58, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 9.5f)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            pnlBottom.Controls.Add(btnClose);

            tblRoot.Controls.Add(pnlBottom, 0, 4);
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
                Font = new Font("Microsoft JhengHei UI", 9.8f, FontStyle.Bold),
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
                Padding = new Padding(ScaleDpi(8), ScaleDpi(6), ScaleDpi(8), ScaleDpi(6)),
                Margin = Padding.Empty
            };
            pnlCell.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlCell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var lblKey = new Label
            {
                Text = labelText,
                ForeColor = Color.FromArgb(165, 172, 192),
                Font = new Font("Microsoft JhengHei UI", 9.2f),
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };

            var lblVal = new Label
            {
                Text = "—",
                ForeColor = valueColor,
                Font = new Font("Consolas", 10.5f, FontStyle.Bold),
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
            if (string.IsNullOrWhiteSpace(_lblPlayerIdValue.Text)) return;
            try
            {
                Clipboard.SetText(_lblPlayerIdValue.Text);
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
                    ? _battleService.FetchAvatarBitmapAsync(initialImgPath, ScaleDpi(76), _cts.Token)
                    : null;

                var profileRes = await profileTask;
                if (_cts.IsCancellationRequested || this.IsDisposed) return;

                if (!profileRes.IsSuccess || profileRes.Data == null)
                {
                    _lblLoadingStatus.Text = $"❌ 無法取得詳細資料：{profileRes.ErrorMessage ?? "未知錯誤"}";
                    _lblLoadingStatus.ForeColor = Color.FromArgb(235, 95, 95);
                    return;
                }

                var data = profileRes.Data;
                var ch = data.Character;

                this.Text = $"玩家詳細資訊 - {data.Nickname}";
                _lblPlayerIdValue.Text = !string.IsNullOrWhiteSpace(data.Id) ? data.Id : _playerId;

                bool isDead = ch?.IsDead ?? _summaryHint?.IsDead ?? false;
                bool isSoiled = ch?.IsSoiled ?? _summaryHint?.IsSoiled ?? false;

                _lblNickname.Text = data.Nickname;
                _lblNickname.ForeColor = GetNicknameColor(isDead, isSoiled);

                _lblStatusTags.Text = FormatStatusBadgeText(isDead, isSoiled);
                _lblStatusTags.ForeColor = isDead ? DeadNameColor : SoiledNameColor;

                if (ch != null)
                {
                    string titlePart = !string.IsNullOrWhiteSpace(ch.Title) ? $"（{ch.Title}）" : string.Empty;
                    _lblCharacterTitle.Text = $"角色：{ch.Name}{titlePart}";
                    _lblLevelExp.Text = $"等級：Lv.{ch.Level} ｜ 經驗值：{ch.Exp:N0}（總經驗：{ch.TotalExp:N0}）";

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
                        avatarTask = _battleService.FetchAvatarBitmapAsync(ch.ImagePath, ScaleDpi(76), _cts.Token);
                    }
                }

                _lblLoadingStatus.Text = "✔ 詳細資料載入完成（點擊玩家 ID 或「複製 ID」可直接複製）";
                _lblLoadingStatus.ForeColor = Color.FromArgb(110, 215, 150);

                if (avatarTask != null)
                {
                    var bmp = await avatarTask;
                    if (!_cts.IsCancellationRequested && !this.IsDisposed && bmp != null)
                    {
                        _loadedAvatar?.Dispose();
                        _loadedAvatar = bmp;
                        _picAvatar.Image = _loadedAvatar;
                    }
                }
            }
            catch (Exception ex) when (!_cts.IsCancellationRequested)
            {
                if (!this.IsDisposed)
                {
                    _lblLoadingStatus.Text = $"❌ 載入發生例外：{ex.Message}";
                    _lblLoadingStatus.ForeColor = Color.FromArgb(235, 95, 95);
                }
            }
        }

        private static Color GetNicknameColor(bool isDead, bool isSoiled)
        {
            if (isDead) return DeadNameColor;
            if (isSoiled) return SoiledNameColor;
            return NormalNameColor;
        }

        private static string FormatStatusBadgeText(bool isDead, bool isSoiled)
        {
            if (isDead && isSoiled) return "[死亡 / 沾屎]";
            if (isDead) return "[死亡]";
            if (isSoiled) return "[沾屎]";
            return string.Empty;
        }
    }
}

