using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FontAwesome.Sharp;
using MyDoujinBot.Models;
using MyDoujinBot.Models.ExternalEvents;
using MyDoujinBot.Services;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 事件偏好編輯視窗：勾選要降低事件機率的角色，並顯示各角色事件給點計數與攻略組分級。
    /// 支援檢視篩選（全部／已排除／未排除）、攻略建議一鍵勾選、多條件複合篩選。
    /// 點選「儲存設定」後寫入方案，不會自動送出伺服器（須於外層點選「更新」）。
    /// </summary>
    public sealed class EventPreferenceEditorForm : Form
    {
        private sealed class RowInfo
        {
            public string CharacterId = string.Empty;
            public string DisplayName = string.Empty;
            public ChainStatCounts? Chain;
        }

        private sealed class ConditionRow
        {
            public FlowLayoutPanel Container = null!;
            public Label LblIndex = null!;
            public ComboBox CmbStat = null!;
            public ComboBox CmbMetric = null!;
            public ComboBox CmbOperator = null!;
            public NumericUpDown NudThreshold = null!;
            public Button BtnRemove = null!;
        }

        private static readonly (string Key, string Header)[] StatColumns =
        {
            ("hp", "HP"), ("攻", "攻擊"), ("防", "防禦"), ("體", "體力"), ("敏", "敏捷"),
            ("反", "反應"), ("技", "技巧"), ("智", "智力"), ("幸", "幸運")
        };

        // 配色常數（共用靜態 Color 結構，零額外資源開銷）
        private static readonly Color FormBackColor       = Color.FromArgb(20, 20, 26);
        private static readonly Color CardBackColor       = Color.FromArgb(28, 30, 40);
        private static readonly Color InputBackColor      = Color.FromArgb(38, 40, 52);
        private static readonly Color GridBackColor       = Color.FromArgb(22, 23, 30);
        private static readonly Color GridRowBackColor    = Color.FromArgb(24, 25, 34);
        private static readonly Color GridAltRowBackColor = Color.FromArgb(28, 30, 40);
        private static readonly Color GridCheckedRowColor = Color.FromArgb(40, 32, 42);
        private static readonly Color GridHeaderBackColor = Color.FromArgb(34, 37, 50);
        private static readonly Color GridSelectBackColor = Color.FromArgb(48, 62, 96);
        private static readonly Color GridLineColor       = Color.FromArgb(42, 44, 58);

        private static readonly Color TextPrimaryColor    = Color.FromArgb(225, 228, 240);
        private static readonly Color TextMutedColor      = Color.FromArgb(145, 152, 175);
        private static readonly Color TextDimZeroColor    = Color.FromArgb(88, 94, 115);
        private static readonly Color TextNegativeColor   = Color.FromArgb(240, 185, 95);
        private static readonly Color TierTopColor        = Color.FromArgb(100, 220, 145);
        private static readonly Color TierMidColor        = Color.FromArgb(130, 195, 250);
        private static readonly Color TierLowColor        = Color.FromArgb(220, 145, 115);

        private readonly EventPreferencePreset _preset;
        private readonly HashSet<string> _existingOtherNames;
        private readonly List<ChainStatCounts> _chains;
        private readonly ExternalRouteFile _route;
        private readonly HashSet<string> _initialPending;
        private readonly HashSet<string> _listedIds = new();
        private readonly List<ConditionRow> _conditions = new();

        private TextBox _txtName = null!;
        private DataGridView _grid = null!;
        private ComboBox _cmbViewFilter = null!;
        private ComboBox _cmbLogic = null!;
        private FlowLayoutPanel _pnlConditions = null!;
        private CheckBox _chkRatio = null!;
        private Label _lblSummary = null!;
        private SortOrder _chkSortOrder = SortOrder.None;

        private ToolTip? _toolTip;
        private Form? _archetypeRefForm;

        private float DpiScale => (float)this.DeviceDpi / 96f;
        private int ScaleDpi(int value) => (int)Math.Round(value * DpiScale);

        public EventPreferenceEditorForm(
            IEnumerable<CharacterEntry> characters,
            ExternalRouteFile route,
            EventPreferencePreset preset,
            IEnumerable<string>? existingOtherNames = null)
        {
            _preset = preset;
            _existingOtherNames = existingOtherNames != null
                ? new HashSet<string>(existingOtherNames, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _route = route;
            _chains = EventStatCounter.Count(route);
            _initialPending = new HashSet<string>(preset.DeprioritizedCharacterIds ?? new List<string>());

            this.Text = $"編輯事件偏好 - {_preset.Name}";
            this.Size = new Size(ScaleDpi(940), ScaleDpi(650));
            this.MinimumSize = new Size(ScaleDpi(880), ScaleDpi(520));
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = FormBackColor;
            this.ForeColor = TextPrimaryColor;
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            BuildUI();
            PopulateRows(characters);
            UpdateSummary();
        }

        private void BuildUI()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(ScaleDpi(12), ScaleDpi(10), ScaleDpi(12), ScaleDpi(10)),
                BackColor = FormBackColor
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 0: 頂部設定卡
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 1: 攻略組推薦卡
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 2: 複合條件面板卡
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 3: 符號圖例說明列
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Row 4: 資料表格
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 5: 底部統計與儲存按鈕

            // ═══════════════════════════════════════════════════════════════════
            // Row 0: 頂部設定卡（左側：方案名稱 + 檢視篩選 + 顯示切換 ｜ 右側：清空）
            // ═══════════════════════════════════════════════════════════════════
            var pnlTopCard = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = CardBackColor,
                Padding = new Padding(ScaleDpi(12), ScaleDpi(6), ScaleDpi(12), ScaleDpi(6)),
                Margin = new Padding(0, 0, 0, ScaleDpi(6))
            };
            pnlTopCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pnlTopCard.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlTopCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var flpTopLeft = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.Left,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };

            var lblPresetName = new Label
            {
                Text = "方案：",
                ForeColor = Color.FromArgb(190, 195, 215),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, ScaleDpi(6), ScaleDpi(2), 0)
            };
            _txtName = new TextBox
            {
                Text = _preset.Name,
                Width = ScaleDpi(180),
                BackColor = InputBackColor,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Margin = new Padding(0, ScaleDpi(3), ScaleDpi(12), ScaleDpi(2))
            };

            var lblView = new Label
            {
                Text = "篩選：",
                ForeColor = Color.FromArgb(190, 195, 215),
                AutoSize = true,
                Margin = new Padding(0, ScaleDpi(6), ScaleDpi(2), 0)
            };
            _cmbViewFilter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ScaleDpi(105),
                BackColor = InputBackColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(12), ScaleDpi(2))
            };
            _cmbViewFilter.Items.AddRange(new object[] { "全部角色", "已勾選排除", "未勾選排除" });
            _cmbViewFilter.SelectedIndex = 0;
            _cmbViewFilter.SelectedIndexChanged += (_, _) => ApplyViewFilter();

            _chkRatio = new CheckBox
            {
                Text = "顯示佔比 (%)",
                ForeColor = Color.FromArgb(200, 205, 225),
                AutoSize = true,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, ScaleDpi(5), 0, 0)
            };
            _chkRatio.CheckedChanged += (_, _) => RefreshStatValues();

            flpTopLeft.Controls.AddRange(new Control[] { lblPresetName, _txtName, lblView, _cmbViewFilter, _chkRatio });

            var btnClear = MakeIconButton("清空目前顯示", Color.FromArgb(80, 48, 52), IconChar.RotateLeft);
            btnClear.Anchor = AnchorStyles.Right;
            btnClear.Margin = new Padding(0, ScaleDpi(1), 0, ScaleDpi(1));
            btnClear.Click += (_, _) => SetVisibleRowsChecked(_ => false);

            pnlTopCard.Controls.Add(flpTopLeft, 0, 0);
            pnlTopCard.Controls.Add(btnClear, 1, 0);

            // ═══════════════════════════════════════════════════════════════════
            // Row 1: 攻略組推薦獨立區塊（標題單獨一行，換行後為按鈕）
            // ═══════════════════════════════════════════════════════════════════
            var pnlGuideCard = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = CardBackColor,
                Padding = new Padding(ScaleDpi(12), ScaleDpi(6), ScaleDpi(12), ScaleDpi(6)),
                Margin = new Padding(0, 0, 0, ScaleDpi(6))
            };
            pnlGuideCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pnlGuideCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pnlGuideCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblGuideTitle = new Label
            {
                Text = "▎套用攻略組推薦",
                AutoSize = true,
                ForeColor = Color.FromArgb(145, 185, 255),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, ScaleDpi(2), 0, ScaleDpi(4))
            };

            var flpGuideButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.Left,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(ScaleDpi(4), 0, 0, 0)
            };

            var btnRecInt = MakeIconButton("智力推薦", Color.FromArgb(45, 92, 148), IconChar.WandMagicSparkles);
            btnRecInt.Margin = new Padding(0, ScaleDpi(1), ScaleDpi(6), ScaleDpi(1));
            btnRecInt.Click += (_, _) => ApplyGuideUntick("int");
            SetProfileToolTip(btnRecInt, "int");

            var btnRecAtk = MakeIconButton("攻擊推薦", Color.FromArgb(45, 92, 148), IconChar.Gavel);
            btnRecAtk.Margin = new Padding(0, ScaleDpi(1), ScaleDpi(6), ScaleDpi(1));
            btnRecAtk.Click += (_, _) => ApplyGuideUntick("atk");
            SetProfileToolTip(btnRecAtk, "atk");

            var btnRecHyb = MakeIconButton("兼具推薦", Color.FromArgb(45, 92, 148), IconChar.ScaleBalanced);
            btnRecHyb.Margin = new Padding(0, ScaleDpi(1), ScaleDpi(6), ScaleDpi(1));
            btnRecHyb.Click += (_, _) => ApplyGuideUntick("hyb");
            SetProfileToolTip(btnRecHyb, "hyb");

            var btnArchetypeList = MakeIconButton("流派詳細", Color.FromArgb(52, 74, 102), IconChar.ListUl);
            btnArchetypeList.Margin = new Padding(0, ScaleDpi(1), 0, ScaleDpi(1));
            btnArchetypeList.Click += (_, _) => ShowArchetypeListDialog();

            flpGuideButtons.Controls.AddRange(new Control[] { btnRecInt, btnRecAtk, btnRecHyb, btnArchetypeList });

            pnlGuideCard.Controls.Add(lblGuideTitle, 0, 0);
            pnlGuideCard.Controls.Add(flpGuideButtons, 0, 1);

            // ═══════════════════════════════════════════════════════════════════
            // Row 1: 複合條件一鍵勾選卡
            // ═══════════════════════════════════════════════════════════════════
            var pnlCompoundCard = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = CardBackColor,
                Padding = new Padding(ScaleDpi(12), ScaleDpi(6), ScaleDpi(12), ScaleDpi(6)),
                Margin = new Padding(0, 0, 0, ScaleDpi(6))
            };
            pnlCompoundCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pnlCompoundCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pnlCompoundCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var pnlCompoundHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, ScaleDpi(4))
            };
            pnlCompoundHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pnlCompoundHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlCompoundHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var flpCompoundLeft = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.Left,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };

            var lblCompTitle = new Label
            {
                Text = "▎複合條件一鍵勾選",
                AutoSize = true,
                ForeColor = Color.FromArgb(145, 185, 255),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, ScaleDpi(5), ScaleDpi(10), 0)
            };

            var lblLogic = new Label
            {
                Text = "多條件關係：",
                AutoSize = true,
                ForeColor = Color.FromArgb(190, 195, 215),
                Margin = new Padding(0, ScaleDpi(5), ScaleDpi(2), 0)
            };

            _cmbLogic = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ScaleDpi(135),
                BackColor = InputBackColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(10), ScaleDpi(2))
            };
            _cmbLogic.Items.AddRange(new object[] { "全部符合 (AND)", "任一符合 (OR)" });
            _cmbLogic.SelectedIndex = 0;

            var btnAddCond = MakeIconButton("新增條件", Color.FromArgb(52, 66, 92), IconChar.Plus);
            btnAddCond.Click += (_, _) => AddConditionRow();

            flpCompoundLeft.Controls.AddRange(new Control[] { lblCompTitle, lblLogic, _cmbLogic, btnAddCond });

            var flpCompoundRight = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };

            var btnPreviewCompound = MakeIconButton("篩選確認", Color.FromArgb(48, 86, 130), IconChar.Filter);
            btnPreviewCompound.Margin = new Padding(0, ScaleDpi(1), ScaleDpi(6), ScaleDpi(1));
            btnPreviewCompound.Click += (_, _) => PreviewCompoundRule();

            var btnApplyCompound = MakeIconButton("套用條件勾選（覆蓋目前顯示列）", Color.FromArgb(40, 108, 74), IconChar.CheckDouble);
            btnApplyCompound.Margin = new Padding(0, ScaleDpi(1), 0, ScaleDpi(1));
            btnApplyCompound.Click += (_, _) => ApplyCompoundRule();

            flpCompoundRight.Controls.AddRange(new Control[] { btnPreviewCompound, btnApplyCompound });

            pnlCompoundHeader.Controls.Add(flpCompoundLeft, 0, 0);
            pnlCompoundHeader.Controls.Add(flpCompoundRight, 1, 0);

            _pnlConditions = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };
            AddConditionRow(defaultStatIndex: 7); // 預設第一條：智力 事件數 < 2

            pnlCompoundCard.Controls.Add(pnlCompoundHeader, 0, 0);
            pnlCompoundCard.Controls.Add(_pnlConditions, 0, 1);

            // ═══════════════════════════════════════════════════════════════════
            // Row 2: 簡潔圖例與操作提示列
            // ═══════════════════════════════════════════════════════════════════
            var lblLegend = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ForeColor = TextMutedColor,
                Text = "💡 欄位標題可點擊排序 ｜ 「*」僅成功時給點　「▼」含扣點事件（滑鼠移至數值可看明細） ｜ 一鍵勾選僅覆蓋目前篩選顯示的角色列，隱藏列維持原狀態",
                Margin = new Padding(ScaleDpi(2), 0, 0, ScaleDpi(4))
            };

            // ═══════════════════════════════════════════════════════════════════
            // Row 3: 角色與事件統計表格
            // ═══════════════════════════════════════════════════════════════════
            _grid = CreateStyledGrid();

            // ═══════════════════════════════════════════════════════════════════
            // Row 4: 底部統計與儲存／取消按鈕
            // ═══════════════════════════════════════════════════════════════════
            var pnlBottom = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Margin = new Padding(0, ScaleDpi(10), 0, 0)
            };
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pnlBottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _lblSummary = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = Color.FromArgb(180, 190, 215),
                Font = new Font("Microsoft JhengHei UI", 9.8f, FontStyle.Bold)
            };

            var pnlButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Margin = Padding.Empty
            };

            var btnSave = new IconButton
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
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(12), ScaleDpi(5), ScaleDpi(6), ScaleDpi(5)),
                Margin = Padding.Empty
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (_, _) => OnSaveClicked();

            var btnCancel = new IconButton
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
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(10), ScaleDpi(5), ScaleDpi(6), ScaleDpi(5)),
                Margin = new Padding(0, 0, ScaleDpi(8), 0)
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (_, _) => this.Close();

            pnlButtons.Controls.Add(btnSave);
            pnlButtons.Controls.Add(btnCancel);
            pnlBottom.Controls.Add(_lblSummary, 0, 0);
            pnlBottom.Controls.Add(pnlButtons, 1, 0);

            root.Controls.Add(pnlTopCard, 0, 0);
            root.Controls.Add(pnlGuideCard, 0, 1);
            root.Controls.Add(pnlCompoundCard, 0, 2);
            root.Controls.Add(lblLegend, 0, 3);
            root.Controls.Add(_grid, 0, 4);
            root.Controls.Add(pnlBottom, 0, 5);
            this.Controls.Add(root);
            this.CancelButton = btnCancel;
        }

        private DataGridView CreateStyledGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = GridBackColor,
                BorderStyle = BorderStyle.FixedSingle,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                EnableHeadersVisualStyles = false,
                GridColor = GridLineColor,
                RowTemplate = { Height = ScaleDpi(30) },
                ColumnHeadersHeight = ScaleDpi(34),
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };

            // 啟用雙緩衝，捲動與排序零閃爍
            try
            {
                typeof(DataGridView).InvokeMember("DoubleBuffered",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                    null, grid, new object[] { true });
            }
            catch { }

            grid.ColumnHeadersDefaultCellStyle.BackColor = GridHeaderBackColor;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(215, 220, 240);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = GridHeaderBackColor;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(215, 220, 240);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            grid.DefaultCellStyle.BackColor = GridRowBackColor;
            grid.DefaultCellStyle.ForeColor = TextPrimaryColor;
            grid.DefaultCellStyle.SelectionBackColor = GridSelectBackColor;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.AlternatingRowsDefaultCellStyle.BackColor = GridAltRowBackColor;

            // 1. 勾選欄
            var colChk = new DataGridViewCheckBoxColumn
            {
                Name = "chk",
                HeaderText = "排除",
                Width = ScaleDpi(44),
                MinimumWidth = ScaleDpi(42),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.Programmatic
            };

            // 2. 角色名稱欄
            var colName = new DataGridViewTextBoxColumn
            {
                Name = "name",
                HeaderText = "角色名稱",
                ReadOnly = true,
                Width = ScaleDpi(135),
                MinimumWidth = ScaleDpi(110),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
            colName.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            colName.DefaultCellStyle.Padding = new Padding(ScaleDpi(4), 0, ScaleDpi(4), 0);
            colName.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
            colName.HeaderCell.Style.Padding = new Padding(ScaleDpi(4), 0, 0, 0);

            // 3. 事件總數欄
            var colTotal = new DataGridViewTextBoxColumn
            {
                Name = "total",
                HeaderText = "事件數",
                ReadOnly = true,
                Width = ScaleDpi(56),
                MinimumWidth = ScaleDpi(52),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.Automatic
            };

            grid.Columns.Add(colChk);
            grid.Columns.Add(colName);
            grid.Columns.Add(colTotal);

            // 4. 九大屬性欄（依比例自動均分剩餘寬度，消除右側留白）
            foreach (var s in StatColumns)
            {
                var colStat = new DataGridViewTextBoxColumn
                {
                    Name = "s_" + s.Key,
                    HeaderText = s.Header,
                    ReadOnly = true,
                    MinimumWidth = ScaleDpi(38),
                    FillWeight = 10f,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    SortMode = DataGridViewColumnSortMode.Automatic
                };
                grid.Columns.Add(colStat);
            }

            // 5. 攻略組分級欄
            var colTierInt = new DataGridViewTextBoxColumn
            {
                Name = "tierInt",
                HeaderText = "智力",
                ReadOnly = true,
                Width = ScaleDpi(48),
                MinimumWidth = ScaleDpi(46),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
            var colTierAtk = new DataGridViewTextBoxColumn
            {
                Name = "tierAtk",
                HeaderText = "攻擊",
                ReadOnly = true,
                Width = ScaleDpi(48),
                MinimumWidth = ScaleDpi(46),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
            var colTierHyb = new DataGridViewTextBoxColumn
            {
                Name = "tierHyb",
                HeaderText = "兼具",
                ReadOnly = true,
                Width = ScaleDpi(48),
                MinimumWidth = ScaleDpi(46),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
            grid.Columns.Add(colTierInt);
            grid.Columns.Add(colTierAtk);
            grid.Columns.Add(colTierHyb);

            grid.CellFormatting += OnCellFormatting;
            grid.CellPainting += (_, e) =>
            {
                if (e.RowIndex >= 0 && (e.PaintParts & DataGridViewPaintParts.Focus) != 0)
                {
                    e.Paint(e.CellBounds, e.PaintParts & ~DataGridViewPaintParts.Focus);
                    e.Handled = true;
                }
            };
            grid.SortCompare += OnSortCompare;
            grid.ColumnHeaderMouseClick += OnColumnHeaderMouseClick;
            grid.CellDoubleClick += (_, e) =>
            {
                // 雙擊整列任意位置亦可快速切換勾選
                if (e.RowIndex >= 0 && e.ColumnIndex > 0)
                {
                    var cell = grid.Rows[e.RowIndex].Cells["chk"];
                    cell.Value = !(cell.Value is true);
                }
            };
            grid.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += (_, e) =>
            {
                if (e.ColumnIndex == 0 && e.RowIndex >= 0)
                {
                    UpdateSummary();
                    grid.InvalidateRow(e.RowIndex);
                }
            };

            return grid;
        }

        private void OnSaveClicked()
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
                MessageBox.Show(this, $"已存在名為【{newName}】的事件偏好方案，請使用其他名稱。", "名稱重複", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtName.Focus();
                return;
            }

            _preset.Name = newName;
            _preset.DeprioritizedCharacterIds = CollectResult().ToList();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void AddConditionRow(int defaultStatIndex = 6)
        {
            var rowPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(ScaleDpi(4), ScaleDpi(2), 0, ScaleDpi(2))
            };

            var lblIdx = new Label
            {
                Text = $"條件 {_conditions.Count + 1}：",
                ForeColor = TextMutedColor,
                AutoSize = true,
                Margin = new Padding(0, ScaleDpi(5), ScaleDpi(4), 0)
            };

            var cmbStat = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ScaleDpi(82),
                BackColor = InputBackColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(6), 0)
            };
            foreach (var s in StatColumns) cmbStat.Items.Add(s.Header);
            cmbStat.SelectedIndex = Math.Clamp(defaultStatIndex, 0, StatColumns.Length - 1);

            var cmbMetric = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ScaleDpi(96),
                BackColor = InputBackColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(6), 0)
            };
            cmbMetric.Items.AddRange(new object[] { "事件數", "佔比 (%)" });
            cmbMetric.SelectedIndex = 0;

            var cmbOp = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ScaleDpi(58),
                BackColor = InputBackColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(6), 0)
            };
            cmbOp.Items.AddRange(new object[] { "<", "<=" });
            cmbOp.SelectedIndex = 0;

            var nud = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 100,
                Value = 2,
                Width = ScaleDpi(64),
                BackColor = InputBackColor,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(6), 0)
            };

            var btnRemove = new Button
            {
                Text = "移除",
                AutoSize = true,
                BackColor = Color.FromArgb(75, 42, 46),
                ForeColor = Color.FromArgb(235, 200, 200),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 8.8f),
                Padding = new Padding(ScaleDpi(4), ScaleDpi(1), ScaleDpi(4), ScaleDpi(1)),
                Margin = new Padding(0, ScaleDpi(1), 0, 0)
            };
            btnRemove.FlatAppearance.BorderSize = 0;

            var cond = new ConditionRow
            {
                Container = rowPanel,
                LblIndex = lblIdx,
                CmbStat = cmbStat,
                CmbMetric = cmbMetric,
                CmbOperator = cmbOp,
                NudThreshold = nud,
                BtnRemove = btnRemove
            };
            btnRemove.Click += (_, _) => RemoveConditionRow(cond);

            rowPanel.Controls.AddRange(new Control[] { lblIdx, cmbStat, cmbMetric, cmbOp, nud, btnRemove });
            _conditions.Add(cond);
            _pnlConditions.Controls.Add(rowPanel);
            UpdateConditionRemoveButtons();
        }

        private void RemoveConditionRow(ConditionRow cond)
        {
            _conditions.Remove(cond);
            _pnlConditions.Controls.Remove(cond.Container);
            cond.Container.Dispose();
            UpdateConditionRemoveButtons();
        }

        private void UpdateConditionRemoveButtons()
        {
            for (int i = 0; i < _conditions.Count; i++)
            {
                _conditions[i].LblIndex.Text = $"條件 {i + 1}：";
                _conditions[i].BtnRemove.Enabled = true;
                _conditions[i].BtnRemove.Visible = true;
            }
        }

        private IconButton MakeIconButton(string text, Color back, IconChar icon)
        {
            var btn = new IconButton
            {
                Text = $" {text}",
                AutoSize = true,
                BackColor = back,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.2f),
                Cursor = Cursors.Hand,
                IconChar = icon,
                IconColor = Color.White,
                IconSize = ScaleDpi(15),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(8), ScaleDpi(3), ScaleDpi(6), ScaleDpi(3)),
                Margin = new Padding(0, ScaleDpi(1), ScaleDpi(6), ScaleDpi(1))
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void PopulateRows(IEnumerable<CharacterEntry> characters)
        {
            var distinctChars = new List<CharacterEntry>();
            var seen = new HashSet<string>();
            foreach (var c in characters)
            {
                if (string.IsNullOrWhiteSpace(c.CharacterId) || !seen.Add(c.CharacterId)) continue;
                distinctChars.Add(c);
            }

            // 同名角色（例如「孤鳥」三個型態）於表格顯示「名稱（稱號）」區分
            var duplicateNames = distinctChars
                .GroupBy(c => c.Name.Trim())
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet();

            foreach (var c in distinctChars)
            {
                var chain = CharacterChainMatcher.Match(c, _chains);
                _listedIds.Add(c.CharacterId);

                string displayName = duplicateNames.Contains(c.Name.Trim()) && !string.IsNullOrWhiteSpace(c.Title)
                    ? $"{c.Name}（{c.Title}）"
                    : c.Name;

                int idx = _grid.Rows.Add();
                var row = _grid.Rows[idx];
                row.Tag = new RowInfo
                {
                    CharacterId = c.CharacterId,
                    DisplayName = displayName,
                    Chain = chain
                };
                row.Cells["chk"].Value = _initialPending.Contains(c.CharacterId);
                row.Cells["name"].Value = displayName;
                row.Cells["total"].Value = chain != null ? chain.TotalEvents : -1;
                row.Cells["tierInt"].Value = TierOf("int", chain);
                row.Cells["tierAtk"].Value = TierOf("atk", chain);
                row.Cells["tierHyb"].Value = TierOf("hyb", chain);

                if (chain == null)
                {
                    row.Cells["name"].ToolTipText = "事件庫沒有對應的角色線，無計數資料";
                }
            }

            RefreshStatValues();
        }

        private string TierOf(string profile, ChainStatCounts? chain)
        {
            if (chain == null) return "-";
            if (!_route.Overview.TryGetValue(profile, out var p)) return "-";
            var line = p.Lines.FirstOrDefault(l => l.Name == chain.ChainName);
            return line == null ? "-" : line.Tier;
        }

        private void RefreshStatValues()
        {
            bool ratio = _chkRatio.Checked;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                var info = (RowInfo)row.Tag!;
                foreach (var s in StatColumns)
                {
                    var cell = row.Cells["s_" + s.Key];
                    if (info.Chain == null)
                    {
                        cell.Value = -1.0;
                        continue;
                    }
                    cell.Value = ratio
                        ? Math.Round(info.Chain.Ratio(s.Key) * 100.0, 1)
                        : (double)info.Chain.Stats[s.Key].AnyCount;
                    var c = info.Chain.Stats[s.Key];
                    cell.ToolTipText = c.AnyCount == 0
                        ? string.Empty
                        : $"成功時給 {c.SuccessCount} 個事件、失敗時 {c.FailCount} 個事件、含扣點 {c.NegativeCount} 個事件（事件總數 {info.Chain.TotalEvents}）";
                }
            }
            _grid.Invalidate();
        }

        private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.CellStyle == null) return;
            var row = _grid.Rows[e.RowIndex];
            if (row.Tag is not RowInfo info) return;

            var style = e.CellStyle;
            bool isChecked = row.Cells["chk"].Value is true;
            if (isChecked)
            {
                style.BackColor = GridCheckedRowColor;
            }

            string col = _grid.Columns[e.ColumnIndex].Name;

            if (col == "name")
            {
                style.ForeColor = info.Chain == null ? TextMutedColor : TextPrimaryColor;
                return;
            }

            if (col == "total")
            {
                if (info.Chain == null)
                {
                    e.Value = "無事件資料";
                    style.ForeColor = TextDimZeroColor;
                    e.FormattingApplied = true;
                }
                else
                {
                    style.ForeColor = Color.FromArgb(190, 205, 235);
                }
                return;
            }

            if (col == "tierInt" || col == "tierAtk" || col == "tierHyb")
            {
                string raw = e.Value?.ToString() ?? "-";
                switch (raw)
                {
                    case "top":
                        e.Value = "Top";
                        style.ForeColor = TierTopColor;
                        e.FormattingApplied = true;
                        break;
                    case "mid":
                        e.Value = "Mid";
                        style.ForeColor = TierMidColor;
                        e.FormattingApplied = true;
                        break;
                    case "low":
                        e.Value = "Low";
                        style.ForeColor = TierLowColor;
                        e.FormattingApplied = true;
                        break;
                    default:
                        style.ForeColor = TextDimZeroColor;
                        break;
                }
                return;
            }

            if (!col.StartsWith("s_")) return;

            if (info.Chain == null)
            {
                e.Value = "-";
                style.ForeColor = TextDimZeroColor;
                e.FormattingApplied = true;
                return;
            }

            string statKey = col[2..];
            var c = info.Chain.Stats[statKey];
            if (c.AnyCount == 0)
            {
                e.Value = _chkRatio.Checked ? "0%" : "0";
                style.ForeColor = TextDimZeroColor;
                e.FormattingApplied = true;
                return;
            }

            string text = _chkRatio.Checked ? $"{info.Chain.Ratio(statKey) * 100:0.#}%" : c.AnyCount.ToString();
            if (c.OnlyOnSuccess) text += "*";
            if (c.NegativeCount > 0) text += "▼";

            e.Value = text;
            style.ForeColor = c.NegativeCount > 0 ? TextNegativeColor : TextPrimaryColor;
            e.FormattingApplied = true;
        }

        private void OnColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex != 0)
            {
                _chkSortOrder = SortOrder.None;
                _grid.Columns[0].HeaderCell.SortGlyphDirection = SortOrder.None;
                return;
            }

            _chkSortOrder = _chkSortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            var dir = _chkSortOrder == SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending;
            _grid.Sort(_grid.Columns[0], dir);
            _grid.Columns[0].HeaderCell.SortGlyphDirection = _chkSortOrder;
        }

        private static int TierRank(string? tier) => tier switch
        {
            "top" => 3,
            "mid" => 2,
            "low" => 1,
            _ => 0
        };

        private void OnSortCompare(object? sender, DataGridViewSortCompareEventArgs e)
        {
            string col = e.Column.Name;
            if (col == "chk")
            {
                bool b1 = e.CellValue1 is true;
                bool b2 = e.CellValue2 is true;
                e.SortResult = b1.CompareTo(b2);
            }
            else if (col == "total" || col.StartsWith("s_"))
            {
                double d1 = Convert.ToDouble(e.CellValue1 ?? -1.0);
                double d2 = Convert.ToDouble(e.CellValue2 ?? -1.0);
                e.SortResult = d1.CompareTo(d2);
            }
            else if (col == "tierInt" || col == "tierAtk" || col == "tierHyb")
            {
                e.SortResult = TierRank(e.CellValue1?.ToString()).CompareTo(TierRank(e.CellValue2?.ToString()));
            }
            else
            {
                e.SortResult = string.Compare(e.CellValue1?.ToString(), e.CellValue2?.ToString(), StringComparison.CurrentCulture);
            }

            if (e.SortResult == 0)
            {
                string n1 = (_grid.Rows[e.RowIndex1].Tag as RowInfo)?.DisplayName ?? string.Empty;
                string n2 = (_grid.Rows[e.RowIndex2].Tag as RowInfo)?.DisplayName ?? string.Empty;
                e.SortResult = string.Compare(n1, n2, StringComparison.CurrentCulture);
            }
            e.Handled = true;
        }

        private void ApplyViewFilter()
        {
            int mode = _cmbViewFilter.SelectedIndex; // 0=全部角色, 1=已勾選排除, 2=未勾選排除
            _grid.CurrentCell = null;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                bool isChecked = row.Cells["chk"].Value is true;
                row.Visible = mode switch
                {
                    1 => isChecked,
                    2 => !isChecked,
                    _ => true
                };
            }
            UpdateSummary();
        }

        private void SetProfileToolTip(IconButton btn, string profileKey)
        {
            if (!_route.Overview.TryGetValue(profileKey, out var p)) return;
            string tip = $"【{p.Label}】評分權重：\n{p.Order}";
            if (p.Median > 0) tip += $"\n中位數評分：{p.Median}";
            if (_toolTip == null)
            {
                _toolTip = new ToolTip
                {
                    AutoPopDelay = 10000,
                    InitialDelay = 200,
                    ReshowDelay = 100
                };
            }
            _toolTip.SetToolTip(btn, tip);
        }

        private void SetVisibleRowsChecked(Func<RowInfo, bool> predicate)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!row.Visible) continue;
                row.Cells["chk"].Value = predicate((RowInfo)row.Tag!);
            }
            _grid.Invalidate();
            UpdateSummary();
        }

        private void ShowArchetypeListDialog()
        {
            if (_archetypeRefForm != null && !_archetypeRefForm.IsDisposed)
            {
                _archetypeRefForm.BringToFront();
                _archetypeRefForm.Focus();
                return;
            }

            var dlg = new Form
            {
                Text = "攻略組流派詳細",
                Size = new Size(ScaleDpi(600), ScaleDpi(540)),
                MinimumSize = new Size(ScaleDpi(480), ScaleDpi(400)),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = FormBackColor,
                ForeColor = TextPrimaryColor,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                ShowIcon = false,
                ShowInTaskbar = false
            };

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(ScaleDpi(10), ScaleDpi(4))
            };

            var profiles = new[]
            {
                ("int", "智力型"),
                ("atk", "攻擊型"),
                ("hyb", "兼具型")
            };

            foreach (var (key, fallbackTitle) in profiles)
            {
                if (!_route.Overview.TryGetValue(key, out var prof)) continue;
                string title = string.IsNullOrWhiteSpace(prof.Label) ? fallbackTitle : prof.Label;

                var page = new TabPage(title)
                {
                    BackColor = FormBackColor,
                    ForeColor = TextPrimaryColor,
                    Padding = new Padding(ScaleDpi(8))
                };

                var lblOrder = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    MaximumSize = new Size(ScaleDpi(550), 0),
                    ForeColor = Color.FromArgb(180, 200, 240),
                    Text = $"權重：{prof.Order}" +
                           (prof.Median > 0 ? $"  |  中位數：{prof.Median}" : "") +
                           $"  |  保留：{prof.Keep.Count} / 降低：{prof.Reduce.Count}",
                    Padding = new Padding(0, 0, 0, ScaleDpi(6))
                };

                var refGrid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                    ReadOnly = true,
                    RowHeadersVisible = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    BackgroundColor = GridBackColor,
                    BorderStyle = BorderStyle.FixedSingle,
                    CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                    EnableHeadersVisualStyles = false,
                    GridColor = GridLineColor,
                    RowTemplate = { Height = ScaleDpi(26) },
                    ColumnHeadersHeight = ScaleDpi(30),
                    ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
                };
                refGrid.ColumnHeadersDefaultCellStyle.BackColor = GridHeaderBackColor;
                refGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(215, 220, 240);
                refGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 9.2f, FontStyle.Bold);
                refGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                refGrid.DefaultCellStyle.BackColor = GridRowBackColor;
                refGrid.DefaultCellStyle.ForeColor = TextPrimaryColor;
                refGrid.DefaultCellStyle.SelectionBackColor = GridSelectBackColor;
                refGrid.AlternatingRowsDefaultCellStyle.BackColor = GridAltRowBackColor;

                refGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "rank",
                    HeaderText = "#",
                    Width = ScaleDpi(40),
                    DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
                });
                refGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "name",
                    HeaderText = "角色名稱",
                    Width = ScaleDpi(155)
                });
                refGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "sub",
                    HeaderText = "稱號／備註",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                });
                refGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "val",
                    HeaderText = "期望分",
                    Width = ScaleDpi(64),
                    DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
                });
                refGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "ev",
                    HeaderText = "事件",
                    Width = ScaleDpi(48),
                    DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
                });
                refGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "tier",
                    HeaderText = "建議",
                    Width = ScaleDpi(72),
                    DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
                });

                var keepSet = new HashSet<string>(prof.Keep);
                if (prof.Lines != null && prof.Lines.Count > 0)
                {
                    foreach (var line in prof.Lines)
                    {
                        bool isKeep = keepSet.Contains(line.Name) || string.Equals(line.Tier, "top", StringComparison.OrdinalIgnoreCase);
                        string advice = isKeep ? "保留" : "降低";
                        if (line.Forced) advice += "(必打)";

                        int idx = refGrid.Rows.Add(
                            line.Rank,
                            line.Name,
                            line.Sub,
                            line.Value.ToString("0.##"),
                            line.Events,
                            advice);
                        refGrid.Rows[idx].Cells["tier"].Style.ForeColor = isKeep ? TierTopColor : TierLowColor;
                    }
                }
                else
                {
                    int rank = 1;
                    foreach (var name in prof.Keep)
                    {
                        int idx = refGrid.Rows.Add(rank++, name, "-", "-", "-", "保留");
                        refGrid.Rows[idx].Cells["tier"].Style.ForeColor = TierTopColor;
                    }
                    foreach (var name in prof.Reduce)
                    {
                        int idx = refGrid.Rows.Add(rank++, name, "-", "-", "-", "降低");
                        refGrid.Rows[idx].Cells["tier"].Style.ForeColor = TierLowColor;
                    }
                }

                page.Controls.Add(refGrid);
                page.Controls.Add(lblOrder);
                tabs.TabPages.Add(page);
            }

            dlg.Controls.Add(tabs);
            _archetypeRefForm = dlg;
            dlg.FormClosed += (_, _) => _archetypeRefForm = null;
            dlg.Show(this);
        }

        private void ApplyGuideUntick(string profile)
        {
            if (!_route.Overview.TryGetValue(profile, out var p))
            {
                MessageBox.Show(this, "事件庫沒有此攻略建議資料。", "無法套用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 新版事件庫欄位名稱為 reduce，舊版為 untick；若兩者皆無再依 cut 或 recommend/keep 反向排除
            var excludeList = new List<string>();
            if (p.Reduce != null && p.Reduce.Count > 0)
            {
                excludeList.AddRange(p.Reduce);
            }
            else if (p.Untick != null && p.Untick.Count > 0)
            {
                excludeList.AddRange(p.Untick);
            }

            if (p.Cut != null && p.Cut.Count > 0)
            {
                excludeList.AddRange(p.Cut);
            }

            // 若事件庫只有 keep 或 recommend 清單，則排除未被推薦的角色
            var keepList = (p.Keep != null && p.Keep.Count > 0) ? p.Keep : p.Recommend;

            if (excludeList.Count == 0 && (keepList == null || keepList.Count == 0))
            {
                MessageBox.Show(this, "事件庫沒有此攻略建議資料。", "無法套用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var excludeNames = new HashSet<string>(excludeList);
            var keepNames = new HashSet<string>(keepList ?? new List<string>());

            SetVisibleRowsChecked(info =>
            {
                if (info.Chain == null) return false;
                string chainName = info.Chain.ChainName;

                // 若有明確排除名單，命中則勾選排除
                if (excludeNames.Count > 0)
                {
                    return excludeNames.Contains(chainName) ||
                           chainName.Split('／', '/').Any(part => excludeNames.Contains(part.Trim()));
                }

                // 否則若只有保留名單，未在保留名單內者勾選排除
                return !keepNames.Contains(chainName) &&
                       !chainName.Split('／', '/').Any(part => keepNames.Contains(part.Trim()));
            });

            string label = !string.IsNullOrWhiteSpace(p.Label)
                ? p.Label
                : profile switch { "int" => "智力型", "atk" => "攻擊型", "hyb" => "兼具型", _ => profile };
            _txtName.Text = $"攻略組推薦：{label}";
        }

        private void PreviewCompoundRule()
        {
            bool matchAll = _cmbLogic.SelectedIndex == 0; // 0=AND, 1=OR
            _grid.CurrentCell = null;

            if (_conditions.Count == 0)
            {
                foreach (DataGridViewRow row in _grid.Rows)
                    row.Visible = true;
                UpdateSummary();
                return;
            }

            foreach (DataGridViewRow row in _grid.Rows)
            {
                var info = (RowInfo)row.Tag!;
                if (info.Chain == null)
                {
                    row.Visible = false;
                    continue;
                }
                row.Visible = matchAll
                    ? _conditions.All(c => EvaluateCondition(info.Chain, c))
                    : _conditions.Any(c => EvaluateCondition(info.Chain, c));
            }
            UpdateSummary();
        }

        private void ApplyCompoundRule()
        {
            if (_conditions.Count == 0)
            {
                MessageBox.Show(this, "目前沒有任何篩選條件，請先按「新增條件」。", "無條件", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool matchAll = _cmbLogic.SelectedIndex == 0; // 0=AND, 1=OR
            SetVisibleRowsChecked(info =>
            {
                if (info.Chain == null) return false;
                return matchAll
                    ? _conditions.All(c => EvaluateCondition(info.Chain, c))
                    : _conditions.Any(c => EvaluateCondition(info.Chain, c));
            });
        }

        private static bool EvaluateCondition(ChainStatCounts chain, ConditionRow cond)
        {
            string key = StatColumns[cond.CmbStat.SelectedIndex].Key;
            bool byRatio = cond.CmbMetric.SelectedIndex == 1;
            bool lessOrEqual = cond.CmbOperator.SelectedIndex == 1; // 0="<", 1="<="
            double threshold = (double)cond.NudThreshold.Value;

            double val = byRatio ? chain.Ratio(key) * 100.0 : chain.Stats[key].AnyCount;
            return lessOrEqual ? val <= threshold : val < threshold;
        }

        private void UpdateSummary()
        {
            if (_lblSummary == null) return;
            int checkedCount = _grid.Rows.Cast<DataGridViewRow>().Count(r => r.Cells["chk"].Value is true);
            int visibleCount = _grid.Rows.Cast<DataGridViewRow>().Count(r => r.Visible);
            _lblSummary.Text = $"已勾選排除：{checkedCount} / {_grid.Rows.Count} 位角色（目前列表顯示 {visibleCount} 位）";
        }

        private HashSet<string> CollectResult()
        {
            // 保留不在角色清單中的原有 ID，只更新清單內角色
            var result = new HashSet<string>(_initialPending.Where(id => !_listedIds.Contains(id)));
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Cells["chk"].Value is true)
                    result.Add(((RowInfo)row.Tag!).CharacterId);
            }
            return result;
        }
    }
}
