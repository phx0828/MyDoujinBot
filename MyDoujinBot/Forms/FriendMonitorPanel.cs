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
        private Label _lblStatus = null!;
        private DoubleBufferedDataGridView _dgv = null!;

        private bool _isShowingSearchResults = false;
        private string _lastSearchKeyword = string.Empty;
        private CancellationTokenSource? _queryCts;
        private int _hoveredRowIndex = -1;

        private readonly List<PlayerSummaryItem> _currentItems = new();
        private Bitmap? _placeholderBitmap;

        private float DpiScale => (float)this.DeviceDpi / 96f;
        private int ScaleDpi(int value) => (int)Math.Round(value * DpiScale);

        public FriendMonitorPanel(BattleService battleService, Func<string> tokenProvider)
        {
            _battleService = battleService;
            _tokenProvider = tokenProvider;

            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(24, 24, 30);
            this.Padding = new Padding(ScaleDpi(64), ScaleDpi(14), ScaleDpi(64), ScaleDpi(12));

            BuildUI();
            this.Resize += (_, _) => UpdateResponsivePadding();
        }

        private void UpdateResponsivePadding()
        {
            if (this.IsDisposed || this.ClientSize.Width <= 0) return;

            int maxContentWidth = ScaleDpi(760);
            int minSidePadding = ScaleDpi(48);
            int extra = this.ClientSize.Width - maxContentWidth;
            int sidePadding = extra > minSidePadding * 2 ? extra / 2 : minSidePadding;

            var targetPadding = new Padding(sidePadding, ScaleDpi(14), sidePadding, ScaleDpi(12));
            if (this.Padding != targetPadding)
            {
                this.Padding = targetPadding;
            }
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
            UpdateModeState();

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
                RowCount = 3,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tblMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Row 0: 搜尋與操作工具列
            tblMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // Row 1: 載入狀態提示列
            tblMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // Row 2: 條列表格

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
                Margin = new Padding(0, 0, 0, ScaleDpi(6))
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
                PlaceholderText = "輸入玩家暱稱搜尋",
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
            // Row 1: 載入狀態提示列
            // ═══════════════════════════════════════════════════════════════════
            _lblStatus = new Label
            {
                Text = string.Empty,
                ForeColor = Color.FromArgb(145, 205, 165),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Visible = false,
                Margin = new Padding(ScaleDpi(2), 0, 0, ScaleDpi(6))
            };
            tblMain.Controls.Add(_lblStatus, 0, 1);

            // ═══════════════════════════════════════════════════════════════════
            // Row 2: 條列表格 (DataGridView)
            // ═══════════════════════════════════════════════════════════════════
            _dgv = new DoubleBufferedDataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(20, 20, 26),
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = Color.FromArgb(42, 44, 58),
                CellBorderStyle = DataGridViewCellBorderStyle.Single,
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
                RowTemplate = { Height = ScaleDpi(66) },
                Margin = new Padding(0, 0, 0, ScaleDpi(6))
            };

            _dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _dgv.ColumnHeadersHeight = ScaleDpi(40);
            _dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(32, 35, 48),
                ForeColor = Color.FromArgb(185, 195, 220),
                SelectionBackColor = Color.FromArgb(32, 35, 48),
                SelectionForeColor = Color.FromArgb(185, 195, 220),
                Font = new Font("Microsoft JhengHei UI", 10.8f, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Padding = new Padding(ScaleDpi(4), 0, ScaleDpi(4), 0)
            };

            _dgv.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(24, 25, 34),
                ForeColor = NormalNameColor,
                SelectionBackColor = Color.FromArgb(38, 46, 68),
                SelectionForeColor = NormalNameColor,
                Font = new Font("Microsoft JhengHei UI", 10.2f),
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Padding = new Padding(ScaleDpi(4), 0, ScaleDpi(4), 0)
            };

            // 欄位 0: 頭像
            var colAvatar = new DataGridViewImageColumn
            {
                Name = "colAvatar",
                HeaderText = "頭像",
                ImageLayout = DataGridViewImageCellLayout.Zoom,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = ScaleDpi(66),
                Resizable = DataGridViewTriState.False,
                SortMode = DataGridViewColumnSortMode.NotSortable
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
                Width = ScaleDpi(76),
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colLevel.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colLevel.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colLevel.DefaultCellStyle.Font = new Font("Consolas", 11f, FontStyle.Bold);
            colLevel.DefaultCellStyle.ForeColor = Color.FromArgb(245, 210, 110);
            colLevel.DefaultCellStyle.SelectionForeColor = Color.FromArgb(245, 210, 110);

            // 欄位 2: 暱稱（下方合併顯示個人狀態）
            var colNickname = new DataGridViewTextBoxColumn
            {
                Name = "colNickname",
                HeaderText = "暱稱",
                FillWeight = 42f,
                MinimumWidth = ScaleDpi(110),
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colNickname.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colNickname.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colNickname.DefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 11f, FontStyle.Bold);

            // 欄位 3: 角色（下方合併顯示個人狀態）
            var colCharacter = new DataGridViewTextBoxColumn
            {
                Name = "colCharacter",
                HeaderText = "角色",
                FillWeight = 58f,
                MinimumWidth = ScaleDpi(140),
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colCharacter.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colCharacter.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colCharacter.DefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 10.8f);
            colCharacter.DefaultCellStyle.ForeColor = Color.FromArgb(165, 205, 250);
            colCharacter.DefaultCellStyle.SelectionForeColor = Color.FromArgb(165, 205, 250);

            // 欄位 4: 層數
            var colFloor = new DataGridViewTextBoxColumn
            {
                Name = "colFloor",
                HeaderText = "層數",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = ScaleDpi(112),
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colFloor.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colFloor.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colFloor.DefaultCellStyle.Font = new Font("Consolas", 11f, FontStyle.Bold);
            colFloor.DefaultCellStyle.ForeColor = Color.FromArgb(145, 225, 175);
            colFloor.DefaultCellStyle.SelectionForeColor = Color.FromArgb(145, 225, 175);

            // 欄位 5: 操作按鈕 (關注 / 移除)
            var colAction = new DataGridViewTextBoxColumn
            {
                Name = "colAction",
                HeaderText = "操作",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = ScaleDpi(92),
                Resizable = DataGridViewTriState.False,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colAction.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colAction.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            _dgv.Columns.AddRange(colAvatar, colLevel, colNickname, colCharacter, colFloor, colAction);

            _dgv.CellClick += OnDgvCellClick;
            _dgv.CellPainting += OnDgvCellPainting;
            _dgv.CellMouseEnter += OnDgvCellMouseEnter;
            _dgv.CellMouseLeave += OnDgvCellMouseLeave;
            _dgv.ColumnWidthChanged += (_, _) => _dgv.Invalidate();

            tblMain.Controls.Add(_dgv, 0, 2);
            this.Controls.Add(tblMain);

            UpdateModeState();
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

        private void UpdateModeState()
        {
            _btnBackToFollowed.Visible = _isShowingSearchResults;
        }

        /// <summary>
        /// 重新整理所有已關注群友的狀態（逐筆呼叫 GET /api/players?page=1&amp;limit=10&amp;sort=level&amp;order=desc&amp;name=...）。
        /// </summary>
        public async Task RefreshFollowedPlayersAsync()
        {
            string token = _tokenProvider();
            _isShowingSearchResults = false;
            UpdateModeState();

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
                SetStatusText(string.Empty);
                return;
            }

            _queryCts?.Cancel();
            _queryCts?.Dispose();
            var cts = new CancellationTokenSource();
            _queryCts = cts;

            ClearGridAndBitmaps();
            SetStatusText($"⏳ 正在載入玩家資訊 (0 / {followedList.Count})...", isWarning: true);

            bool settingsModified = false;

            try
            {
                for (int i = 0; i < followedList.Count; i++)
                {
                    if (cts.IsCancellationRequested || this.IsDisposed) return;

                    var entry = followedList[i];
                    SetStatusText($"⏳ 正在載入「{entry.Nickname}」({i + 1} / {followedList.Count})...", isWarning: true);

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

                    int rowIdx = AppendPlayerRow(matched, out bool avatarCached);
                    if (!avatarCached && !string.IsNullOrWhiteSpace(matched.ImagePath))
                    {
                        _ = LoadRowAvatarAsync(rowIdx, matched.ImagePath, cts.Token);
                    }
                }

                if (settingsModified)
                {
                    AppSettingsManager.Save();
                }

                UpdateModeState();
                SetStatusText($"✔ 玩家資訊載入完成（{DateTime.Now:HH:mm:ss}）");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!cts.IsCancellationRequested && !this.IsDisposed)
                {
                    SetStatusText($"❌ 載入發生錯誤：{ex.Message}", isError: true);
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
            UpdateModeState();
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
                    int rowIdx = AppendPlayerRow(item, out bool avatarCached);
                    if (!avatarCached && !string.IsNullOrWhiteSpace(item.ImagePath))
                    {
                        _ = LoadRowAvatarAsync(rowIdx, item.ImagePath, cts.Token);
                    }
                }

                UpdateModeState();
                if (players.Count == 0)
                {
                    SetStatusText($"查無包含「{keyword}」的玩家。", isWarning: true);
                }
                else
                {
                    SetStatusText($"✔ 搜尋「{keyword}」完成（共 {players.Count} 筆）");
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

        private int AppendPlayerRow(PlayerSummaryItem item, out bool avatarCached)
        {
            _currentItems.Add(item);

            string levelText = item.Level > 0 ? $"Lv.{item.Level}" : "—";
            string charText = !string.IsNullOrWhiteSpace(item.CharacterTitle)
                ? $"{item.CharacterName}（{item.CharacterTitle}）"
                : item.CharacterName;
            string floorText = (item.Floor > 0 || item.HighestFloor > 0)
                ? $"{StatHelper.ConvertFloor(item.Floor)}F / {StatHelper.ConvertFloor(item.HighestFloor)}F"
                : "—";
            string actionText = IsPlayerFollowed(item) ? "移除" : "＋ 關注";

            Bitmap? cachedAvatar = _battleService.TryGetCachedAvatarBitmap(item.ImagePath, 0);
            avatarCached = cachedAvatar != null;

            int rowIdx = _dgv.Rows.Add(
                cachedAvatar ?? GetPlaceholderBitmap(),
                levelText,
                item.Nickname,
                charText,
                floorText,
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
            var bmp = await _battleService.FetchAvatarBitmapAsync(imagePath, 0, ct);
            if (ct.IsCancellationRequested || this.IsDisposed || bmp == null)
            {
                return;
            }

            if (rowIndex < 0 || rowIndex >= _dgv.Rows.Count)
            {
                return;
            }

            _dgv.Rows[rowIndex].Cells["colAvatar"].Value = bmp;
            _dgv.InvalidateCell(_dgv.Columns["colAvatar"]!.Index, rowIndex);
        }

        private async void OnAddFriendByPromptClicked(object? sender, EventArgs e)
        {
            string? inputName = PromptDialog.Show(
                this.FindForm(),
                "新增關注群友",
                "請輸入玩家暱稱：",
                defaultValue: string.Empty,
                placeholder: "例如：大木");

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
                    }
                    else
                    {
                        _dgv.Rows.RemoveAt(e.RowIndex);
                        _currentItems.RemoveAt(e.RowIndex);
                        UpdateModeState();
                    }
                }
                else
                {
                    AddOrUpdateFollowedPlayer(item);
                    _dgv.Rows[e.RowIndex].Cells[actionColIndex].Value = "移除";
                    _dgv.InvalidateCell(actionColIndex, e.RowIndex);
                }
                return;
            }

            // 點擊其餘任意顯示欄位（頭像、暱稱、等級、角色、個人狀態、層數）→ 進入詳細確認數值視窗
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

            var ownerForm = this.FindForm();
            int openDetailCount = Application.OpenForms.OfType<PlayerDetailForm>().Count();

            var detailForm = new PlayerDetailForm(_battleService, item.Id, token, item);
            if (ownerForm != null && openDetailCount > 0)
            {
                detailForm.StartPosition = FormStartPosition.Manual;
                int offset = (openDetailCount % 6) * ScaleDpi(28);
                int baseX = ownerForm.Left + (ownerForm.Width - detailForm.Width) / 2;
                int baseY = ownerForm.Top + (ownerForm.Height - detailForm.Height) / 2;
                detailForm.Location = new Point(Math.Max(0, baseX + offset), Math.Max(0, baseY + offset));
            }
            detailForm.Show(ownerForm);
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
            if (e.Graphics == null || e.ColumnIndex < 0) return;

            // 標題列：確保所有欄位標題完全水平與垂直置中（不受排序圖示預留空間影響）
            if (e.RowIndex == -1)
            {
                using (var headerBrush = new SolidBrush(Color.FromArgb(32, 35, 48)))
                {
                    e.Graphics.FillRectangle(headerBrush, e.CellBounds);
                }

                using (var gridPen = new Pen(_dgv.GridColor))
                {
                    e.Graphics.DrawLine(gridPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                    e.Graphics.DrawLine(gridPen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                }

                string headerText = e.FormattedValue?.ToString() ?? string.Empty;
                Font headerFont = _dgv.ColumnHeadersDefaultCellStyle.Font ?? _dgv.Font;
                TextRenderer.DrawText(
                    e.Graphics,
                    headerText,
                    headerFont,
                    e.CellBounds,
                    Color.FromArgb(185, 195, 220),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

                e.Handled = true;
                return;
            }

            if (e.RowIndex < 0 || e.RowIndex >= _currentItems.Count) return;

            bool isHovered = e.RowIndex == _hoveredRowIndex;
            bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
            Color rowBackColor = isHovered
                ? Color.FromArgb(36, 42, 60)
                : isSelected
                    ? Color.FromArgb(34, 40, 58)
                    : (e.RowIndex % 2 == 0 ? Color.FromArgb(24, 25, 34) : Color.FromArgb(28, 30, 40));

            var item = _currentItems[e.RowIndex];
            int avatarColIndex = _dgv.Columns["colAvatar"]!.Index;
            int nickColIndex = _dgv.Columns["colNickname"]!.Index;
            int charColIndex = _dgv.Columns["colCharacter"]!.Index;
            int actionColIndex = _dgv.Columns["colAction"]!.Index;

            // 頭像欄：保留原圖完整比例與高解析度，使用 HighQualityBicubic 等比例縮小置中繪製
            if (e.ColumnIndex == avatarColIndex)
            {
                using (var backBrush = new SolidBrush(rowBackColor))
                {
                    e.Graphics.FillRectangle(backBrush, e.CellBounds);
                }

                using (var gridPen = new Pen(_dgv.GridColor))
                {
                    e.Graphics.DrawLine(gridPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                    e.Graphics.DrawLine(gridPen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                }

                if (_dgv.Rows[e.RowIndex].Cells[avatarColIndex].Value is Bitmap bmp && bmp.Width > 0 && bmp.Height > 0)
                {
                    int pad = ScaleDpi(5);
                    int boxW = Math.Max(1, e.CellBounds.Width - 1 - pad * 2);
                    int boxH = Math.Max(1, e.CellBounds.Height - 1 - pad * 2);

                    float scale = Math.Min((float)boxW / bmp.Width, (float)boxH / bmp.Height);
                    int drawW = Math.Max(1, (int)Math.Round(bmp.Width * scale));
                    int drawH = Math.Max(1, (int)Math.Round(bmp.Height * scale));
                    int drawX = e.CellBounds.X + (e.CellBounds.Width - 1 - drawW) / 2;
                    int drawY = e.CellBounds.Y + (e.CellBounds.Height - 1 - drawH) / 2;

                    var state = e.Graphics.Save();
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
                    e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                    e.Graphics.DrawImage(bmp, new Rectangle(drawX, drawY, drawW, drawH));
                    e.Graphics.Restore(state);
                }

                e.Handled = true;
                return;
            }

            // 中段二欄（暱稱、角色）：分為上下兩列，上列顯示各自欄位，下列跨「暱稱 + 角色」兩欄合併靠左顯示「個人狀態」
            if (e.ColumnIndex == nickColIndex || e.ColumnIndex == charColIndex)
            {
                using (var backBrush = new SolidBrush(rowBackColor))
                {
                    e.Graphics.FillRectangle(backBrush, e.CellBounds);
                }

                int topHeight = e.CellBounds.Height / 2;
                int midY = e.CellBounds.Top + topHeight;

                string topText;
                Color topColor;
                Font topFont;

                if (e.ColumnIndex == nickColIndex)
                {
                    topText = item.Nickname;
                    topColor = item.IsDead
                        ? DeadNameColor
                        : item.IsSoiled
                            ? SoiledNameColor
                            : NormalNameColor;
                    topFont = _dgv.Columns[nickColIndex].DefaultCellStyle.Font ?? _dgv.Font;
                }
                else
                {
                    topText = !string.IsNullOrWhiteSpace(item.CharacterTitle)
                        ? $"{item.CharacterName}（{item.CharacterTitle}）"
                        : item.CharacterName;
                    topColor = Color.FromArgb(165, 205, 250);
                    topFont = _dgv.Columns[charColIndex].DefaultCellStyle.Font ?? _dgv.Font;
                }

                var topRect = new Rectangle(
                    e.CellBounds.X + ScaleDpi(4),
                    e.CellBounds.Y,
                    Math.Max(1, e.CellBounds.Width - ScaleDpi(8)),
                    topHeight);

                TextRenderer.DrawText(
                    e.Graphics,
                    topText,
                    topFont,
                    topRect,
                    topColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

                // 下半列：跨「暱稱 + 角色」兩欄靠左繪製個人狀態
                int nickW = _dgv.Columns[nickColIndex].Width;
                int charW = _dgv.Columns[charColIndex].Width;
                int spanLeft = e.ColumnIndex == nickColIndex
                    ? e.CellBounds.Left
                    : e.CellBounds.Left - nickW;
                int spanWidth = nickW + charW;

                var bottomSpanRect = new Rectangle(
                    spanLeft + ScaleDpi(10),
                    midY + 1,
                    Math.Max(1, spanWidth - ScaleDpi(20)),
                    Math.Max(1, e.CellBounds.Bottom - 1 - (midY + 1)));

                if (!string.IsNullOrWhiteSpace(item.PersonalStatus))
                {
                    var state = e.Graphics.Save();
                    e.Graphics.SetClip(e.CellBounds);
                    TextRenderer.DrawText(
                        e.Graphics,
                        item.PersonalStatus,
                        _dgv.DefaultCellStyle.Font ?? _dgv.Font,
                        bottomSpanRect,
                        Color.FromArgb(185, 190, 210),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping);
                    e.Graphics.Restore(state);
                }

                using (var gridPen = new Pen(_dgv.GridColor))
                {
                    // 上下兩列分隔線
                    e.Graphics.DrawLine(gridPen, e.CellBounds.Left, midY, e.CellBounds.Right, midY);
                    // 底部分隔線
                    e.Graphics.DrawLine(gridPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);

                    // 右側格線：暱稱僅畫上半列，讓下半列視覺上合併為單一跨欄列；角色畫全高以與層數分隔
                    if (e.ColumnIndex == charColIndex)
                    {
                        e.Graphics.DrawLine(gridPen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                    }
                    else
                    {
                        e.Graphics.DrawLine(gridPen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, midY);
                    }
                }

                e.Handled = true;
                return;
            }

            if (e.ColumnIndex == actionColIndex)
            {
                using (var backBrush = new SolidBrush(rowBackColor))
                {
                    e.Graphics.FillRectangle(backBrush, e.CellBounds);
                }

                using (var gridPen = new Pen(_dgv.GridColor))
                {
                    e.Graphics.DrawLine(gridPen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
                    e.Graphics.DrawLine(gridPen, e.CellBounds.Right - 1, e.CellBounds.Top, e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                }

                bool followed = IsPlayerFollowed(item);
                string btnText = followed ? "移除" : "＋ 關注";
                Color btnBack = followed ? Color.FromArgb(105, 45, 50) : Color.FromArgb(40, 125, 78);

                int btnW = ScaleDpi(74);
                int btnH = ScaleDpi(30);
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
                    new Font("Microsoft JhengHei UI", 9.8f, FontStyle.Bold),
                    btnRect,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                e.Handled = true;
                return;
            }

            // 一般欄位（等級、層數）套用 Hover 與奇偶列底色
            e.CellStyle!.BackColor = rowBackColor;
            e.CellStyle.SelectionBackColor = rowBackColor;
        }

        private void SetStatusText(string text, bool isWarning = false, bool isError = false)
        {
            if (this.IsDisposed || _lblStatus == null) return;
            _lblStatus.Text = text;
            _lblStatus.Visible = !string.IsNullOrWhiteSpace(text);
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

