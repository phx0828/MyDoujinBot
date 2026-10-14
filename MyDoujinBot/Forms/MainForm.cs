using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Toolkit.Uwp.Notifications;
using MyDoujinBot.Models;
using MyDoujinBot.Services;
using MyDoujinBot.Utilities;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// MyDoujin Bot 主視窗。
    /// 採用「左側導航頁籤 (Sidebar) + 主工作畫布 (Main Canvas) + 底部精簡狀態列與 Tooltip + 抽屜式日誌 (Log Drawer)」架構。
    /// 全面使用彈性容器（TableLayoutPanel / FlowLayoutPanel），不寫死特定 PX，自適應 DPI 與視窗縮放。
    /// </summary>
    public partial class MainForm : Form
    {
        // =====================================================================
        // 核心狀態與 Service
        // =====================================================================
        private string _currentToken = string.Empty;
        private readonly TrainingService _trainingService = new();
        private readonly EventService _eventService = new();
        private readonly BattleService _battleService = new();

        private TrainingLoop? _trainingLoop;
        private BattleLoop? _battleLoop;
        private PipelineLoop? _pipelineLoop;
        private EventSelectionForm? _currentEventForm;

        private CancellationTokenSource? _cts;
        private readonly System.Windows.Forms.Timer _elapsedTimer = new() { Interval = 1000 };
        private DateTime _loopStartTime;

        // 當前運作中的模式 ("training", "battle", "pipeline")
        private string _activeRunningMode = string.Empty;
        private string _selectedNavTab = "training";

        // 最後戰報快取
        private BattleResult? _lastBattleResult;
        private string _targetPlayerName = string.Empty;

        // 系統托盤
        private NotifyIcon _notifyIcon = null!;

        // =====================================================================
        // UI 控制項宣告
        // =====================================================================

        // --- 根排版容器 ---
        private TableLayoutPanel tblRoot = null!;

        // --- 側邊導航 ---
        private Panel pnlSidebar = null!;
        private TableLayoutPanel pnlNav = null!;
        private Label lblSidebarToken = null!;
        private Button btnNavTraining = null!;
        private Button btnNavBattle = null!;
        private Button btnNavPipeline = null!;
        private Button btnNavEvents = null!;
        private Button btnNavStats = null!;
        private Button btnNavSettings = null!;
        private readonly List<Button> _navButtons = new();

        // --- 主畫布與分頁 Panel ---
        private Panel pnlMainCanvas = null!;
        private Panel pnlViewTraining = null!;
        private Panel pnlViewBattle = null!;
        private Panel pnlViewPipeline = null!;
        private Panel pnlViewEvents = null!;
        private Panel pnlViewStats = null!;
        private Panel pnlViewSettings = null!;
        private Panel pnlEventOverlay = null!;

        // --- 底部狀態列 ---
        private Panel pnlStatusBar = null!;
        private Label lblStatusBadge = null!;
        private Label lblStatusProgress = null!;
        private Label lblStatusLevel = null!;
        private Label lblStatusEvents = null!;
        private Label lblStatusPipeline = null!;
        private Label lblStatusElapsed = null!;
        private Button btnToggleLog = null!;
        private Button btnGlobalStart = null!;
        private Button btnGlobalStop = null!;
        private ToolTip statusToolTip = null!;

        // --- 底部可收合日誌抽屜 ---
        private Panel pnlLogDrawer = null!;
        private RichTextBox rtbLog = null!;
        private CheckBox chkLogAutoScroll = null!;
        private int _logLineCount = 0;

        // --- 分頁 1: 自動訓練控制項 ---
        private ComboBox cmbAction = null!;
        private ListBox lstActionSequence = null!;
        private Button btnAddAction = null!;
        private Button btnClearSequence = null!;
        private RadioButton rbTrainCount = null!;
        private RadioButton rbTrainTime = null!;
        private NumericUpDown nudTrainCount = null!;
        private NumericUpDown nudTrainMinutes = null!;
        private NumericUpDown nudTrainExtraDelay = null!;

        // --- 分頁 2: 自動戰鬥控制項 ---
        private TextBox txtTargetPlayerId = null!;
        private Button btnGetPlayerInfo = null!;
        private Label lblPlayerInfo = null!;
        private RadioButton rbBattleChallenge = null!;
        private RadioButton rbBattleChado = null!;
        private RadioButton rbBattleCount = null!;
        private RadioButton rbBattleTime = null!;
        private NumericUpDown nudBattleCount = null!;
        private NumericUpDown nudBattleMinutes = null!;
        private NumericUpDown nudBattleExtraDelay = null!;
        private Button btnShowLastReport = null!;

        // --- 分頁 3: 階梯排程控制項 ---
        private TableLayoutPanel tblStageList = null!;
        private TextBox txtNewStageName = null!;
        private ComboBox cmbNewStageActionType = null!;
        private ComboBox cmbNewStageTrainAction = null!;
        private TextBox txtNewStageBattleTarget = null!;
        private ComboBox cmbNewStageBattleType = null!;
        private ComboBox cmbNewStageConditionType = null!;
        private NumericUpDown nudNewStageConditionValue = null!;
        private Button btnAddStage = null!;
        private Button btnClearStages = null!;
        private NumericUpDown nudPipelineDelay = null!;
        private Panel pnlNewStageTrainSub = null!;
        private Panel pnlNewStageBattleSub = null!;

        // --- 分頁 4: 事件應對控制項 ---
        private RadioButton rbEventAutoHighest = null!;
        private RadioButton rbEventAutoRandom = null!;
        private RadioButton rbEventManualInline = null!;
        private RadioButton rbEventManualPopup = null!;
        private CheckBox chkEventNotify = null!;
        private CheckBox chkEventNotifySound = null!;

        // --- 分頁 5: 戰報分析控制項 ---
        private TextBox txtStatsReportId = null!;
        private Button btnStatsQuery = null!;
        private RichTextBox rtbStatsOutput = null!;
        private Label lblStatsStatus = null!;

        // --- 分頁 6: 系統設定控制項 ---
        private TextBox txtSettingsToken = null!;
        private Button btnSaveSettingsToken = null!;
        private Label lblTokenStatusDetail = null!;

        // 統計暫存
        private LoopStats _currentStats = new();

        public MainForm()
        {
            this.Text = $"MyDoujin Bot v{Application.ProductVersion}";
            var iconPath = ResolveAppIconPath();
            if (iconPath != null)
            {
                try { this.Icon = new Icon(iconPath); } catch { }
            }
            this.Size = new Size(1060, 720);
            this.MinimumSize = new Size(950, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(24, 24, 30);
            this.ForeColor = Color.FromArgb(220, 220, 230);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.KeyPreview = true;

            // DPI 自動縮放
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            // 讀取持久化設定
            AppSettingsManager.Load();
            _currentToken = AppSettingsManager.Current.Token;
            _selectedNavTab = string.IsNullOrEmpty(AppSettingsManager.Current.ActiveTab) ? "training" : AppSettingsManager.Current.ActiveTab;

            InitializeUI();
            InitializeNotifyIcon();
            RestoreSavedSettings();

            this.KeyDown += OnFormKeyDown;
        }

        private void OnFormKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5 && btnGlobalStart.Enabled)
            {
                e.Handled = true;
                btnGlobalStart.PerformClick();
            }
            else if (e.KeyCode == Keys.F6 && btnGlobalStop.Enabled)
            {
                e.Handled = true;
                btnGlobalStop.PerformClick();
            }
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = this.Icon ?? SystemIcons.Application,
                Text = "MyDoujin Bot",
                Visible = true
            };
            _notifyIcon.DoubleClick += (_, _) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            };
        }

        // DPI 縮放 Helper (支援 100%、125%、150%、200% 等任意螢幕縮放)
        private float DpiScale => (float)this.DeviceDpi / 96f;
        private int ScaleDpi(int value) => (int)Math.Round(value * DpiScale);
        private float ScaleDpi(float value) => value * DpiScale;

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            ApplyDpiLayout();
        }

        private void ApplyDpiLayout()
        {
            if (tblRoot == null) return;
            tblRoot.ColumnStyles[0].Width = ScaleDpi(195F);
            tblRoot.RowStyles[2].Height = ScaleDpi(40F);
            if (pnlLogDrawer != null && pnlLogDrawer.Visible)
            {
                tblRoot.RowStyles[1].Height = ScaleDpi(170F);
            }
            if (pnlNav != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    if (i < pnlNav.RowStyles.Count)
                        pnlNav.RowStyles[i].Height = ScaleDpi(42F);
                }
            }
        }

        // =====================================================================
        // UI 根佈局建立
        // =====================================================================
        private void InitializeUI()
        {
            this.SuspendLayout();

            statusToolTip = new ToolTip
            {
                AutoPopDelay = 8000,
                InitialDelay = 300,
                ReshowDelay = 150,
                BackColor = Color.FromArgb(35, 35, 45),
                ForeColor = Color.FromArgb(230, 230, 240)
            };

            // ── 根容器：TableLayoutPanel（2欄 3列，DPI 自適應）──
            // Col 0: 側邊導航欄 (基底 195px，隨 DPI 動態縮放：100%->195, 150%->293, 200%->390)
            // Col 1: 主內容畫布 (100% 彈性填滿其餘寬度)
            // Row 0: 工作畫布 (100% 彈性高度)
            // Row 1: 日誌抽屜 (收合時 0px，展開時 ScaleDpi(170px))
            // Row 2: 底部狀態列 (ScaleDpi(40px))
            tblRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Color.FromArgb(24, 24, 30),
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleDpi(195F)));
            tblRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleDpi(40F)));
            this.Controls.Add(tblRoot);

            // 建立各面板並加入 tblRoot
            BuildSidebar();
            BuildStatusBar();
            BuildLogDrawer();
            BuildMainCanvas();

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        // =====================================================================
        // 1. 底部精簡狀態列 (Status Bar)
        // =====================================================================
        private void BuildStatusBar()
        {
            pnlStatusBar = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 38,
                BackColor = Color.FromArgb(18, 18, 24),
                Padding = new Padding(ScaleDpi(12), ScaleDpi(3), ScaleDpi(12), ScaleDpi(3)),
                Margin = Padding.Empty
            };
            tblRoot.Controls.Add(pnlStatusBar, 0, 2);
            tblRoot.SetColumnSpan(pnlStatusBar, 2);

            var tblStatus = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 10,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblStatus.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 0: 狀態
            tblStatus.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 1: 進度
            tblStatus.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 2: 等級
            tblStatus.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 3: 事件
            tblStatus.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 4: 排程階段
            tblStatus.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 5: 運作時間
            tblStatus.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // 6: Spacer
            tblStatus.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 7: 日誌按鈕
            tblStatus.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 8: 開始
            tblStatus.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // 9: 停止
            tblStatus.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pnlStatusBar.Controls.Add(tblStatus);

            lblStatusBadge = new Label
            {
                Text = "● 已停止",
                ForeColor = Color.FromArgb(160, 160, 175),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 14, 0)
            };
            tblStatus.Controls.Add(lblStatusBadge, 0, 0);

            lblStatusProgress = new Label
            {
                Text = "進度: 0 / 500 次",
                ForeColor = Color.FromArgb(200, 200, 215),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 14, 0)
            };
            tblStatus.Controls.Add(lblStatusProgress, 1, 0);

            lblStatusLevel = new Label
            {
                Text = "等級: Lv.—",
                ForeColor = Color.FromArgb(255, 215, 80),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 14, 0)
            };
            tblStatus.Controls.Add(lblStatusLevel, 2, 0);

            lblStatusEvents = new Label
            {
                Text = "事件: 0 次",
                ForeColor = Color.FromArgb(190, 160, 255),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 14, 0)
            };
            tblStatus.Controls.Add(lblStatusEvents, 3, 0);

            lblStatusPipeline = new Label
            {
                Text = "排程: 階段 1/1",
                ForeColor = Color.FromArgb(140, 200, 255),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Visible = false,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 14, 0)
            };
            tblStatus.Controls.Add(lblStatusPipeline, 4, 0);

            lblStatusElapsed = new Label
            {
                Text = "運作: 00:00:00",
                ForeColor = Color.FromArgb(160, 160, 180),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 14, 0)
            };
            tblStatus.Controls.Add(lblStatusElapsed, 5, 0);

            // Spacer
            tblStatus.Controls.Add(new Label { Text = "", AutoSize = false }, 6, 0);

            btnToggleLog = new Button
            {
                Text = "▲ 展開日誌",
                AutoSize = true,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(200, 200, 220),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Margin = new Padding(0, 0, 10, 0)
            };
            btnToggleLog.FlatAppearance.BorderSize = 0;
            btnToggleLog.Click += (s, e) => ToggleLogDrawer();
            tblStatus.Controls.Add(btnToggleLog, 7, 0);

            btnGlobalStart = new Button
            {
                Text = "▶ 開始 (F5)",
                AutoSize = true,
                BackColor = Color.FromArgb(35, 140, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Padding = new Padding(12, 2, 12, 2),
                Margin = new Padding(0, 0, 6, 0)
            };
            btnGlobalStart.FlatAppearance.BorderSize = 0;
            btnGlobalStart.Click += OnStartClicked;
            tblStatus.Controls.Add(btnGlobalStart, 8, 0);

            btnGlobalStop = new Button
            {
                Text = "⏹ 停止 (F6)",
                AutoSize = true,
                BackColor = Color.FromArgb(150, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false,
                Padding = new Padding(12, 2, 12, 2),
                Margin = Padding.Empty
            };
            btnGlobalStop.FlatAppearance.BorderSize = 0;
            btnGlobalStop.Click += OnStopClicked;
            tblStatus.Controls.Add(btnGlobalStop, 9, 0);

            UpdateTooltips();
        }

        private void UpdateTooltips()
        {
            double winRate = _currentStats.RunCount > 0
                ? (_currentStats.SuccessCount * 100.0 / _currentStats.RunCount)
                : 0.0;
            statusToolTip.SetToolTip(lblStatusProgress,
                $"已執行總數：{_currentStats.RunCount} 次\n" +
                $"成功 / 勝利：{_currentStats.SuccessCount} 次\n" +
                $"失敗 / 戰敗：{_currentStats.FailCount} 次\n" +
                $"達成率 / 勝率：{winRate:F1}%");

            string gainedCharStr = _currentStats.GainedCharacters.Count > 0
                ? string.Join("、", _currentStats.GainedCharacters)
                : "無";
            statusToolTip.SetToolTip(lblStatusLevel,
                $"當前角色等級：Lv.{_currentStats.CurrentLevel}\n" +
                $"累積獲得 EXP：{_currentStats.TotalExp:N0}\n" +
                $"本次獲得角色：{gainedCharStr}");

            double eventPassRate = _currentStats.EventCount > 0
                ? (_currentStats.EventSuccessCount * 100.0 / _currentStats.EventCount)
                : 0.0;
            statusToolTip.SetToolTip(lblStatusEvents,
                $"遭遇事件總數：{_currentStats.EventCount} 次\n" +
                $"事件判定成功：{_currentStats.EventSuccessCount} 次\n" +
                $"事件判定失敗：{_currentStats.EventFailCount} 次\n" +
                $"事件成功率：{eventPassRate:F1}%");

            statusToolTip.SetToolTip(lblStatusBadge,
                $"運作狀態：{lblStatusBadge.Text}\n" +
                $"點擊「開始」即可啟動當前頁籤之任務。");
        }

        // =====================================================================
        // 2. 底部可收合日誌抽屜 (Log Drawer)
        // =====================================================================
        private void BuildLogDrawer()
        {
            pnlLogDrawer = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 170,
                Visible = false,
                BackColor = Color.FromArgb(14, 14, 18),
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            tblRoot.Controls.Add(pnlLogDrawer, 0, 1);
            tblRoot.SetColumnSpan(pnlLogDrawer, 2);

            var pnlLogHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 28,
                BackColor = Color.FromArgb(25, 25, 34),
                Padding = new Padding(10, 0, 10, 0)
            };
            pnlLogDrawer.Controls.Add(pnlLogHeader);

            var lblLogTitle = new Label
            {
                Text = "運行日誌 (Live Log)",
                ForeColor = Color.FromArgb(180, 180, 200),
                Font = new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold),
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            };
            pnlLogHeader.Controls.Add(lblLogTitle);

            var pnlLogActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };
            pnlLogHeader.Controls.Add(pnlLogActions);

            chkLogAutoScroll = new CheckBox
            {
                Text = "自動捲動",
                Checked = true,
                ForeColor = Color.FromArgb(170, 170, 190),
                AutoSize = true,
                Margin = new Padding(0, 4, 12, 0)
            };
            pnlLogActions.Controls.Add(chkLogAutoScroll);

            var btnClearLog = new Button
            {
                Text = "清空",
                AutoSize = true,
                BackColor = Color.FromArgb(45, 45, 58),
                ForeColor = Color.FromArgb(200, 200, 215),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                Margin = new Padding(0, 2, 6, 0)
            };
            btnClearLog.FlatAppearance.BorderSize = 0;
            btnClearLog.Click += (s, e) => { rtbLog.Clear(); _logLineCount = 0; };
            pnlLogActions.Controls.Add(btnClearLog);

            var btnCloseLog = new Button
            {
                Text = "✕ 隱藏",
                AutoSize = true,
                BackColor = Color.FromArgb(45, 45, 58),
                ForeColor = Color.FromArgb(200, 200, 215),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                Margin = new Padding(0, 2, 0, 0)
            };
            btnCloseLog.FlatAppearance.BorderSize = 0;
            btnCloseLog.Click += (s, e) => ToggleLogDrawer();
            pnlLogActions.Controls.Add(btnCloseLog);

            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 12, 16),
                ForeColor = Color.FromArgb(210, 210, 225),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5f),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            pnlLogDrawer.Controls.Add(rtbLog);
            rtbLog.BringToFront();
        }

        private void ToggleLogDrawer()
        {
            bool show = !pnlLogDrawer.Visible;
            pnlLogDrawer.Visible = show;
            tblRoot.RowStyles[1].Height = show ? ScaleDpi(170F) : 0F;
            btnToggleLog.Text = show ? "▼ 收合日誌" : "▲ 展開日誌";
        }

        // =====================================================================
        // 3. 左側功能導航欄 (Sidebar Navigation)
        // =====================================================================
        private void BuildSidebar()
        {
            pnlSidebar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 16, 22),
                Padding = new Padding(ScaleDpi(6), ScaleDpi(10), ScaleDpi(6), ScaleDpi(8)),
                Margin = Padding.Empty
            };
            tblRoot.Controls.Add(pnlSidebar, 0, 0);

            var tblSidebarLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblSidebarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tblSidebarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // Row 0: Brand
            tblSidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Row 1: Nav buttons
            tblSidebarLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // Row 2: Token status
            pnlSidebar.Controls.Add(tblSidebarLayout);

            // 標題區域
            var pnlBrand = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(ScaleDpi(4), 0, ScaleDpi(4), ScaleDpi(10)),
                Padding = Padding.Empty
            };
            pnlBrand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pnlBrand.Controls.Add(new Label
            {
                Text = "⚡ MyDoujin Bot",
                ForeColor = Color.FromArgb(240, 240, 250),
                Font = new Font("Microsoft JhengHei UI", 11f, FontStyle.Bold),
                AutoSize = true,
                Margin = Padding.Empty
            }, 0, 0);
            pnlBrand.Controls.Add(new Label
            {
                Text = "自動化工作站 v1.3.0",
                ForeColor = Color.FromArgb(120, 120, 140),
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                AutoSize = true,
                Margin = new Padding(0, ScaleDpi(2), 0, 0)
            }, 0, 1);
            tblSidebarLayout.Controls.Add(pnlBrand, 0, 0);

            // 導航按鈕容器 (使用 TableLayoutPanel 保證按鈕 100% 寬度自適應填滿，無寫死固定寬度)
            pnlNav = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            pnlNav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 6; i++)
            {
                pnlNav.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleDpi(42F)));
            }
            pnlNav.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 剩餘空間填充
            tblSidebarLayout.Controls.Add(pnlNav, 0, 1);

            btnNavTraining  = CreateNavButton("🎯  自動訓練", "training");
            btnNavBattle    = CreateNavButton("⚔️  自動戰鬥", "battle");
            btnNavPipeline  = CreateNavButton("🔄  階梯排程", "pipeline");
            btnNavEvents    = CreateNavButton("📜  事件應對", "events");
            btnNavStats     = CreateNavButton("📊  戰報分析", "stats");
            btnNavSettings  = CreateNavButton("⚙️  連線設定", "settings");

            pnlNav.Controls.Add(btnNavTraining, 0, 0);
            pnlNav.Controls.Add(btnNavBattle, 0, 1);
            pnlNav.Controls.Add(btnNavPipeline, 0, 2);
            pnlNav.Controls.Add(btnNavEvents, 0, 3);
            pnlNav.Controls.Add(btnNavStats, 0, 4);
            pnlNav.Controls.Add(btnNavSettings, 0, 5);

            // 底部 Token 狀態指示
            lblSidebarToken = new Label
            {
                Dock = DockStyle.Fill,
                Text = "● Token 尚未設定",
                ForeColor = Color.FromArgb(220, 90, 90),
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                Height = ScaleDpi(32),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, ScaleDpi(4), 0, 0)
            };
            lblSidebarToken.Click += (s, e) => SwitchNavTab("settings");
            tblSidebarLayout.Controls.Add(lblSidebarToken, 0, 2);
        }

        private Button CreateNavButton(string text, string tabKey)
        {
            var btn = new Button
            {
                Text = text,
                Tag = tabKey,
                Dock = DockStyle.Fill,
                Height = ScaleDpi(38),
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(170, 170, 190),
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(12), 0, 0, 0),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Cursor = Cursors.Hand,
                Margin = new Padding(ScaleDpi(2), ScaleDpi(2), ScaleDpi(2), ScaleDpi(2))
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => SwitchNavTab(tabKey);
            btn.MouseEnter += (s, e) =>
            {
                if ((string)btn.Tag! != _selectedNavTab)
                    btn.BackColor = Color.FromArgb(28, 30, 42);
            };
            btn.MouseLeave += (s, e) =>
            {
                if ((string)btn.Tag! != _selectedNavTab)
                    btn.BackColor = Color.Transparent;
            };
            _navButtons.Add(btn);
            return btn;
        }

        private void SwitchNavTab(string tabKey)
        {
            _selectedNavTab = tabKey;
            AppSettingsManager.Current.ActiveTab = tabKey;
            AppSettingsManager.Save();

            foreach (var btn in _navButtons)
            {
                bool isActive = (string)btn.Tag! == tabKey;
                btn.BackColor = isActive ? Color.FromArgb(40, 45, 65) : Color.Transparent;
                btn.ForeColor = isActive ? Color.White : Color.FromArgb(170, 170, 190);
                btn.Font = new Font("Microsoft JhengHei UI", 9.5f, isActive ? FontStyle.Bold : FontStyle.Regular);
            }

            pnlViewTraining.Visible = tabKey == "training";
            pnlViewBattle.Visible   = tabKey == "battle";
            pnlViewPipeline.Visible = tabKey == "pipeline";
            pnlViewEvents.Visible   = tabKey == "events";
            pnlViewStats.Visible    = tabKey == "stats";
            pnlViewSettings.Visible = tabKey == "settings";

            if (tabKey == "training") pnlViewTraining.BringToFront();
            else if (tabKey == "battle") pnlViewBattle.BringToFront();
            else if (tabKey == "pipeline") pnlViewPipeline.BringToFront();
            else if (tabKey == "events") pnlViewEvents.BringToFront();
            else if (tabKey == "stats") pnlViewStats.BringToFront();
            else if (tabKey == "settings") pnlViewSettings.BringToFront();

            // 依目前頁籤動態更新開始按鈕文字
            if (_cts == null && btnGlobalStart != null)
            {
                btnGlobalStart.Text = tabKey switch
                {
                    "battle"   => "▶ 開始戰鬥 (F5)",
                    "pipeline" => "▶ 開始排程 (F5)",
                    _          => "▶ 開始訓練 (F5)"
                };
            }
        }

        // =====================================================================
        // 4. 中央主工作畫布 (Main Canvas) 與各頁籤建立
        // =====================================================================
        private void BuildMainCanvas()
        {
            pnlMainCanvas = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 24, 30),
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            tblRoot.Controls.Add(pnlMainCanvas, 1, 0);

            // 建立各頁籤檢視
            pnlViewTraining = BuildTrainingView();
            pnlViewBattle   = BuildBattleView();
            pnlViewPipeline = BuildPipelineView();
            pnlViewEvents   = BuildEventsView();
            pnlViewStats    = BuildStatsView();
            pnlViewSettings = BuildSettingsView();

            // 內嵌事件 Overlay（覆蓋於主畫布）
            pnlEventOverlay = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 28),
                Visible = false,
                Padding = Padding.Empty
            };

            pnlMainCanvas.Controls.Add(pnlViewTraining);
            pnlMainCanvas.Controls.Add(pnlViewBattle);
            pnlMainCanvas.Controls.Add(pnlViewPipeline);
            pnlMainCanvas.Controls.Add(pnlViewEvents);
            pnlMainCanvas.Controls.Add(pnlViewStats);
            pnlMainCanvas.Controls.Add(pnlViewSettings);
            pnlMainCanvas.Controls.Add(pnlEventOverlay);

            SwitchNavTab(_selectedNavTab);
        }


        // ─────────────────────────────────────────────────────────────────────
        // 頁籤 1: 自動訓練檢視
        // ─────────────────────────────────────────────────────────────────────
        private Panel BuildTrainingView()
        {
            var outer = CreateScrollableTabPanel();
            var tbl = CreateContentTable(outer);

            AddSectionHeader(tbl, "🎯  自動訓練設定");

            AddRowLabel(tbl, "選擇訓練項目：");
            cmbAction = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 2, 0, 6)
            };
            foreach (var action in TrainingActions.All) cmbAction.Items.Add(action);
            if (cmbAction.Items.Count > 0) cmbAction.SelectedIndex = 0;
            TblAdd(tbl, cmbAction);

            // 加入/清除清單按鈕
            btnAddAction = new Button
            {
                Text = "➕ 加入循環清單",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 65, 100),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnAddAction.FlatAppearance.BorderSize = 0;
            btnAddAction.Click += (s, e) =>
            {
                if (cmbAction.SelectedItem is TrainingAction action)
                {
                    lstActionSequence.Items.Add(action);
                    SaveTrainingSequence();
                }
            };

            btnClearSequence = new Button
            {
                Text = "🗑️ 清空清單",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(85, 45, 45),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnClearSequence.FlatAppearance.BorderSize = 0;
            btnClearSequence.Click += (s, e) =>
            {
                lstActionSequence.Items.Clear();
                SaveTrainingSequence();
            };
            TblAdd(tbl, MakeHRow(btnAddAction, btnClearSequence));

            // 輪流清單 ListBox
            lstActionSequence = new ListBox
            {
                Dock = DockStyle.Fill,
                Height = 85,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 0, 10)
            };
            TblAdd(tbl, lstActionSequence);

            AddSectionHeader(tbl, "執行限制與冷卻延遲");
            rbTrainCount = new RadioButton { Text = "執行指定次數", Checked = true, ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true };
            rbTrainTime  = new RadioButton { Text = "執行指定時間", ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true, Margin = new Padding(12, 0, 0, 0) };
            rbTrainCount.CheckedChanged += (s, e) => OnTrainExecutionModeChanged();
            rbTrainTime.CheckedChanged  += (s, e) => OnTrainExecutionModeChanged();
            TblAdd(tbl, MakeHRow(rbTrainCount, rbTrainTime));

            nudTrainCount   = new NumericUpDown { Width = 110, Minimum = 1, Maximum = 99999, Value = 500, BackColor = Color.FromArgb(40, 40, 52), ForeColor = Color.FromArgb(220, 220, 230) };
            nudTrainMinutes = new NumericUpDown { Width = 110, Minimum = 1, Maximum = 9999,  Value = 1440, BackColor = Color.FromArgb(40, 40, 52), ForeColor = Color.FromArgb(220, 220, 230), Visible = false };
            TblAdd(tbl, MakeHRow(nudTrainCount, MakeLabel("次"), nudTrainMinutes, MakeLabel("分鐘", visible: false)));

            AddRowLabel(tbl, "防封號安全隨機延遲：");
            nudTrainExtraDelay = new NumericUpDown { Width = 110, Minimum = 0, Maximum = 60, Value = 2, BackColor = Color.FromArgb(40, 40, 52), ForeColor = Color.FromArgb(220, 220, 230) };
            TblAdd(tbl, MakeHRow(nudTrainExtraDelay, MakeLabel("秒 (0 ~ 此值)")));

            return outer;
        }

        private void OnTrainExecutionModeChanged()
        {
            bool isCount = rbTrainCount.Checked;
            nudTrainCount.Visible = isCount;
            nudTrainMinutes.Visible = !isCount;
        }

        private void SaveTrainingSequence()
        {
            var seq = lstActionSequence.Items.Cast<TrainingAction>().Select(a => a.ActionId).ToList();
            AppSettingsManager.Current.TrainingSequence = seq;
            AppSettingsManager.Save();
        }

        // ─────────────────────────────────────────────────────────────────────
        // 頁籤 2: 自動戰鬥檢視
        // ─────────────────────────────────────────────────────────────────────
        private Panel BuildBattleView()
        {
            var outer = CreateScrollableTabPanel();
            var tbl = CreateContentTable(outer);

            AddSectionHeader(tbl, "⚔️  自動戰鬥設定");

            AddRowLabel(tbl, "戰鬥目標玩家 ID：");
            txtTargetPlayerId = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Text = AppSettingsManager.Current.LastBattleTargetId
            };
            btnGetPlayerInfo = new Button
            {
                Text = "🔍 驗證 ID",
                AutoSize = true,
                BackColor = Color.FromArgb(50, 65, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(6, 0, 0, 0)
            };
            btnGetPlayerInfo.FlatAppearance.BorderSize = 0;
            btnGetPlayerInfo.Click += OnGetPlayerInfoClicked;

            var idRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = Padding.Empty
            };
            idRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            idRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            idRow.Controls.Add(txtTargetPlayerId, 0, 0);
            idRow.Controls.Add(btnGetPlayerInfo, 1, 0);
            TblAdd(tbl, idRow);

            lblPlayerInfo = new Label
            {
                Text = "請輸入玩家 ID 並點擊驗證",
                ForeColor = Color.FromArgb(160, 160, 180),
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 10)
            };
            TblAdd(tbl, lblPlayerInfo);

            AddSectionHeader(tbl, "戰鬥模式");
            rbBattleChallenge = new RadioButton { Text = "友好切磋 (一般切磋)", Checked = true, ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true };
            rbBattleChado     = new RadioButton { Text = "我要茶渡你 (特殊挑戰)", ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true, Margin = new Padding(12, 0, 0, 0) };
            if (AppSettingsManager.Current.BattleMode == "Chado") rbBattleChado.Checked = true;
            TblAdd(tbl, MakeHRow(rbBattleChallenge, rbBattleChado));

            btnShowLastReport = new Button
            {
                Text = "📄 查看最後戰報",
                AutoSize = true,
                BackColor = Color.FromArgb(45, 50, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = false,
                Margin = new Padding(0, 4, 0, 10)
            };
            btnShowLastReport.FlatAppearance.BorderSize = 0;
            btnShowLastReport.Click += (s, e) =>
            {
                if (_lastBattleResult != null)
                {
                    using var f = new BattleReportForm(_lastBattleResult);
                    f.ShowDialog(this);
                }
            };
            TblAdd(tbl, btnShowLastReport);

            AddSectionHeader(tbl, "執行限制與冷卻延遲");
            rbBattleCount = new RadioButton { Text = "執行指定次數", Checked = true, ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true };
            rbBattleTime  = new RadioButton { Text = "執行指定時間", ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true, Margin = new Padding(12, 0, 0, 0) };
            rbBattleCount.CheckedChanged += (s, e) => { nudBattleCount.Visible = rbBattleCount.Checked; nudBattleMinutes.Visible = !rbBattleCount.Checked; };
            TblAdd(tbl, MakeHRow(rbBattleCount, rbBattleTime));

            nudBattleCount   = new NumericUpDown { Width = 110, Minimum = 1, Maximum = 99999, Value = 500, BackColor = Color.FromArgb(40, 40, 52), ForeColor = Color.FromArgb(220, 220, 230) };
            nudBattleMinutes = new NumericUpDown { Width = 110, Minimum = 1, Maximum = 9999,  Value = 1440, BackColor = Color.FromArgb(40, 40, 52), ForeColor = Color.FromArgb(220, 220, 230), Visible = false };
            TblAdd(tbl, MakeHRow(nudBattleCount, MakeLabel("次"), nudBattleMinutes, MakeLabel("分鐘", visible: false)));

            AddRowLabel(tbl, "防封號安全隨機延遲：");
            nudBattleExtraDelay = new NumericUpDown { Width = 110, Minimum = 0, Maximum = 60, Value = 2, BackColor = Color.FromArgb(40, 40, 52), ForeColor = Color.FromArgb(220, 220, 230) };
            TblAdd(tbl, MakeHRow(nudBattleExtraDelay, MakeLabel("秒 (0 ~ 此值)")));

            return outer;
        }

        private async void OnGetPlayerInfoClicked(object? sender, EventArgs e)
        {
            string targetId = txtTargetPlayerId.Text.Trim();
            if (string.IsNullOrEmpty(targetId))
            {
                MessageBox.Show("請先輸入玩家 ID", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_currentToken))
            {
                MessageBox.Show("請先至「連線設定」設定 Bearer Token", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnGetPlayerInfo.Enabled = false;
            lblPlayerInfo.Text = "查詢中...";
            try
            {
                var result = await _battleService.GetPlayerProfileAsync(targetId, _currentToken);
                if (result.IsSuccess && result.Data != null)
                {
                    var data = result.Data;
                    string cName = data.Character != null ? data.Character.Name : "無";
                    int cLevel = data.Character != null ? data.Character.Level : 0;
                    lblPlayerInfo.Text = $"對手：{data.Nickname} ｜ 角色：Lv.{cLevel} {cName}";
                    lblPlayerInfo.ForeColor = Color.FromArgb(80, 220, 120);
                    _targetPlayerName = data.Nickname ?? string.Empty;
                }
                else
                {
                    lblPlayerInfo.Text = $"查詢失敗：{result.ErrorMessage}";
                    lblPlayerInfo.ForeColor = Color.FromArgb(220, 80, 80);
                }
            }
            finally
            {
                btnGetPlayerInfo.Enabled = true;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 頁籤 3: 階梯行動排程檢視 (Pipeline Scheduler)
        // ─────────────────────────────────────────────────────────────────────
        private Panel BuildPipelineView()
        {
            var outer = CreateScrollableTabPanel();
            var tbl = CreateContentTable(outer);

            AddSectionHeader(tbl, "🔄  階梯行動排程 (Pipeline)");

            var lblDesc = new Label
            {
                Text = "依序執行階段隊列，達成指定目標後自動平滑切換至下一階段，直到全部完成。",
                ForeColor = Color.FromArgb(160, 160, 180),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            TblAdd(tbl, lblDesc);

            // 階段列表容器
            tblStageList = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 0,
                AutoSize = true,
                BackColor = Color.FromArgb(20, 20, 26),
                Padding = new Padding(6),
                Margin = new Padding(0, 0, 0, 8)
            };
            tblStageList.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            TblAdd(tbl, tblStageList);

            // 清空排程按鈕
            btnClearStages = new Button
            {
                Text = "🗑️ 清空所有階段",
                AutoSize = true,
                BackColor = Color.FromArgb(85, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 12)
            };
            btnClearStages.FlatAppearance.BorderSize = 0;
            btnClearStages.Click += (s, e) =>
            {
                AppSettingsManager.Current.PipelineStages.Clear();
                AppSettingsManager.Save();
                RefreshPipelineStageList();
            };
            TblAdd(tbl, btnClearStages);

            AddSectionHeader(tbl, "➕  新增排程階段");

            AddRowLabel(tbl, "階段名稱：");
            txtNewStageName = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Text = "階段 1"
            };
            TblAdd(tbl, txtNewStageName);

            AddRowLabel(tbl, "執行動作類型：");
            cmbNewStageActionType = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat
            };
            cmbNewStageActionType.Items.Add("訓練 (Training)");
            cmbNewStageActionType.Items.Add("戰鬥 (Battle)");
            cmbNewStageActionType.SelectedIndex = 0;
            cmbNewStageActionType.SelectedIndexChanged += (s, e) =>
            {
                bool isTrain = cmbNewStageActionType.SelectedIndex == 0;
                pnlNewStageTrainSub.Visible = isTrain;
                pnlNewStageBattleSub.Visible = !isTrain;
            };
            TblAdd(tbl, cmbNewStageActionType);

            // 訓練子選項
            pnlNewStageTrainSub = new Panel { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(0, 2, 0, 4) };
            cmbNewStageTrainAction = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat
            };
            foreach (var act in TrainingActions.All) cmbNewStageTrainAction.Items.Add(act);
            if (cmbNewStageTrainAction.Items.Count > 0) cmbNewStageTrainAction.SelectedIndex = 0;
            pnlNewStageTrainSub.Controls.Add(cmbNewStageTrainAction);
            TblAdd(tbl, pnlNewStageTrainSub);

            // 戰鬥子選項
            pnlNewStageBattleSub = new Panel { Dock = DockStyle.Fill, AutoSize = true, Visible = false, Margin = new Padding(0, 2, 0, 4) };
            var tblBattleSub = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1 };
            tblBattleSub.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            tblBattleSub.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            txtNewStageBattleTarget = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "輸入戰鬥對象玩家 ID"
            };
            cmbNewStageBattleType = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat
            };
            cmbNewStageBattleType.Items.Add("友好切磋");
            cmbNewStageBattleType.Items.Add("我要茶渡你");
            cmbNewStageBattleType.SelectedIndex = 0;
            tblBattleSub.Controls.Add(txtNewStageBattleTarget, 0, 0);
            tblBattleSub.Controls.Add(cmbNewStageBattleType, 1, 0);
            pnlNewStageBattleSub.Controls.Add(tblBattleSub);
            TblAdd(tbl, pnlNewStageBattleSub);

            AddRowLabel(tbl, "切換至下一階段之達成條件：");
            var tblCondition = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1 };
            tblCondition.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            tblCondition.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            cmbNewStageConditionType = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat
            };
            cmbNewStageConditionType.Items.Add("角色等級達到 (>= Lv)");
            cmbNewStageConditionType.Items.Add("執行滿次數 (次)");
            cmbNewStageConditionType.Items.Add("運行滿時間 (分鐘)");
            cmbNewStageConditionType.SelectedIndex = 0;

            nudNewStageConditionValue = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 1, Maximum = 99999, Value = 80,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230)
            };
            tblCondition.Controls.Add(cmbNewStageConditionType, 0, 0);
            tblCondition.Controls.Add(nudNewStageConditionValue, 1, 0);
            TblAdd(tbl, tblCondition);

            btnAddStage = new Button
            {
                Text = "➕ 加入排程清單",
                AutoSize = true,
                BackColor = Color.FromArgb(40, 120, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 6, 0, 10)
            };
            btnAddStage.FlatAppearance.BorderSize = 0;
            btnAddStage.Click += OnAddStageClicked;
            TblAdd(tbl, btnAddStage);

            AddRowLabel(tbl, "排程冷卻額外隨機延遲：");
            nudPipelineDelay = new NumericUpDown { Width = 110, Minimum = 0, Maximum = 60, Value = 2, BackColor = Color.FromArgb(40, 40, 52), ForeColor = Color.FromArgb(220, 220, 230) };
            TblAdd(tbl, MakeHRow(nudPipelineDelay, MakeLabel("秒 (0 ~ 此值)")));

            RefreshPipelineStageList();
            return outer;
        }

        private void OnAddStageClicked(object? sender, EventArgs e)
        {
            var stage = new PipelineStage
            {
                Name = string.IsNullOrWhiteSpace(txtNewStageName.Text) ? $"階段 {AppSettingsManager.Current.PipelineStages.Count + 1}" : txtNewStageName.Text.Trim(),
                ActionType = cmbNewStageActionType.SelectedIndex == 0 ? StageActionType.Training : StageActionType.Battle,
                ConditionValue = (int)nudNewStageConditionValue.Value
            };

            if (stage.ActionType == StageActionType.Training)
            {
                if (cmbNewStageTrainAction.SelectedItem is TrainingAction act)
                    stage.TrainingActionId = act.ActionId;
            }
            else
            {
                stage.BattleTargetId = txtNewStageBattleTarget.Text.Trim();
                stage.BattleType = cmbNewStageBattleType.SelectedIndex == 0 ? BattleType.Challenge : BattleType.Chado;
                if (string.IsNullOrEmpty(stage.BattleTargetId))
                {
                    MessageBox.Show("請輸入戰鬥目標玩家 ID", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            stage.ConditionType = cmbNewStageConditionType.SelectedIndex switch
            {
                1 => StageConditionType.RunCountReach,
                2 => StageConditionType.MinutesReach,
                _ => StageConditionType.LevelReach
            };

            AppSettingsManager.Current.PipelineStages.Add(stage);
            AppSettingsManager.Save();

            txtNewStageName.Text = $"階段 {AppSettingsManager.Current.PipelineStages.Count + 1}";
            RefreshPipelineStageList();
        }

        private void RefreshPipelineStageList()
        {
            tblStageList.SuspendLayout();
            tblStageList.Controls.Clear();
            tblStageList.RowCount = 0;

            var stages = AppSettingsManager.Current.PipelineStages;
            if (stages.Count == 0)
            {
                tblStageList.Controls.Add(new Label
                {
                    Text = "尚未新增任何階段。請使用下方表單新增排程階段。",
                    ForeColor = Color.FromArgb(140, 140, 160),
                    AutoSize = true,
                    Padding = new Padding(6)
                });
            }
            else
            {
                for (int i = 0; i < stages.Count; i++)
                {
                    int index = i;
                    var st = stages[i];

                    var card = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 3,
                        RowCount = 1,
                        AutoSize = true,
                        BackColor = Color.FromArgb(32, 32, 42),
                        Padding = new Padding(10, 6, 10, 6),
                        Margin = new Padding(0, 2, 0, 4)
                    };
                    card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                    card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

                    var lblIdx = new Label
                    {
                        Text = $"#{index + 1}",
                        ForeColor = Color.FromArgb(130, 170, 255),
                        Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                        AutoSize = true,
                        Anchor = AnchorStyles.Left
                    };

                    var lblInfo = new Label
                    {
                        Text = $"【{st.Name}】 {st.GetActionSummary()}  ──►  達成條件：{st.GetConditionSummary()}",
                        ForeColor = Color.FromArgb(220, 220, 235),
                        AutoSize = true,
                        Anchor = AnchorStyles.Left,
                        Margin = new Padding(10, 0, 0, 0)
                    };

                    var btnDel = new Button
                    {
                        Text = "✕",
                        Width = 26, Height = 24,
                        BackColor = Color.FromArgb(70, 40, 40),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Cursor = Cursors.Hand
                    };
                    btnDel.FlatAppearance.BorderSize = 0;
                    btnDel.Click += (s, e) =>
                    {
                        AppSettingsManager.Current.PipelineStages.RemoveAt(index);
                        AppSettingsManager.Save();
                        RefreshPipelineStageList();
                    };

                    card.Controls.Add(lblIdx, 0, 0);
                    card.Controls.Add(lblInfo, 1, 0);
                    card.Controls.Add(btnDel, 2, 0);

                    tblStageList.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    tblStageList.Controls.Add(card, 0, index);
                }
            }

            tblStageList.ResumeLayout();
        }

        // ─────────────────────────────────────────────────────────────────────
        // 頁籤 4: 事件應對偏好檢視 (Events View)
        // ─────────────────────────────────────────────────────────────────────
        private Panel BuildEventsView()
        {
            var outer = CreateScrollableTabPanel();
            var tbl = CreateContentTable(outer);

            AddSectionHeader(tbl, "📜  遭遇事件應對偏好");

            rbEventAutoHighest  = new RadioButton { Text = "自動選擇（推薦：優先選最高成功率）", Checked = true, ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true };
            rbEventAutoRandom   = new RadioButton { Text = "自動選擇（完全隨機）", ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            rbEventManualInline = new RadioButton { Text = "手動選擇（零干擾內嵌於主畫面，不奪取焦點）", ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            rbEventManualPopup  = new RadioButton { Text = "手動選擇（獨立彈出視窗，奪取焦點）", ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true, Margin = new Padding(0, 4, 0, 10) };

            rbEventAutoHighest.CheckedChanged  += (_, _) => SaveEventPreferences();
            rbEventAutoRandom.CheckedChanged   += (_, _) => SaveEventPreferences();
            rbEventManualInline.CheckedChanged += (_, _) => SaveEventPreferences();
            rbEventManualPopup.CheckedChanged  += (_, _) => SaveEventPreferences();

            TblAdd(tbl, rbEventAutoHighest);
            TblAdd(tbl, rbEventAutoRandom);
            TblAdd(tbl, rbEventManualInline);
            TblAdd(tbl, rbEventManualPopup);

            AddSectionHeader(tbl, "系統通知設定");
            chkEventNotify = new CheckBox
            {
                Text = "啟用 Windows 系統通知（僅在手動零干擾內嵌模式下，事件觸發時彈出氣泡提示）",
                Checked = AppSettingsManager.Current.EnableSystemNotify,
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 4)
            };
            chkEventNotify.CheckedChanged += (_, _) => SaveEventPreferences();
            TblAdd(tbl, chkEventNotify);

            chkEventNotifySound = new CheckBox
            {
                Text = "開啟通知音效",
                Checked = AppSettingsManager.Current.EnableSystemNotifySound,
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true,
                Margin = new Padding(24, 0, 0, 12)
            };
            chkEventNotifySound.CheckedChanged += (_, _) => SaveEventPreferences();
            TblAdd(tbl, chkEventNotifySound);

            AddSectionHeader(tbl, "💡  事件記憶庫與策略組合管理 (規劃中)");
            var lblFeatureNote = new Label
            {
                Text = "此處預留空間供未來擴充「事件記憶字典庫」與「自訂指定選項多組合方案」。\n目前可使用上方的手動零干擾模式或全自動策略安心掛機。",
                ForeColor = Color.FromArgb(150, 150, 175),
                AutoSize = true,
                Padding = new Padding(8),
                BackColor = Color.FromArgb(28, 28, 36)
            };
            TblAdd(tbl, lblFeatureNote);

            return outer;
        }

        private void SaveEventPreferences()
        {
            if (rbEventAutoRandom.Checked) AppSettingsManager.Current.EventMode = "auto-random";
            else if (rbEventManualInline.Checked) AppSettingsManager.Current.EventMode = "inline";
            else if (rbEventManualPopup.Checked) AppSettingsManager.Current.EventMode = "popup";
            else AppSettingsManager.Current.EventMode = "auto";

            AppSettingsManager.Current.EnableSystemNotify = chkEventNotify.Checked;
            AppSettingsManager.Current.EnableSystemNotifySound = chkEventNotifySound.Checked;
            AppSettingsManager.Save();
        }

        // ─────────────────────────────────────────────────────────────────────
        // 頁籤 5: 戰報分析檢視 (Battle Analytics View)
        // ─────────────────────────────────────────────────────────────────────
        private Panel BuildStatsView()
        {
            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(24, 24, 30)
            };

            var pnlTop = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 0, 8)
            };
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            pnlTop.Controls.Add(new Label
            {
                Text = "戰報編號：",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = Color.FromArgb(190, 190, 210)
            }, 0, 0);

            txtStatsReportId = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 10f)
            };
            txtStatsReportId.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    btnStatsQuery.PerformClick();
                }
            };
            pnlTop.Controls.Add(txtStatsReportId, 1, 0);

            btnStatsQuery = new Button
            {
                Text = "🔍 取得數據",
                AutoSize = true,
                BackColor = Color.FromArgb(50, 65, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(8, 0, 0, 0)
            };
            btnStatsQuery.FlatAppearance.BorderSize = 0;
            btnStatsQuery.Click += OnStatsQueryClicked;
            pnlTop.Controls.Add(btnStatsQuery, 2, 0);

            lblStatsStatus = new Label
            {
                Text = "請輸入戰報編號，並點擊「取得數據」以分析輸出/承傷/命中數據。",
                AutoSize = true,
                ForeColor = Color.FromArgb(150, 150, 175),
                Margin = new Padding(0, 4, 0, 0)
            };
            pnlTop.Controls.Add(lblStatsStatus, 0, 1);
            pnlTop.SetColumnSpan(lblStatsStatus, 3);
            pnlMain.Controls.Add(pnlTop);

            rtbStatsOutput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(14, 14, 18),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10f),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = false
            };
            pnlMain.Controls.Add(rtbStatsOutput);
            rtbStatsOutput.BringToFront();

            return pnlMain;
        }

        private async void OnStatsQueryClicked(object? sender, EventArgs e)
        {
            string reportId = txtStatsReportId.Text.Trim();
            if (string.IsNullOrEmpty(reportId))
            {
                lblStatsStatus.Text = "請輸入戰報編號。";
                lblStatsStatus.ForeColor = Color.FromArgb(220, 80, 80);
                return;
            }
            if (string.IsNullOrWhiteSpace(_currentToken))
            {
                lblStatsStatus.Text = "請先至「連線設定」填入 Bearer Token。";
                lblStatsStatus.ForeColor = Color.FromArgb(220, 80, 80);
                return;
            }

            btnStatsQuery.Enabled = false;
            lblStatsStatus.Text = $"正在查詢戰報 #{reportId}...";
            lblStatsStatus.ForeColor = Color.FromArgb(140, 200, 255);
            rtbStatsOutput.Clear();

            try
            {
                var response = await _battleService.GetBattleReportAsync(reportId, _currentToken);
                if (!response.IsSuccess || response.Data == null)
                {
                    lblStatsStatus.Text = response.ErrorMessage ?? "取得戰報失敗。";
                    lblStatsStatus.ForeColor = Color.FromArgb(220, 80, 80);
                    return;
                }

                if (!BattleStatsAnalyzer.TryAnalyze(response.Data, out var analysis, out string error))
                {
                    lblStatsStatus.Text = $"分析異常：{error}";
                    lblStatsStatus.ForeColor = Color.FromArgb(220, 80, 80);
                    return;
                }

                var lines = BattleStatsChartBuilder.Build(analysis!);
                RenderChartToOutput(rtbStatsOutput, lines);
                lblStatsStatus.Text = $"戰報 #{reportId} 數據解析完成。";
                lblStatsStatus.ForeColor = Color.FromArgb(80, 220, 120);
            }
            catch (Exception ex)
            {
                lblStatsStatus.Text = $"發生異常：{ex.Message}";
                lblStatsStatus.ForeColor = Color.FromArgb(220, 80, 80);
            }
            finally
            {
                btnStatsQuery.Enabled = true;
            }
        }

        private static void RenderChartToOutput(RichTextBox rtb, List<List<ChartSegment>> lines)
        {
            rtb.Clear();
            foreach (var line in lines)
            {
                foreach (var seg in line)
                {
                    rtb.SelectionStart = rtb.TextLength;
                    rtb.SelectionLength = 0;
                    var (col, fontStyle) = seg.Style switch
                    {
                        ChartStyle.OutputBar => (Color.FromArgb(246, 135, 139), FontStyle.Regular),
                        ChartStyle.TakenBar  => (Color.FromArgb(129, 185, 255), FontStyle.Regular),
                        ChartStyle.HealBar   => (Color.FromArgb(123, 213, 166), FontStyle.Regular),
                        ChartStyle.Fallen    => (Color.FromArgb(255, 160, 60),  FontStyle.Bold),
                        ChartStyle.Dead      => (Color.FromArgb(246, 100, 100), FontStyle.Bold),
                        ChartStyle.Heading   => (Color.FromArgb(255, 215, 70),  FontStyle.Bold),
                        ChartStyle.Muted     => (Color.FromArgb(160, 160, 180), FontStyle.Italic),
                        _                    => (Color.FromArgb(220, 220, 230), FontStyle.Regular)
                    };
                    rtb.SelectionColor = col;
                    rtb.SelectionFont = new Font(rtb.Font, fontStyle);
                    rtb.AppendText(seg.Text);
                }
                rtb.AppendText("\n");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 頁籤 6: 系統設定檢視 (Settings View)
        // ─────────────────────────────────────────────────────────────────────
        private Panel BuildSettingsView()
        {
            var outer = CreateScrollableTabPanel();
            var tbl = CreateContentTable(outer);

            AddSectionHeader(tbl, "⚙️  連線授權設定");

            AddRowLabel(tbl, "Bearer Token：");
            txtSettingsToken = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5f),
                PlaceholderText = "請輸入 Bearer Token",
                Text = _currentToken
            };
            TblAdd(tbl, txtSettingsToken);

            btnSaveSettingsToken = new Button
            {
                Text = "💾 儲存 Token 設定",
                AutoSize = true,
                BackColor = Color.FromArgb(40, 140, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 4, 0, 8)
            };
            btnSaveSettingsToken.FlatAppearance.BorderSize = 0;
            btnSaveSettingsToken.Click += (s, e) =>
            {
                _currentToken = txtSettingsToken.Text.Trim();
                AppSettingsManager.Current.Token = _currentToken;
                AppSettingsManager.Save();
                UpdateTokenStatusVisual();
                MessageBox.Show("Token 設定已成功儲存！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            TblAdd(tbl, btnSaveSettingsToken);

            lblTokenStatusDetail = new Label
            {
                Text = "⚠ Token 儲存於本機 settings.json，請勿於公共電腦使用。",
                ForeColor = Color.FromArgb(200, 120, 120),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12)
            };
            TblAdd(tbl, lblTokenStatusDetail);

            AddSectionHeader(tbl, "關於系統");
            var lblAbout = new Label
            {
                Text = "MyDoujin Bot - 專屬網頁遊戲自動化工作站\n" +
                       "核心架構：關注點分離 (SoC)、全非同步 Async/Await、自適應 DPI 排版、極簡低雜訊 Dark Mode。\n" +
                       "快捷鍵提示：[F5] 開始運行 ｜ [F6] 停止運行",
                ForeColor = Color.FromArgb(150, 150, 170),
                AutoSize = true,
                Padding = new Padding(8),
                BackColor = Color.FromArgb(28, 28, 36)
            };
            TblAdd(tbl, lblAbout);

            return outer;
        }

        private void UpdateTokenStatusVisual()
        {
            if (string.IsNullOrWhiteSpace(_currentToken))
            {
                lblSidebarToken.Text = "⚠ Token 未設定";
                lblSidebarToken.ForeColor = Color.FromArgb(220, 90, 90);
                if (lblTokenStatusDetail != null)
                {
                    lblTokenStatusDetail.Text = "⚠ 尚未設定 Token，無法發送任何 API 請求。";
                    lblTokenStatusDetail.ForeColor = Color.FromArgb(220, 90, 90);
                }
            }
            else
            {
                lblSidebarToken.Text = "● Token 已就緒";
                lblSidebarToken.ForeColor = Color.FromArgb(80, 220, 120);
                if (lblTokenStatusDetail != null)
                {
                    lblTokenStatusDetail.Text = "✓ Token 已正確設定並持久化。";
                    lblTokenStatusDetail.ForeColor = Color.FromArgb(80, 220, 120);
                }
            }
        }

        // =====================================================================
        // 5. 執行控制（開始 / 停止）與迴圈排程
        // =====================================================================
        private async void OnStartClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentToken))
            {
                AppendLog("[錯誤] 請先至「連線設定」填入 Bearer Token。", Color.FromArgb(220, 80, 80));
                SwitchNavTab("settings");
                return;
            }

            // 依目前所處分頁決定執行模式
            _activeRunningMode = _selectedNavTab switch
            {
                "battle"   => "battle",
                "pipeline" => "pipeline",
                _          => "training"
            };

            TrainingAction? selectedTrainingAction = null;
            if (_activeRunningMode == "training")
            {
                if (cmbAction.SelectedItem is not TrainingAction action)
                {
                    AppendLog("[錯誤] 請選擇欲執行的訓練項目。", Color.FromArgb(220, 80, 80));
                    return;
                }
                selectedTrainingAction = action;
            }
            else if (_activeRunningMode == "battle")
            {
                if (string.IsNullOrWhiteSpace(txtTargetPlayerId.Text))
                {
                    AppendLog("[錯誤] 請輸入戰鬥目標玩家 ID。", Color.FromArgb(220, 80, 80));
                    return;
                }
            }
            else if (_activeRunningMode == "pipeline")
            {
                if (AppSettingsManager.Current.PipelineStages.Count == 0)
                {
                    AppendLog("[錯誤] 階梯排程清單為空，請先於排程頁籤新增至少一個階段。", Color.FromArgb(220, 80, 80));
                    return;
                }
            }

            SaveCurrentUiSettings();

            _cts = new CancellationTokenSource();
            SetControlsEnabled(false);
            btnGlobalStop.Enabled = true;
            btnShowLastReport.Visible = false;

            _loopStartTime = DateTime.Now;
            _elapsedTimer.Tick += OnElapsedTick;
            _elapsedTimer.Start();

            bool isInline = rbEventManualInline.Checked;
            bool isAutoEvent = rbEventAutoHighest.Checked || rbEventAutoRandom.Checked;
            AutoEventStrategy? autoStrategy = isAutoEvent
                ? (rbEventAutoRandom.Checked ? AutoEventStrategy.Random : AutoEventStrategy.HighestSuccessRate)
                : null;

            try
            {
                if (_activeRunningMode == "training")
                {
                    lblStatusPipeline.Visible = false;
                    var settings = new LoopSettings
                    {
                        Token = _currentToken,
                        ActionId = selectedTrainingAction!.ActionId,
                        ActionSequence = lstActionSequence.Items.Cast<TrainingAction>().Select(a => a.ActionId).ToList(),
                        Mode = rbTrainCount.Checked ? ExecutionMode.Count : ExecutionMode.Time,
                        CountLimit = (int)nudTrainCount.Value,
                        TimeLimitMinutes = (int)nudTrainMinutes.Value,
                        ExtraDelaySeconds = (double)nudTrainExtraDelay.Value
                    };

                    _trainingLoop = new TrainingLoop(_trainingService, _eventService)
                    {
                        AutoStrategy = autoStrategy,
                        OnLog = AppendLog,
                        OnStatusChanged = UpdateStatusBadge,
                        OnStatsUpdated = UpdateTelemetryStats,
                        OnManualEventSelect = isAutoEvent ? null : (isInline ? HandleInlineEventAsync : HandleManualEventAsync),
                        OnManualEventResult = isAutoEvent ? null : (isInline ? ShowInlineEventResultAsync : HandleManualEventResultAsync),
                        OnCloseManualUi = () => { _currentEventForm?.Close(); HideEventOverlay(); }
                    };

                    AppendLog($"開始自動訓練：{selectedTrainingAction.DisplayName}", Color.FromArgb(140, 200, 255));
                    await Task.Run(() => _trainingLoop.RunAsync(settings, _cts.Token), _cts.Token);
                }
                else if (_activeRunningMode == "battle")
                {
                    lblStatusPipeline.Visible = false;
                    var bSettings = new BattleLoopSettings
                    {
                        Token = _currentToken,
                        TargetPlayerId = txtTargetPlayerId.Text.Trim(),
                        TargetPlayerName = string.IsNullOrEmpty(_targetPlayerName) ? txtTargetPlayerId.Text.Trim() : _targetPlayerName,
                        Type = rbBattleChallenge.Checked ? BattleType.Challenge : BattleType.Chado,
                        Mode = rbBattleCount.Checked ? ExecutionMode.Count : ExecutionMode.Time,
                        CountLimit = (int)nudBattleCount.Value,
                        TimeLimitMinutes = (int)nudBattleMinutes.Value,
                        ExtraDelaySeconds = (double)nudBattleExtraDelay.Value
                    };

                    _battleLoop = new BattleLoop(_battleService, _eventService)
                    {
                        AutoStrategy = autoStrategy,
                        OnLog = AppendLog,
                        OnStatusChanged = UpdateStatusBadge,
                        OnStatsUpdated = (bs) =>
                        {
                            _currentStats.RunCount = bs.RunCount;
                            _currentStats.SuccessCount = bs.WinCount;
                            _currentStats.FailCount = bs.LossCount;
                            _currentStats.EventCount = bs.EventCount;
                            _currentStats.EventSuccessCount = bs.EventSuccessCount;
                            _currentStats.EventFailCount = bs.EventFailCount;
                            _currentStats.TotalExp = bs.TotalExp;
                            _currentStats.CurrentLevel = bs.CurrentLevel;
                            _currentStats.NextRunCountdownSeconds = bs.NextRunCountdownSeconds;
                            _currentStats.GainedCharacters = bs.GainedCharacters;
                            UpdateTelemetryStats(_currentStats);
                        },
                        OnManualEventSelect = isAutoEvent ? null : (isInline ? HandleInlineEventAsync : HandleManualEventAsync),
                        OnManualEventResult = isAutoEvent ? null : (isInline ? ShowInlineEventResultAsync : HandleManualEventResultAsync),
                        OnCloseManualUi = () => { _currentEventForm?.Close(); HideEventOverlay(); },
                        OnLastBattleResultUpdated = (result) =>
                        {
                            _lastBattleResult = result;
                            this.BeginInvoke(() => btnShowLastReport.Visible = true);
                        }
                    };

                    AppendLog($"開始自動戰鬥：{(bSettings.Type == BattleType.Challenge ? "友好切磋" : "我要茶渡你")}，目標: {bSettings.TargetPlayerName}", Color.FromArgb(140, 200, 255));
                    await Task.Run(() => _battleLoop.RunAsync(bSettings, _cts.Token), _cts.Token);
                }
                else if (_activeRunningMode == "pipeline")
                {
                    lblStatusPipeline.Visible = true;
                    var pSettings = new PipelineLoopSettings
                    {
                        Token = _currentToken,
                        Stages = AppSettingsManager.Current.PipelineStages,
                        ExtraDelaySeconds = (double)nudPipelineDelay.Value
                    };

                    _pipelineLoop = new PipelineLoop(_trainingService, _battleService, _eventService)
                    {
                        AutoStrategy = autoStrategy,
                        OnLog = AppendLog,
                        OnStatusChanged = UpdateStatusBadge,
                        OnStatsUpdated = UpdateTelemetryStats,
                        OnStageChanged = (idx, total, stage) =>
                        {
                            this.BeginInvoke(() =>
                            {
                                lblStatusPipeline.Text = $"排程: 階段 {idx}/{total} ({stage.Name})";
                                UpdateTooltips();
                            });
                        },
                        OnManualEventSelect = isAutoEvent ? null : (isInline ? HandleInlineEventAsync : HandleManualEventAsync),
                        OnManualEventResult = isAutoEvent ? null : (isInline ? ShowInlineEventResultAsync : HandleManualEventResultAsync),
                        OnCloseManualUi = () => { _currentEventForm?.Close(); HideEventOverlay(); }
                    };

                    AppendLog($"開始執行階梯排程（共 {pSettings.Stages.Count} 階段）...", Color.FromArgb(140, 200, 255));
                    await Task.Run(() => _pipelineLoop.RunAsync(pSettings, _cts.Token), _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppendLog($"[異常中斷] {ex.GetType().Name}: {ex.Message}", Color.FromArgb(220, 80, 80));
            }
            finally
            {
                _elapsedTimer.Stop();
                _elapsedTimer.Tick -= OnElapsedTick;
                SetControlsEnabled(true);
                btnGlobalStop.Enabled = false;
                UpdateStatusBadge(LoopStatus.Stopped);
            }
        }

        private void OnStopClicked(object? sender, EventArgs e)
        {
            _cts?.Cancel();
            _currentEventForm?.Close();
            HideEventOverlay();
            AppendLog("使用者按下停止，正在安全中止排程...", Color.FromArgb(220, 160, 80));
            btnGlobalStop.Enabled = false;
        }

        private void SetControlsEnabled(bool enabled)
        {
            btnNavTraining.Enabled = enabled;
            btnNavBattle.Enabled   = enabled;
            btnNavPipeline.Enabled = enabled;
            btnNavEvents.Enabled   = enabled;
            btnNavSettings.Enabled = enabled;
            btnGlobalStart.Enabled = enabled;
        }

        private void OnElapsedTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _loopStartTime;
            lblStatusElapsed.Text = $"運作: {elapsed:hh\\:mm\\:ss}";
        }

        private void UpdateStatusBadge(LoopStatus status)
        {
            if (lblStatusBadge.InvokeRequired)
            {
                lblStatusBadge.Invoke(() => UpdateStatusBadge(status));
                return;
            }

            (lblStatusBadge.Text, lblStatusBadge.ForeColor) = status switch
            {
                LoopStatus.Running         => ("● 執行中",     Color.FromArgb(80, 220, 120)),
                LoopStatus.WaitingCooldown => (_currentStats.NextRunCountdownSeconds > 0
                                                ? $"● 冷卻: {_currentStats.NextRunCountdownSeconds:F1}s"
                                                : "● 等待冷卻",   Color.FromArgb(170, 215, 255)),
                LoopStatus.WaitingEvent    => ("● 等待事件決策", Color.FromArgb(200, 160, 255)),
                LoopStatus.Completed       => ("● 已完成",     Color.FromArgb(140, 200, 255)),
                LoopStatus.Error           => ("● 發生錯誤",   Color.FromArgb(220, 80, 80)),
                _                          => ("● 已停止",     Color.FromArgb(160, 160, 175))
            };

            UpdateTooltips();
        }

        private void UpdateTelemetryStats(LoopStats stats)
        {
            if (lblStatusProgress.InvokeRequired)
            {
                lblStatusProgress.Invoke(() => UpdateTelemetryStats(stats));
                return;
            }

            _currentStats = stats;
            int targetLimit = _activeRunningMode == "battle" ? (int)nudBattleCount.Value : (int)nudTrainCount.Value;
            lblStatusProgress.Text = $"進度: {stats.RunCount} / {targetLimit} 次";
            lblStatusLevel.Text    = stats.CurrentLevel > 0 ? $"等級: Lv.{stats.CurrentLevel}" : "等級: Lv.—";
            lblStatusEvents.Text   = $"事件: {stats.EventCount} 次";

            if (stats.NextRunCountdownSeconds > 0)
                lblStatusBadge.Text = $"● 冷卻: {stats.NextRunCountdownSeconds:F1}s";

            UpdateTooltips();
        }

        // =====================================================================
        // 6. 遭遇事件決策處理（手動彈窗與零干擾內嵌 Overlay）
        // =====================================================================
        private Task<string?> HandleManualEventAsync(PendingEvent pe, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<string?>();
            var reg = ct.Register(() =>
            {
                this.BeginInvoke(() => _currentEventForm?.Close());
                tcs.TrySetResult(null);
            });

            this.Invoke(() =>
            {
                _currentEventForm = new EventSelectionForm(pe);
                _currentEventForm.OptionSelected += optId =>
                {
                    reg.Dispose();
                    tcs.TrySetResult(optId);
                };
                _currentEventForm.FormClosed += (_, _) =>
                {
                    reg.Dispose();
                    tcs.TrySetResult(null);
                    _currentEventForm = null;
                };
                _currentEventForm.Show(this);
            });

            return tcs.Task;
        }

        private Task HandleManualEventResultAsync(EventResult er, CancellationToken ct)
        {
            var form = _currentEventForm;
            if (form != null && !form.IsDisposed)
            {
                var tcs = new TaskCompletionSource<bool>();
                this.Invoke(() =>
                {
                    if (form != null && !form.IsDisposed)
                        form.ShowResultAsync(er, ct).ContinueWith(_ => tcs.TrySetResult(true));
                    else
                        tcs.TrySetResult(true);
                });
                return tcs.Task;
            }
            return Task.CompletedTask;
        }

        private Task<string?> HandleInlineEventAsync(PendingEvent pe, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<string?>();
            var reg = ct.Register(() =>
            {
                this.BeginInvoke(() => { HideEventOverlay(); tcs.TrySetResult(null); });
            });

            this.Invoke(() =>
            {
                ShowInlineEventUI(pe, optId =>
                {
                    reg.Dispose();
                    tcs.TrySetResult(optId);
                });

                if (AppSettingsManager.Current.EnableSystemNotify)
                {
                    try
                    {
                        ToastNotificationManagerCompat.History.Clear();
                        new ToastContentBuilder()
                            .AddText($"事件觸發：{pe.Name}")
                            .AddText(string.IsNullOrWhiteSpace(pe.Description) ? "請切換至 MyDoujin Bot 視窗選擇選項。" : pe.Description)
                            .AddAudio(new ToastAudio { Silent = !AppSettingsManager.Current.EnableSystemNotifySound })
                            .Show();
                    }
                    catch { }
                }
            });

            return tcs.Task;
        }

        private void ShowInlineEventUI(PendingEvent pe, Action<string> onSelected)
        {
            pnlEventOverlay.Controls.Clear();
            pnlEventOverlay.Visible = true;
            pnlEventOverlay.BringToFront();

            var outer = CreateScrollableTabPanel();
            var tbl = CreateContentTable(outer);

            AddSectionHeader(tbl, $"⚡  遭遇隨機事件：{pe.Name}");

            if (!string.IsNullOrWhiteSpace(pe.Description))
            {
                var lblDesc = new Label
                {
                    Text = pe.Description,
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    ForeColor = Color.FromArgb(200, 200, 215),
                    Margin = new Padding(0, 4, 0, 10)
                };
                TblAdd(tbl, lblDesc);
            }

            AddRowLabel(tbl, "請點選應對選項：");

            foreach (var opt in pe.Options)
            {
                string chanceTxt = opt.SuccessChance.HasValue ? $"（成功率: {opt.SuccessChance}%）" : "（無判定）";
                string statTxt = !string.IsNullOrEmpty(opt.CheckStat) ? $" [{opt.CheckStat.ToUpper()} 檢定]" : "";

                var btnOpt = new Button
                {
                    Text = $"{opt.Name}{statTxt} {chanceTxt}",
                    Dock = DockStyle.Top,
                    Height = 44,
                    BackColor = Color.FromArgb(45, 50, 75),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(14, 0, 0, 0),
                    Font = new Font("Microsoft JhengHei UI", 9.5f),
                    Margin = new Padding(0, 3, 0, 4)
                };
                btnOpt.FlatAppearance.BorderSize = 0;
                btnOpt.Click += (s, e) =>
                {
                    onSelected(opt.Id);
                };
                TblAdd(tbl, btnOpt);
            }

            pnlEventOverlay.Controls.Add(outer);
        }

        private Task ShowInlineEventResultAsync(EventResult er, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            var reg = ct.Register(() =>
            {
                this.BeginInvoke(() => { HideEventOverlay(); tcs.TrySetResult(false); });
            });

            this.Invoke(() =>
            {
                pnlEventOverlay.Controls.Clear();
                pnlEventOverlay.Visible = true;
                pnlEventOverlay.BringToFront();

                var outer = CreateScrollableTabPanel();
                var tbl = CreateContentTable(outer);

                string title = er.IsSuccess ? "✓  事件判定成功！" : "✕  事件判定失敗";
                Color col = er.IsSuccess ? Color.FromArgb(80, 220, 120) : Color.FromArgb(220, 80, 80);
                AddSectionHeader(tbl, title);

                if (!string.IsNullOrWhiteSpace(er.Text))
                {
                    var lblRes = new Label
                    {
                        Text = er.Text,
                        Dock = DockStyle.Fill,
                        AutoSize = true,
                        ForeColor = Color.FromArgb(220, 220, 230),
                        Margin = new Padding(0, 6, 0, 10)
                    };
                    TblAdd(tbl, lblRes);
                }

                if (er.Roll.HasValue)
                {
                    var lblRoll = new Label
                    {
                        Text = $"擲骰明細：Roll ({er.Roll}) + 屬性 ({er.EffectiveStat}) = 總計 {er.Total} ｜ 難度 DC: {er.DC}",
                        ForeColor = Color.FromArgb(170, 170, 200),
                        AutoSize = true,
                        Margin = new Padding(0, 0, 0, 10)
                    };
                    TblAdd(tbl, lblRoll);
                }

                var btnOk = new Button
                {
                    Text = "關閉並繼續 (OK)",
                    AutoSize = true,
                    BackColor = Color.FromArgb(40, 140, 80),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                    Padding = new Padding(16, 4, 16, 4),
                    Margin = new Padding(0, 8, 0, 0)
                };
                btnOk.FlatAppearance.BorderSize = 0;
                btnOk.Click += (s, e) =>
                {
                    reg.Dispose();
                    HideEventOverlay();
                    tcs.TrySetResult(true);
                };
                TblAdd(tbl, btnOk);

                pnlEventOverlay.Controls.Add(outer);
            });

            return tcs.Task;
        }

        private void HideEventOverlay()
        {
            pnlEventOverlay.Visible = false;
            pnlEventOverlay.Controls.Clear();
        }

        // =====================================================================
        // 7. 日誌輸出 Helper
        // =====================================================================
        private void AppendLog(string message, Color color)
        {
            if (rtbLog.InvokeRequired)
            {
                rtbLog.Invoke(() => AppendLog(message, color));
                return;
            }

            if (_logLineCount >= 2000)
            {
                rtbLog.Clear();
                _logLineCount = 0;
            }

            string time = DateTime.Now.ToString("HH:mm:ss");
            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = Color.FromArgb(120, 120, 140);
            rtbLog.AppendText($"[{time}] ");

            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = color;
            rtbLog.AppendText(message + "\n");
            _logLineCount++;

            if (chkLogAutoScroll != null && chkLogAutoScroll.Checked)
                rtbLog.ScrollToCaret();
        }

        // =====================================================================
        // 8. 介面設定持久化儲存與還原
        // =====================================================================
        private void RestoreSavedSettings()
        {
            UpdateTokenStatusVisual();

            nudTrainCount.Value = Math.Clamp(AppSettingsManager.Current.TargetCount, (int)nudTrainCount.Minimum, (int)nudTrainCount.Maximum);
            nudTrainMinutes.Value = Math.Clamp(AppSettingsManager.Current.TargetMinutes, (int)nudTrainMinutes.Minimum, (int)nudTrainMinutes.Maximum);
            nudTrainExtraDelay.Value = Math.Clamp(AppSettingsManager.Current.ExtraDelay, (int)nudTrainExtraDelay.Minimum, (int)nudTrainExtraDelay.Maximum);

            if (AppSettingsManager.Current.ExecutionMode == "time") rbTrainTime.Checked = true;
            else rbTrainCount.Checked = true;
            OnTrainExecutionModeChanged();

            if (!string.IsNullOrEmpty(AppSettingsManager.Current.LastSelectedActionId))
            {
                var savedAction = TrainingActions.All.FirstOrDefault(a => a.ActionId == AppSettingsManager.Current.LastSelectedActionId);
                if (savedAction != null) cmbAction.SelectedItem = savedAction;
            }

            foreach (var actId in AppSettingsManager.Current.TrainingSequence)
            {
                var action = TrainingActions.All.FirstOrDefault(a => a.ActionId == actId);
                if (action != null) lstActionSequence.Items.Add(action);
            }

            // 事件模式還原
            string eMode = AppSettingsManager.Current.EventMode;
            if (eMode == "auto-random") rbEventAutoRandom.Checked = true;
            else if (eMode == "inline") rbEventManualInline.Checked = true;
            else if (eMode == "popup") rbEventManualPopup.Checked = true;
            else rbEventAutoHighest.Checked = true;
        }

        private void SaveCurrentUiSettings()
        {
            AppSettingsManager.Current.TargetCount = (int)nudTrainCount.Value;
            AppSettingsManager.Current.TargetMinutes = (int)nudTrainMinutes.Value;
            AppSettingsManager.Current.ExtraDelay = (int)nudTrainExtraDelay.Value;
            AppSettingsManager.Current.ExecutionMode = rbTrainTime.Checked ? "time" : "count";

            if (cmbAction.SelectedItem is TrainingAction action)
                AppSettingsManager.Current.LastSelectedActionId = action.ActionId;

            AppSettingsManager.Current.LastBattleTargetId = txtTargetPlayerId.Text.Trim();
            AppSettingsManager.Current.BattleMode = rbBattleChallenge.Checked ? "Challenge" : "Chado";
            AppSettingsManager.Save();
        }

        // =====================================================================
        // 9. UI Layout Helper 方法（全面 TableLayoutPanel，無寫死固定 PX）
        // =====================================================================
        private static Panel CreateScrollableTabPanel()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(24, 24, 30),
                Padding = new Padding(16, 12, 16, 16)
            };
        }

        private static TableLayoutPanel CreateContentTable(Panel parent)
        {
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 0,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            parent.Controls.Add(tbl);
            return tbl;
        }

        private static void TblAdd(TableLayoutPanel tbl, Control ctrl, int topMargin = 4)
        {
            int row = tbl.RowCount;
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowCount = row + 1;
            ctrl.Margin = new Padding(ctrl.Margin.Left, topMargin, ctrl.Margin.Right, ctrl.Margin.Bottom);
            tbl.Controls.Add(ctrl, 0, row);
        }

        private static void AddSectionHeader(TableLayoutPanel tbl, string title)
        {
            var header = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 2,
                Dock = DockStyle.Fill,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 10, 0, 6)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));

            header.Controls.Add(new Label
            {
                Text = title,
                ForeColor = Color.FromArgb(140, 180, 255),
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, 0, 4)
            }, 0, 0);

            header.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 65),
                Height = 1,
                AutoSize = false,
                Text = ""
            }, 0, 1);

            TblAdd(tbl, header, topMargin: 6);
        }

        private static void AddRowLabel(TableLayoutPanel tbl, string text)
        {
            TblAdd(tbl, new Label
            {
                Text = text,
                ForeColor = Color.FromArgb(190, 190, 210),
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 2)
            }, topMargin: 4);
        }

        private static FlowLayoutPanel MakeHRow(params Control[] controls)
        {
            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 2, 0, 4)
            };
            foreach (var c in controls)
            {
                c.Anchor = AnchorStyles.Left;
                row.Controls.Add(c);
            }
            return row;
        }

        private static Label MakeLabel(string text, Color? color = null, bool visible = true)
        {
            return new Label
            {
                Text = text,
                ForeColor = color ?? Color.FromArgb(185, 185, 205),
                AutoSize = true,
                Visible = visible,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(4, 0, 8, 0)
            };
        }

        private static string? ResolveAppIconPath()
        {
            var candidates = new[]
            {
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico"),
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "MyDoujinBot", "Resources", "app.ico"),
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Resources", "app.ico")
            };

            foreach (var path in candidates)
            {
                if (System.IO.File.Exists(path)) return path;
            }
            return null;
        }
    }
}
