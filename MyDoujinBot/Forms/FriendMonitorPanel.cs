using System;
using System.Collections.Generic;
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
    /// 【關注群友】分頁主面板。
    /// 提供關注名單管理（新增/移除/重新整理）、玩家暱稱搜尋、頭像即時載入、
    /// 死亡(紅字)/沾屎(褐字)狀態著色，以及點擊任意欄位開啟單一玩家詳細資訊視窗。
    /// </summary>
    public sealed class FriendMonitorPanel : Panel
    {
        private static readonly Color DeadNameColor   = Color.FromArgb(245, 85, 85);
        private static readonly Color SoiledNameColor = Color.FromArgb(195, 135, 75);
        private static readonly Color NormalNameColor = Color.FromArgb(230, 230, 242);

        private readonly BattleService _battleService;
        private readonly Func<string> _tokenProvider;

        private TextBox _txtSearch = null!;
        private IconButton _btnSearch = null!;
        private IconButton _btnBackToFollowed = null!;
        private IconButton _btnAddFriend = null!;
        private IconButton _btnRefresh = null!;
        private Label _lblModeTitle = null!;
        private Label _lblStatus = null!;
        private DoubleBufferedDataGridView _dgv = null!;

        private bool _isShowingSearchResults = false;
        private string _lastSearchKeyword = string.Empty;
        private CancellationTokenSource? _queryCts;
        private int _hoveredRowIndex = -1;

        private readonly List<PlayerSummaryItem> _currentItems = new();
        private readonly List<Bitmap> _activeRowBitmaps = new();
        private Bitmap? _placeholderBitmap;

        private float DpiScale => (float)this.DeviceDpi / 96f;
        private int ScaleDpi(int value) => (int)Math.Round(value * DpiScale);

        public FriendMonitorPanel(BattleService battleService, Func<string> tokenProvider)
        {
            _battleService = battleService;
            _tokenProvider = tokenProvider;

            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(24, 24, 30);
            this.Padding = new Padding(ScaleDpi(14), ScaleDpi(12), ScaleDpi(14), ScaleDpi(12));

            BuildUI();
        }

        /// <summary>
        /// 當使用者切換至【關注群友】分頁時由 MainForm 呼叫，自動發動查詢更新所有關注對象狀態。
        /// </summary>
        public void OnTabActivated()
        {
            _ = RefreshFollowedPlayersAsync();
        }

        /// <summary>
        /// 當備份還原後重新同步關注名單。
        /// </summary>
        public void ReloadFromSettings()
        {
            _queryCts?.Cancel();
            _isShowingSearchResults = false;
            _btnBackToFollowed.Visible = false;
            ClearGridAndBitmaps();
            UpdateModeTitleLabel();

            int count = AppSettingsManager.Current.FollowedPlayers?.Count ?? 0;
            SetStatusText($"✔ 已從備份還原關注名單（共 {count} 人），切換至此分頁或點擊「重新整理關注狀態」即可更新狀態。");

            if (this.Parent != null && this.Visible)
            {
                _ = RefreshFollowedPlayersAsync();
            }
        }

        private void BuildUI()
        {
            var tblMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tblMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Row 0: 搜尋與操作工具列
            tblMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Row 1: 當前檢視標題與狀態列
            tblMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // Row 2: 條列表格
            tblMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Row 3: 底部圖例與提示

            // ═══════════════════════════════════════════════════════════════════
            // Row 0: 頂部搜尋與管理列
            // ═══════════════════════════════════════════════════════════════════
            var pnlTopBar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 6,
                RowCount = 1,
                BackColor = Color.FromArgb(30, 32, 42),
                Padding = new Padding(ScaleDpi(10), ScaleDpi(8), ScaleDpi(10), ScaleDpi(8)),
                Margin = new Padding(0, 0, 0, ScaleDpi(8))
            };
            pnlTopBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // 0: 標籤
            pnlTopBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));  // 1: 搜尋輸入框
            pnlTopBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // 2: 查詢玩家按鈕
            pnlTopBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // 3: 返回關注清單按鈕
            pnlTopBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // 4: 新增關注按鈕
            pnlTopBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // 5: 重新整理按鈕

            var lblSearchTitle = new Label
            {
                Text = "查詢玩家：",
                ForeColor = Color.FromArgb(200, 205, 225),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, ScaleDpi(6), 0)
            };

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 42, 54),
                ForeColor = Color.FromArgb(230, 230, 240),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft JhengHei UI", 9.8f),
                PlaceholderText = "輸入玩家暱稱搜尋（查到後可點擊查看詳情或右側直接關注）...",
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(8), ScaleDpi(2))
            };
            _txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    _ = ExecuteSearchPlayersAsync(_txtSearch.Text.Trim());
                }
            };

            _btnSearch = CreateBarButton("查詢玩家", Color.FromArgb(48, 88, 150), IconChar.MagnifyingGlass);
            _btnSearch.Click += (_, _) => _ = ExecuteSearchPlayersAsync(_txtSearch.Text.Trim());

            _btnBackToFollowed = CreateBarButton("返回關注列表", Color.FromArgb(65, 70, 95), IconChar.ArrowLeft);
            _btnBackToFollowed.Visible = false;
            _btnBackToFollowed.Click += (_, _) => _ = RefreshFollowedPlayersAsync();

            _btnAddFriend = CreateBarButton("新增關注", Color.FromArgb(42, 115, 78), IconChar.UserPlus);
            _btnAddFriend.Click += OnAddFriendByPromptClicked;

            _btnRefresh = CreateBarButton("重新整理", Color.FromArgb(52, 75, 110), IconChar.Rotate);
            _btnRefresh.Click += (_, _) =>
            {
                if (_isShowingSearchResults && !string.IsNullOrWhiteSpace(_lastSearchKeyword))
                {
                    _ = ExecuteSearchPlayersAsync(_lastSearchKeyword);
                }
                else
                {
                    _ = RefreshFollowedPlayersAsync();
                }
            };

            pnlTopBar.Controls.Add(lblSearchTitle, 0, 0);
            pnlTopBar.Controls.Add(_txtSearch, 1, 0);
            pnlTopBar.Controls.Add(_btnSearch, 2, 0);
            pnlTopBar.Controls.Add(_btnBackToFollowed, 3, 0);
            pnlTopBar.Controls.Add(_btnAddFriend, 4, 0);
            pnlTopBar.Controls.Add(_btnRefresh, 5, 0);

            tblMain.Controls.Add(pnlTopBar, 0, 0);

            // ═══════════════════════════════════════════════════════════════════
            // Row 1: 當前模式標題與狀態提示列
            // ═══════════════════════════════════════════════════════════════════
            var pnlStatusRow = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, ScaleDpi(6))
            };
            pnlStatusRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlStatusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            _lblModeTitle = new Label
            {
                Text = "▎已關注群友 (0)",
                ForeColor = Color.FromArgb(135, 180, 255),
                Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, ScaleDpi(14), 0)
            };

            _lblStatus = new Label
            {
                Text = "點擊表格任一欄位可查看該玩家詳細數值與複製 ID。",
                ForeColor = Color.FromArgb(155, 165, 185),
                Font = new Font("Microsoft JhengHei UI", 9.2f),
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };

            pnlStatusRow.Controls.Add(_lblModeTitle, 0, 0);
            pnlStatusRow.Controls.Add(_lblStatus, 1, 0);
            tblMain.Controls.Add(pnlStatusRow, 0, 1);

            // ═══════════════════════════════════════════════════════════════════
            // Row 2: 條列表格 (DataGridView)
            // ═══════════════════════════════════════════════════════════════════
            _dgv = new DoubleBufferedDataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(20, 20, 26),
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(42, 44, 58),
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                EnableHeadersVisualStyles = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowTemplate = { Height = ScaleDpi(52) },
                Margin = new Padding(0, 0, 0, ScaleDpi(6))
            };

            _dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _dgv.ColumnHeadersHeight = ScaleDpi(36);
            _dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(32, 35, 48),
                ForeColor = Color.FromArgb(185, 195, 220),
                SelectionBackColor = Color.FromArgb(32, 35, 48),
                SelectionForeColor = Color.FromArgb(185, 195, 220),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(6), 0, ScaleDpi(6), 0)
            };

            _dgv.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(24, 25, 34),
                ForeColor = NormalNameColor,
                SelectionBackColor = Color.FromArgb(38, 46, 68),
                SelectionForeColor = NormalNameColor,
                Font = new Font("Microsoft JhengHei UI", 9.8f),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(6), 0, ScaleDpi(6), 0)
            };

            // 欄位 0: 頭像
            var colAvatar = new DataGridViewImageColumn
            {
                Name = "colAvatar",
                HeaderText = "頭像",
                ImageLayout = DataGridViewImageCellLayout.Zoom,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = ScaleDpi(62),
                Resizable = DataGridViewTriState.False
            };
            colAvatar.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colAvatar.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colAvatar.DefaultCellStyle.Padding = new Padding(ScaleDpi(4));

            // 欄位 1: 等級
            var colLevel = new DataGridViewTextBoxColumn
            {
                Name = "colLevel",
                HeaderText = "等級",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = ScaleDpi(74)
            };
            colLevel.DefaultCellStyle.Font = new Font("Consolas", 10f, FontStyle.Bold);
            colLevel.DefaultCellStyle.ForeColor = Color.FromArgb(245, 210, 110);
            colLevel.DefaultCellStyle.SelectionForeColor = Color.FromArgb(245, 210, 110);

            // 欄位 2: 暱稱
            var colNickname = new DataGridViewTextBoxColumn
            {
                Name = "colNickname",
                HeaderText = "暱稱",
                FillWeight = 22f,
                MinimumWidth = ScaleDpi(110)
            };
            colNickname.DefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold);

            // 欄位 3: 角色 (以及稱號)
            var colCharacter = new DataGridViewTextBoxColumn
            {
                Name = "colCharacter",
                HeaderText = "角色（稱號）",
                FillWeight = 28f,
                MinimumWidth = ScaleDpi(140)
            };
            colCharacter.DefaultCellStyle.ForeColor = Color.FromArgb(165, 205, 250);
            colCharacter.DefaultCellStyle.SelectionForeColor = Color.FromArgb(165, 205, 250);

            // 欄位 4: 層數 (現今/最大)
            var colFloor = new DataGridViewTextBoxColumn
            {
                Name = "colFloor",
                HeaderText = "層數 (現今/最大)",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = ScaleDpi(130)
            };
            colFloor.DefaultCellStyle.Font = new Font("Consolas", 10f);
            colFloor.DefaultCellStyle.ForeColor = Color.FromArgb(145, 225, 175);
            colFloor.DefaultCellStyle.SelectionForeColor = Color.FromArgb(145, 225, 175);

            // 欄位 5: 個人狀態
            var colPersonalStatus = new DataGridViewTextBoxColumn
            {
                Name = "colPersonalStatus",
                HeaderText = "個人狀態",
                FillWeight = 35f,
                MinimumWidth = ScaleDpi(150)
            };
            colPersonalStatus.DefaultCellStyle.ForeColor = Color.FromArgb(185, 190, 210);
            colPersonalStatus.DefaultCellStyle.SelectionForeColor = Color.FromArgb(215, 220, 235);

            // 欄位 6: 操作按鈕 (關注 / 移除)
            var colAction = new DataGridViewTextBoxColumn
            {
                Name = "colAction",
                HeaderText = "操作",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = ScaleDpi(96),
                Resizable = DataGridViewTriState.False
            };
            colAction.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colAction.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            _dgv.Columns.AddRange(colAvatar, colLevel, colNickname, colCharacter, colFloor, colPersonalStatus, colAction);

            _dgv.CellClick += OnDgvCellClick;
            _dgv.CellPainting += OnDgvCellPainting;
            _dgv.CellMouseEnter += OnDgvCellMouseEnter;
            _dgv.CellMouseLeave += OnDgvCellMouseLeave;

            tblMain.Controls.Add(_dgv, 0, 2);

            // ═══════════════════════════════════════════════════════════════════
            // Row 3: 底部顏色圖例與操作提示
            // ═══════════════════════════════════════════════════════════════════
            var pnlLegend = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };

            pnlLegend.Controls.Add(new Label
            {
                Text = "暱稱顏色圖例：",
                ForeColor = Color.FromArgb(145, 150, 170),
                Font = new Font("Microsoft JhengHei UI", 9f),
                AutoSize = true,
                Margin = new Padding(0, 0, ScaleDpi(4), 0)
            });
            pnlLegend.Controls.Add(new Label
            {
                Text = "● 紅色＝死亡",
                ForeColor = DeadNameColor,
                Font = new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, ScaleDpi(12), 0)
            });
            pnlLegend.Controls.Add(new Label
            {
                Text = "● 褐色＝沾屎",
                ForeColor = SoiledNameColor,
                Font = new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, ScaleDpi(12), 0)
            });
            pnlLegend.Controls.Add(new Label
            {
                Text = "｜ 點擊列表中任一欄位即可開啟該玩家的詳細屬性與戰績視窗。",
                ForeColor = Color.FromArgb(145, 150, 170),
                Font = new Font("Microsoft JhengHei UI", 9f),
                AutoSize = true
            });

            tblMain.Controls.Add(pnlLegend, 0, 3);
            this.Controls.Add(tblMain);

            UpdateModeTitleLabel();
        }

        private IconButton CreateBarButton(string text, Color backColor, IconChar icon)
        {
            var btn = new IconButton
            {
                Text = $" {text}",
                AutoSize = true,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.2f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                IconChar = icon,
                IconColor = Color.White,
                IconSize = ScaleDpi(16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(8), ScaleDpi(3), ScaleDpi(8), ScaleDpi(3)),
                Margin = new Padding(ScaleDpi(4), 0, 0, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private Bitmap GetPlaceholderBitmap()
        {
            if (_placeholderBitmap != null) return _placeholderBitmap;

            int size = ScaleDpi(42);
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(36, 38, 50));
            using var pen = new Pen(Color.FromArgb(65, 70, 90), 1f);
            g.DrawRectangle(pen, 0, 0, size - 1, size - 1);
            _placeholderBitmap = bmp;
            return _placeholderBitmap;
        }

        private void ClearGridAndBitmaps()
        {
            _dgv.Rows.Clear();
            _currentItems.Clear();
            foreach (var bmp in _activeRowBitmaps)
            {
                bmp.Dispose();
            }
            _activeRowBitmaps.Clear();
            _hoveredRowIndex = -1;
        }

        private bool IsPlayerFollowed(PlayerSummaryItem item)
        {
            var list = AppSettingsManager.Current.FollowedPlayers;
            if (list == null || list.Count == 0) return false;

            return list.Any(f =>
                (!string.IsNullOrWhiteSpace(f.Id) && !string.IsNullOrWhiteSpace(item.Id) && string.Equals(f.Id, item.Id, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(f.Nickname, item.Nickname, StringComparison.OrdinalIgnoreCase));
        }

        private void UpdateModeTitleLabel()
        {
            int followedCount = AppSettingsManager.Current.FollowedPlayers?.Count ?? 0;
            if (_isShowingSearchResults)
            {
                _lblModeTitle.Text = $"▎搜尋結果：「{_lastSearchKeyword}」（共 {_currentItems.Count} 筆）";
                _lblModeTitle.ForeColor = Color.FromArgb(245, 200, 105);
                _btnBackToFollowed.Visible = true;
            }
            else
            {
                _lblModeTitle.Text = $"▎已關注群友 ({followedCount})";
                _lblModeTitle.ForeColor = Color.FromArgb(135, 180, 255);
                _btnBackToFollowed.Visible = false;
            }
        }

        /// <summary>
        /// 重新整理所有已關注群友的狀態（逐筆呼叫 GET /api/players?page=1&amp;limit=10&amp;sort=level&amp;order=desc&amp;name=...）。
        /// </summary>
        public async Task RefreshFollowedPlayersAsync()
        {
            string token = _tokenProvider();
            _isShowingSearchResults = false;
            UpdateModeTitleLabel();

            if (string.IsNullOrWhiteSpace(token))
            {
                ClearGridAndBitmaps();
                SetStatusText("⚠ 尚未設定 Bearer Token，請先至「設定」分頁設定 Token。", isWarning: true);
                return;
            }

            var followedList = AppSettingsManager.Current.FollowedPlayers?.ToList() ?? new List<FollowedPlayerEntry>();
            if (followedList.Count == 0)
            {
                ClearGridAndBitmaps();
                SetStatusText("目前尚未關注任何群友。您可在上方輸入玩家暱稱搜尋並加入關注，或點擊「新增關注」。");
                return;
            }

            _queryCts?.Cancel();
            _queryCts?.Dispose();
            var cts = new CancellationTokenSource();
            _queryCts = cts;

            ClearGridAndBitmaps();
            SetStatusText($"⏳ 正在更新關注群友狀態 (0 / {followedList.Count})...", isWarning: true);

            int completed = 0;
            bool settingsModified = false;

            try
            {
                for (int i = 0; i < followedList.Count; i++)
                {
                    if (cts.IsCancellationRequested || this.IsDisposed) return;

                    var entry = followedList[i];
                    SetStatusText($"⏳ 正在查詢「{entry.Nickname}」({i + 1} / {followedList.Count})...", isWarning: true);

                    PlayerSummaryItem? matched = await FetchSingleFollowedPlayerAsync(entry, token, cts.Token);
                    if (cts.IsCancellationRequested || this.IsDisposed) return;

                    if (matched != null)
                    {
                        // 同步更新儲存的 ID 與最新暱稱
                        if (!string.IsNullOrWhiteSpace(matched.Id) && entry.Id != matched.Id)
                        {
                            entry.Id = matched.Id;
                            settingsModified = true;
                        }
                        if (!string.IsNullOrWhiteSpace(matched.Nickname) && entry.Nickname != matched.Nickname)
                        {
                            entry.Nickname = matched.Nickname;
                            settingsModified = true;
                        }
                    }
                    else
                    {
                        // 若查無該玩家，仍顯示一筆保留列讓使用者知道或移除
                        matched = new PlayerSummaryItem
                        {
                            Id = entry.Id,
                            Nickname = entry.Nickname,
                            CharacterName = "（查無資料或已改名）",
                            PersonalStatus = "無法自 /api/players 查詢到此玩家"
                        };
                    }

                    int rowIdx = AppendPlayerRow(matched);
                    if (!string.IsNullOrWhiteSpace(matched.ImagePath))
                    {
                        _ = LoadRowAvatarAsync(rowIdx, matched.ImagePath, cts.Token);
                    }

                    completed++;
                }

                if (settingsModified)
                {
                    AppSettingsManager.Save();
                }

                UpdateModeTitleLabel();
                SetStatusText($"✔ 已更新 {completed} 位關注群友狀態（{DateTime.Now:HH:mm:ss}）。點擊任一欄位可查看詳細數值。");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!cts.IsCancellationRequested && !this.IsDisposed)
                {
                    SetStatusText($"❌ 更新關注清單時發生錯誤：{ex.Message}", isError: true);
                }
            }
        }

        private async Task<PlayerSummaryItem?> FetchSingleFollowedPlayerAsync(
            FollowedPlayerEntry entry,
            string token,
            CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(entry.Nickname))
            {
                var res = await _battleService.SearchPlayersAsync(entry.Nickname, token, cancellationToken: ct);
                if (res.IsSuccess && res.Data?.Players != null && res.Data.Players.Count > 0)
                {
                    var players = res.Data.Players;
                    // 1. 若有存 ID，優先以 ID 精確比對
                    if (!string.IsNullOrWhiteSpace(entry.Id))
                    {
                        var byId = players.FirstOrDefault(p => string.Equals(p.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
                        if (byId != null) return byId;
                    }

                    // 2. 其次比對暱稱完全一致者
                    var byExactName = players.FirstOrDefault(p => string.Equals(p.Nickname, entry.Nickname, StringComparison.OrdinalIgnoreCase));
                    if (byExactName != null) return byExactName;

                    // 3. 若無存 ID 時退回第一筆
                    if (string.IsNullOrWhiteSpace(entry.Id))
                    {
                        return players[0];
                    }
                }
            }

            // 備援：若玩家改了暱稱但我們存有他的 ID，可先打 /api/players/{id} 查出新暱稱後再查一次列表
            if (!string.IsNullOrWhiteSpace(entry.Id))
            {
                var profileRes = await _battleService.GetPlayerProfileAsync(entry.Id, token, ct);
                if (profileRes.IsSuccess && profileRes.Data != null && !string.IsNullOrWhiteSpace(profileRes.Data.Nickname))
                {
                    string newNick = profileRes.Data.Nickname;
                    var retryRes = await _battleService.SearchPlayersAsync(newNick, token, cancellationToken: ct);
                    if (retryRes.IsSuccess && retryRes.Data?.Players != null)
                    {
                        var byId = retryRes.Data.Players.FirstOrDefault(p => string.Equals(p.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
                        if (byId != null) return byId;
                    }

                    var ch = profileRes.Data.Character;
                    return new PlayerSummaryItem
                    {
                        Id = profileRes.Data.Id,
                        Nickname = profileRes.Data.Nickname,
                        Level = ch?.Level ?? 0,
                        CharacterName = ch?.Name ?? string.Empty,
                        CharacterTitle = ch?.Title ?? string.Empty,
                        ImagePath = ch?.ImagePath ?? string.Empty,
                        IsDead = ch?.IsDead ?? false,
                        IsSoiled = ch?.IsSoiled ?? false
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// 依輸入的暱稱關鍵字呼叫 GET /api/players 並將結果顯示於表格。
        /// </summary>
        private async Task ExecuteSearchPlayersAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                await RefreshFollowedPlayersAsync();
                return;
            }

            string token = _tokenProvider();
            if (string.IsNullOrWhiteSpace(token))
            {
                SetStatusText("⚠ 尚未設定 Bearer Token，請先至「設定」分頁設定 Token。", isWarning: true);
                return;
            }

            _queryCts?.Cancel();
            _queryCts?.Dispose();
            var cts = new CancellationTokenSource();
            _queryCts = cts;

            _isShowingSearchResults = true;
            _lastSearchKeyword = keyword;
            ClearGridAndBitmaps();
            UpdateModeTitleLabel();
            SetStatusText($"⏳ 正在搜尋玩家「{keyword}」...", isWarning: true);

            try
            {
                var res = await _battleService.SearchPlayersAsync(keyword, token, cancellationToken: cts.Token);
                if (cts.IsCancellationRequested || this.IsDisposed) return;

                if (!res.IsSuccess || res.Data == null)
                {
                    SetStatusText($"❌ 查詢失敗：{res.ErrorMessage ?? "未知錯誤"}", isError: true);
                    return;
                }

                var players = res.Data.Players ?? new List<PlayerSummaryItem>();
                for (int i = 0; i < players.Count; i++)
                {
                    var item = players[i];
                    int rowIdx = AppendPlayerRow(item);
                    if (!string.IsNullOrWhiteSpace(item.ImagePath))
                    {
                        _ = LoadRowAvatarAsync(rowIdx, item.ImagePath, cts.Token);
                    }
                }

                UpdateModeTitleLabel();
                if (players.Count == 0)
                {
                    SetStatusText($"查無包含「{keyword}」的玩家。可點擊「返回關注列表」回到關注名單。", isWarning: true);
                }
                else
                {
                    SetStatusText($"✔ 搜尋「{keyword}」共找到 {players.Count} 筆結果。點擊任一欄查看詳細數值，點擊右側「＋ 關注」可加入關注清單。");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!cts.IsCancellationRequested && !this.IsDisposed)
                {
                    SetStatusText($"❌ 查詢發生例外：{ex.Message}", isError: true);
                }
            }
        }

        private int AppendPlayerRow(PlayerSummaryItem item)
        {
            _currentItems.Add(item);

            string levelText = item.Level > 0 ? $"Lv.{item.Level}" : "—";
            string charText = !string.IsNullOrWhiteSpace(item.CharacterTitle)
                ? $"{item.CharacterName}（{item.CharacterTitle}）"
                : item.CharacterName;
            string floorText = (item.Floor > 0 || item.HighestFloor > 0)
                ? $"{item.Floor} / {item.HighestFloor}"
                : "—";
            string actionText = IsPlayerFollowed(item) ? "移除" : "＋ 關注";

            int rowIdx = _dgv.Rows.Add(
                GetPlaceholderBitmap(),
                levelText,
                item.Nickname,
                charText,
                floorText,
                item.PersonalStatus,
                actionText);

            ApplyRowNicknameColor(rowIdx, item);
            return rowIdx;
        }

        private void ApplyRowNicknameColor(int rowIdx, PlayerSummaryItem item)
        {
            if (rowIdx < 0 || rowIdx >= _dgv.Rows.Count) return;

            Color nameColor = item.IsDead
                ? DeadNameColor
                : item.IsSoiled
                    ? SoiledNameColor
                    : NormalNameColor;

            var cell = _dgv.Rows[rowIdx].Cells["colNickname"];
            cell.Style.ForeColor = nameColor;
            cell.Style.SelectionForeColor = nameColor;
        }

        private async Task LoadRowAvatarAsync(int rowIndex, string imagePath, CancellationToken ct)
        {
            var bmp = await _battleService.FetchAvatarBitmapAsync(imagePath, ScaleDpi(44), ct);
            if (ct.IsCancellationRequested || this.IsDisposed || bmp == null)
            {
                bmp?.Dispose();
                return;
            }

            if (rowIndex < 0 || rowIndex >= _dgv.Rows.Count)
            {
                bmp.Dispose();
                return;
            }

            _activeRowBitmaps.Add(bmp);
            _dgv.Rows[rowIndex].Cells["colAvatar"].Value = bmp;
        }

        private async void OnAddFriendByPromptClicked(object? sender, EventArgs e)
        {
            string? inputName = PromptDialog.Show(
                this.FindForm(),
                "新增關注群友",
                "請輸入欲關注的玩家暱稱（系統將自動透過 /api/players 查詢並記錄該玩家）：",
                defaultValue: string.Empty,
                placeholder: "例如：老ㄐ訶德");

            if (string.IsNullOrWhiteSpace(inputName)) return;
            inputName = inputName.Trim();

            string token = _tokenProvider();
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(this.FindForm(), "請先至「設定」分頁設定 Bearer Token。", "未設定 Token", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetStatusText($"⏳ 正在查詢玩家「{inputName}」...", isWarning: true);

            var res = await _battleService.SearchPlayersAsync(inputName, token);
            if (!res.IsSuccess || res.Data?.Players == null || res.Data.Players.Count == 0)
            {
                SetStatusText($"❌ 找不到暱稱為「{inputName}」的玩家。", isError: true);
                MessageBox.Show(this.FindForm(), $"查無暱稱為「{inputName}」的玩家，請確認名稱是否正確。", "查無玩家", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 優先選擇暱稱完全相符者，若有多筆模糊相符則切到搜尋結果讓使用者直接點選關注
            var exactMatch = res.Data.Players.FirstOrDefault(p => string.Equals(p.Nickname, inputName, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                AddOrUpdateFollowedPlayer(exactMatch);
                await RefreshFollowedPlayersAsync();
            }
            else
            {
                _txtSearch.Text = inputName;
                await ExecuteSearchPlayersAsync(inputName);
                MessageBox.Show(
                    this.FindForm(),
                    $"未找到暱稱完全等於「{inputName}」的玩家，已為您列出 {res.Data.Players.Count} 筆相似結果，請直接點擊目標右側的「＋ 關注」按鈕。",
                    "請選擇目標玩家",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void AddOrUpdateFollowedPlayer(PlayerSummaryItem item)
        {
            var list = AppSettingsManager.Current.FollowedPlayers;
            var existing = list.FirstOrDefault(f =>
                (!string.IsNullOrWhiteSpace(f.Id) && !string.IsNullOrWhiteSpace(item.Id) && string.Equals(f.Id, item.Id, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(f.Nickname, item.Nickname, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                if (!string.IsNullOrWhiteSpace(item.Id)) existing.Id = item.Id;
                if (!string.IsNullOrWhiteSpace(item.Nickname)) existing.Nickname = item.Nickname;
            }
            else
            {
                list.Add(new FollowedPlayerEntry
                {
                    Id = item.Id,
                    Nickname = item.Nickname
                });
            }

            AppSettingsManager.Save();
        }

        private void RemoveFollowedPlayer(PlayerSummaryItem item)
        {
            var list = AppSettingsManager.Current.FollowedPlayers;
            list.RemoveAll(f =>
                (!string.IsNullOrWhiteSpace(f.Id) && !string.IsNullOrWhiteSpace(item.Id) && string.Equals(f.Id, item.Id, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(f.Nickname, item.Nickname, StringComparison.OrdinalIgnoreCase));
            AppSettingsManager.Save();
        }

        private void OnDgvCellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _currentItems.Count) return;

            var item = _currentItems[e.RowIndex];
            int actionColIndex = _dgv.Columns["colAction"]!.Index;

            // 點擊最右側「操作（關注 / 移除）」欄位
            if (e.ColumnIndex == actionColIndex)
            {
                if (IsPlayerFollowed(item))
                {
                    RemoveFollowedPlayer(item);
                    if (_isShowingSearchResults)
                    {
                        _dgv.Rows[e.RowIndex].Cells[actionColIndex].Value = "＋ 關注";
                        _dgv.InvalidateCell(actionColIndex, e.RowIndex);
                        SetStatusText($"已將「{item.Nickname}」自關注清單移除。");
                    }
                    else
                    {
                        _dgv.Rows.RemoveAt(e.RowIndex);
                        _currentItems.RemoveAt(e.RowIndex);
                        UpdateModeTitleLabel();
                        SetStatusText($"已將「{item.Nickname}」自關注清單移除。");
                    }
                }
                else
                {
                    AddOrUpdateFollowedPlayer(item);
                    _dgv.Rows[e.RowIndex].Cells[actionColIndex].Value = "移除";
                    _dgv.InvalidateCell(actionColIndex, e.RowIndex);
                    SetStatusText($"✔ 已將「{item.Nickname}」加入關注清單！");
                }
                return;
            }

            // 點擊其餘任意顯示欄位（頭像、等級、暱稱、角色、層數、個人狀態）→ 進入詳細確認數值視窗
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                MessageBox.Show(this.FindForm(), "此筆記錄缺少玩家 ID，無法查詢詳細數值。", "無法查詢", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string token = _tokenProvider();
            if (string.IsNullOrWhiteSpace(token))
            {
                MessageBox.Show(this.FindForm(), "請先至「設定」分頁設定 Bearer Token。", "未設定 Token", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var detailForm = new PlayerDetailForm(_battleService, item.Id, token, item);
            detailForm.ShowDialog(this.FindForm());
        }

        private void OnDgvCellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _dgv.Rows.Count)
            {
                _dgv.Cursor = Cursors.Hand;
                if (_hoveredRowIndex != e.RowIndex)
                {
                    int prev = _hoveredRowIndex;
                    _hoveredRowIndex = e.RowIndex;
                    if (prev >= 0 && prev < _dgv.Rows.Count) _dgv.InvalidateRow(prev);
                    _dgv.InvalidateRow(_hoveredRowIndex);
                }
            }
            else
            {
                _dgv.Cursor = Cursors.Default;
            }
        }

        private void OnDgvCellMouseLeave(object? sender, DataGridViewCellEventArgs e)
        {
            _dgv.Cursor = Cursors.Default;
            if (_hoveredRowIndex >= 0 && _hoveredRowIndex < _dgv.Rows.Count)
            {
                int prev = _hoveredRowIndex;
                _hoveredRowIndex = -1;
                _dgv.InvalidateRow(prev);
            }
        }

        private void OnDgvCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _currentItems.Count || e.Graphics == null) return;

            bool isHovered = e.RowIndex == _hoveredRowIndex;
            Color rowBackColor = isHovered
                ? Color.FromArgb(36, 42, 60)
                : (e.RowIndex % 2 == 0 ? Color.FromArgb(24, 25, 34) : Color.FromArgb(28, 30, 40));

            int actionColIndex = _dgv.Columns["colAction"]!.Index;
            if (e.ColumnIndex == actionColIndex)
            {
                using (var backBrush = new SolidBrush(rowBackColor))
                {
                    e.Graphics.FillRectangle(backBrush, e.CellBounds);
                }

                // 繪製底部分隔線
                using (var gridPen = new Pen(_dgv.GridColor))
                {
                    e.Graphics.DrawLine(gridPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                }

                var item = _currentItems[e.RowIndex];
                bool followed = IsPlayerFollowed(item);
                string btnText = followed ? "移除" : "＋ 關注";
                Color btnBack = followed ? Color.FromArgb(105, 45, 50) : Color.FromArgb(40, 125, 78);

                int btnW = ScaleDpi(74);
                int btnH = ScaleDpi(28);
                int btnX = e.CellBounds.X + (e.CellBounds.Width - btnW) / 2;
                int btnY = e.CellBounds.Y + (e.CellBounds.Height - btnH) / 2;
                var btnRect = new Rectangle(btnX, btnY, btnW, btnH);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var btnBrush = new SolidBrush(btnBack))
                {
                    e.Graphics.FillRectangle(btnBrush, btnRect);
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    btnText,
                    new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold),
                    btnRect,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                e.Handled = true;
                return;
            }

            // 一般欄位套用 Hover 與奇偶列底色
            e.CellStyle!.BackColor = rowBackColor;
            e.CellStyle.SelectionBackColor = isHovered ? Color.FromArgb(42, 50, 72) : Color.FromArgb(34, 40, 58);
        }

        private void SetStatusText(string text, bool isWarning = false, bool isError = false)
        {
            if (this.IsDisposed) return;
            _lblStatus.Text = text;
            _lblStatus.ForeColor = isError
                ? Color.FromArgb(235, 95, 95)
                : isWarning
                    ? Color.FromArgb(235, 185, 80)
                    : Color.FromArgb(145, 205, 165);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _queryCts?.Cancel();
                _queryCts?.Dispose();
                foreach (var bmp in _activeRowBitmaps)
                {
                    bmp.Dispose();
                }
                _activeRowBitmaps.Clear();
                _placeholderBitmap?.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class DoubleBufferedDataGridView : DataGridView
        {
            public DoubleBufferedDataGridView()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }
        }
    }
}

