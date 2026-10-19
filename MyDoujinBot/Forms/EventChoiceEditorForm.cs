using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FontAwesome.Sharp;
using MyDoujinBot.Models;
using MyDoujinBot.Services;
using MyDoujinBot.Utilities;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 事件指定選項獨立編輯視窗。
    /// 包含關鍵字/屬性篩選、角色分類清單、事件自繪清單、全部清空與儲存/取消功能。
    /// 編輯過程僅操作記憶體字典，點選儲存後方寫入方案檔案。
    /// </summary>
    public sealed class EventChoiceEditorForm : Form
    {
        private readonly EventArchiveService _archiveService;
        private readonly EventChoicePreset _preset;
        private readonly HashSet<string> _existingOtherNames;
        private readonly Dictionary<string, string> _workingChoices;
        private List<EventRegistryItem> _allRegistryEvents = new();
        private readonly List<IEventRowItem> _renderedRows = new();

        // 介面控制項
        private bool _isSaved = false;
        private TextBox _txtName = null!;
        private TextBox _txtSearch = null!;
        private ComboBox _cmbStatFilter = null!;
        private ComboBox _cmbStatusFilter = null!;
        private IconButton _btnClearAll = null!;

        private ListBox _lstCharacters = null!;
        private DoubleBufferedListBox _lstEvents = null!;
        private Label _lblSummary = null!;

        private IconButton _btnSave = null!;
        private IconButton _btnCancel = null!;

        private readonly System.Windows.Forms.Timer _searchDebounceTimer = new() { Interval = 300 };

        private float DpiScale => (float)this.DeviceDpi / 96f;
        private int ScaleDpi(int value) => (int)Math.Round(value * DpiScale);

        public EventChoiceEditorForm(
            EventArchiveService archiveService,
            EventChoicePreset preset,
            IEnumerable<string>? existingOtherNames = null)
        {
            _archiveService = archiveService;
            _preset = preset;
            _existingOtherNames = existingOtherNames != null
                ? new HashSet<string>(existingOtherNames, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _workingChoices = new Dictionary<string, string>(preset.Choices ?? new Dictionary<string, string>());

            this.Text = $"編輯事件指定選項 - {_preset.Name}";
            this.Size = new Size(ScaleDpi(980), ScaleDpi(740));
            this.MinimumSize = new Size(ScaleDpi(860), ScaleDpi(580));
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(20, 20, 26);
            this.ForeColor = Color.FromArgb(220, 220, 235);
            this.Font = new Font("Microsoft JhengHei UI", 10f);
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            LoadRegistryData();
            BuildUI();

            _searchDebounceTimer.Tick += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                RefreshRenderedRows(preserveScroll: false);
            };

            RefreshRenderedRows(preserveScroll: false);
        }

        private void LoadRegistryData()
        {
            _allRegistryEvents = _archiveService.LoadRegistryItems();
        }

        private void BuildUI()
        {
            var pnlMainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = new Padding(ScaleDpi(12), ScaleDpi(10), ScaleDpi(12), ScaleDpi(12))
            };
            pnlMainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pnlMainTable.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 0: 篩選列
            pnlMainTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Row 1: 角色與事件列表雙欄 (填滿剩餘高度)
            pnlMainTable.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 2: 底部儲存與取消按鈕列
            // ── Row 0: 方案名稱與搜尋篩選列 ──
            var pnlTopCard = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.FromArgb(26, 26, 34),
                Padding = new Padding(ScaleDpi(8), ScaleDpi(6), ScaleDpi(8), ScaleDpi(6)),
                Margin = new Padding(0, 0, 0, ScaleDpi(8))
            };
            pnlTopCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pnlTopCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pnlTopCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Line 1: 方案名稱
            var pnlLine1 = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            var lblPresetName = new Label
            {
                Text = "方案名稱：",
                ForeColor = Color.FromArgb(180, 180, 200),
                Font = new Font("Microsoft JhengHei UI", 9.8f),
                AutoSize = true,
                Margin = new Padding(0, ScaleDpi(6), ScaleDpi(2), 0)
            };

            _txtName = new TextBox
            {
                Text = _preset.Name,
                Width = ScaleDpi(150),
                BackColor = Color.FromArgb(36, 38, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Margin = new Padding(0, ScaleDpi(3), ScaleDpi(12), ScaleDpi(4))
            };

            pnlLine1.Controls.Add(lblPresetName);
            pnlLine1.Controls.Add(_txtName);
            pnlTopCard.Controls.Add(pnlLine1, 0, 0);

            // Line 2: 左側搜尋與篩選 (FlowLayoutPanel) + 右側重置按鈕 (靠右對齊)
            var pnlLine2 = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0, ScaleDpi(2), 0, 0),
                Padding = Padding.Empty
            };
            pnlLine2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pnlLine2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlLine2.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var pnlFilters = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            var lblSearch = new Label
            {
                Text = "搜尋：",
                ForeColor = Color.FromArgb(180, 180, 200),
                Font = new Font("Microsoft JhengHei UI", 9.8f),
                AutoSize = true,
                Margin = new Padding(0, ScaleDpi(6), ScaleDpi(2), 0)
            };

            _txtSearch = new TextBox
            {
                Width = ScaleDpi(175),
                BackColor = Color.FromArgb(36, 38, 48),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Margin = new Padding(0, ScaleDpi(3), ScaleDpi(12), ScaleDpi(4))
            };
            _txtSearch.TextChanged += (s, e) =>
            {
                _searchDebounceTimer.Stop();
                _searchDebounceTimer.Start();
            };

            var lblStat = new Label
            {
                Text = "屬性篩選：",
                ForeColor = Color.FromArgb(180, 180, 200),
                Font = new Font("Microsoft JhengHei UI", 9.8f),
                AutoSize = true,
                Margin = new Padding(0, ScaleDpi(6), ScaleDpi(2), 0)
            };

            _cmbStatFilter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ScaleDpi(115),
                BackColor = Color.FromArgb(36, 38, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Margin = new Padding(0, ScaleDpi(3), ScaleDpi(12), ScaleDpi(4))
            };
            _cmbStatFilter.Items.AddRange(new object[] {
                "全部", "HP", "攻擊", "防禦", "體力", "敏捷", "反應速度", "技巧", "智力", "幸運"
            });
            _cmbStatFilter.SelectedIndex = 0;
            _cmbStatFilter.SelectedIndexChanged += (s, e) => RefreshRenderedRows(preserveScroll: false);

            var lblStatus = new Label
            {
                Text = "指定狀態：",
                ForeColor = Color.FromArgb(180, 180, 200),
                Font = new Font("Microsoft JhengHei UI", 9.8f),
                AutoSize = true,
                Margin = new Padding(0, ScaleDpi(6), ScaleDpi(2), 0)
            };

            _cmbStatusFilter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ScaleDpi(105),
                BackColor = Color.FromArgb(36, 38, 48),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Margin = new Padding(0, ScaleDpi(3), ScaleDpi(12), ScaleDpi(4))
            };
            _cmbStatusFilter.Items.AddRange(new object[] {
                "全部", "已指定", "未指定"
            });
            _cmbStatusFilter.SelectedIndex = 0;
            _cmbStatusFilter.SelectedIndexChanged += (s, e) => RefreshRenderedRows(preserveScroll: false);

            pnlFilters.Controls.Add(lblSearch);
            pnlFilters.Controls.Add(_txtSearch);
            pnlFilters.Controls.Add(lblStat);
            pnlFilters.Controls.Add(_cmbStatFilter);
            pnlFilters.Controls.Add(lblStatus);
            pnlFilters.Controls.Add(_cmbStatusFilter);

            _btnClearAll = CreateCompactButton("重置所有選項", Color.FromArgb(50, 55, 68), IconChar.ArrowRotateLeft);
            _btnClearAll.Anchor = AnchorStyles.Right;
            _btnClearAll.Margin = new Padding(ScaleDpi(6), ScaleDpi(2), 0, ScaleDpi(4));
            _btnClearAll.Click += OnClearAllClicked;

            pnlLine2.Controls.Add(pnlFilters, 0, 0);
            pnlLine2.Controls.Add(_btnClearAll, 1, 0);
            pnlTopCard.Controls.Add(pnlLine2, 0, 1);

            pnlMainTable.Controls.Add(pnlTopCard, 0, 0);

            // ── Row 1: 雙欄 (左側角色清單 + 右側選項大框與摘要) ──
            var pnlListsSplit = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            pnlListsSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleDpi(200)));
            pnlListsSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _lstCharacters = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 25, 32),
                ForeColor = Color.FromArgb(215, 215, 230),
                Font = new Font("Microsoft JhengHei UI", 9.8f),
                BorderStyle = BorderStyle.FixedSingle,
                ItemHeight = ScaleDpi(26),
                Margin = new Padding(0, 0, ScaleDpi(6), 0)
            };
            _lstCharacters.SelectedIndexChanged += (s, e) => RefreshRenderedRows(preserveScroll: false);

            var pnlRightCol = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            pnlRightCol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pnlRightCol.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pnlRightCol.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _lblSummary = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(150, 160, 185),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                TextAlign = ContentAlignment.MiddleRight,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(4))
            };

            _lstEvents = new DoubleBufferedListBox
            {
                Dock = DockStyle.Fill,
                DrawMode = DrawMode.OwnerDrawVariable,
                BackColor = Color.FromArgb(22, 23, 30),
                ForeColor = Color.FromArgb(230, 230, 240),
                Font = new Font("Microsoft JhengHei UI", 9.8f),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = Padding.Empty
            };
            _lstEvents.MeasureItem += OnMeasureEventListItem;
            _lstEvents.DrawItem += OnDrawEventListItem;
            _lstEvents.MouseUp += OnEventListMouseUp;

            pnlRightCol.Controls.Add(_lblSummary, 0, 0);
            pnlRightCol.Controls.Add(_lstEvents, 0, 1);

            pnlListsSplit.Controls.Add(_lstCharacters, 0, 0);
            pnlListsSplit.Controls.Add(pnlRightCol, 1, 0);
            pnlMainTable.Controls.Add(pnlListsSplit, 0, 1);

            // ── Row 2: 底部按鈕列 (儲存/取消按鈕) ──
            var flpButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0, ScaleDpi(8), 0, 0)
            };

            _btnSave = new IconButton
            {
                Text = " 儲存設定",
                AutoSize = true,
                BackColor = Color.FromArgb(40, 110, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.8f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                IconChar = IconChar.Check,
                IconColor = Color.White,
                IconSize = ScaleDpi(17),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(16), ScaleDpi(5), ScaleDpi(16), ScaleDpi(5)),
                Margin = Padding.Empty
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += (s, e) =>
            {
                string newName = _txtName.Text.Trim();
                if (string.IsNullOrWhiteSpace(newName))
                {
                    MessageBox.Show(this, "方案名稱不得為空，請輸入方案名稱。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtName.Focus();
                    return;
                }

                if (_existingOtherNames.Contains(newName))
                {
                    MessageBox.Show(this, $"已存在名為【{newName}】的自選方案，請使用其他名稱。", "名稱重複", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtName.Focus();
                    return;
                }

                _preset.Name = newName;
                _preset.Choices = new Dictionary<string, string>(_workingChoices);
                _isSaved = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            _btnCancel = new IconButton
            {
                Text = " 取消",
                AutoSize = true,
                BackColor = Color.FromArgb(50, 55, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.8f),
                Cursor = Cursors.Hand,
                IconChar = IconChar.Xmark,
                IconColor = Color.White,
                IconSize = ScaleDpi(17),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(12), ScaleDpi(5), ScaleDpi(12), ScaleDpi(5)),
                Margin = new Padding(0, 0, ScaleDpi(8), 0)
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) =>
            {
                this.Close();
            };

            flpButtons.Controls.Add(_btnSave);
            flpButtons.Controls.Add(_btnCancel);
            pnlMainTable.Controls.Add(flpButtons, 0, 2);

            this.Controls.Add(pnlMainTable);

            PopulateCharacterList();
        }

        private void PopulateCharacterList()
        {
            _lstCharacters.Items.Clear();
            _lstCharacters.Items.Add($"全部 ({_allRegistryEvents.Count})");

            var groups = _allRegistryEvents
                .GroupBy(e => e.ChainName)
                .OrderBy(g => g.Key);

            foreach (var g in groups)
            {
                _lstCharacters.Items.Add($"{g.Key} ({g.Count()})");
            }

            if (_lstCharacters.Items.Count > 0)
            {
                _lstCharacters.SelectedIndex = 0;
            }
        }

        private void RefreshRenderedRows(bool preserveScroll)
        {
            int previousTopIndex = preserveScroll && _lstEvents.Items.Count > 0 ? _lstEvents.TopIndex : 0;

            _renderedRows.Clear();

            string selectedCharItem = _lstCharacters.SelectedItem?.ToString() ?? "全部";
            string selectedChainName = selectedCharItem.Split(' ')[0];

            string keyword = _txtSearch.Text.Trim();
            string? statFilterName = _cmbStatFilter.SelectedItem?.ToString();
            string? statFilterKey = statFilterName switch
            {
                "HP" => "hp",
                "攻擊" => "攻",
                "防禦" => "防",
                "體力" => "體",
                "敏捷" => "敏",
                "反應速度" => "反",
                "技巧" => "技",
                "智力" => "智",
                "幸運" => "幸",
                _ => null
            };

            string? statusFilter = _cmbStatusFilter?.SelectedItem?.ToString();

            int matchedEventsCount = 0;

            foreach (var ev in _allRegistryEvents)
            {
                if (selectedChainName != "全部" && ev.ChainName != selectedChainName)
                {
                    continue;
                }

                bool titleMatches = string.IsNullOrEmpty(keyword) || ev.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                bool optionMatches = string.IsNullOrEmpty(keyword) || ev.Options.Any(o => o.Label.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                if (!titleMatches && !optionMatches)
                {
                    continue;
                }

                if (statFilterKey != null)
                {
                    bool hasStat = ev.Options.Any(o => o.OnSuccess.TryGetValue(statFilterKey, out int val) && val > 0);
                    if (!hasStat) continue;
                }

                bool isAssigned = _workingChoices.TryGetValue(ev.EventId, out var optId) && !string.IsNullOrEmpty(optId);
                if (statusFilter == "已指定" && !isAssigned)
                {
                    continue;
                }
                if (statusFilter == "未指定" && isAssigned)
                {
                    continue;
                }

                matchedEventsCount++;

                string? selectedOptionId = isAssigned ? optId : null;
                string? selectedOptionLabel = null;
                if (isAssigned && optId != null)
                {
                    var matchOpt = ev.Options.FirstOrDefault(o => o.OptionId == optId);
                    selectedOptionLabel = matchOpt?.Label ?? optId;
                }

                _renderedRows.Add(new EventHeaderRowItem
                {
                    EventId = ev.EventId,
                    Title = ev.Title,
                    ChainName = ev.ChainName,
                    Order = ev.Order,
                    SelectedOptionId = selectedOptionId,
                    SelectedOptionLabel = selectedOptionLabel
                });

                foreach (var opt in ev.Options)
                {
                    bool isOptionSelected = (selectedOptionId == opt.OptionId);
                    bool isFaded = (selectedOptionId != null && !isOptionSelected);

                    _renderedRows.Add(new EventOptionRowItem
                    {
                        EventId = ev.EventId,
                        OptionId = opt.OptionId,
                        Label = opt.Label,
                        GuideKind = opt.GuideKind,
                        GuideWhy = opt.GuideWhy,
                        HasEncounter = opt.HasEncounter,
                        StatsSummary = opt.StatsSummary,
                        IsSelected = isOptionSelected,
                        IsFaded = isFaded
                    });
                }
            }

            _lstEvents.BeginUpdate();
            _lstEvents.Items.Clear();
            foreach (var r in _renderedRows)
            {
                _lstEvents.Items.Add(r);
            }

            if (preserveScroll && _lstEvents.Items.Count > 0)
            {
                _lstEvents.TopIndex = Math.Min(previousTopIndex, _lstEvents.Items.Count - 1);
            }
            _lstEvents.EndUpdate();

            UpdateSummaryLabel(matchedEventsCount);
        }

        private void UpdateSummaryLabel(int? matchedCount = null)
        {
            int totalConfigured = _workingChoices.Count;
            int matched = matchedCount ?? _renderedRows.OfType<EventHeaderRowItem>().Count();
            _lblSummary.Text = $"已設定 {totalConfigured} 個事件選項 ｜ 符合篩選 {matched} 個事件";
        }

        private void OnEventListMouseUp(object? sender, MouseEventArgs e)
        {
            int index = _lstEvents.IndexFromPoint(e.Location);
            if (index < 0 || index >= _lstEvents.Items.Count) return;

            var item = _lstEvents.Items[index];
            if (item is EventOptionRowItem optItem)
            {
                // 若當前已選中此選項，點選表示取消自選；否則表示設為該選項
                if (_workingChoices.TryGetValue(optItem.EventId, out var currentOptId) && currentOptId == optItem.OptionId)
                {
                    _workingChoices.Remove(optItem.EventId);
                }
                else
                {
                    _workingChoices[optItem.EventId] = optItem.OptionId;
                }

                // 局部更新渲染狀態，不呼叫 Items.Clear() 避免滾動位置重置
                string? newlySelectedOptionId = _workingChoices.TryGetValue(optItem.EventId, out var nId) ? nId : null;
                var ev = _allRegistryEvents.FirstOrDefault(x => x.EventId == optItem.EventId);

                foreach (var row in _renderedRows)
                {
                    if (row is EventHeaderRowItem header && header.EventId == optItem.EventId)
                    {
                        header.SelectedOptionId = newlySelectedOptionId;
                        if (newlySelectedOptionId != null)
                        {
                            var matchOpt = ev?.Options.FirstOrDefault(o => o.OptionId == newlySelectedOptionId);
                            header.SelectedOptionLabel = matchOpt?.Label ?? newlySelectedOptionId;
                        }
                        else
                        {
                            header.SelectedOptionLabel = null;
                        }
                    }
                    else if (row is EventOptionRowItem opt && opt.EventId == optItem.EventId)
                    {
                        opt.IsSelected = (opt.OptionId == newlySelectedOptionId);
                        opt.IsFaded = (newlySelectedOptionId != null && !opt.IsSelected);
                    }
                }

                UpdateSummaryLabel();
                _lstEvents.Invalidate();
            }
        }

        private void OnClearAllClicked(object? sender, EventArgs e)
        {
            if (_workingChoices.Count == 0)
            {
                MessageBox.Show(this, "目前方案尚未指定任何事件選項，無須清空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(this,
                $"確定要清空目前方案全部 {_workingChoices.Count} 個已指定選項嗎？\n按下後將全部重設為未指定狀態。",
                "清空確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes) return;

            _workingChoices.Clear();

            // 局部重設所有已渲染行之選取狀態
            foreach (var row in _renderedRows)
            {
                if (row is EventHeaderRowItem header)
                {
                    header.SelectedOptionId = null;
                    header.SelectedOptionLabel = null;
                }
                else if (row is EventOptionRowItem opt)
                {
                    opt.IsSelected = false;
                    opt.IsFaded = false;
                }
            }

            if (_cmbStatusFilter != null && _cmbStatusFilter.SelectedIndex == 1)
            {
                RefreshRenderedRows(preserveScroll: false);
            }
            else
            {
                UpdateSummaryLabel();
                _lstEvents.Invalidate();
            }
        }

        private void OnMeasureEventListItem(object? sender, MeasureItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _renderedRows.Count) return;
            e.ItemHeight = _renderedRows[e.Index].GetItemHeight(DpiScale);
        }

        private void OnDrawEventListItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _renderedRows.Count) return;
            e.DrawBackground();
            _renderedRows[e.Index].Draw(e.Graphics, e.Bounds, DpiScale);
        }

        private IconButton CreateCompactButton(string text, Color backColor, IconChar icon)
        {
            var btn = new IconButton
            {
                Text = $" {text}",
                AutoSize = true,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Cursor = Cursors.Hand,
                IconChar = icon,
                IconColor = Color.White,
                IconSize = ScaleDpi(16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(8), ScaleDpi(4), ScaleDpi(8), ScaleDpi(4)),
                Margin = new Padding(0, ScaleDpi(1), ScaleDpi(6), ScaleDpi(4))
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isSaved)
            {
                if (!ConfirmCloseWithoutSaving())
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnFormClosing(e);
        }

        private bool ConfirmCloseWithoutSaving()
        {
            using var dlg = new Form
            {
                Text = "提示",
                Size = new Size(ScaleDpi(330), ScaleDpi(165)),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                BackColor = Color.FromArgb(28, 28, 36),
                ForeColor = Color.FromArgb(220, 220, 235),
                Font = new Font("Microsoft JhengHei UI", 10f)
            };

            var pnlButtonRow = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = ScaleDpi(50),
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 0, ScaleDpi(10))
            };
            pnlButtonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pnlButtonRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlButtonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var flpCenter = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };

            var btnConfirmClose = new Button
            {
                Text = "關閉",
                DialogResult = DialogResult.OK,
                Size = new Size(ScaleDpi(85), ScaleDpi(32)),
                BackColor = Color.FromArgb(140, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.8f),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, ScaleDpi(12), 0)
            };
            btnConfirmClose.FlatAppearance.BorderSize = 0;

            var btnCancelClose = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Size = new Size(ScaleDpi(85), ScaleDpi(32)),
                BackColor = Color.FromArgb(50, 55, 68),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.8f),
                Cursor = Cursors.Hand,
                Margin = Padding.Empty
            };
            btnCancelClose.FlatAppearance.BorderSize = 0;

            flpCenter.Controls.Add(btnConfirmClose);
            flpCenter.Controls.Add(btnCancelClose);
            pnlButtonRow.Controls.Add(flpCenter, 1, 0);

            var lbl = new Label
            {
                Text = "尚未存檔，是否關閉？",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(230, 230, 240),
                Font = new Font("Microsoft JhengHei UI", 10f)
            };

            dlg.Controls.Add(lbl);
            dlg.Controls.Add(pnlButtonRow);
            lbl.BringToFront();

            dlg.AcceptButton = btnConfirmClose;
            dlg.CancelButton = btnCancelClose;

            return dlg.ShowDialog(this) == DialogResult.OK;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _searchDebounceTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 自繪輔助控制項與行資料介面
        // ─────────────────────────────────────────────────────────────────────

        public class DoubleBufferedListBox : ListBox
        {
            public DoubleBufferedListBox()
            {
                this.SetStyle(
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint,
                    true);
                this.DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                using var region = new Region(e.ClipRectangle);
                e.Graphics.Clip = region;
                e.Graphics.FillRectangle(new SolidBrush(this.BackColor), e.ClipRectangle);

                if (this.Items.Count > 0)
                {
                    for (int i = 0; i < this.Items.Count; i++)
                    {
                        var rect = this.GetItemRectangle(i);
                        if (e.ClipRectangle.IntersectsWith(rect))
                        {
                            var state = DrawItemState.Default;
                            if (this.SelectedIndex == i) state |= DrawItemState.Selected;
                            var args = new DrawItemEventArgs(
                                e.Graphics, this.Font, rect, i, state, this.ForeColor, this.BackColor);
                            OnDrawItem(args);
                        }
                    }
                }
                base.OnPaint(e);
            }
        }

        public interface IEventRowItem
        {
            int GetItemHeight(float dpiScale);
            void Draw(Graphics g, Rectangle bounds, float dpiScale);
        }

        public class EventHeaderRowItem : IEventRowItem
        {
            public string EventId { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string ChainName { get; set; } = string.Empty;
            public int Order { get; set; }
            public string? SelectedOptionId { get; set; }
            public string? SelectedOptionLabel { get; set; }

            public int GetItemHeight(float dpiScale) => (int)Math.Round(34 * dpiScale);

            public void Draw(Graphics g, Rectangle bounds, float dpiScale)
            {
                using var bgBrush = new SolidBrush(Color.FromArgb(32, 35, 46));
                g.FillRectangle(bgBrush, bounds);

                using var linePen = new Pen(Color.FromArgb(48, 52, 68));
                g.DrawLine(linePen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);

                int pad = (int)Math.Round(8 * dpiScale);
                int y = bounds.Y + (int)Math.Round(7 * dpiScale);

                // 角色徽章
                string chainTag = $"[{ChainName}]";
                using var chainFont = new Font("Microsoft JhengHei UI", 9.2f, FontStyle.Bold);
                Size chainSize = TextRenderer.MeasureText(g, chainTag, chainFont, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, chainTag, chainFont, new Point(bounds.Left + pad, y + 1), Color.FromArgb(135, 175, 235), TextFormatFlags.NoPadding);

                // 事件標題
                string titleText = $"#{Order} {Title}";
                using var titleFont = new Font("Microsoft JhengHei UI", 9.8f, FontStyle.Bold);
                int titleX = bounds.Left + pad + chainSize.Width + (int)Math.Round(6 * dpiScale);
                TextRenderer.DrawText(g, titleText, titleFont, new Point(titleX, y), Color.FromArgb(245, 240, 255), TextFormatFlags.NoPadding);

                // 右側設定狀態標註
                string statusText = SelectedOptionId != null
                    ? $"指定: {SelectedOptionLabel}"
                    : "未指定";
                Color statusColor = SelectedOptionId != null
                    ? Color.FromArgb(120, 220, 160)
                    : Color.FromArgb(130, 130, 150);

                using var statusFont = new Font("Microsoft JhengHei UI", 9.2f);
                Size statusSize = TextRenderer.MeasureText(g, statusText, statusFont, Size.Empty, TextFormatFlags.NoPadding);
                int statusX = bounds.Right - pad - statusSize.Width;
                if (statusX > titleX + 20)
                {
                    TextRenderer.DrawText(g, statusText, statusFont, new Point(statusX, y + 1), statusColor, TextFormatFlags.NoPadding);
                }
            }
        }

        public class EventOptionRowItem : IEventRowItem
        {
            public string EventId { get; set; } = string.Empty;
            public string OptionId { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public string GuideKind { get; set; } = string.Empty;
            public string GuideWhy { get; set; } = string.Empty;
            public bool HasEncounter { get; set; }
            public string StatsSummary { get; set; } = string.Empty;
            public bool IsSelected { get; set; }
            public bool IsFaded { get; set; }

            public int GetItemHeight(float dpiScale) => (int)Math.Round(32 * dpiScale);

            public void Draw(Graphics g, Rectangle bounds, float dpiScale)
            {
                Color bg = IsSelected
                    ? Color.FromArgb(30, 42, 60)
                    : Color.FromArgb(22, 23, 30);

                using var bgBrush = new SolidBrush(bg);
                g.FillRectangle(bgBrush, bounds);

                int pad = (int)Math.Round(24 * dpiScale);
                int centerY = bounds.Y + bounds.Height / 2;

                // 1. 單選圈圈 (Radio Circle)
                int radioRadius = (int)Math.Round(6.5 * dpiScale);
                int radioX = pad;
                int radioY = centerY - radioRadius;
                var radioRect = new Rectangle(radioX, radioY, radioRadius * 2, radioRadius * 2);

                if (IsSelected)
                {
                    using var fillBrush = new SolidBrush(Color.FromArgb(80, 160, 255));
                    using var ringPen = new Pen(Color.FromArgb(120, 180, 255), 1.5f);
                    g.DrawEllipse(ringPen, radioRect);
                    int innerPad = (int)Math.Round(3 * dpiScale);
                    g.FillEllipse(fillBrush, radioX + innerPad, radioY + innerPad, (radioRadius - innerPad) * 2, (radioRadius - innerPad) * 2);
                }
                else
                {
                    using var ringPen = new Pen(Color.FromArgb(85, 85, 105), 1.2f);
                    g.DrawEllipse(ringPen, radioRect);
                }

                // 2. 選項文字
                int textX = radioX + radioRadius * 2 + (int)Math.Round(8 * dpiScale);
                int textY = centerY - (int)Math.Round(9 * dpiScale);

                Color textColor = IsSelected
                    ? Color.FromArgb(255, 255, 255)
                    : (IsFaded ? Color.FromArgb(120, 120, 135) : Color.FromArgb(220, 220, 230));

                using var font = new Font("Microsoft JhengHei UI", 9.8f, IsSelected ? FontStyle.Bold : FontStyle.Regular);
                Size textSize = TextRenderer.MeasureText(g, Label, font, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, Label, font, new Point(textX, textY), textColor, TextFormatFlags.NoPadding);

                // 3. 獎勵屬性變化（緊接在選項文字之後）
                if (!string.IsNullOrEmpty(StatsSummary))
                {
                    int statsX = textX + textSize.Width + (int)Math.Round(10 * dpiScale);
                    using var statFont = new Font("Microsoft JhengHei UI", 9.8f, FontStyle.Bold);
                    Color statColor = IsFaded ? Color.FromArgb(50, 130, 120) : Color.FromArgb(79, 211, 192);
                    TextRenderer.DrawText(g, StatsSummary, statFont, new Point(statsX, textY), statColor, TextFormatFlags.NoPadding);
                }

                // 4. 右側標籤群（由右至左對齊，文字不縮小，移除【】符號）
                using var tagFont = new Font("Microsoft JhengHei UI", 9.8f, FontStyle.Bold);
                int currentRightX = bounds.Right - (int)Math.Round(12 * dpiScale);

                // 4a. 遭遇戰標記
                if (HasEncounter)
                {
                    string encText = "有遭遇戰";
                    Size encSize = TextRenderer.MeasureText(g, encText, tagFont, Size.Empty, TextFormatFlags.NoPadding);
                    int encX = currentRightX - encSize.Width;
                    TextRenderer.DrawText(g, encText, tagFont, new Point(encX, textY), Color.FromArgb(240, 75, 75), TextFormatFlags.NoPadding);
                    currentRightX = encX - (int)Math.Round(8 * dpiScale);
                }

                // 4b. 推薦/必選/別選標籤
                string guideTag = GuideKind switch
                {
                    "route" => "推薦",
                    "always" => "必選",
                    "never" => "別選",
                    _ => string.Empty
                };

                if (!string.IsNullOrEmpty(guideTag))
                {
                    Color badgeTextCol = GuideKind switch
                    {
                        "route" => Color.FromArgb(228, 162, 76),
                        "always" => Color.FromArgb(242, 200, 75),
                        "never" => Color.FromArgb(255, 130, 130),
                        _ => Color.White
                    };
                    Size badgeSize = TextRenderer.MeasureText(g, guideTag, tagFont, Size.Empty, TextFormatFlags.NoPadding);
                    int badgeX = currentRightX - badgeSize.Width;
                    TextRenderer.DrawText(g, guideTag, tagFont, new Point(badgeX, textY), badgeTextCol, TextFormatFlags.NoPadding);
                }
            }
        }
    }
}
