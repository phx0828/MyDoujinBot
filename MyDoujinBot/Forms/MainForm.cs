using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FontAwesome.Sharp;
using Icon = System.Drawing.Icon;
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
        private readonly UserService _userService = new();
        private readonly EventArchiveService _eventArchiveService = new();

        private TrainingLoop? _trainingLoop;
        private BattleLoop? _battleLoop;
        private PipelineLoop? _pipelineLoop;
        private EventSelectionForm? _currentEventForm;

        private CancellationTokenSource? _cts;
        private readonly System.Windows.Forms.Timer _elapsedTimer = new() { Interval = 1000 };
        private readonly System.Windows.Forms.Timer _statusUpdateTimer = new() { Interval = 60000 };
        private DateTime _loopStartTime;

        // 當前運作中的模式 ("training", "battle", "pipeline")
        private string _activeRunningMode = string.Empty;
        private string _selectedNavTab = "training";
        private LoopStatus _lastReportedStatus = LoopStatus.Stopped;

        // 最後戰報快取
        private BattleResult? _lastBattleResult;
        private string _targetPlayerName = string.Empty;
        private bool _isRestoringSettings;

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

        // --- 底部精簡狀態列 ---
        private Panel pnlStatusBar = null!;
        private Label lblStatusToken = null!;
        private Label lblStatusFloorReward = null!;
        private Label lblStatusLevel = null!;
        private ToolTip statusToolTip = null!;

        // --- 各頁面專屬開始 / 停止控制項 ---
        private Button btnTrainStart = null!;
        private Button btnTrainStop = null!;
        private Button btnBattleStart = null!;
        private Button btnBattleStop = null!;
        private Button btnPipelineStart = null!;
        private Button btnPipelineStop = null!;

        // --- 右側面板 (即時狀態 + 即時 LOG) ---
        private Panel pnlRightPanel = null!;
        private Label lblStatusBadge = null!;
        private Label lblStatusCooldown = null!;
        private Label lblStatusElapsed = null!;
        private Label lblStatusPipelineStage = null!;
        private Label lblStatusRuns = null!;
        private Label lblStatusSuccess = null!;
        private Label lblStatusSlash = null!;
        private Label lblStatusFail = null!;
        private Label lblStatusChado = null!;
        private Label lblStatusBattleRuns = null!;
        private FlowLayoutPanel pnlStatusBattleSuccessLoss = null!;
        private Label lblStatusBattleSuccess = null!;
        private Label lblStatusBattleSlash = null!;
        private Label lblStatusBattleFail = null!;
        private Label lblStatusBattleChado = null!;
        private Label lblStatusEvents = null!;
        private Label lblStatusEventSuccess = null!;
        private Label lblStatusEventFail = null!;
        private RichTextBox rtbLog = null!;
        private CheckBox chkLogAutoScroll = null!;
        private int _logLineCount = 0;

        // --- 分頁 1: 自動訓練控制項 ---
        private ComboBox cmbAction = null!;
        private FlowLayoutPanel pnlTrainingSequencePills = null!;
        private readonly List<TrainingAction> _trainingSequenceList = new();
        private Button btnAddAction = null!;
        private Button btnClearSequence = null!;
        private RadioButton rbTrainCount = null!;
        private RadioButton rbTrainTime = null!;
        private NumericUpDown nudTrainCount = null!;
        private Label lblTrainCountUnit = null!;
        private NumericUpDown nudTrainMinutes = null!;
        private Label lblTrainMinutesUnit = null!;
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
        private Label lblBattleCountUnit = null!;
        private NumericUpDown nudBattleMinutes = null!;
        private Label lblBattleMinutesUnit = null!;
        private NumericUpDown nudBattleExtraDelay = null!;
        private Button btnShowLastReport = null!;

        // --- 分頁 3: 行動排程控制項 ---
        private TableLayoutPanel tblStageList = null!;
        private Button btnAddStage = null!;
        private Button btnClearStages = null!;
        private NumericUpDown nudPipelineDelay = null!;

        // --- 方案化排程管理 ---
        private readonly PresetManager<PipelinePreset> _pipelinePresetManager = new("pipeline");
        private PipelinePreset _activePipelinePreset = null!;
        private ComboBox cmbPipelinePresets = null!;
        private Button btnNewPreset = null!;
        private Button btnRenamePreset = null!;
        private Button btnDeletePreset = null!;
        private Button btnExportPreset = null!;
        private Button btnImportPreset = null!;

        // --- 分頁 4: 事件應對控制項 ---
        private RadioButton rbEventAuto = null!;
        private RadioButton rbEventManual = null!;
        private RadioButton rbEventAutoHighest = null!;
        private RadioButton rbEventAutoRandom = null!;
        private RadioButton rbEventManualInline = null!;
        private RadioButton rbEventManualPopup = null!;
        private Panel pnlEventAutoSub = null!;
        private Panel pnlEventManualSub = null!;
        private CheckBox chkEventNotify = null!;
        private CheckBox chkEventNotifySound = null!;
        private Label lblEventArchiveStatus = null!;
        private Button btnUpdateEventArchive = null!;

        // --- 分頁 5: 戰報分析控制項 ---
        private TextBox txtStatsReportId = null!;
        private Button btnStatsQuery = null!;
        private RichTextBox rtbStatsOutput = null!;
        private Label lblStatsStatus = null!;

        // --- 分頁 6: 系統設定控制項 ---
        private TextBox txtSettingsToken = null!;
        private Button btnSaveSettingsToken = null!;
        private Label lblTokenStatusDetail = null!;
        private CheckBox chkSettingsAutoClaimFloorReward = null!;
        private PictureBox picCurrentIconPreview = null!;
        private Label lblCurrentIconName = null!;

        // 統計暫存
        private LoopStats _currentStats = new();

        public MainForm()
        {
            this.Text = $"MyDoujin Bot v{Application.ProductVersion}";
            this.Size = new Size(1220, 760);
            this.MinimumSize = new Size(1080, 660);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(24, 24, 30);
            this.ForeColor = Color.FromArgb(220, 220, 230);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.KeyPreview = true;

            // 啟用表單雙緩衝與最佳化繪製樣式，徹底消除拖曳移動時的 GDI 閃爍與摩擦延遲
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.UserPaint, false);
            this.DoubleBuffered = true;

            // DPI 自動縮放
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            // 讀取持久化設定
            AppSettingsManager.Load();
            _currentToken = AppSettingsManager.Current.Token;
            _selectedNavTab = string.IsNullOrEmpty(AppSettingsManager.Current.ActiveTab) ? "training" : AppSettingsManager.Current.ActiveTab;

            var iconPath = ResolveAppIconPath(AppSettingsManager.Current.SelectedAppIcon);
            if (iconPath != null)
            {
                try { this.Icon = new Icon(iconPath); } catch { }
            }

            _isRestoringSettings = true;
            try
            {
                _eventArchiveService.EnsureInitialized();
                InitializeUI();
                InitializeNotifyIcon();
                RestoreSavedSettings();
            }
            finally
            {
                _isRestoringSettings = false;
            }

            // 監聽全域 UserContext 個人資料更新事件，即時更新右下角顯示
            UserContext.OnProfileUpdated += () =>
            {
                if (this.IsDisposed || !this.IsHandleCreated) return;
                try
                {
                    this.BeginInvoke(() =>
                    {
                        if (lblStatusLevel != null)
                        {
                            lblStatusLevel.Text = UserContext.Current.SummaryText;
                        }
                        UpdateFloorRewardStatusVisual();
                    });
                }
                catch { }
            };

            // 每 60 秒輪詢更新樓層獎勵冷卻與可領取動態狀態，若開啟自動領取且達到條件則自動領取
            _statusUpdateTimer.Tick += async (s, e) =>
            {
                UpdateFloorRewardStatusVisual();

                if (AppSettingsManager.Current.AutoClaimFloorReward &&
                    UserContext.Current.IsAuthenticated &&
                    UserContext.Current.CanClaimFloorReward)
                {
                    await TryClaimFloorRewardAsync();
                }
            };
            _statusUpdateTimer.Start();
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

            // 確保應用程式結束或關閉時必定清除 Windows 工作列圖示，避免殘留幽靈圖示
            Application.ApplicationExit += (_, _) => CleanUpNotifyIcon();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => CleanUpNotifyIcon();
        }

        private void CleanUpNotifyIcon()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null!;
                }
            }
            catch { }
        }

        // DPI 縮放 Helper (支援 100%、125%、150%、200% 等任意螢幕縮放)
        private float DpiScale => (float)this.DeviceDpi / 96f;
        private int ScaleDpi(int value) => (int)Math.Round(value * DpiScale);
        private float ScaleDpi(float value) => value * DpiScale;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            EnsureFormFitsScreen();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await HandleStartupTokenCheckAsync();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // WS_CLIPCHILDREN: 防止表單重繪底色時覆蓋子控制項，大幅減輕 GDI 繪製重疊與拖曳延遲
                cp.Style |= 0x02000000;
                return cp;
            }
        }

        // 側邊欄底色：子容器與導航按鈕統一使用實色，避免 Color.Transparent 造成的層層父背景重繪
        private static readonly Color SidebarBackColor = Color.FromArgb(16, 16, 22);

        private bool _isSizeMoveActive;
        private Size _clientSizeAtSizeMoveStart;
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;
        private const int WM_ERASEBKGND = 0x0014;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ENTERSIZEMOVE)
            {
                _isSizeMoveActive = true;
                _clientSizeAtSizeMoveStart = this.ClientSize;
                this.SuspendLayout();
                tblRoot?.SuspendLayout();
            }
            else if (m.Msg == WM_EXITSIZEMOVE)
            {
                _isSizeMoveActive = false;
                bool sizeChanged = this.ClientSize != _clientSizeAtSizeMoveStart;

                if (!sizeChanged)
                {
                    // 純移動視窗：尺寸未變，不需要任何排版與重繪（原本每次放開滑鼠都會重排 3~4 輪並全面重畫）
                    tblRoot?.ResumeLayout(false);
                    this.ResumeLayout(false);
                }
                else
                {
                    // 有縮放：先在暫停狀態下更新右側欄寬，再恢復並只執行「一次」排版（由 base.OnResize 觸發）
                    UpdateRightPanelWidth();
                    tblRoot?.ResumeLayout(false);
                    this.ResumeLayout(false);
                    base.OnResize(EventArgs.Empty);
                    this.Invalidate(true);
                }
            }
            else if (m.Msg == WM_ERASEBKGND)
            {
                // 視窗已被 tblRoot 100% 填滿，攔截並忽略重複的背景清除，避免拖曳移動時產生摩擦延遲與白閃
                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);
        }

        protected override void OnResize(EventArgs e)
        {
            // 關鍵：拖曳縮放邊框期間，完全不呼叫 base.OnResize 與 PerformLayout，徹底杜絕拖曳延遲與卡頓
            if (_isSizeMoveActive)
            {
                return;
            }

            base.OnResize(e);
            UpdateRightPanelWidth();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            ApplyDpiLayout();
            EnsureFormFitsScreen();
        }

        /// <summary>
        /// 螢幕邊界安全檢查：確保視窗與 MinimumSize 絕對不會超出使用者的實體螢幕工作區。
        /// 徹底解決 Windows 150% ~ 200% 高縮放環境下視窗撐破螢幕、底部按鈕或右側面板被遮蔽的跑版問題。
        /// </summary>
        private void EnsureFormFitsScreen()
        {
            try
            {
                var screen = Screen.FromControl(this) ?? Screen.PrimaryScreen;
                if (screen == null) return;

                var workingArea = screen.WorkingArea;

                // 彈性動態計算 MinimumSize：限制不可超過工作區
                int minW = Math.Min(ScaleDpi(860), workingArea.Width);
                int minH = Math.Min(ScaleDpi(520), workingArea.Height);
                this.MinimumSize = new Size(minW, minH);

                // 若 DPI 自動縮放後的初始視窗超出螢幕可視區域，自動限縮並置中
                int targetW = Math.Min(this.Width, workingArea.Width);
                int targetH = Math.Min(this.Height, workingArea.Height);

                if (this.Width > workingArea.Width || this.Height > workingArea.Height)
                {
                    this.Size = new Size(targetW, targetH);
                    this.Location = new Point(
                        workingArea.X + Math.Max(0, (workingArea.Width - targetW) / 2),
                        workingArea.Y + Math.Max(0, (workingArea.Height - targetH) / 2)
                    );
                }

                UpdateRightPanelWidth();
            }
            catch
            {
                // 忽略非關鍵螢幕測量異常
            }
        }

        /// <summary>
        /// 動態調整右側面板寬度：依目前可視空間按比例自適應分配，保證中央主畫布有充足寬度。
        /// </summary>
        private void UpdateRightPanelWidth()
        {
            if (tblRoot == null || tblRoot.ColumnStyles.Count < 3) return;

            bool showRightPanel = pnlRightPanel != null && pnlRightPanel.Visible;
            if (!showRightPanel)
            {
                tblRoot.ColumnStyles[2].Width = 0F;
                return;
            }

            int sidebarWidth = (int)tblRoot.ColumnStyles[0].Width;
            int availableWidth = this.ClientSize.Width - sidebarWidth;
            int idealRightWidth = ScaleDpi(390);

            // 保證中央主畫布至少有 ScaleDpi(320) 的工作空間
            int minCanvasWidth = ScaleDpi(320);
            int maxRightWidth = Math.Max(ScaleDpi(260), availableWidth - minCanvasWidth);
            int finalRightWidth = Math.Min(idealRightWidth, maxRightWidth);

            int targetWidth = Math.Max(ScaleDpi(260), finalRightWidth);
            if (Math.Abs(tblRoot.ColumnStyles[2].Width - targetWidth) < 1f) return;

            tblRoot.ColumnStyles[2].Width = targetWidth;
        }

        private void ApplyDpiLayout()
        {
            if (tblRoot == null) return;
            tblRoot.ColumnStyles[0].Width = ScaleDpi(195F);
            UpdateRightPanelWidth();

            if (tblRoot.RowStyles.Count > 1)
            {
                tblRoot.RowStyles[1].Height = ScaleDpi(38F);
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

            // ── 根容器：TableLayoutPanel（3欄 2列，DPI 自適應）──
            // Col 0: 側邊導航欄 (基底 195px，隨 DPI 動態縮放)
            // Col 1: 主內容畫布 (100% 彈性填滿其餘寬度)
            // Col 2: 右側面板 (即時狀態 + 即時 LOG，基底 390px)
            // Row 0: 工作畫布 (100% 彈性高度)
            // Row 1: 底部狀態列 (ScaleDpi(38px))
            tblRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = Color.FromArgb(24, 24, 30),
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleDpi(195F)));
            tblRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScaleDpi(390F)));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, ScaleDpi(38F)));
            this.Controls.Add(tblRoot);

            // 建立各面板並加入 tblRoot
            BuildSidebar();
            BuildMainCanvas();
            BuildRightPanel();
            BuildStatusBar();

            // 為關鍵主畫布與根排版啟用雙緩衝，避免子元件排版時閃爍
            EnableDoubleBuffering(tblRoot);
            EnableDoubleBuffering(pnlMainCanvas);

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
                Height = ScaleDpi(38),
                BackColor = Color.FromArgb(18, 18, 24),
                Padding = new Padding(ScaleDpi(16), ScaleDpi(3), ScaleDpi(16), ScaleDpi(3)),
                Margin = Padding.Empty
            };
            tblRoot.Controls.Add(pnlStatusBar, 0, 1);
            tblRoot.SetColumnSpan(pnlStatusBar, 3);

            var tblStatus = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblStatus.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblStatus.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tblStatus.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pnlStatusBar.Controls.Add(tblStatus);

            var pnlStatusLeft = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Anchor = AnchorStyles.Left
            };
            tblStatus.Controls.Add(pnlStatusLeft, 0, 0);

            lblStatusToken = new Label
            {
                Text = "● Token：檢查中...",
                ForeColor = Color.FromArgb(160, 160, 180),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(16), ScaleDpi(2))
            };
            lblStatusToken.Click += (s, e) => SwitchNavTab("settings");
            pnlStatusLeft.Controls.Add(lblStatusToken);

            // 右下角容器：樓層獎勵狀態 + 玩家摘要（等級、樓層）
            var pnlStatusRight = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Anchor = AnchorStyles.Right
            };
            tblStatus.Controls.Add(pnlStatusRight, 1, 0);

            lblStatusFloorReward = new Label
            {
                Text = "樓層獎勵：—",
                ForeColor = Color.FromArgb(200, 200, 220), // 一般顏色
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(16), ScaleDpi(2))
            };
            pnlStatusRight.Controls.Add(lblStatusFloorReward);

            // 右下角：玩家名 ｜ 等級 ｜ 目前樓層：41F（使用黃色呈現）
            lblStatusLevel = new Label
            {
                Text = UserContext.Current.SummaryText,
                ForeColor = Color.FromArgb(245, 210, 60), // 黃色
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, ScaleDpi(2), 0, ScaleDpi(2))
            };
            pnlStatusRight.Controls.Add(lblStatusLevel);
        }

        // =====================================================================
        // 2. 右側面板 (即時狀態 + 即時日誌)
        // =====================================================================
        private void BuildRightPanel()
        {
            pnlRightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 26),
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            tblRoot.Controls.Add(pnlRightPanel, 2, 0);

            var tblRight = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblRight.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblRight.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // 上方即時狀態 (自適應字型高度)
            tblRight.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // 下方即時日誌
            pnlRightPanel.Controls.Add(tblRight);

            // ── 上方即時狀態面板 ──
            var pnlStatusCard = new Panel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.FromArgb(26, 26, 36),
                Padding = new Padding(ScaleDpi(14), ScaleDpi(10), ScaleDpi(14), ScaleDpi(10)),
                Margin = new Padding(0, 0, 0, ScaleDpi(2))
            };
            tblRight.Controls.Add(pnlStatusCard, 0, 0);

            var tblStatusContent = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 5,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblStatusContent.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblStatusContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int r = 0; r < 5; r++)
                tblStatusContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pnlStatusCard.Controls.Add(tblStatusContent);

            // Row 0: [圈圈]執行中/已停止 + [冷卻倒數]    運作時間
            var pnlBadgeGroup = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Anchor = AnchorStyles.Left
            };
            tblStatusContent.Controls.Add(pnlBadgeGroup, 0, 0);

            lblStatusBadge = new Label
            {
                Text = "● 已停止",
                ForeColor = Color.FromArgb(160, 160, 175),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(2), 0, ScaleDpi(2))
            };
            pnlBadgeGroup.Controls.Add(lblStatusBadge);

            lblStatusCooldown = new Label
            {
                Text = "",
                ForeColor = Color.FromArgb(140, 205, 255),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(ScaleDpi(6), ScaleDpi(3), 0, ScaleDpi(2))
            };
            pnlBadgeGroup.Controls.Add(lblStatusCooldown);

            lblStatusElapsed = new Label
            {
                Text = "00:00:00",
                ForeColor = Color.FromArgb(200, 200, 215),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, ScaleDpi(2), 0, ScaleDpi(2))
            };
            tblStatusContent.Controls.Add(lblStatusElapsed, 1, 0);

            // Row 1 (行動排程專用)：#? 階段 執行什麼 直到什麼
            lblStatusPipelineStage = new Label
            {
                Text = "",
                ForeColor = Color.FromArgb(255, 215, 90),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(2), 0, ScaleDpi(2)),
                Visible = false
            };
            tblStatusContent.Controls.Add(lblStatusPipelineStage, 0, 1);
            tblStatusContent.SetColumnSpan(lblStatusPipelineStage, 2);

            // Row 2: 訓練次數 (戰鬥次數 / 排程總計)  成功/失敗 (勝利/失敗)
            lblStatusRuns = new Label
            {
                Text = "訓練次數：0 次",
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(2), 0, ScaleDpi(2))
            };

            var pnlStatusSuccessLoss = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, ScaleDpi(2), 0, ScaleDpi(2)),
                Padding = Padding.Empty
            };
            lblStatusSuccess = new Label
            {
                Text = "0 成功",
                ForeColor = Color.FromArgb(80, 220, 120),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Margin = Padding.Empty
            };
            lblStatusSlash = new Label
            {
                Text = " / ",
                ForeColor = Color.FromArgb(140, 140, 160),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Margin = Padding.Empty
            };
            lblStatusFail = new Label
            {
                Text = "0 失敗",
                ForeColor = Color.FromArgb(240, 95, 95),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Margin = Padding.Empty
            };
            lblStatusChado = new Label
            {
                Text = "",
                ForeColor = Color.FromArgb(200, 155, 106),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold),
                Visible = false,
                Margin = new Padding(ScaleDpi(2), 0, 0, 0)
            };
            pnlStatusSuccessLoss.Controls.Add(lblStatusSuccess);
            pnlStatusSuccessLoss.Controls.Add(lblStatusSlash);
            pnlStatusSuccessLoss.Controls.Add(lblStatusFail);
            pnlStatusSuccessLoss.Controls.Add(lblStatusChado);

            tblStatusContent.Controls.Add(lblStatusRuns, 0, 2);
            tblStatusContent.Controls.Add(pnlStatusSuccessLoss, 1, 2);

            // Row 3 (行動排程專用)：總計戰鬥次數  勝利/失敗
            lblStatusBattleRuns = new Label
            {
                Text = "總計戰鬥次數：0 次",
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(2), 0, ScaleDpi(2)),
                Visible = false
            };

            pnlStatusBattleSuccessLoss = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, ScaleDpi(2), 0, ScaleDpi(2)),
                Padding = Padding.Empty,
                Visible = false
            };
            lblStatusBattleSuccess = new Label
            {
                Text = "0 勝利",
                ForeColor = Color.FromArgb(80, 220, 120),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Margin = Padding.Empty
            };
            lblStatusBattleSlash = new Label
            {
                Text = " / ",
                ForeColor = Color.FromArgb(140, 140, 160),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Margin = Padding.Empty
            };
            lblStatusBattleFail = new Label
            {
                Text = "0 失敗",
                ForeColor = Color.FromArgb(240, 95, 95),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Margin = Padding.Empty
            };
            lblStatusBattleChado = new Label
            {
                Text = "",
                ForeColor = Color.FromArgb(200, 155, 106),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold),
                Visible = false,
                Margin = new Padding(ScaleDpi(2), 0, 0, 0)
            };
            pnlStatusBattleSuccessLoss.Controls.Add(lblStatusBattleSuccess);
            pnlStatusBattleSuccessLoss.Controls.Add(lblStatusBattleSlash);
            pnlStatusBattleSuccessLoss.Controls.Add(lblStatusBattleFail);
            pnlStatusBattleSuccessLoss.Controls.Add(lblStatusBattleChado);

            tblStatusContent.Controls.Add(lblStatusBattleRuns, 0, 3);
            tblStatusContent.Controls.Add(pnlStatusBattleSuccessLoss, 1, 3);

            // Row 4: 觸發事件  成功/失敗
            lblStatusEvents = new Label
            {
                Text = "觸發事件：0 次",
                ForeColor = Color.FromArgb(190, 160, 255),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(2), 0, ScaleDpi(2))
            };

            var pnlStatusEventOutcome = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, ScaleDpi(2), 0, ScaleDpi(2)),
                Padding = Padding.Empty
            };
            lblStatusEventSuccess = new Label
            {
                Text = "0 成功",
                ForeColor = Color.FromArgb(80, 220, 120),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Margin = Padding.Empty
            };
            var lblStatusEventSlash = new Label
            {
                Text = " / ",
                ForeColor = Color.FromArgb(140, 140, 160),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Margin = Padding.Empty
            };
            lblStatusEventFail = new Label
            {
                Text = "0 失敗",
                ForeColor = Color.FromArgb(240, 95, 95),
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Margin = Padding.Empty
            };
            pnlStatusEventOutcome.Controls.Add(lblStatusEventSuccess);
            pnlStatusEventOutcome.Controls.Add(lblStatusEventSlash);
            pnlStatusEventOutcome.Controls.Add(lblStatusEventFail);

            tblStatusContent.Controls.Add(lblStatusEvents, 0, 4);
            tblStatusContent.Controls.Add(pnlStatusEventOutcome, 1, 4);

            // ── 下方即時日誌面板 ──
            var pnlLogCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 16, 22),
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            tblRight.Controls.Add(pnlLogCard, 0, 1);

            var pnlLogTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = ScaleDpi(32),
                BackColor = Color.FromArgb(28, 28, 38),
                Padding = new Padding(ScaleDpi(12), ScaleDpi(3), ScaleDpi(12), ScaleDpi(3))
            };
            pnlLogCard.Controls.Add(pnlLogTop);

            var lblLogHeader = new Label
            {
                Text = "執行紀錄",
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(200, 200, 220),
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            };
            pnlLogTop.Controls.Add(lblLogHeader);

            var pnlLogActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };
            pnlLogTop.Controls.Add(pnlLogActions);

            chkLogAutoScroll = new CheckBox
            {
                Text = "自動捲動",
                Checked = true,
                ForeColor = Color.FromArgb(170, 170, 190),
                AutoSize = true,
                Margin = new Padding(0, ScaleDpi(4), ScaleDpi(6), 0)
            };
            pnlLogActions.Controls.Add(chkLogAutoScroll);

            btnShowLastReport = new IconButton
            {
                Text = " 最後戰報",
                AutoSize = true,
                BackColor = Color.FromArgb(45, 50, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = false,
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                IconChar = IconChar.FileLines,
                IconColor = Color.White,
                IconSize = ScaleDpi(16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(6), 0)
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
            pnlLogActions.Controls.Add(btnShowLastReport);

            var btnClearLog = new IconButton
            {
                Text = " 清空",
                AutoSize = true,
                BackColor = Color.FromArgb(50, 40, 40),
                ForeColor = Color.FromArgb(220, 200, 200),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                IconChar = IconChar.TrashCan,
                IconColor = Color.FromArgb(220, 200, 200),
                IconSize = ScaleDpi(16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, ScaleDpi(2), 0, 0)
            };
            btnClearLog.FlatAppearance.BorderSize = 0;
            btnClearLog.Click += (s, e) => { rtbLog.Clear(); _logLineCount = 0; };
            pnlLogActions.Controls.Add(btnClearLog);

            // LOG 內容本體容器（帶有各自專屬 Padding，文字與邊框不緊貼）
            var pnlLogBody = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(14, 14, 18),
                Padding = new Padding(ScaleDpi(12), ScaleDpi(6), ScaleDpi(6), ScaleDpi(8)),
                Margin = Padding.Empty
            };
            pnlLogCard.Controls.Add(pnlLogBody);
            pnlLogBody.BringToFront();

            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(14, 14, 18),
                ForeColor = Color.FromArgb(210, 210, 225),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5f),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            pnlLogBody.Controls.Add(rtbLog);

            // 內嵌事件 Overlay（掛在 pnlLogBody 內，遮蔽 rtbLog）
            pnlEventOverlay = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(22, 22, 32),
                Visible = false,
                Padding = Padding.Empty
            };
            pnlLogBody.Controls.Add(pnlEventOverlay);
            rtbLog.BringToFront();
        }

        // =====================================================================
        // 3. 左側功能導航欄 (Sidebar Navigation)
        // =====================================================================
        private void BuildSidebar()
        {
            pnlSidebar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SidebarBackColor,
                Padding = new Padding(ScaleDpi(6), ScaleDpi(10), ScaleDpi(6), ScaleDpi(8)),
                Margin = Padding.Empty
            };
            tblRoot.Controls.Add(pnlSidebar, 0, 0);

            var tblSidebarLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = SidebarBackColor,
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
                RowCount = 1,
                AutoSize = true,
                BackColor = SidebarBackColor,
                Margin = new Padding(ScaleDpi(4), 0, ScaleDpi(4), ScaleDpi(12)),
                Padding = Padding.Empty
            };
            pnlBrand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pnlBrand.Controls.Add(new Label
            {
                Text = "⚡ MyDoujin Bot",
                ForeColor = Color.FromArgb(240, 240, 250),
                Font = new Font("Microsoft JhengHei UI", 11.5f, FontStyle.Bold),
                AutoSize = true,
                Margin = Padding.Empty
            }, 0, 0);
            tblSidebarLayout.Controls.Add(pnlBrand, 0, 0);

            // 導航按鈕容器 (使用 TableLayoutPanel 保證按鈕 100% 寬度自適應填滿，無寫死固定寬度)
            pnlNav = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,
                BackColor = SidebarBackColor,
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

            btnNavTraining  = CreateNavButton("自動訓練", "training", IconChar.Bullseye);
            btnNavBattle    = CreateNavButton("自動戰鬥", "battle", IconChar.ShieldHalved);
            btnNavPipeline  = CreateNavButton("行動排程", "pipeline", IconChar.Rotate);
            btnNavEvents    = CreateNavButton("事件應對", "events", IconChar.Scroll);
            btnNavStats     = CreateNavButton("戰報分析", "stats", IconChar.ChartBar);
            btnNavSettings  = CreateNavButton("設定", "settings", IconChar.Gear);

            pnlNav.Controls.Add(btnNavTraining, 0, 0);
            pnlNav.Controls.Add(btnNavBattle, 0, 1);
            pnlNav.Controls.Add(btnNavPipeline, 0, 2);
            pnlNav.Controls.Add(btnNavEvents, 0, 3);
            pnlNav.Controls.Add(btnNavStats, 0, 4);
            pnlNav.Controls.Add(btnNavSettings, 0, 5);

            // 底部 Token 狀態指示（未設定時顯示警告，已設定則隱藏不佔版面）
            lblSidebarToken = new Label
            {
                Dock = DockStyle.Fill,
                Text = string.IsNullOrWhiteSpace(_currentToken) ? "● Token 尚未設定" : string.Empty,
                ForeColor = Color.FromArgb(220, 90, 90),
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                Height = ScaleDpi(32),
                Cursor = Cursors.Hand,
                Visible = string.IsNullOrWhiteSpace(_currentToken),
                Margin = new Padding(0, ScaleDpi(4), 0, 0)
            };
            lblSidebarToken.Click += (s, e) => { if (_cts == null) SwitchNavTab("settings"); };
            tblSidebarLayout.Controls.Add(lblSidebarToken, 0, 2);
        }

        private IconButton CreateNavButton(string text, string tabKey, IconChar icon)
        {
            var btn = new IconButton
            {
                Text = "  " + text,
                Tag = tabKey,
                Dock = DockStyle.Fill,
                Height = ScaleDpi(38),
                BackColor = SidebarBackColor,
                ForeColor = Color.FromArgb(170, 170, 190),
                IconChar = icon,
                IconColor = Color.FromArgb(170, 170, 190),
                IconSize = ScaleDpi(20),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                Padding = new Padding(ScaleDpi(12), 0, 0, 0),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Cursor = Cursors.Hand,
                TabStop = false,
                Margin = new Padding(ScaleDpi(2), ScaleDpi(2), ScaleDpi(2), ScaleDpi(2))
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 30, 42);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(42, 48, 70);
            btn.Click += (s, e) => { if (_selectedNavTab != tabKey) SwitchNavTab(tabKey); };
            btn.MouseEnter += (s, e) =>
            {
                if ((string)btn.Tag! != _selectedNavTab)
                {
                    btn.BackColor = Color.FromArgb(28, 30, 42);
                    btn.IconColor = Color.FromArgb(220, 220, 240);
                }
            };
            btn.MouseLeave += (s, e) =>
            {
                if ((string)btn.Tag! != _selectedNavTab)
                {
                    btn.BackColor = SidebarBackColor;
                    btn.IconColor = Color.FromArgb(170, 170, 190);
                }
            };
            _navButtons.Add(btn);
            return btn;
        }

        private void SwitchNavTab(string tabKey)
        {
            if (pnlMainCanvas == null || pnlViewTraining == null || pnlViewPipeline == null)
            {
                _selectedNavTab = tabKey;
                return;
            }

            _selectedNavTab = tabKey;
            AppSettingsManager.Current.ActiveTab = tabKey;

            // 1. 更新側邊欄按鈕選中樣式（確保啟動時及每次切換時皆正確高亮）
            foreach (var btn in _navButtons)
            {
                bool isActive = (string)btn.Tag! == tabKey;
                btn.BackColor = isActive ? Color.FromArgb(42, 48, 70) : SidebarBackColor;
                btn.ForeColor = isActive ? Color.FromArgb(250, 250, 255) : Color.FromArgb(170, 170, 190);
                if (btn is IconButton ib)
                {
                    ib.IconColor = isActive ? Color.FromArgb(250, 250, 255) : Color.FromArgb(170, 170, 190);
                }
            }

            // 2. 右側面板依分頁顯示/隱藏：訓練、戰鬥、排程顯示；事件、統計、設定隱藏
            bool showRightPanel = tabKey == "training" || tabKey == "battle" || tabKey == "pipeline";
            if (pnlRightPanel != null)
            {
                pnlRightPanel.Visible = showRightPanel;
            }
            UpdateRightPanelWidth();

            // 抑制主畫布繪製與排版，動態掛載當前頁籤（將非作用中頁籤移出視窗樹，徹底釋放 400+ 閒置 HWND 控制項）
            pnlMainCanvas.SuspendLayout();
            try
            {
                Control targetView = tabKey switch
                {
                    "battle"   => pnlViewBattle,
                    "pipeline" => pnlViewPipeline,
                    "events"   => pnlViewEvents,
                    "stats"    => pnlViewStats,
                    "settings" => pnlViewSettings,
                    _          => pnlViewTraining
                };

                // 核心關鍵：只將當前頁籤掛載到 pnlMainCanvas，移出其他非使用中的頁籤。
                // 如此一來，無論使用者開啟過多少個頁籤，視窗永遠只承載 1 個頁籤的極輕量控制項，
                // 拖曳順暢度永遠保持在「剛開啟時」的極致絲滑！
                if (pnlMainCanvas.Controls.Count != 1 || pnlMainCanvas.Controls[0] != targetView)
                {
                    pnlMainCanvas.Controls.Clear();
                    targetView.Dock = DockStyle.Fill;
                    targetView.Visible = true;
                    pnlMainCanvas.Controls.Add(targetView);
                }
            }
            finally
            {
                pnlMainCanvas.ResumeLayout(true);
            }

            if (lblStatusRuns != null)
            {
                UpdateTelemetryStats(_currentStats);
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

            // 初始化方案化排程
            InitializePipelinePresets();

            // 建立各頁籤檢視（物件常駐記憶體保持狀態，但畫面樹永遠只由 SwitchNavTab 掛載當前作用中頁籤）
            pnlViewTraining = BuildTrainingView();
            pnlViewBattle   = BuildBattleView();
            pnlViewPipeline = BuildPipelineView();
            pnlViewEvents   = BuildEventsView();
            pnlViewStats    = BuildStatsView();
            pnlViewSettings = BuildSettingsView();

            EnableDoubleBuffering(pnlMainCanvas);
            if (pnlRightPanel != null) EnableDoubleBuffering(pnlRightPanel);

            SwitchNavTab(_selectedNavTab);
        }


        // ─────────────────────────────────────────────────────────────────────
        // 頁籤 1: 自動訓練檢視
        // ─────────────────────────────────────────────────────────────────────
        private Panel BuildTrainingView()
        {
            var outer = CreateScrollableTabPanel();
            var tbl = CreateContentTable(outer);

            AddSectionHeader(tbl, "自動訓練設定");

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
            cmbAction.SelectedIndexChanged += (s, e) => { if (!_isRestoringSettings) SaveCurrentUiSettings(); };
            TblAdd(tbl, cmbAction);

            // 加入/清除清單按鈕
            btnAddAction = new IconButton
            {
                Text = " 加入循環清單",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(42, 85, 140),
                ForeColor = Color.White,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Cursor = Cursors.Hand,
                IconChar = IconChar.Plus,
                IconColor = Color.White,
                IconSize = ScaleDpi(16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft
            };
            btnAddAction.FlatAppearance.BorderSize = 0;
            btnAddAction.Click += (s, e) =>
            {
                if (cmbAction.SelectedItem is TrainingAction action)
                {
                    _trainingSequenceList.Add(action);
                    SaveTrainingSequence();
                    RefreshTrainingSequencePills();
                }
            };

            btnClearSequence = new IconButton
            {
                Text = " 清空清單",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(85, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Cursor = Cursors.Hand,
                IconChar = IconChar.TrashCan,
                IconColor = Color.White,
                IconSize = ScaleDpi(16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft
            };
            btnClearSequence.FlatAppearance.BorderSize = 0;
            btnClearSequence.Click += (s, e) =>
            {
                _trainingSequenceList.Clear();
                SaveTrainingSequence();
                RefreshTrainingSequencePills();
            };
            TblAdd(tbl, MakeHRow(btnAddAction, btnClearSequence));

            // 循環訓練項目橫列 (橢圓 Pill Badge 容器)
            pnlTrainingSequencePills = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.FromArgb(24, 24, 30),
                Margin = new Padding(0, 4, 0, 10),
                Padding = Padding.Empty
            };
            TblAdd(tbl, pnlTrainingSequencePills);

            AddSectionHeader(tbl, "執行限制與冷卻延遲");
            rbTrainTime  = new RadioButton { Text = "執行指定時間", Checked = true, ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true };
            rbTrainCount = new RadioButton { Text = "執行指定次數", ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true, Margin = new Padding(12, 0, 0, 0) };
            rbTrainTime.CheckedChanged  += (s, e) => { if (!_isRestoringSettings) { OnTrainExecutionModeChanged(); SaveCurrentUiSettings(); } };
            rbTrainCount.CheckedChanged += (s, e) => { if (!_isRestoringSettings) { OnTrainExecutionModeChanged(); SaveCurrentUiSettings(); } };
            TblAdd(tbl, MakeHRow(rbTrainTime, rbTrainCount));

            nudTrainMinutes = new NumericUpDown { Width = ScaleDpi(110), Minimum = 1, Maximum = 9999,  Value = 1440, BackColor = Color.FromArgb(40, 40, 52), ForeColor = Color.FromArgb(220, 220, 230), Visible = true };
            lblTrainMinutesUnit = MakeLabel("分鐘", visible: true);
            nudTrainCount   = new NumericUpDown { Width = ScaleDpi(110), Minimum = 1, Maximum = 99999, Value = 500, BackColor = Color.FromArgb(40, 40, 52), ForeColor = Color.FromArgb(220, 220, 230), Visible = false };
            lblTrainCountUnit = MakeLabel("次", visible: false);
            nudTrainMinutes.ValueChanged += (s, e) => { if (!_isRestoringSettings) SaveCurrentUiSettings(); };
            nudTrainCount.ValueChanged   += (s, e) => { if (!_isRestoringSettings) SaveCurrentUiSettings(); };
            TblAdd(tbl, MakeHRow(nudTrainMinutes, lblTrainMinutesUnit, nudTrainCount, lblTrainCountUnit));

            AddRowLabel(tbl, "隨機延遲：");
            nudTrainExtraDelay = new NumericUpDown { Width = ScaleDpi(110), Minimum = 0, Maximum = 60, Value = 1, BackColor = Color.FromArgb(40, 40, 52), ForeColor = Color.FromArgb(220, 220, 230) };
            nudTrainExtraDelay.ValueChanged += (s, e) => { if (!_isRestoringSettings) SaveCurrentUiSettings(); };
            TblAdd(tbl, MakeHRow(nudTrainExtraDelay, MakeLabel("秒 (0 ~ 此值)")));

            AddSectionHeader(tbl, "執行控制");
            btnTrainStart = new Button
            {
                Text = "▶ 開始訓練",
                AutoSize = true,
                BackColor = Color.FromArgb(35, 140, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Padding = new Padding(ScaleDpi(16), ScaleDpi(6), ScaleDpi(16), ScaleDpi(6)),
                Margin = new Padding(0, 0, ScaleDpi(10), 0)
            };
            btnTrainStart.FlatAppearance.BorderSize = 0;
            btnTrainStart.Click += OnStartClicked;

            btnTrainStop = new Button
            {
                Text = "⏹ 停止",
                AutoSize = true,
                BackColor = Color.FromArgb(150, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false,
                Padding = new Padding(ScaleDpi(16), ScaleDpi(6), ScaleDpi(16), ScaleDpi(6)),
                Margin = Padding.Empty
            };
            btnTrainStop.FlatAppearance.BorderSize = 0;
            btnTrainStop.Click += OnStopClicked;
            TblAdd(tbl, MakeHRow(btnTrainStart, btnTrainStop), topMargin: ScaleDpi(8));

            RefreshTrainingSequencePills();
            return outer;
        }

        private void OnTrainExecutionModeChanged()
        {
            if (rbTrainTime == null) return;
            bool isTime = rbTrainTime.Checked;
            if (nudTrainMinutes != null) nudTrainMinutes.Visible = isTime;
            if (lblTrainMinutesUnit != null) lblTrainMinutesUnit.Visible = isTime;
            if (nudTrainCount != null) nudTrainCount.Visible = !isTime;
            if (lblTrainCountUnit != null) lblTrainCountUnit.Visible = !isTime;
        }

        private void RefreshTrainingSequencePills()
        {
            if (pnlTrainingSequencePills == null) return;
            pnlTrainingSequencePills.SuspendLayout();
            pnlTrainingSequencePills.Controls.Clear();

            if (_trainingSequenceList.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "（尚未加入循環項目，將僅重複執行上方選擇之項目）",
                    ForeColor = Color.FromArgb(120, 120, 145),
                    Font = new Font("Microsoft JhengHei UI", 8.5f),
                    AutoSize = true,
                    Margin = new Padding(2, 4, 0, 4)
                };
                pnlTrainingSequencePills.Controls.Add(lblEmpty);
            }
            else
            {
                for (int i = 0; i < _trainingSequenceList.Count; i++)
                {
                    int index = i;
                    var action = _trainingSequenceList[i];
                    var pill = CreatePillBadge($"{index + 1}. {action.DisplayName}", () =>
                    {
                        if (index >= 0 && index < _trainingSequenceList.Count)
                        {
                            _trainingSequenceList.RemoveAt(index);
                            SaveTrainingSequence();
                            RefreshTrainingSequencePills();
                        }
                    });
                    pill.Enabled = _cts == null;

                    statusToolTip.SetToolTip(pill, $"第 {index + 1} 順位：{action.DisplayName}\n點擊以自清單中移除");
                    pnlTrainingSequencePills.Controls.Add(pill);
                }
            }

            pnlTrainingSequencePills.ResumeLayout();
        }

        private Control CreatePillBadge(string text, Action onRemove)
        {
            return new PillBadgeControl(text, onRemove, DpiScale);
        }

        private void SaveTrainingSequence()
        {
            AppSettingsManager.Current.TrainingSequence = _trainingSequenceList.Select(a => a.ActionId).ToList();
            AppSettingsManager.Save();
        }

        // ─────────────────────────────────────────────────────────────────────
        // 頁籤 2: 自動戰鬥檢視
        // ─────────────────────────────────────────────────────────────────────
        private Panel BuildBattleView()
        {
            var outer = CreateScrollableTabPanel();
            var tbl = CreateContentTable(outer);

            AddSectionHeader(tbl, "自動戰鬥設定");

            AddRowLabel(tbl, "戰鬥目標玩家 ID：");
            txtTargetPlayerId = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Text = AppSettingsManager.Current.LastBattleTargetId
            };
            txtTargetPlayerId.TextChanged += (s, e) => { if (!_isRestoringSettings) SaveCurrentUiSettings(); };
            btnGetPlayerInfo = new IconButton
            {
                Text = " 驗證 ID",
                AutoSize = true,
                BackColor = Color.FromArgb(50, 65, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                IconChar = IconChar.MagnifyingGlass,
                IconColor = Color.White,
                IconSize = ScaleDpi(16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
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
            rbBattleChallenge.CheckedChanged += (s, e) => { if (!_isRestoringSettings) { SaveCurrentUiSettings(); if (_cts == null && lblStatusRuns != null) UpdateTelemetryStats(_currentStats); } };
            rbBattleChado.CheckedChanged     += (s, e) => { if (!_isRestoringSettings) { SaveCurrentUiSettings(); if (_cts == null && lblStatusRuns != null) UpdateTelemetryStats(_currentStats); } };
            TblAdd(tbl, MakeHRow(rbBattleChallenge, rbBattleChado));

            btnShowLastReport = new IconButton
            {
                Text = " 查看最後戰報",
                AutoSize = true,
                BackColor = Color.FromArgb(45, 50, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = false,
                Font = new Font("Microsoft JhengHei UI", 9f),
                IconChar = IconChar.FileLines,
                IconColor = Color.White,
                IconSize = ScaleDpi(16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
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
            rbBattleTime  = new RadioButton { Text = "執行指定時間", Checked = true, ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true };
            rbBattleCount = new RadioButton { Text = "執行指定次數", ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true, Margin = new Padding(12, 0, 0, 0) };
            rbBattleTime.CheckedChanged  += (s, e) => { if (!_isRestoringSettings) { OnBattleExecutionModeChanged(); SaveCurrentUiSettings(); } };
            rbBattleCount.CheckedChanged += (s, e) => { if (!_isRestoringSettings) { OnBattleExecutionModeChanged(); SaveCurrentUiSettings(); } };
            TblAdd(tbl, MakeHRow(rbBattleTime, rbBattleCount));

            nudBattleMinutes = new NumericUpDown { Width = ScaleDpi(110), Minimum = 1, Maximum = 9999,  Value = 1440, BackColor = Color.FromArgb(40, 40, 52), ForeColor = Color.FromArgb(220, 220, 230), Visible = true };
            lblBattleMinutesUnit = MakeLabel("分鐘", visible: true);
            nudBattleCount   = new NumericUpDown { Width = ScaleDpi(110), Minimum = 1, Maximum = 99999, Value = 500, BackColor = Color.FromArgb(40, 40, 52), ForeColor = Color.FromArgb(220, 220, 230), Visible = false };
            lblBattleCountUnit = MakeLabel("次", visible: false);
            nudBattleMinutes.ValueChanged += (s, e) => { if (!_isRestoringSettings) SaveCurrentUiSettings(); };
            nudBattleCount.ValueChanged   += (s, e) => { if (!_isRestoringSettings) SaveCurrentUiSettings(); };
            TblAdd(tbl, MakeHRow(nudBattleMinutes, lblBattleMinutesUnit, nudBattleCount, lblBattleCountUnit));

            AddRowLabel(tbl, "隨機延遲：");
            nudBattleExtraDelay = new NumericUpDown { Width = ScaleDpi(110), Minimum = 0, Maximum = 60, Value = 1, BackColor = Color.FromArgb(40, 40, 52), ForeColor = Color.FromArgb(220, 220, 230) };
            nudBattleExtraDelay.ValueChanged += (s, e) => { if (!_isRestoringSettings) SaveCurrentUiSettings(); };
            TblAdd(tbl, MakeHRow(nudBattleExtraDelay, MakeLabel("秒 (0 ~ 此值)")));

            AddSectionHeader(tbl, "執行控制");
            btnBattleStart = new Button
            {
                Text = "▶ 開始戰鬥",
                AutoSize = true,
                BackColor = Color.FromArgb(35, 140, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Padding = new Padding(ScaleDpi(16), ScaleDpi(6), ScaleDpi(16), ScaleDpi(6)),
                Margin = new Padding(0, 0, ScaleDpi(10), 0)
            };
            btnBattleStart.FlatAppearance.BorderSize = 0;
            btnBattleStart.Click += OnStartClicked;

            btnBattleStop = new Button
            {
                Text = "⏹ 停止",
                AutoSize = true,
                BackColor = Color.FromArgb(150, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false,
                Padding = new Padding(ScaleDpi(16), ScaleDpi(6), ScaleDpi(16), ScaleDpi(6)),
                Margin = Padding.Empty
            };
            btnBattleStop.FlatAppearance.BorderSize = 0;
            btnBattleStop.Click += OnStopClicked;
            TblAdd(tbl, MakeHRow(btnBattleStart, btnBattleStop), topMargin: ScaleDpi(8));

            return outer;
        }

        private void OnBattleExecutionModeChanged()
        {
            if (rbBattleTime == null) return;
            bool isTime = rbBattleTime.Checked;
            if (nudBattleMinutes != null) nudBattleMinutes.Visible = isTime;
            if (lblBattleMinutesUnit != null) lblBattleMinutesUnit.Visible = isTime;
            if (nudBattleCount != null) nudBattleCount.Visible = !isTime;
            if (lblBattleCountUnit != null) lblBattleCountUnit.Visible = !isTime;
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
        // 頁籤 3: 行動排程檢視 (Pipeline Scheduler)
        // ─────────────────────────────────────────────────────────────────────
        private Panel BuildPipelineView()
        {
            var outer = CreateScrollableTabPanel();
            var tbl = CreateContentTable(outer);

            // 建立方案管理列（初始化所有方案控制項，包括全域按鈕 btnExportPreset, btnImportPreset）
            var pnlPresetSection = BuildPipelinePresetPanel();

            // 頂部大標題：右側放置全域備份/分享按鈕 [📤 匯出] [📥 匯入]
            AddSectionHeader(tbl, "行動排程 (Pipeline)", btnExportPreset, btnImportPreset);

            var lblDesc = new Label
            {
                Text = "依序執行階段隊列，達成指定目標後自動平滑切換至下一階段，直到全部完成。",
                ForeColor = Color.FromArgb(160, 160, 180),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(8))
            };
            TblAdd(tbl, lblDesc);

            // 當前方案操作列（單行緊湊列：當前方案標籤、下拉選單、另存、重新命名、刪除）
            TblAdd(tbl, pnlPresetSection);

            // 階段清單標題（最右側靠右放置 ● 自動儲存 綠色標記）
            var lblAutoSaveHint = new Label
            {
                Text = "● 自動儲存",
                ForeColor = Color.FromArgb(100, 185, 130),
                Font = new Font("Microsoft JhengHei UI", 9f),
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Margin = new Padding(0, ScaleDpi(2), 0, 0)
            };
            statusToolTip.SetToolTip(lblAutoSaveHint, "排程所有卡片修改、增刪與順序異動均即時自動儲存至方案與設定檔");

            AddSectionHeader(tbl, "排程階段清單", lblAutoSaveHint);

            // 階段列表容器 (卡片式)
            tblStageList = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 0,
                AutoSize = true,
                BackColor = Color.FromArgb(18, 18, 24),
                Padding = new Padding(ScaleDpi(6)),
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };
            tblStageList.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            TblAdd(tbl, tblStageList);

            // 底部操作按鈕行 (新增階段 / 清空所有階段 - 次要操作，尺寸縮小且色彩低調，避免與 開始/停止 搶焦點)
            var pnlStageBar = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, ScaleDpi(14))
            };

            btnAddStage = new IconButton
            {
                Text = " 新增階段",
                AutoSize = true,
                BackColor = Color.FromArgb(48, 68, 98), // 沉穩藍灰，區隔於開始按鈕的鮮明綠色
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Cursor = Cursors.Hand,
                IconChar = IconChar.Plus,
                IconColor = Color.White,
                IconSize = ScaleDpi(16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(10), ScaleDpi(4), ScaleDpi(10), ScaleDpi(4)),
                Margin = new Padding(0, 0, ScaleDpi(8), 0)
            };
            btnAddStage.FlatAppearance.BorderSize = 0;
            btnAddStage.Click += OnAddNewStageClicked;

            btnClearStages = new IconButton
            {
                Text = " 清空所有階段",
                AutoSize = true,
                BackColor = Color.FromArgb(58, 54, 62), // 中性低調炭灰，區隔於停止按鈕的鮮明紅色
                ForeColor = Color.FromArgb(220, 220, 220),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Cursor = Cursors.Hand,
                IconChar = IconChar.TrashCan,
                IconColor = Color.FromArgb(220, 220, 220),
                IconSize = ScaleDpi(16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(8), ScaleDpi(4), ScaleDpi(8), ScaleDpi(4)),
                Margin = Padding.Empty
            };
            btnClearStages.FlatAppearance.BorderSize = 0;
            btnClearStages.Click += (s, e) =>
            {
                if (AppSettingsManager.Current.PipelineStages.Count == 0) return;
                var res = MessageBox.Show("確定要清空所有排程階段嗎？", "清空確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res == DialogResult.Yes)
                {
                    AppSettingsManager.Current.PipelineStages.Clear();
                    AutoSavePipeline();
                    RefreshPresetComboBox(_activePipelinePreset.Id);
                    RefreshPipelineStageList();
                }
            };

            pnlStageBar.Controls.Add(btnAddStage);
            pnlStageBar.Controls.Add(btnClearStages);
            TblAdd(tbl, pnlStageBar);

            // 額外隨機延遲設定
            AddRowLabel(tbl, "排程冷卻額外隨機延遲：");
            nudPipelineDelay = new NumericUpDown
            {
                Width = ScaleDpi(110),
                Minimum = 0,
                Maximum = 60,
                Value = 1,
                BackColor = Color.FromArgb(40, 40, 52),
                ForeColor = Color.FromArgb(220, 220, 230)
            };
            nudPipelineDelay.ValueChanged += (s, e) => { if (!_isRestoringSettings) SaveCurrentUiSettings(); };
            TblAdd(tbl, MakeHRow(nudPipelineDelay, MakeLabel("秒 (0 ~ 此值)")));

            AddSectionHeader(tbl, "執行控制");
            btnPipelineStart = new Button
            {
                Text = "▶ 開始排程",
                AutoSize = true,
                BackColor = Color.FromArgb(35, 140, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Padding = new Padding(ScaleDpi(16), ScaleDpi(6), ScaleDpi(16), ScaleDpi(6)),
                Margin = new Padding(0, 0, ScaleDpi(10), 0)
            };
            btnPipelineStart.FlatAppearance.BorderSize = 0;
            btnPipelineStart.Click += OnStartClicked;

            btnPipelineStop = new Button
            {
                Text = "⏹ 停止",
                AutoSize = true,
                BackColor = Color.FromArgb(150, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false,
                Padding = new Padding(ScaleDpi(16), ScaleDpi(6), ScaleDpi(16), ScaleDpi(6)),
                Margin = Padding.Empty
            };
            btnPipelineStop.FlatAppearance.BorderSize = 0;
            btnPipelineStop.Click += OnStopClicked;
            TblAdd(tbl, MakeHRow(btnPipelineStart, btnPipelineStop), topMargin: ScaleDpi(8));

            RefreshPipelineStageList();
            return outer;
        }

        private void OnAddNewStageClicked(object? sender, EventArgs e)
        {
            var stages = AppSettingsManager.Current.PipelineStages;
            var newStage = new PipelineStage
            {
                Name = $"階段 #{stages.Count + 1}",
                ActionType = StageActionType.Training,
                TrainingActionId = "hunt",
                TrainingActionSequence = new List<string>(),
                ConditionType = StageConditionType.LevelReach,
                ConditionValue = 80
            };
            using var form = new PipelineStageEditForm(newStage, stages.Count + 1, isNew: true, _currentToken, _battleService);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                stages.Add(form.Stage);
                AutoSavePipeline();
                RefreshPresetComboBox(_activePipelinePreset.Id);
                RefreshPipelineStageList();
            }
        }

        private void StartEditStage(int index)
        {
            var stages = AppSettingsManager.Current.PipelineStages;
            if (index < 0 || index >= stages.Count) return;

            var currentStage = stages[index];
            using var form = new PipelineStageEditForm(currentStage, index + 1, isNew: false, _currentToken, _battleService);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                stages[index] = form.Stage;
                AutoSavePipeline();
                RefreshPresetComboBox(_activePipelinePreset.Id);
                RefreshPipelineStageList();
            }
        }

        private void MoveStage(int index, int direction)
        {
            var stages = AppSettingsManager.Current.PipelineStages;
            int targetIndex = index + direction;
            if (targetIndex < 0 || targetIndex >= stages.Count) return;

            var temp = stages[index];
            stages[index] = stages[targetIndex];
            stages[targetIndex] = temp;

            AutoSavePipeline();
            RefreshPresetComboBox(_activePipelinePreset.Id);
            RefreshPipelineStageList();
        }

        private Button CreateStageNavButton(string arrow, bool enabled)
        {
            var btn = new Button
            {
                Text = arrow,
                Width = ScaleDpi(28),
                Height = ScaleDpi(26),
                BackColor = enabled ? Color.FromArgb(46, 48, 62) : Color.FromArgb(24, 24, 32),
                ForeColor = enabled ? Color.FromArgb(220, 220, 235) : Color.FromArgb(85, 85, 105),
                Enabled = enabled,
                FlatStyle = FlatStyle.Flat,
                Cursor = enabled ? Cursors.Hand : Cursors.Default,
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                Margin = new Padding(ScaleDpi(2), 0, ScaleDpi(2), 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private static string GetTrainingActionDisplayName(string actionId)
        {
            var act = TrainingActions.All.FirstOrDefault(a => a.ActionId == actionId);
            return act?.DisplayName ?? actionId;
        }

        private void RefreshPipelineStageList()
        {
            if (tblStageList == null) return;

            tblStageList.SuspendLayout();
            tblStageList.Controls.Clear();
            tblStageList.RowCount = 0;
            tblStageList.RowStyles.Clear();

            var stages = AppSettingsManager.Current.PipelineStages;
            if (stages.Count == 0)
            {
                var pnlEmpty = new Panel
                {
                    Dock = DockStyle.Fill,
                    Height = ScaleDpi(70),
                    BackColor = Color.FromArgb(24, 24, 32),
                    Padding = new Padding(ScaleDpi(12)),
                    Margin = new Padding(0, ScaleDpi(4), 0, ScaleDpi(4))
                };
                var lblEmpty = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "尚未新增任何排程階段。\n點擊下方「➕ 新增階段」按鈕彈出視窗設定行動！",
                    ForeColor = Color.FromArgb(145, 145, 170),
                    Font = new Font("Microsoft JhengHei UI", 9.5f),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                pnlEmpty.Controls.Add(lblEmpty);
                tblStageList.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                tblStageList.Controls.Add(pnlEmpty, 0, 0);
            }
            else
            {
                for (int i = 0; i < stages.Count; i++)
                {
                    int index = i;
                    var st = stages[i];
                    Control card = CreateStageLockedCard(index, st);

                    tblStageList.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    tblStageList.Controls.Add(card, 0, index);
                }
            }

            EnableDoubleBuffering(tblStageList);
            tblStageList.ResumeLayout(true);
            UpdatePipelineStartButtonState();
            if (_cts == null && _selectedNavTab == "pipeline" && lblStatusRuns != null)
            {
                UpdateTelemetryStats(_currentStats);
            }
        }

        private Control CreateStageLockedCard(int index, PipelineStage st)
        {
            var stages = AppSettingsManager.Current.PipelineStages;

            var card = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 3,
                AutoSize = true,
                BackColor = Color.FromArgb(28, 28, 38),
                Padding = new Padding(ScaleDpi(12), ScaleDpi(8), ScaleDpi(12), ScaleDpi(8)),
                Margin = new Padding(0, ScaleDpi(4), 0, ScaleDpi(4))
            };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Row 0, Col 0: 階段標號（固定顯示當前順序 #1 #2 #3）
            var lblBadge = new Label
            {
                Text = $"#{index + 1} 階段",
                ForeColor = Color.FromArgb(130, 185, 255),
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(8), ScaleDpi(4))
            };

            // Row 0, Col 1: 右側操作按鈕列 (▲, ▼, ✏, ✕)
            var pnlButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Margin = new Padding(0, 0, 0, ScaleDpi(4))
            };

            var btnUp = CreateStageNavButton("▲", index > 0);
            statusToolTip.SetToolTip(btnUp, "上移此階段順位");
            btnUp.Click += (s, e) => MoveStage(index, -1);

            var btnDown = CreateStageNavButton("▼", index < stages.Count - 1);
            statusToolTip.SetToolTip(btnDown, "下移此階段順位");
            btnDown.Click += (s, e) => MoveStage(index, 1);

            var btnEdit = new Button
            {
                Text = "✏",
                Width = ScaleDpi(28),
                Height = ScaleDpi(26),
                BackColor = Color.FromArgb(48, 62, 88),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Margin = new Padding(ScaleDpi(2), 0, ScaleDpi(2), 0)
            };
            btnEdit.FlatAppearance.BorderSize = 0;
            statusToolTip.SetToolTip(btnEdit, "編輯此階段");
            btnEdit.Click += (s, e) => StartEditStage(index);

            var btnDel = new Button
            {
                Text = "✕",
                Width = ScaleDpi(28),
                Height = ScaleDpi(26),
                BackColor = Color.FromArgb(75, 42, 42),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Margin = new Padding(ScaleDpi(2), 0, 0, 0)
            };
            btnDel.FlatAppearance.BorderSize = 0;
            statusToolTip.SetToolTip(btnDel, "刪除此階段");
            btnDel.Click += (s, e) =>
            {
                stages.RemoveAt(index);
                AutoSavePipeline();
                RefreshPresetComboBox(_activePipelinePreset.Id);
                RefreshPipelineStageList();
            };

            pnlButtons.Controls.Add(btnUp);
            pnlButtons.Controls.Add(btnDown);
            pnlButtons.Controls.Add(btnEdit);
            pnlButtons.Controls.Add(btnDel);

            // Row 1, Col 0 (ColSpan 2): 執行動作
            var pnlActionRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, ScaleDpi(3))
            };

            var lblActionTitle = new Label
            {
                Text = "執行：",
                ForeColor = Color.FromArgb(170, 175, 195),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(2), 0)
            };

            string actionText;
            if (st.ActionType == StageActionType.Training)
            {
                if (st.TrainingActionSequence != null && st.TrainingActionSequence.Count > 1)
                {
                    var names = st.TrainingActionSequence.Select(GetTrainingActionDisplayName).ToList();
                    actionText = $"[訓練] {string.Join(" → ", names)}";
                }
                else
                {
                    actionText = $"[訓練] {GetTrainingActionDisplayName(st.TrainingActionId)}";
                }
            }
            else
            {
                actionText = $"[戰鬥] {(st.BattleType == BattleType.Challenge ? "友好切磋" : "我要茶渡你")} (ID: {st.BattleTargetId})";
            }

            var lblActionVal = new Label
            {
                Text = actionText,
                ForeColor = Color.FromArgb(235, 235, 245),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(2), 0, 0)
            };
            statusToolTip.SetToolTip(lblActionVal, actionText);

            pnlActionRow.Controls.Add(lblActionTitle);
            pnlActionRow.Controls.Add(lblActionVal);

            // 若為戰鬥且有已驗證的對手資訊，保留顯示對手名稱與角色等級
            FlowLayoutPanel? pnlOpponentRow = null;
            if (st.ActionType == StageActionType.Battle && (!string.IsNullOrEmpty(st.BattleTargetName) || !string.IsNullOrEmpty(st.BattleTargetDetail)))
            {
                pnlOpponentRow = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = true,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0, 0, 0, ScaleDpi(3))
                };

                var lblOpponentTitle = new Label
                {
                    Text = "對手：",
                    ForeColor = Color.FromArgb(170, 175, 195),
                    Font = new Font("Microsoft JhengHei UI", 9.5f),
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Margin = new Padding(0, ScaleDpi(2), ScaleDpi(2), 0)
                };

                string oppInfo = string.IsNullOrEmpty(st.BattleTargetDetail)
                    ? st.BattleTargetName
                    : $"{st.BattleTargetName} ｜ 角色：{st.BattleTargetDetail}";

                var lblOpponentVal = new Label
                {
                    Text = oppInfo,
                    ForeColor = Color.FromArgb(130, 215, 160),
                    Font = new Font("Microsoft JhengHei UI", 9.5f),
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Margin = new Padding(0, ScaleDpi(2), 0, 0)
                };

                pnlOpponentRow.Controls.Add(lblOpponentTitle);
                pnlOpponentRow.Controls.Add(lblOpponentVal);
            }

            // Row 達成條件
            var pnlCondRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };

            var lblCondTitle = new Label
            {
                Text = "直到：",
                ForeColor = Color.FromArgb(170, 175, 195),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(1), ScaleDpi(2), 0)
            };

            var lblCondVal = new Label
            {
                Text = st.GetConditionSummary(),
                ForeColor = Color.FromArgb(200, 215, 240),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(1), 0, 0)
            };
            statusToolTip.SetToolTip(lblCondVal, $"達成條件：{st.GetConditionSummary()}");

            pnlCondRow.Controls.Add(lblCondTitle);
            pnlCondRow.Controls.Add(lblCondVal);

            card.Controls.Add(lblBadge, 0, 0);
            card.Controls.Add(pnlButtons, 1, 0);

            int curRow = 1;
            card.Controls.Add(pnlActionRow, 0, curRow);
            card.SetColumnSpan(pnlActionRow, 2);
            curRow++;

            if (pnlOpponentRow != null)
            {
                card.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                card.Controls.Add(pnlOpponentRow, 0, curRow);
                card.SetColumnSpan(pnlOpponentRow, 2);
                curRow++;
            }

            card.Controls.Add(pnlCondRow, 0, curRow);
            card.SetColumnSpan(pnlCondRow, 2);
            card.RowCount = curRow + 1;

            return card;
        }

        // =====================================================================
        // 方案化排程管理邏輯 (Preset Management & Export / Import)
        // =====================================================================
        private void InitializePipelinePresets()
        {
            var presets = _pipelinePresetManager.LoadAll();
            if (presets.Count == 0)
            {
                var initial = new PipelinePreset
                {
                    Name = "預設排程",
                    Stages = new List<PipelineStage>(AppSettingsManager.Current.PipelineStages)
                };
                _pipelinePresetManager.Save(initial);
                presets.Add(initial);
            }

            string lastId = AppSettingsManager.Current.ActivePipelinePresetId;
            _activePipelinePreset = presets.FirstOrDefault(p => p.Id == lastId) ?? presets[0];

            AppSettingsManager.Current.ActivePipelinePresetId = _activePipelinePreset.Id;
            AppSettingsManager.Current.PipelineStages = _activePipelinePreset.Stages;
        }

        private Panel BuildPipelinePresetPanel()
        {
            var pnlPresetBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.FromArgb(28, 28, 38),
                Padding = new Padding(ScaleDpi(8), ScaleDpi(6), ScaleDpi(8), ScaleDpi(6)),
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };

            var lblPresetTitle = new Label
            {
                Text = "當前方案：",
                ForeColor = Color.FromArgb(210, 210, 235),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(5), ScaleDpi(4), ScaleDpi(2))
            };

            cmbPipelinePresets = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ScaleDpi(150),
                BackColor = Color.FromArgb(42, 42, 56),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(6), ScaleDpi(2))
            };
            cmbPipelinePresets.SelectedIndexChanged += OnPresetSelectionChanged;

            // 另存、重新命名、刪除（全面自動儲存，移除多餘的手動儲存按鈕）
            btnNewPreset = CreateCompactButton("另存", Color.FromArgb(42, 120, 75), IconChar.Plus);
            btnNewPreset.Margin = new Padding(0, ScaleDpi(2), ScaleDpi(4), ScaleDpi(2));
            btnNewPreset.Click += (s, e) => CreateNewPreset();
            statusToolTip.SetToolTip(btnNewPreset, "以目前排程階段清單另存為新方案");

            btnRenamePreset = CreateCompactButton("重新命名", Color.FromArgb(55, 60, 75), IconChar.PenToSquare);
            btnRenamePreset.Margin = new Padding(0, ScaleDpi(2), ScaleDpi(4), ScaleDpi(2));
            btnRenamePreset.Click += (s, e) => RenameActivePreset();
            statusToolTip.SetToolTip(btnRenamePreset, "變更當前方案名稱");

            btnDeletePreset = CreateCompactButton("刪除", Color.FromArgb(85, 45, 45), IconChar.TrashCan);
            btnDeletePreset.Margin = new Padding(0, ScaleDpi(2), ScaleDpi(6), ScaleDpi(2));
            btnDeletePreset.Click += (s, e) => DeleteActivePreset();
            statusToolTip.SetToolTip(btnDeletePreset, "刪除當前方案");

            // 匯出與匯入按鈕（將掛載於頁面最頂部標題列右側，不佔用方案列寬度）
            btnExportPreset = CreateCompactButton("匯出", Color.FromArgb(50, 65, 88), IconChar.FileExport);
            btnExportPreset.Margin = new Padding(0, ScaleDpi(2), ScaleDpi(4), ScaleDpi(2));
            btnExportPreset.Click += (s, e) => ExportActivePreset();
            statusToolTip.SetToolTip(btnExportPreset, "將當前方案匯出成 JSON 檔案以供備份或分享");

            btnImportPreset = CreateCompactButton("匯入", Color.FromArgb(50, 65, 88), IconChar.FileImport);
            btnImportPreset.Margin = new Padding(ScaleDpi(2), ScaleDpi(2), 0, ScaleDpi(2));
            btnImportPreset.Click += (s, e) => ImportPresetFromFile();
            statusToolTip.SetToolTip(btnImportPreset, "從 JSON 檔案匯入方案");

            pnlPresetBar.Controls.Add(lblPresetTitle);
            pnlPresetBar.Controls.Add(cmbPipelinePresets);
            pnlPresetBar.Controls.Add(btnNewPreset);
            pnlPresetBar.Controls.Add(btnRenamePreset);
            pnlPresetBar.Controls.Add(btnDeletePreset);

            RefreshPresetComboBox();
            return pnlPresetBar;
        }

        private IconButton CreateCompactButton(string text, Color backColor, IconChar icon = IconChar.None)
        {
            var btn = new IconButton
            {
                Text = " " + text,
                AutoSize = true,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Cursor = Cursors.Hand,
                Padding = new Padding(ScaleDpi(8), ScaleDpi(3), ScaleDpi(8), ScaleDpi(3)),
                Margin = new Padding(0, ScaleDpi(2), ScaleDpi(6), ScaleDpi(2))
            };
            if (icon != IconChar.None)
            {
                btn.IconChar = icon;
                btn.IconColor = Color.White;
                btn.IconSize = ScaleDpi(16);
                btn.TextImageRelation = TextImageRelation.ImageBeforeText;
                btn.ImageAlign = ContentAlignment.MiddleLeft;
            }
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void RefreshPresetComboBox(string? selectId = null)
        {
            if (cmbPipelinePresets == null) return;

            cmbPipelinePresets.SelectedIndexChanged -= OnPresetSelectionChanged;
            cmbPipelinePresets.Items.Clear();

            var presets = _pipelinePresetManager.LoadAll();
            int selectedIndex = 0;
            string targetId = selectId ?? _activePipelinePreset?.Id ?? string.Empty;

            for (int i = 0; i < presets.Count; i++)
            {
                cmbPipelinePresets.Items.Add(presets[i]);
                if (presets[i].Id == targetId)
                {
                    selectedIndex = i;
                }
            }

            if (cmbPipelinePresets.Items.Count > 0)
            {
                cmbPipelinePresets.SelectedIndex = selectedIndex;
                _activePipelinePreset = (PipelinePreset)cmbPipelinePresets.Items[selectedIndex]!;
            }

            cmbPipelinePresets.SelectedIndexChanged += OnPresetSelectionChanged;
        }

        private void OnPresetSelectionChanged(object? sender, EventArgs e)
        {
            if (cmbPipelinePresets.SelectedItem is PipelinePreset selected)
            {
                _activePipelinePreset = selected;
                AppSettingsManager.Current.ActivePipelinePresetId = selected.Id;
                AppSettingsManager.Current.PipelineStages = selected.Stages;
                AppSettingsManager.Save();

                RefreshPipelineStageList();
            }
        }

        private void AutoSavePipeline()
        {
            if (_activePipelinePreset != null)
            {
                _activePipelinePreset.Stages = AppSettingsManager.Current.PipelineStages;
                _activePipelinePreset.UpdatedAt = DateTime.Now;
                _pipelinePresetManager.Save(_activePipelinePreset);
            }
            AppSettingsManager.Save();
            UpdatePipelineStartButtonState();
        }

        private void UpdatePipelineStartButtonState()
        {
            if (btnPipelineStart == null) return;

            if (_cts != null)
            {
                btnPipelineStart.Enabled = false;
                statusToolTip.SetToolTip(btnPipelineStart, "排程執行中");
                return;
            }

            bool hasStages = AppSettingsManager.Current.PipelineStages.Count > 0;
            btnPipelineStart.Enabled = hasStages;
            btnPipelineStart.BackColor = hasStages ? Color.FromArgb(35, 140, 70) : Color.FromArgb(50, 65, 55);
            statusToolTip.SetToolTip(btnPipelineStart, hasStages ? "開始依序執行排程階段隊列" : "尚未新增排程階段，請先新增階段");
        }

        private void CreateNewPreset()
        {
            string baseName = (_activePipelinePreset?.Name ?? "新排程方案").Replace(" (副本)", "").Replace("(副本)", "").Trim();
            string? name = PromptDialog.Show(this, "另存新排程方案", "請輸入新方案名稱：", baseName);
            if (string.IsNullOrWhiteSpace(name)) return;

            var newPreset = _activePipelinePreset != null
                ? _activePipelinePreset.Clone(name.Trim())
                : new PipelinePreset { Name = name.Trim() };

            _pipelinePresetManager.Save(newPreset);
            _activePipelinePreset = newPreset;
            AppSettingsManager.Current.ActivePipelinePresetId = newPreset.Id;
            AppSettingsManager.Current.PipelineStages = newPreset.Stages;
            AppSettingsManager.Save();

            RefreshPresetComboBox(newPreset.Id);
            RefreshPipelineStageList();

            MessageBox.Show($"已成功另存新方案「{newPreset.Name}」！", "新增成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RenameActivePreset()
        {
            if (_activePipelinePreset == null) return;

            string? newName = PromptDialog.Show(this, "重新命名方案", "請輸入方案的新名稱：", _activePipelinePreset.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName.Trim() == _activePipelinePreset.Name) return;

            _activePipelinePreset.Name = newName.Trim();
            _pipelinePresetManager.Save(_activePipelinePreset);
            RefreshPresetComboBox(_activePipelinePreset.Id);
        }

        private void DeleteActivePreset()
        {
            if (_activePipelinePreset == null) return;

            var all = _pipelinePresetManager.LoadAll();
            if (all.Count <= 1)
            {
                MessageBox.Show("至少必須保留一個排程方案，無法刪除最後一個方案。", "無法刪除", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"確定要刪除排程方案「{_activePipelinePreset.Name}」嗎？\n此動作無法復原。",
                "刪除確認",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            _pipelinePresetManager.Delete(_activePipelinePreset.Id);

            all = _pipelinePresetManager.LoadAll();
            _activePipelinePreset = all[0];
            AppSettingsManager.Current.ActivePipelinePresetId = _activePipelinePreset.Id;
            AppSettingsManager.Current.PipelineStages = _activePipelinePreset.Stages;
            AppSettingsManager.Save();

            RefreshPresetComboBox(_activePipelinePreset.Id);
            RefreshPipelineStageList();
        }

        private void ExportActivePreset()
        {
            if (_activePipelinePreset == null) return;

            using var sfd = new SaveFileDialog
            {
                Title = "匯出排程方案",
                Filter = "排程方案檔案 (*.json)|*.json|所有檔案 (*.*)|*.*",
                FileName = $"{_activePipelinePreset.Name}.json"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    _pipelinePresetManager.ExportToFile(_activePipelinePreset, sfd.FileName);
                    MessageBox.Show($"排程方案「{_activePipelinePreset.Name}」已成功匯出至：\n{sfd.FileName}", "匯出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"匯出失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportPresetFromFile()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "匯入排程方案",
                Filter = "排程方案檔案 (*.json)|*.json|所有檔案 (*.*)|*.*"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    var imported = _pipelinePresetManager.ImportFromFile(ofd.FileName);
                    _activePipelinePreset = imported;
                    AppSettingsManager.Current.ActivePipelinePresetId = imported.Id;
                    AppSettingsManager.Current.PipelineStages = imported.Stages;
                    AppSettingsManager.Save();

                    RefreshPresetComboBox(imported.Id);
                    RefreshPipelineStageList();

                    MessageBox.Show($"成功匯入排程方案「{imported.Name}」（包含 {imported.Stages.Count} 個階段）！", "匯入成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"匯入失敗：{ex.Message}", "匯入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 頁籤 4: 事件應對偏好檢視 (Events View)
        // ─────────────────────────────────────────────────────────────────────
        private Panel BuildEventsView()
        {
            var outer = CreateScrollableTabPanel();
            var tbl = CreateContentTable(outer);

            AddSectionHeader(tbl, "遭遇事件應對");

            // ── 第一層：自動選擇 ──
            rbEventAuto = new RadioButton
            {
                Text = "自動選擇",
                Checked = true,
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true
            };
            rbEventAuto.CheckedChanged += (_, _) => OnEventModeGroupChanged();
            TblAdd(tbl, rbEventAuto);

            // 第二層：自動子選項
            pnlEventAutoSub = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(ScaleDpi(24), 2, 0, 8)
            };
            var flpAutoSub = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = Padding.Empty
            };
            rbEventAutoHighest = new RadioButton
            {
                Text = "優先選最高成功率",
                Checked = true,
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true
            };
            rbEventAutoRandom = new RadioButton
            {
                Text = "完全隨機",
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 0)
            };
            rbEventAutoHighest.CheckedChanged += (_, _) => SaveEventPreferences();
            rbEventAutoRandom.CheckedChanged  += (_, _) => SaveEventPreferences();
            flpAutoSub.Controls.Add(rbEventAutoHighest);
            flpAutoSub.Controls.Add(rbEventAutoRandom);
            pnlEventAutoSub.Controls.Add(flpAutoSub);
            TblAdd(tbl, pnlEventAutoSub);

            // ── 第一層：手動選擇 ──
            rbEventManual = new RadioButton
            {
                Text = "手動選擇",
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 0)
            };
            rbEventManual.CheckedChanged += (_, _) => OnEventModeGroupChanged();
            TblAdd(tbl, rbEventManual);

            // 第二層：手動子選項
            pnlEventManualSub = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Enabled = false,
                Margin = new Padding(ScaleDpi(24), 2, 0, 10)
            };
            var flpManualSub = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = Padding.Empty
            };
            rbEventManualInline = new RadioButton
            {
                Text = "內嵌於主畫面（零干擾）",
                Checked = true,
                ForeColor = Color.FromArgb(120, 120, 140),
                AutoSize = true
            };
            rbEventManualPopup = new RadioButton
            {
                Text = "獨立彈出視窗（奪取焦點）",
                ForeColor = Color.FromArgb(120, 120, 140),
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 0)
            };
            rbEventManualInline.CheckedChanged += (_, _) => SaveEventPreferences();
            rbEventManualPopup.CheckedChanged  += (_, _) => SaveEventPreferences();
            flpManualSub.Controls.Add(rbEventManualInline);
            flpManualSub.Controls.Add(rbEventManualPopup);
            pnlEventManualSub.Controls.Add(flpManualSub);
            TblAdd(tbl, pnlEventManualSub);

            AddSectionHeader(tbl, "系統通知");
            chkEventNotify = new CheckBox
            {
                Text = "啟用 Windows 系統通知（僅內嵌模式有效）",
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
                Margin = new Padding(ScaleDpi(24), 0, 0, 12)
            };
            chkEventNotifySound.CheckedChanged += (_, _) => SaveEventPreferences();
            TblAdd(tbl, chkEventNotifySound);

            AddSectionHeader(tbl, "第三方事件檔案庫");

            var pnlArchiveRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 6)
            };
            pnlArchiveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pnlArchiveRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            lblEventArchiveStatus = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(170, 175, 200),
                Font = new Font("Microsoft JhengHei UI", 9f),
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, ScaleDpi(4), 0, 0)
            };

            btnUpdateEventArchive = new IconButton
            {
                Text = " 更新事件庫",
                AutoSize = true,
                BackColor = Color.FromArgb(48, 68, 98),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Cursor = Cursors.Hand,
                IconChar = IconChar.Rotate,
                IconColor = Color.White,
                IconSize = ScaleDpi(18),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ScaleDpi(10), ScaleDpi(4), ScaleDpi(10), ScaleDpi(4)),
                Margin = new Padding(ScaleDpi(8), 0, 0, 0)
            };
            btnUpdateEventArchive.FlatAppearance.BorderSize = 0;
            btnUpdateEventArchive.Click += OnUpdateEventArchiveClicked;

            pnlArchiveRow.Controls.Add(lblEventArchiveStatus, 0, 0);
            pnlArchiveRow.Controls.Add(btnUpdateEventArchive, 1, 0);
            TblAdd(tbl, pnlArchiveRow);

            var lblArchiveHint = new Label
            {
                Text = "資料來源：我的同人攻略圖鑑網。資料將保存至本機 data/events/ (EventBrief.json 與 EventRoute.json)。",
                ForeColor = Color.FromArgb(130, 130, 150),
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12)
            };
            TblAdd(tbl, lblArchiveHint);

            UpdateEventArchiveStatusText();

            return outer;
        }

        private void UpdateEventArchiveStatusText()
        {
            if (lblEventArchiveStatus == null) return;
            var meta = _eventArchiveService.GetMetadata();
            string timeText = meta.LastUpdatedAt.HasValue
                ? meta.LastUpdatedAt.Value.ToString("yyyy/MM/dd HH:mm")
                : "尚未更新";
            lblEventArchiveStatus.Text = $"本機事件庫：{meta.BriefEventCount} 個事件、{meta.RouteChainCount} 條路線 ｜ 上次更新: {timeText}";
            lblEventArchiveStatus.ForeColor = Color.FromArgb(140, 200, 160);
        }

        private async void OnUpdateEventArchiveClicked(object? sender, EventArgs e)
        {
            if (btnUpdateEventArchive == null) return;

            // 鎖定畫面，避免尚未更新完成使用者搶著操作
            btnUpdateEventArchive.Enabled = false;
            var prevCursor = this.Cursor;
            this.Cursor = Cursors.WaitCursor;

            bool prevAuto = rbEventAuto.Enabled;
            bool prevManual = rbEventManual.Enabled;
            bool prevNotify = chkEventNotify.Enabled;
            bool prevNotifySound = chkEventNotifySound.Enabled;

            rbEventAuto.Enabled = false;
            rbEventManual.Enabled = false;
            chkEventNotify.Enabled = false;
            chkEventNotifySound.Enabled = false;
            foreach (var navBtn in _navButtons) navBtn.Enabled = false;

            lblEventArchiveStatus.Text = "⏳ 正在連線至【我的同仁攻略圖鑑網】取得最新資料...";
            lblEventArchiveStatus.ForeColor = Color.FromArgb(235, 185, 80);
            statusToolTip.SetToolTip(btnUpdateEventArchive, "事件庫更新中，畫面已鎖定");

            try
            {
                var result = await Task.Run(async () =>
                {
                    return await _eventArchiveService.UpdateFromRemoteAsync(
                        onProgress: msg =>
                        {
                            if (!this.IsDisposed && this.IsHandleCreated)
                            {
                                try
                                {
                                    this.BeginInvoke(() =>
                                    {
                                        lblEventArchiveStatus.Text = $"⏳ {msg}";
                                    });
                                }
                                catch { }
                            }
                        });
                });

                if (result.Success)
                {
                    static string FormatDelta(int diff) => diff switch
                    {
                        > 0 => $"+{diff}",
                        < 0 => $"{diff}",
                        _ => "無增減"
                    };

                    string briefDelta = FormatDelta(result.BriefEventDiff);
                    string chainDelta = FormatDelta(result.RouteChainDiff);

                    UpdateEventArchiveStatusText();
                    AppendLog($"[事件庫] {result.Message}", Color.FromArgb(100, 220, 140));

                    string detail = "事件庫已更新完成！\n\n" +
                                    $"• 事件總數：{result.BriefEventCount} 個（{briefDelta}）\n" +
                                    $"• 路線總數：{result.RouteChainCount} 條（{chainDelta}）\n" +
                                    $"• 路線節點：{result.RouteEventCount} 個（{FormatDelta(result.RouteEventDiff)}）\n\n";

                    MessageBox.Show(this, detail, "事件庫更新成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                lblEventArchiveStatus.Text = $"❌ 更新失敗：{ex.Message}";
                lblEventArchiveStatus.ForeColor = Color.FromArgb(235, 90, 90);
                AppendLog($"[事件庫] 更新失敗：{ex.Message}", Color.FromArgb(235, 90, 90));
                MessageBox.Show(this, $"更新事件庫時發生錯誤：\n{ex.Message}", "更新失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnUpdateEventArchive.Enabled = true;
                rbEventAuto.Enabled = prevAuto;
                rbEventManual.Enabled = prevManual;
                chkEventNotify.Enabled = prevNotify;
                chkEventNotifySound.Enabled = prevNotifySound;
                foreach (var navBtn in _navButtons) navBtn.Enabled = true;
                this.Cursor = prevCursor;
                statusToolTip.SetToolTip(btnUpdateEventArchive, "點擊連線第三方網站線上更新本機事件庫");
            }
        }

        private void OnEventModeGroupChanged()
        {
            bool isAuto = rbEventAuto.Checked;
            pnlEventAutoSub.Enabled = isAuto;
            rbEventAutoHighest.ForeColor = isAuto ? Color.FromArgb(210, 210, 225) : Color.FromArgb(120, 120, 140);
            rbEventAutoRandom.ForeColor = isAuto ? Color.FromArgb(210, 210, 225) : Color.FromArgb(120, 120, 140);

            bool isManual = rbEventManual.Checked;
            pnlEventManualSub.Enabled = isManual;
            rbEventManualInline.ForeColor = isManual ? Color.FromArgb(210, 210, 225) : Color.FromArgb(120, 120, 140);
            rbEventManualPopup.ForeColor = isManual ? Color.FromArgb(210, 210, 225) : Color.FromArgb(120, 120, 140);

            SaveEventPreferences();
        }

        private void SaveEventPreferences()
        {
            if (_isRestoringSettings) return;

            if (rbEventAuto.Checked)
            {
                AppSettingsManager.Current.EventMode = rbEventAutoRandom.Checked ? "auto-random" : "auto";
            }
            else
            {
                AppSettingsManager.Current.EventMode = rbEventManualPopup.Checked ? "popup" : "inline";
            }

            if (rbEventManualPopup.Checked)
                AppSettingsManager.Current.ManualDisplayMode = "popup";
            else if (rbEventManualInline.Checked)
                AppSettingsManager.Current.ManualDisplayMode = "inline";

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
                RowCount = 3,
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

            btnStatsQuery = new IconButton
            {
                Text = " 取得數據",
                AutoSize = true,
                BackColor = Color.FromArgb(50, 65, 100),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                IconChar = IconChar.MagnifyingGlass,
                IconColor = Color.White,
                IconSize = ScaleDpi(18),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
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

            var lblNotice = new Label
            {
                Text = "💡 提示：戰鬥 LOG 種類繁多，部分特殊技能或效果可能存在無法精確辨識歸屬的誤差，統計數據僅供參考。",
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 160, 90),
                Font = new Font("Microsoft JhengHei UI", 8.8f),
                Margin = new Padding(0, 4, 0, 0)
            };
            pnlTop.Controls.Add(lblNotice, 0, 2);
            pnlTop.SetColumnSpan(lblNotice, 3);

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

            // ── 區段 1: 連線設定 ──
            AddSectionHeader(tbl, "連線設定");

            AddRowLabel(tbl, "Bearer Token：");

            var tblTokenRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 2, 0, 6),
                Padding = Padding.Empty
            };
            tblTokenRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tblTokenRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

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

            btnSaveSettingsToken = new IconButton
            {
                Text = " 儲存 Token 設定",
                AutoSize = true,
                BackColor = Color.FromArgb(40, 140, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                IconChar = IconChar.FloppyDisk,
                IconColor = Color.White,
                IconSize = ScaleDpi(18),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(ScaleDpi(8), 0, 0, 0)
            };
            btnSaveSettingsToken.FlatAppearance.BorderSize = 0;
            btnSaveSettingsToken.Click += async (s, e) =>
            {
                var input = txtSettingsToken.Text.Trim();
                if (string.IsNullOrWhiteSpace(input))
                {
                    _currentToken = string.Empty;
                    AppSettingsManager.Current.Token = string.Empty;
                    AppSettingsManager.Save();
                    UserContext.Clear();
                    UpdateTokenStatusVisual();
                    AppendLog("[系統] Token 設定已清除。", Color.FromArgb(200, 200, 210));
                    MessageBox.Show(this, "Token 已清除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                btnSaveSettingsToken.Enabled = false;
                btnSaveSettingsToken.Text = " 驗證中...";
                try
                {
                    await ValidateAndApplyTokenAsync(input, isFirstTime: false);
                }
                finally
                {
                    btnSaveSettingsToken.Enabled = true;
                    btnSaveSettingsToken.Text = " 儲存 Token 設定";
                }
            };

            tblTokenRow.Controls.Add(txtSettingsToken, 0, 0);
            tblTokenRow.Controls.Add(btnSaveSettingsToken, 1, 0);
            TblAdd(tbl, tblTokenRow);

            lblTokenStatusDetail = new Label
            {
                Text = string.IsNullOrWhiteSpace(_currentToken) ? "⚠ 尚未設定 Token" : "✓ Token 已設定",
                ForeColor = string.IsNullOrWhiteSpace(_currentToken) ? Color.FromArgb(220, 90, 90) : Color.FromArgb(80, 220, 120),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };
            TblAdd(tbl, lblTokenStatusDetail);

            // ── 區段 2: 樓層獎勵設定 ──
            AddSectionHeader(tbl, "樓層獎勵設定");

            var pnlFloorRewardGroup = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0, ScaleDpi(2), 0, ScaleDpi(12)),
                Padding = Padding.Empty
            };

            chkSettingsAutoClaimFloorReward = new CheckBox
            {
                Text = "自動領取樓層獎勵",
                ForeColor = Color.FromArgb(220, 220, 230),
                BackColor = Color.Transparent,
                AutoSize = true,
                Checked = AppSettingsManager.Current.AutoClaimFloorReward,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Margin = new Padding(ScaleDpi(16), 0, 0, ScaleDpi(4)),
                Cursor = Cursors.Hand
            };
            chkSettingsAutoClaimFloorReward.CheckedChanged += async (s, e) =>
            {
                if (!_isRestoringSettings)
                {
                    AppSettingsManager.Current.AutoClaimFloorReward = chkSettingsAutoClaimFloorReward.Checked;
                    AppSettingsManager.Save();
                    UpdateFloorRewardStatusVisual();

                    if (chkSettingsAutoClaimFloorReward.Checked &&
                        UserContext.Current.IsAuthenticated &&
                        UserContext.Current.CanClaimFloorReward)
                    {
                        await TryClaimFloorRewardAsync();
                    }
                }
            };
            pnlFloorRewardGroup.Controls.Add(chkSettingsAutoClaimFloorReward);

            var lblFloorRewardNote = new Label
            {
                Text = "💡 系統每 60 秒計算一次剩餘冷卻時間，領取時程可能存在約 60 秒以內的誤差。",
                ForeColor = Color.FromArgb(140, 140, 160),
                Font = new Font("Microsoft JhengHei UI", 8.5f),
                AutoSize = true,
                Margin = new Padding(ScaleDpi(36), 0, 0, 0)
            };
            pnlFloorRewardGroup.Controls.Add(lblFloorRewardNote);

            TblAdd(tbl, pnlFloorRewardGroup);

            // ── 區段 3: 應用程式圖示 ──
            AddSectionHeader(tbl, "應用程式圖示設定");

            var pnlIconRow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(ScaleDpi(16), ScaleDpi(2), 0, ScaleDpi(16)),
                Padding = Padding.Empty
            };

            picCurrentIconPreview = new PictureBox
            {
                Size = new Size(ScaleDpi(32), ScaleDpi(32)),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, ScaleDpi(10), 0)
            };
            pnlIconRow.Controls.Add(picCurrentIconPreview);

            lblCurrentIconName = new Label
            {
                Text = $"目前圖示：{AppSettingsManager.Current.SelectedAppIcon}",
                ForeColor = Color.FromArgb(200, 200, 215),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(6), ScaleDpi(16), 0)
            };
            pnlIconRow.Controls.Add(lblCurrentIconName);

            var btnChangeIcon = new Button
            {
                Text = "更改圖示",
                AutoSize = true,
                BackColor = Color.FromArgb(45, 120, 200),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Padding = new Padding(ScaleDpi(14), ScaleDpi(4), ScaleDpi(14), ScaleDpi(4)),
                Margin = Padding.Empty
            };
            btnChangeIcon.FlatAppearance.BorderSize = 0;
            btnChangeIcon.MouseEnter += (s, e) => btnChangeIcon.BackColor = Color.FromArgb(55, 140, 230);
            btnChangeIcon.MouseLeave += (s, e) => btnChangeIcon.BackColor = Color.FromArgb(45, 120, 200);
            btnChangeIcon.Click += (s, e) =>
            {
                using var dlg = new IconSelectionForm(AppSettingsManager.Current.SelectedAppIcon);
                if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedIconFileName))
                {
                    ApplyAppIcon(dlg.SelectedIconFileName);
                    UpdateCurrentIconPreview();
                }
            };
            pnlIconRow.Controls.Add(btnChangeIcon);

            TblAdd(tbl, pnlIconRow);
            UpdateCurrentIconPreview();

            return outer;
        }

        private void UpdateTokenStatusVisual()
        {
            if (string.IsNullOrWhiteSpace(_currentToken))
            {
                lblSidebarToken.Text = "⚠ Token 未設定";
                lblSidebarToken.ForeColor = Color.FromArgb(220, 90, 90);
                lblSidebarToken.Visible = true;
                if (lblStatusToken != null)
                {
                    lblStatusToken.Text = "● Token：未設定";
                    lblStatusToken.ForeColor = Color.FromArgb(220, 90, 90);
                }
                if (lblStatusLevel != null)
                {
                    lblStatusLevel.Text = UserContext.Current.SummaryText;
                    lblStatusLevel.ForeColor = Color.FromArgb(245, 210, 60);
                }
                if (lblTokenStatusDetail != null)
                {
                    lblTokenStatusDetail.Text = "⚠ 尚未設定 Token，無法發送任何 API 請求。";
                    lblTokenStatusDetail.ForeColor = Color.FromArgb(220, 90, 90);
                }
            }
            else if (UserContext.Current.IsAuthenticated)
            {
                lblSidebarToken.Text = string.Empty;
                lblSidebarToken.Visible = false;
                if (lblStatusToken != null)
                {
                    lblStatusToken.Text = "● Token：已驗證";
                    lblStatusToken.ForeColor = Color.FromArgb(80, 220, 120);
                }
                if (lblStatusLevel != null)
                {
                    lblStatusLevel.Text = UserContext.Current.SummaryText;
                    lblStatusLevel.ForeColor = Color.FromArgb(245, 210, 60);
                }
                if (lblTokenStatusDetail != null)
                {
                    lblTokenStatusDetail.Text = $"✓ Token 已驗證（{UserContext.Current.PlayerName}）";
                    lblTokenStatusDetail.ForeColor = Color.FromArgb(80, 220, 120);
                }
            }
            else
            {
                lblSidebarToken.Text = "⚠ Token 未驗證";
                lblSidebarToken.ForeColor = Color.FromArgb(220, 160, 60);
                lblSidebarToken.Visible = true;
                if (lblStatusToken != null)
                {
                    lblStatusToken.Text = "● Token：未驗證";
                    lblStatusToken.ForeColor = Color.FromArgb(220, 160, 60);
                }
                if (lblStatusLevel != null)
                {
                    lblStatusLevel.Text = UserContext.Current.SummaryText;
                    lblStatusLevel.ForeColor = Color.FromArgb(245, 210, 60);
                }
                if (lblTokenStatusDetail != null)
                {
                    lblTokenStatusDetail.Text = "⚠ Token 尚未通過驗證或正在連線。";
                    lblTokenStatusDetail.ForeColor = Color.FromArgb(220, 160, 60);
                }
            }

            UpdateFloorRewardStatusVisual();
        }

        /// <summary>
        /// 更新右下角樓層獎勵狀態指示。
        /// 若已啟用自動領取，顯示「樓層獎勵：自動領取」；
        /// 否則依 cooldownRemainingMs 判定顯示「樓層獎勵：冷卻中」或「樓層獎勵：可領取」。
        /// 採用一般色彩（非黃色）。
        /// </summary>
        private void UpdateFloorRewardStatusVisual()
        {
            if (lblStatusFloorReward == null) return;

            if (AppSettingsManager.Current.AutoClaimFloorReward)
            {
                lblStatusFloorReward.Text = "樓層獎勵：自動領取";
            }
            else
            {
                if (!UserContext.Current.IsAuthenticated)
                {
                    lblStatusFloorReward.Text = "樓層獎勵：—";
                }
                else if (UserContext.Current.CanClaimFloorReward)
                {
                    lblStatusFloorReward.Text = "樓層獎勵：可領取";
                }
                else
                {
                    lblStatusFloorReward.Text = "樓層獎勵：冷卻中";
                }
            }

            // 更新 ToolTip 顯示下次領取時間
            if (statusToolTip != null)
            {
                if (!UserContext.Current.IsAuthenticated || !UserContext.Current.NextFloorRewardClaimTime.HasValue)
                {
                    statusToolTip.SetToolTip(lblStatusFloorReward, "尚未取得樓層獎勵資料");
                }
                else if (UserContext.Current.CanClaimFloorReward)
                {
                    statusToolTip.SetToolTip(lblStatusFloorReward, "下次領取時間：目前已可領取");
                }
                else
                {
                    var nextTimeLocal = UserContext.Current.NextFloorRewardClaimTime.Value.ToLocalTime();
                    statusToolTip.SetToolTip(lblStatusFloorReward, $"下次領取時間：{nextTimeLocal:yyyy/MM/dd HH:mm:ss}");
                }
            }
        }

        private bool _isClaimingFloorReward;

        /// <summary>
        /// 嘗試領取樓層獎勵。若成功以黃色字體記錄日誌，並即時更新 UI。
        /// </summary>
        private async Task<bool> TryClaimFloorRewardAsync()
        {
            if (_isClaimingFloorReward) return false;
            if (string.IsNullOrWhiteSpace(_currentToken) || !UserContext.Current.IsAuthenticated) return false;
            if (!UserContext.Current.CanClaimFloorReward) return false;

            _isClaimingFloorReward = true;
            try
            {
                var result = await _userService.ClaimFloorRewardAsync(_currentToken);
                if (result.IsSuccess && result.Data != null)
                {
                    var data = result.Data;
                    var msg = $"[樓層獎勵] 已領取 {data.Floor}F 樓層獎勵：獲得經驗值 +{data.Exp}";
                    if (data.RewardResult != null && data.RewardResult.LevelUps > 0)
                    {
                        msg += $"，升級 +{data.RewardResult.LevelUps}，目前等級 {data.RewardResult.Level}";
                    }
                    AppendLog(msg, Color.FromArgb(245, 210, 60));

                    UpdateFloorRewardStatusVisual();
                    if (lblStatusLevel != null)
                    {
                        lblStatusLevel.Text = UserContext.Current.SummaryText;
                    }
                    return true;
                }
                else if (!result.IsCancelled)
                {
                    AppendLog($"[警告] 領取樓層獎勵失敗：{result.ErrorMessage}", Color.FromArgb(220, 90, 90));
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[錯誤] 領取樓層獎勵異常：{ex.Message}", Color.FromArgb(220, 80, 80));
            }
            finally
            {
                _isClaimingFloorReward = false;
                UpdateFloorRewardStatusVisual();
            }
            return false;
        }

        /// <summary>
        /// 應用程式啟動時之 Token 檢查與個人資料同步。
        /// 若初次使用（Token 為空），主動彈窗提示使用者輸入；
        /// 若已有 Token，則呼叫 users/me, characters/active, action/status 同步使用者資料。
        /// </summary>
        private async Task HandleStartupTokenCheckAsync()
        {
            if (string.IsNullOrWhiteSpace(_currentToken))
            {
                string? input = PromptDialog.Show(
                    this,
                    "設定 Token",
                    "請輸入 Bearer Token：",
                    "",
                    "請在此貼上 Bearer Token");

                if (string.IsNullOrWhiteSpace(input))
                {
                    AppendLog("[提示] 尚未設定 Token。請至「連線設定」填入 Token 後點擊儲存。", Color.FromArgb(220, 160, 60));
                    SwitchNavTab("settings");
                    UpdateTokenStatusVisual();
                    return;
                }

                await ValidateAndApplyTokenAsync(input, isFirstTime: true);
            }
            else
            {
                AppendLog("[系統] 正在同步使用者個人資料...", Color.FromArgb(130, 170, 255));
                if (lblStatusToken != null)
                {
                    lblStatusToken.Text = "● Token：同步中...";
                    lblStatusToken.ForeColor = Color.FromArgb(130, 170, 255);
                }

                var result = await _userService.ValidateAndFetchProfileAsync(_currentToken);
                if (result.IsSuccess)
                {
                    UpdateTokenStatusVisual();
                    AppendLog("[系統] ✓ 使用者資料同步成功！", Color.FromArgb(80, 220, 120));

                    if (AppSettingsManager.Current.AutoClaimFloorReward && UserContext.Current.CanClaimFloorReward)
                    {
                        _ = TryClaimFloorRewardAsync();
                    }
                }
                else
                {
                    UpdateTokenStatusVisual();
                    AppendLog($"[警告] 使用者資料同步失敗：{result.ErrorMessage}", Color.FromArgb(220, 90, 90));
                    if (lblStatusToken != null)
                    {
                        lblStatusToken.Text = result.IsUnauthorized ? "● Token：已失效" : "● Token：同步失敗";
                        lblStatusToken.ForeColor = Color.FromArgb(220, 90, 90);
                    }
                }
            }
        }

        /// <summary>
        /// 驗證並套用 Token。依序呼叫 users/me, characters/active, action/status，
        /// 必須三隻 API 皆成功回傳才視為有效已驗證。
        /// </summary>
        private async Task<bool> ValidateAndApplyTokenAsync(string rawToken, bool isFirstTime)
        {
            var cleanToken = rawToken.Trim();
            if (cleanToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                cleanToken = cleanToken[7..].Trim();

            if (string.IsNullOrWhiteSpace(cleanToken))
            {
                MessageBox.Show(this, "請輸入有效的 Bearer Token。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            AppendLog("[系統] 正在驗證 Token...", Color.FromArgb(130, 170, 255));
            if (lblStatusToken != null)
            {
                lblStatusToken.Text = "● Token：驗證中...";
                lblStatusToken.ForeColor = Color.FromArgb(130, 170, 255);
            }

            var result = await _userService.ValidateAndFetchProfileAsync(cleanToken);
            if (result.IsSuccess)
            {
                _currentToken = cleanToken;
                AppSettingsManager.Current.Token = _currentToken;
                AppSettingsManager.Save();

                if (txtSettingsToken != null)
                    txtSettingsToken.Text = _currentToken;

                UpdateTokenStatusVisual();
                AppendLog("[系統] ✓ Token 驗證成功！", Color.FromArgb(80, 220, 120));

                if (AppSettingsManager.Current.AutoClaimFloorReward && UserContext.Current.CanClaimFloorReward)
                {
                    _ = TryClaimFloorRewardAsync();
                }

                MessageBox.Show(
                    this,
                    $"Token 驗證成功並已儲存！\n\n玩家名稱：{UserContext.Current.PlayerName}\n出戰角色：{UserContext.Current.CharacterName} (Lv.{UserContext.Current.Level})\n目前樓層：{UserContext.Current.CurrentFloor}F",
                    "驗證成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return true;
            }
            else
            {
                AppendLog($"[錯誤] Token 驗證失敗：{result.ErrorMessage}", Color.FromArgb(220, 80, 80));
                UpdateTokenStatusVisual();
                if (lblStatusToken != null)
                {
                    lblStatusToken.Text = result.IsUnauthorized ? "● Token：無效/過期" : "● Token：驗證失敗";
                    lblStatusToken.ForeColor = Color.FromArgb(220, 90, 90);
                }

                MessageBox.Show(
                    this,
                    $"Token 驗證失敗！\n\n原因：{result.ErrorMessage}\n\n必須成功通過驗證才算有效，請確認 Token 是否正確。",
                    "驗證失敗",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                if (isFirstTime)
                {
                    SwitchNavTab("settings");
                }
                return false;
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

            if (!UserContext.Current.IsAuthenticated)
            {
                AppendLog("[提示] 正在進行 Token 驗證...", Color.FromArgb(130, 170, 255));
                var valid = await ValidateAndApplyTokenAsync(_currentToken, isFirstTime: false);
                if (!valid)
                {
                    AppendLog("[錯誤] Token 尚未通過驗證，無法啟動排程。", Color.FromArgb(220, 80, 80));
                    return;
                }
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
                    AppendLog("[錯誤] 行動排程清單為空，請先於排程頁籤新增至少一個階段。", Color.FromArgb(220, 80, 80));
                    return;
                }
            }

            SaveCurrentUiSettings();

            _cts = new CancellationTokenSource();
            try
            {
                SetControlsEnabled(false);
                btnShowLastReport.Visible = false;

                _loopStartTime = DateTime.Now;
                _currentStats = new LoopStats();
                UpdateTelemetryStats(_currentStats);
                lblStatusElapsed.Text = "00:00:00";
                _elapsedTimer.Tick += OnElapsedTick;
                _elapsedTimer.Start();

                bool isAutoEvent = rbEventAuto.Checked;
                bool isInline = rbEventManual.Checked && rbEventManualInline.Checked;
                AutoEventStrategy? autoStrategy = isAutoEvent
                    ? (rbEventAutoRandom.Checked ? AutoEventStrategy.Random : AutoEventStrategy.HighestSuccessRate)
                    : null;

                if (_activeRunningMode == "training")
                {
                    var settings = new LoopSettings
                    {
                        Token = _currentToken,
                        ActionId = selectedTrainingAction!.ActionId,
                        ActionSequence = _trainingSequenceList.Select(a => a.ActionId).ToList(),
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
                            _currentStats.ChadoSuccessCount = bs.ChadoSuccessCount;
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
                        OnLastBattleResultUpdated = (result) =>
                        {
                            _lastBattleResult = result;
                            this.BeginInvoke(() => btnShowLastReport.Visible = true);
                        },
                        OnManualEventSelect = isAutoEvent ? null : (isInline ? HandleInlineEventAsync : HandleManualEventAsync),
                        OnManualEventResult = isAutoEvent ? null : (isInline ? ShowInlineEventResultAsync : HandleManualEventResultAsync),
                        OnCloseManualUi = () => { _currentEventForm?.Close(); HideEventOverlay(); }
                    };

                    AppendLog($"開始執行行動排程（共 {pSettings.Stages.Count} 階段）...", Color.FromArgb(140, 200, 255));
                    await Task.Run(() => _pipelineLoop.RunAsync(pSettings, _cts.Token), _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppendLog($"[異常中斷] {ex.GetType().Name}: {ex.Message}", Color.FromArgb(220, 80, 80));
                UpdateStatusBadge(LoopStatus.Error);
            }
            finally
            {
                _elapsedTimer.Stop();
                _elapsedTimer.Tick -= OnElapsedTick;
                _cts?.Dispose();
                _cts = null;
                _activeRunningMode = string.Empty;
                SetControlsEnabled(true);
                if (_lastReportedStatus != LoopStatus.Completed && _lastReportedStatus != LoopStatus.Error)
                {
                    UpdateStatusBadge(LoopStatus.Stopped);
                }
            }
        }

        private void OnStopClicked(object? sender, EventArgs e)
        {
            _cts?.Cancel();
            _currentEventForm?.Close();
            HideEventOverlay();
            AppendLog("使用者按下停止，正在安全中止排程...", Color.FromArgb(220, 160, 80));
            // 點擊停止後，先禁用停止按鈕避免重複點擊；開始按鈕與頁籤狀態由 finally 區塊於迴圈安全結束後統一復原
            if (btnTrainStop != null) btnTrainStop.Enabled = false;
            if (btnBattleStop != null) btnBattleStop.Enabled = false;
            if (btnPipelineStop != null) btnPipelineStop.Enabled = false;
            if (lblStatusCooldown != null)
                lblStatusCooldown.Text = "";
        }

        private void SetActionButtonsRunning(bool running)
        {
            if (btnTrainStart != null) btnTrainStart.Enabled = !running;
            if (btnBattleStart != null) btnBattleStart.Enabled = !running;

            if (running)
            {
                if (btnPipelineStart != null) btnPipelineStart.Enabled = false;
            }
            else
            {
                UpdatePipelineStartButtonState();
            }

            // 只有當前正在運行的模式之停止按鈕才需啟用；空閒時全部禁用
            if (btnTrainStop != null)
                btnTrainStop.Enabled = running && (_activeRunningMode == "training" || string.IsNullOrEmpty(_activeRunningMode));
            if (btnBattleStop != null)
                btnBattleStop.Enabled = running && (_activeRunningMode == "battle" || string.IsNullOrEmpty(_activeRunningMode));
            if (btnPipelineStop != null)
                btnPipelineStop.Enabled = running && (_activeRunningMode == "pipeline" || string.IsNullOrEmpty(_activeRunningMode));
        }

        private void SetControlsEnabled(bool enabled)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(() => SetControlsEnabled(enabled));
                return;
            }

            // 1. 左側導覽頁籤狀態管理
            if (enabled)
            {
                // 空閒/停止狀態：所有導覽頁籤皆可點擊切換
                if (btnNavTraining != null) btnNavTraining.Enabled = true;
                if (btnNavBattle != null)   btnNavBattle.Enabled   = true;
                if (btnNavPipeline != null) btnNavPipeline.Enabled = true;
                if (btnNavEvents != null)   btnNavEvents.Enabled   = true;
                if (btnNavStats != null)    btnNavStats.Enabled    = true;
                if (btnNavSettings != null) btnNavSettings.Enabled = true;
            }
            else
            {
                // 執行中狀態：
                // - 當前正在運行的行動頁籤必須保持啟用，確保切換至戰報等頁面後能切換回原本的行動頁面！
                // - 非當前運行的其他行動頁籤停用，避免同時啟動多個任務衝突
                // - 戰報分析頁籤保持啟用（純查詢與數據檢視，不影響背景執行）
                // - 事件應對與連線設定頁籤停用，避免運作中修改關鍵策略或 Token
                if (btnNavTraining != null) btnNavTraining.Enabled = (_activeRunningMode == "training");
                if (btnNavBattle != null)   btnNavBattle.Enabled   = (_activeRunningMode == "battle");
                if (btnNavPipeline != null) btnNavPipeline.Enabled = (_activeRunningMode == "pipeline");
                if (btnNavEvents != null)   btnNavEvents.Enabled   = false;
                if (btnNavStats != null)    btnNavStats.Enabled    = true;
                if (btnNavSettings != null) btnNavSettings.Enabled = false;
            }

            // 2. 自動訓練頁面設定控制項啟用/禁用
            if (cmbAction != null) cmbAction.Enabled = enabled;
            if (btnAddAction != null) btnAddAction.Enabled = enabled;
            if (btnClearSequence != null) btnClearSequence.Enabled = enabled;
            if (rbTrainTime != null) rbTrainTime.Enabled = enabled;
            if (rbTrainCount != null) rbTrainCount.Enabled = enabled;
            if (nudTrainMinutes != null) nudTrainMinutes.Enabled = enabled;
            if (nudTrainCount != null) nudTrainCount.Enabled = enabled;
            if (nudTrainExtraDelay != null) nudTrainExtraDelay.Enabled = enabled;
            if (pnlTrainingSequencePills != null)
            {
                pnlTrainingSequencePills.Enabled = enabled;
                foreach (Control ctrl in pnlTrainingSequencePills.Controls)
                {
                    ctrl.Enabled = enabled;
                }
            }

            // 3. 自動戰鬥頁面設定控制項啟用/禁用
            if (txtTargetPlayerId != null) txtTargetPlayerId.Enabled = enabled;
            if (btnGetPlayerInfo != null) btnGetPlayerInfo.Enabled = enabled;
            if (rbBattleChallenge != null) rbBattleChallenge.Enabled = enabled;
            if (rbBattleChado != null) rbBattleChado.Enabled = enabled;
            if (rbBattleTime != null) rbBattleTime.Enabled = enabled;
            if (rbBattleCount != null) rbBattleCount.Enabled = enabled;
            if (nudBattleMinutes != null) nudBattleMinutes.Enabled = enabled;
            if (nudBattleCount != null) nudBattleCount.Enabled = enabled;
            if (nudBattleExtraDelay != null) nudBattleExtraDelay.Enabled = enabled;

            // 4. 行動排程頁面設定控制項啟用/禁用
            if (tblStageList != null) tblStageList.Enabled = enabled;
            if (btnAddStage != null) btnAddStage.Enabled = enabled;
            if (btnClearStages != null) btnClearStages.Enabled = enabled;
            if (nudPipelineDelay != null) nudPipelineDelay.Enabled = enabled;
            if (cmbPipelinePresets != null) cmbPipelinePresets.Enabled = enabled;
            if (btnNewPreset != null) btnNewPreset.Enabled = enabled;
            if (btnRenamePreset != null) btnRenamePreset.Enabled = enabled;
            if (btnDeletePreset != null) btnDeletePreset.Enabled = enabled;
            if (btnExportPreset != null) btnExportPreset.Enabled = enabled;
            if (btnImportPreset != null) btnImportPreset.Enabled = enabled;

            // 5. 開始與停止按鈕狀態切換
            SetActionButtonsRunning(!enabled);
        }

        private void OnElapsedTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _loopStartTime;
            if (lblStatusElapsed != null)
                lblStatusElapsed.Text = elapsed.ToString(@"hh\:mm\:ss");
        }

        private void UpdateStatusBadge(LoopStatus status)
        {
            if (lblStatusBadge == null) return;
            if (lblStatusBadge.InvokeRequired)
            {
                lblStatusBadge.Invoke(() => UpdateStatusBadge(status));
                return;
            }

            _lastReportedStatus = status;

            (lblStatusBadge.Text, lblStatusBadge.ForeColor) = status switch
            {
                LoopStatus.Running         => ("● 執行中",     Color.FromArgb(80, 220, 120)),
                LoopStatus.WaitingCooldown => ("● 執行中",     Color.FromArgb(80, 220, 120)),
                LoopStatus.WaitingEvent    => ("● 等待中",     Color.FromArgb(200, 160, 255)),
                LoopStatus.Completed       => ("● 已完成",     Color.FromArgb(140, 200, 255)),
                LoopStatus.Error           => ("● 發生錯誤",   Color.FromArgb(220, 80, 80)),
                _                          => ("● 已停止",     Color.FromArgb(160, 160, 175))
            };

            if (status != LoopStatus.Running && status != LoopStatus.WaitingCooldown && lblStatusCooldown != null)
            {
                lblStatusCooldown.Text = "";
            }
        }

        private void UpdateTelemetryStats(LoopStats stats)
        {
            if (lblStatusRuns == null) return;
            if (lblStatusRuns.InvokeRequired)
            {
                lblStatusRuns.Invoke(() => UpdateTelemetryStats(stats));
                return;
            }

            _currentStats = stats;
            bool isPipeline = _activeRunningMode == "pipeline" || (_cts == null && _selectedNavTab == "pipeline");
            bool isPipelineRunning = _activeRunningMode == "pipeline" && _cts != null;
            bool isBattle = _activeRunningMode == "battle" || (_cts == null && _selectedNavTab == "battle");
            bool isTimeMode = isBattle ? rbBattleTime.Checked : rbTrainTime.Checked;

            if (isPipeline)
            {
                if (isPipelineRunning)
                {
                    lblStatusPipelineStage.Visible = true;
                    PipelineStage? displayStage = stats.CurrentStage;
                    int displayStageIdx = stats.CurrentStageIndex;
                    if (displayStage == null)
                    {
                        var stages = AppSettingsManager.Current.PipelineStages;
                        if (stages != null && stages.Count > 0)
                        {
                            displayStageIdx = 1;
                            displayStage = stages[0];
                        }
                    }

                    string stageText = FormatStageStatusText(displayStageIdx > 0 ? displayStageIdx : 1, displayStage);
                    lblStatusPipelineStage.Text = stageText;
                    statusToolTip.SetToolTip(lblStatusPipelineStage, stageText);
                }
                else
                {
                    lblStatusPipelineStage.Visible = false;
                }

                // Row 2: 總計訓練次數與成功/失敗
                lblStatusRuns.ForeColor = Color.FromArgb(210, 210, 225);
                lblStatusRuns.Text = $"總計訓練次數：{stats.TrainingRunCount} 次";

                lblStatusSuccess.ForeColor = Color.FromArgb(80, 220, 120);
                lblStatusSuccess.Text = $"{stats.TrainingSuccessCount} 成功";

                lblStatusSlash.Visible = true;
                lblStatusFail.Visible = true;
                lblStatusFail.ForeColor = Color.FromArgb(240, 95, 95);
                lblStatusFail.Text = $"{stats.TrainingFailCount} 失敗";
                if (lblStatusChado != null) lblStatusChado.Visible = false;

                // Row 3: 總計戰鬥次數與勝利/失敗
                if (lblStatusBattleRuns != null && pnlStatusBattleSuccessLoss != null)
                {
                    lblStatusBattleRuns.Visible = true;
                    lblStatusBattleRuns.ForeColor = Color.FromArgb(210, 210, 225);
                    lblStatusBattleRuns.Text = $"總計戰鬥次數：{stats.BattleRunCount} 次";

                    pnlStatusBattleSuccessLoss.Visible = true;
                    lblStatusBattleSuccess.ForeColor = Color.FromArgb(80, 220, 120);
                    lblStatusBattleSuccess.Text = $"{stats.BattleWinCount} 勝利";

                    lblStatusBattleSlash.Visible = true;
                    lblStatusBattleFail.ForeColor = Color.FromArgb(240, 95, 95);
                    lblStatusBattleFail.Text = $"{stats.BattleLossCount} 失敗";
                    lblStatusBattleFail.Visible = true;

                    if (lblStatusBattleChado != null)
                    {
                        bool hasChadoInPipeline = AppSettingsManager.Current.PipelineStages.Any(s => s.ActionType == StageActionType.Battle && s.BattleType == BattleType.Chado) || stats.ChadoSuccessCount > 0;
                        if (hasChadoInPipeline)
                        {
                            lblStatusBattleChado.Text = $" (💩 {stats.ChadoSuccessCount})";
                            statusToolTip.SetToolTip(lblStatusBattleChado, $"丟下小零食並螺旋下指：{stats.ChadoSuccessCount} 次");
                            lblStatusBattleChado.Visible = true;
                        }
                        else
                        {
                            lblStatusBattleChado.Visible = false;
                        }
                    }
                }
            }
            else if (isBattle)
            {
                lblStatusPipelineStage.Visible = false;
                if (lblStatusBattleRuns != null) lblStatusBattleRuns.Visible = false;
                if (pnlStatusBattleSuccessLoss != null) pnlStatusBattleSuccessLoss.Visible = false;

                lblStatusSlash.Visible = true;
                lblStatusFail.Visible = true;

                if (isTimeMode)
                    lblStatusRuns.Text = $"戰鬥次數：{stats.RunCount} 次";
                else
                {
                    int targetLimit = (int)nudBattleCount.Value;
                    lblStatusRuns.Text = $"戰鬥次數：{stats.RunCount} / {targetLimit} 次";
                }
                lblStatusRuns.ForeColor = Color.FromArgb(210, 210, 225);
                lblStatusSuccess.ForeColor = Color.FromArgb(80, 220, 120);
                lblStatusSuccess.Text = $"{stats.SuccessCount} 勝利";
                lblStatusFail.ForeColor = Color.FromArgb(240, 95, 95);
                lblStatusFail.Text = $"{stats.FailCount} 失敗";

                if (lblStatusChado != null)
                {
                    bool isChadoMode = (rbBattleChado != null && rbBattleChado.Checked) || stats.ChadoSuccessCount > 0;
                    if (isChadoMode)
                    {
                        lblStatusChado.Text = $" (💩 {stats.ChadoSuccessCount})";
                        statusToolTip.SetToolTip(lblStatusChado, $"丟下小零食並螺旋下指：{stats.ChadoSuccessCount} 次");
                        lblStatusChado.Visible = true;
                    }
                    else
                    {
                        lblStatusChado.Visible = false;
                    }
                }
            }
            else
            {
                lblStatusPipelineStage.Visible = false;
                if (lblStatusBattleRuns != null) lblStatusBattleRuns.Visible = false;
                if (pnlStatusBattleSuccessLoss != null) pnlStatusBattleSuccessLoss.Visible = false;

                lblStatusSlash.Visible = true;
                lblStatusFail.Visible = true;

                if (isTimeMode)
                    lblStatusRuns.Text = $"訓練次數：{stats.RunCount} 次";
                else
                {
                    int targetLimit = (int)nudTrainCount.Value;
                    lblStatusRuns.Text = $"訓練次數：{stats.RunCount} / {targetLimit} 次";
                }
                lblStatusRuns.ForeColor = Color.FromArgb(210, 210, 225);
                lblStatusSuccess.ForeColor = Color.FromArgb(80, 220, 120);
                lblStatusSuccess.Text = $"{stats.SuccessCount} 成功";
                lblStatusFail.ForeColor = Color.FromArgb(240, 95, 95);
                lblStatusFail.Text = $"{stats.FailCount} 失敗";

                if (lblStatusChado != null)
                {
                    lblStatusChado.Visible = false;
                }
            }

            lblStatusEvents.Text = $"觸發事件：{stats.EventCount} 次";
            lblStatusEventSuccess.Text = $"{stats.EventSuccessCount} 成功";
            lblStatusEventFail.Text = $"{stats.EventFailCount} 失敗";

            if (lblStatusCooldown != null)
            {
                lblStatusCooldown.Text = stats.NextRunCountdownSeconds > 0
                    ? $"(冷卻：{stats.NextRunCountdownSeconds:F1}s)"
                    : "";
            }

            if (lblStatusLevel != null)
            {
                if (stats.CurrentLevel > 0)
                {
                    UserContext.UpdateLevel(stats.CurrentLevel);
                }
                lblStatusLevel.Text = UserContext.Current.SummaryText;
            }
        }

        private string FormatStageStatusText(int stageIndex, PipelineStage? stage)
        {
            if (stage == null) return "排程尚未設定階段";

            string actionText;
            if (stage.ActionType == StageActionType.Training)
            {
                if (stage.TrainingActionSequence != null && stage.TrainingActionSequence.Count > 1)
                {
                    var names = stage.TrainingActionSequence
                        .Select(id => TrainingActions.All.FirstOrDefault(a => a.ActionId == id)?.DisplayName ?? id)
                        .ToList();
                    string seq = string.Join("→", names);
                    if (seq.Length > 16) seq = seq[..15] + "…";
                    actionText = $"訓練 ({seq})";
                }
                else
                {
                    var action = TrainingActions.All.FirstOrDefault(a => a.ActionId == stage.TrainingActionId);
                    string name = action?.DisplayName ?? stage.TrainingActionId;
                    actionText = $"訓練 ({name})";
                }
            }
            else
            {
                string bType = stage.BattleType == BattleType.Challenge ? "友好切磋" : "我要茶渡你";
                string target = string.IsNullOrWhiteSpace(stage.BattleTargetName)
                    ? (string.IsNullOrWhiteSpace(stage.BattleTargetId) ? "未指定目標" : stage.BattleTargetId)
                    : stage.BattleTargetName;
                if (target.Length > 12)
                {
                    target = target[..11] + "…";
                }
                actionText = $"{bType} ({target})";
            }

            string condText = stage.ConditionType switch
            {
                StageConditionType.LevelReach => $"角色等級 >= Lv.{stage.ConditionValue}",
                StageConditionType.RunCountReach => $"執行滿 {stage.ConditionValue} 次",
                StageConditionType.MinutesReach => $"運行滿 {stage.ConditionValue} 分鐘",
                _ => $"達標值: {stage.ConditionValue}"
            };

            return $"#{stageIndex} 階段 {actionText} 直到 {condText}";
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
            if (pnlEventOverlay.InvokeRequired)
            {
                pnlEventOverlay.Invoke(() => ShowInlineEventUI(pe, onSelected));
                return;
            }

            pnlEventOverlay.SuspendLayout();
            try
            {
                pnlEventOverlay.Controls.Clear();
                pnlEventOverlay.Visible = true;
                pnlEventOverlay.BringToFront();

                var outer = CreateScrollableTabPanel();
                var tbl = CreateContentTable(outer);

                // ── 事件名稱（金黃色醒目標題）──
                var lblEventTitle = new Label
                {
                    Text = $"⚡  遭遇事件：{pe.Name}",
                    Font = new Font("Microsoft JhengHei UI", 12f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(255, 215, 80),
                    AutoSize = true,
                    Margin = new Padding(0, ScaleDpi(4), 0, ScaleDpi(6))
                };
                TblAdd(tbl, lblEventTitle);

                // ── 事件描述 ──
                if (!string.IsNullOrWhiteSpace(pe.Description))
                {
                    var lblDesc = new Label
                    {
                        Text = pe.Description,
                        Font = new Font("Microsoft JhengHei UI", 9.5f),
                        Dock = DockStyle.Fill,
                        AutoSize = true,
                        ForeColor = Color.FromArgb(205, 205, 225),
                        Margin = new Padding(0, 0, 0, ScaleDpi(8))
                    };
                    TblAdd(tbl, lblDesc);
                }

                // ── 細分隔線 ──
                var sep = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 1,
                    BackColor = Color.FromArgb(70, 65, 95),
                    Margin = new Padding(0, 0, 0, ScaleDpi(8))
                };
                TblAdd(tbl, sep);

                // ── 選項提示 ──
                var lblChoose = new Label
                {
                    Text = "請點選應對選項：",
                    Font = new Font("Microsoft JhengHei UI", 9f),
                    ForeColor = Color.FromArgb(160, 160, 200),
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, ScaleDpi(4))
                };
                TblAdd(tbl, lblChoose);

                // ── 選項按鈕清單 ──
                foreach (var opt in pe.Options)
                {
                    string rateText = opt.SuccessChance.HasValue ? $"{opt.SuccessChance}%" : "未知";
                    string statName = !string.IsNullOrEmpty(opt.CheckStat)
                        ? MyDoujinBot.Utilities.StatHelper.GetDisplayName(opt.CheckStat)
                        : "";
                    string mainText = opt.Name;
                    string subText = string.IsNullOrEmpty(opt.CheckStat)
                        ? " (直接行動)"
                        : $" ({statName}檢定 / 成功率：{rateText})";

                    // 依成功率給予深色系視覺回饋（高率偏綠、中率偏褐橘、低率偏深紅、無檢定偏深藍紫）
                    Color btnBack = opt.SuccessChance.HasValue
                        ? (opt.SuccessChance.Value >= 70
                            ? Color.FromArgb(36, 64, 46)
                            : opt.SuccessChance.Value >= 40
                                ? Color.FromArgb(58, 52, 36)
                                : Color.FromArgb(68, 38, 38))
                        : Color.FromArgb(45, 45, 65);

                    Color btnBackHover = Color.FromArgb(
                        Math.Min(btnBack.R + 24, 255),
                        Math.Min(btnBack.G + 24, 255),
                        Math.Min(btnBack.B + 24, 255));

                    var btnOpt = new Button
                    {
                        Dock = DockStyle.Top,
                        Height = ScaleDpi(48),
                        FlatStyle = FlatStyle.Flat,
                        BackColor = btnBack,
                        Cursor = Cursors.Hand,
                        Tag = opt.Id,
                        Text = "",
                        Margin = new Padding(0, ScaleDpi(3), 0, ScaleDpi(4))
                    };
                    btnOpt.FlatAppearance.BorderColor = Color.FromArgb(90, 75, 130);
                    btnOpt.FlatAppearance.BorderSize = 1;

                    var mainFont = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold);
                    var subFont = new Font("Microsoft JhengHei UI", 8.5f);

                    btnOpt.Paint += (s, e) =>
                    {
                        int leftPad = ScaleDpi(12);
                        Size mainSize = TextRenderer.MeasureText(e.Graphics, mainText, mainFont, new Size(btnOpt.Width, btnOpt.Height), TextFormatFlags.NoPadding);
                        Size subSize = TextRenderer.MeasureText(e.Graphics, subText, subFont, new Size(btnOpt.Width, btnOpt.Height), TextFormatFlags.NoPadding);

                        if (leftPad + mainSize.Width + ScaleDpi(4) + subSize.Width <= btnOpt.Width - leftPad)
                        {
                            int mainY = (btnOpt.Height - mainSize.Height) / 2;
                            int subY = (btnOpt.Height - subSize.Height) / 2;
                            TextRenderer.DrawText(e.Graphics, mainText, mainFont, new Point(leftPad, mainY), Color.FromArgb(245, 240, 255), TextFormatFlags.NoPadding);
                            TextRenderer.DrawText(e.Graphics, subText, subFont, new Point(leftPad + mainSize.Width + ScaleDpi(4), subY + 1), Color.FromArgb(175, 175, 200), TextFormatFlags.NoPadding);
                        }
                        else
                        {
                            int topY = ScaleDpi(7);
                            TextRenderer.DrawText(e.Graphics, mainText, mainFont, new Point(leftPad, topY), Color.FromArgb(245, 240, 255), TextFormatFlags.NoPadding);
                            TextRenderer.DrawText(e.Graphics, subText, subFont, new Point(leftPad, topY + mainSize.Height + ScaleDpi(3)), Color.FromArgb(175, 175, 200), TextFormatFlags.NoPadding);
                        }
                    };

                    btnOpt.Disposed += (s, e) =>
                    {
                        mainFont.Dispose();
                        subFont.Dispose();
                    };

                    btnOpt.MouseEnter += (s, e) => btnOpt.BackColor = btnBackHover;
                    btnOpt.MouseLeave += (s, e) => btnOpt.BackColor = btnBack;

                    var optId = opt.Id;
                    btnOpt.Click += (s, e) =>
                    {
                        onSelected(optId);
                    };

                    TblAdd(tbl, btnOpt);
                }

                pnlEventOverlay.Controls.Add(outer);
            }
            finally
            {
                pnlEventOverlay.ResumeLayout(true);
            }
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
                pnlEventOverlay.SuspendLayout();
                try
                {
                    pnlEventOverlay.Controls.Clear();
                    pnlEventOverlay.Visible = true;
                    pnlEventOverlay.BringToFront();

                    var outer = CreateScrollableTabPanel();
                    var tbl = CreateContentTable(outer);

                    string title = er.IsSuccess ? "✓  事件判定成功！" : "✕  事件判定失敗";
                    Color titleColor = er.IsSuccess ? Color.FromArgb(80, 220, 120) : Color.FromArgb(240, 80, 80);

                    var lblResultTitle = new Label
                    {
                        Text = title,
                        Font = new Font("Microsoft JhengHei UI", 13f, FontStyle.Bold),
                        ForeColor = titleColor,
                        AutoSize = true,
                        Margin = new Padding(0, ScaleDpi(4), 0, ScaleDpi(6))
                    };
                    TblAdd(tbl, lblResultTitle);

                    // 判定詳細明細面板（拉滿寬度）
                    if (!string.IsNullOrEmpty(er.Stat) || er.Roll.HasValue)
                    {
                        var pnlCheck = MyDoujinBot.Utilities.EventUiHelper.CreateCheckResultPanel(er, 0);
                        pnlCheck.Dock = DockStyle.Top;
                        pnlCheck.Margin = new Padding(0, 0, 0, ScaleDpi(10));
                        TblAdd(tbl, pnlCheck);
                    }

                    // 故事內文
                    if (!string.IsNullOrWhiteSpace(er.Text))
                    {
                        var lblRes = new Label
                        {
                            Text = er.Text,
                            Dock = DockStyle.Fill,
                            AutoSize = true,
                            Font = new Font("Microsoft JhengHei UI", 9.5f),
                            ForeColor = Color.FromArgb(220, 220, 235),
                            Margin = new Padding(0, ScaleDpi(4), 0, ScaleDpi(10))
                        };
                        TblAdd(tbl, lblRes);
                    }

                    // 獲得獎勵
                    var bonusParts = MyDoujinBot.Utilities.EventUiHelper.BuildBonusStatsParts(er.Rewards?.BonusStats);
                    if (bonusParts.Count > 0)
                    {
                        var lblBonus = new Label
                        {
                            Text = $"🎁 獲得能力提升：{string.Join("、", bonusParts)}",
                            ForeColor = Color.FromArgb(255, 215, 80),
                            Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                            AutoSize = true,
                            Margin = new Padding(0, 0, 0, ScaleDpi(10))
                        };
                        TblAdd(tbl, lblBonus);
                    }

                    var btnOk = new Button
                    {
                        Text = "關閉並繼續 (OK)",
                        AutoSize = true,
                        BackColor = er.IsSuccess ? Color.FromArgb(40, 140, 80) : Color.FromArgb(70, 70, 90),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Cursor = Cursors.Hand,
                        Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                        Padding = new Padding(ScaleDpi(16), ScaleDpi(4), ScaleDpi(16), ScaleDpi(4)),
                        Margin = new Padding(0, ScaleDpi(4), 0, 0)
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
                }
                finally
                {
                    pnlEventOverlay.ResumeLayout(true);
                }
            });

            return tcs.Task;
        }

        private void HideEventOverlay()
        {
            if (pnlEventOverlay.InvokeRequired)
            {
                pnlEventOverlay.Invoke(HideEventOverlay);
                return;
            }

            pnlEventOverlay.Visible = false;
            pnlEventOverlay.Controls.Clear();
            rtbLog.BringToFront();
            pnlEventOverlay.Parent?.PerformLayout();
            UpdateRightPanelWidth();
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
            _isRestoringSettings = true;
            try
            {
                UpdateTokenStatusVisual();

                // 訓練設定還原
                nudTrainCount.Value = Math.Clamp(AppSettingsManager.Current.TargetCount > 0 ? AppSettingsManager.Current.TargetCount : 500, (int)nudTrainCount.Minimum, (int)nudTrainCount.Maximum);
                nudTrainMinutes.Value = Math.Clamp(AppSettingsManager.Current.TargetMinutes > 0 ? AppSettingsManager.Current.TargetMinutes : 1440, (int)nudTrainMinutes.Minimum, (int)nudTrainMinutes.Maximum);
                nudTrainExtraDelay.Value = Math.Clamp(AppSettingsManager.Current.ExtraDelay >= 0 ? AppSettingsManager.Current.ExtraDelay : 1, (int)nudTrainExtraDelay.Minimum, (int)nudTrainExtraDelay.Maximum);

                if (AppSettingsManager.Current.ExecutionMode == "count")
                {
                    rbTrainCount.Checked = true;
                }
                else
                {
                    rbTrainTime.Checked = true;
                }
                OnTrainExecutionModeChanged();

                // 戰鬥設定還原
                nudBattleCount.Value = Math.Clamp(AppSettingsManager.Current.BattleTargetCount > 0 ? AppSettingsManager.Current.BattleTargetCount : 500, (int)nudBattleCount.Minimum, (int)nudBattleCount.Maximum);
                nudBattleMinutes.Value = Math.Clamp(AppSettingsManager.Current.BattleTargetMinutes > 0 ? AppSettingsManager.Current.BattleTargetMinutes : 1440, (int)nudBattleMinutes.Minimum, (int)nudBattleMinutes.Maximum);
                nudBattleExtraDelay.Value = Math.Clamp(AppSettingsManager.Current.BattleExtraDelay >= 0 ? AppSettingsManager.Current.BattleExtraDelay : 1, (int)nudBattleExtraDelay.Minimum, (int)nudBattleExtraDelay.Maximum);

                string bExecMode = string.IsNullOrEmpty(AppSettingsManager.Current.BattleExecutionMode)
                    ? AppSettingsManager.Current.ExecutionMode
                    : AppSettingsManager.Current.BattleExecutionMode;

                if (bExecMode == "count")
                {
                    rbBattleCount.Checked = true;
                }
                else
                {
                    rbBattleTime.Checked = true;
                }
                OnBattleExecutionModeChanged();

                if (nudPipelineDelay != null && AppSettingsManager.Current.PipelineExtraDelay >= 0)
                {
                    nudPipelineDelay.Value = Math.Clamp(AppSettingsManager.Current.PipelineExtraDelay, (int)nudPipelineDelay.Minimum, (int)nudPipelineDelay.Maximum);
                }

                if (!string.IsNullOrEmpty(AppSettingsManager.Current.LastSelectedActionId))
                {
                    var savedAction = TrainingActions.All.FirstOrDefault(a => a.ActionId == AppSettingsManager.Current.LastSelectedActionId);
                    if (savedAction != null) cmbAction.SelectedItem = savedAction;
                }

                txtTargetPlayerId.Text = AppSettingsManager.Current.LastBattleTargetId ?? string.Empty;
                if (AppSettingsManager.Current.BattleMode == "Chado") rbBattleChado.Checked = true;
                else rbBattleChallenge.Checked = true;

                _trainingSequenceList.Clear();
                foreach (var actId in AppSettingsManager.Current.TrainingSequence)
                {
                    var action = TrainingActions.All.FirstOrDefault(a => a.ActionId == actId);
                    if (action != null) _trainingSequenceList.Add(action);
                }
                RefreshTrainingSequencePills();

                // 事件模式還原
                string eMode = AppSettingsManager.Current.EventMode;
                if (eMode == "auto-random")
                {
                    rbEventAuto.Checked = true;
                    rbEventAutoRandom.Checked = true;
                }
                else if (eMode == "inline")
                {
                    rbEventManual.Checked = true;
                    rbEventManualInline.Checked = true;
                }
                else if (eMode == "popup")
                {
                    rbEventManual.Checked = true;
                    rbEventManualPopup.Checked = true;
                }
                else
                {
                    rbEventAuto.Checked = true;
                    rbEventAutoHighest.Checked = true;
                }
                OnEventModeGroupChanged();

                if (chkSettingsAutoClaimFloorReward != null)
                {
                    chkSettingsAutoClaimFloorReward.Checked = AppSettingsManager.Current.AutoClaimFloorReward;
                }
                UpdateFloorRewardStatusVisual();
            }
            finally
            {
                _isRestoringSettings = false;
            }
        }

        private void SaveCurrentUiSettings()
        {
            if (_isRestoringSettings) return;

            if (nudTrainCount != null) AppSettingsManager.Current.TargetCount = (int)nudTrainCount.Value;
            if (nudTrainMinutes != null) AppSettingsManager.Current.TargetMinutes = (int)nudTrainMinutes.Value;
            if (nudTrainExtraDelay != null) AppSettingsManager.Current.ExtraDelay = (int)nudTrainExtraDelay.Value;
            if (rbTrainTime != null) AppSettingsManager.Current.ExecutionMode = rbTrainTime.Checked ? "time" : "count";

            if (nudBattleCount != null) AppSettingsManager.Current.BattleTargetCount = (int)nudBattleCount.Value;
            if (nudBattleMinutes != null) AppSettingsManager.Current.BattleTargetMinutes = (int)nudBattleMinutes.Value;
            if (nudBattleExtraDelay != null) AppSettingsManager.Current.BattleExtraDelay = (int)nudBattleExtraDelay.Value;
            if (rbBattleTime != null) AppSettingsManager.Current.BattleExecutionMode = rbBattleTime.Checked ? "time" : "count";

            if (nudPipelineDelay != null)
                AppSettingsManager.Current.PipelineExtraDelay = (int)nudPipelineDelay.Value;

            if (cmbAction?.SelectedItem is TrainingAction action)
                AppSettingsManager.Current.LastSelectedActionId = action.ActionId;

            if (txtTargetPlayerId != null) AppSettingsManager.Current.LastBattleTargetId = txtTargetPlayerId.Text.Trim();
            if (rbBattleChallenge != null) AppSettingsManager.Current.BattleMode = rbBattleChallenge.Checked ? "Challenge" : "Chado";
            if (chkSettingsAutoClaimFloorReward != null)
                AppSettingsManager.Current.AutoClaimFloorReward = chkSettingsAutoClaimFloorReward.Checked;
            AppSettingsManager.Save();
        }

        // =====================================================================
        // 9. UI Layout Helper 方法（全面 TableLayoutPanel，無寫死固定 PX）
        // =====================================================================
        private Panel CreateScrollableTabPanel()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(24, 24, 30),
                Padding = new Padding(ScaleDpi(16), ScaleDpi(12), ScaleDpi(16), ScaleDpi(16))
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

        private static void AddSectionHeader(TableLayoutPanel tbl, string title, params Control[] actions)
        {
            bool hasActions = actions != null && actions.Length > 0;
            var header = new TableLayoutPanel
            {
                ColumnCount = hasActions ? 2 : 1,
                RowCount = 2,
                Dock = DockStyle.Fill,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 10, 0, 6)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            if (hasActions)
            {
                header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));

            var lblTitle = new Label
            {
                Text = title,
                ForeColor = Color.FromArgb(140, 180, 255),
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 0, 0, 4)
            };
            header.Controls.Add(lblTitle, 0, 0);

            if (hasActions)
            {
                var pnlActions = new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Anchor = AnchorStyles.Right,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0, 0, 0, 2),
                    Padding = Padding.Empty
                };
                foreach (var act in actions!)
                {
                    pnlActions.Controls.Add(act);
                }
                header.Controls.Add(pnlActions, 1, 0);
            }

            var divider = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(50, 50, 65),
                Height = 1,
                AutoSize = false,
                Text = ""
            };
            header.Controls.Add(divider, 0, 1);
            if (hasActions)
            {
                header.SetColumnSpan(divider, 2);
            }

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

        private static void EnableDoubleBuffering(Control? ctrl)
        {
            if (ctrl == null) return;
            try
            {
                typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(ctrl, true, null);
            }
            catch
            {
                // 忽略非關鍵屬性設值
            }
        }
        private void UpdateCurrentIconPreview()
        {
            if (picCurrentIconPreview == null || lblCurrentIconName == null) return;

            string selectedIcon = AppSettingsManager.Current?.SelectedAppIcon ?? "app.ico";
            if (string.IsNullOrWhiteSpace(selectedIcon)) selectedIcon = "app.ico";

            lblCurrentIconName.Text = $"目前圖示：{selectedIcon}";

            try
            {
                var resolved = ResolveAppIconPath(selectedIcon);
                if (resolved != null && System.IO.File.Exists(resolved))
                {
                    using var fs = new System.IO.FileStream(resolved, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                    using var ico = new Icon(fs, new Size(32, 32));
                    var oldImg = picCurrentIconPreview.Image;
                    picCurrentIconPreview.Image = ico.ToBitmap();
                    oldImg?.Dispose();
                }
                else
                {
                    var oldImg = picCurrentIconPreview.Image;
                    picCurrentIconPreview.Image = SystemIcons.Application.ToBitmap();
                    oldImg?.Dispose();
                }
            }
            catch
            {
                // 忽略圖示讀取例外
            }
        }

        private static string? ResolveAppIconPath(string? iconName = null)
        {
            if (string.IsNullOrWhiteSpace(iconName))
            {
                iconName = AppSettingsManager.Current?.SelectedAppIcon;
            }
            if (string.IsNullOrWhiteSpace(iconName))
            {
                iconName = "app.ico";
            }

            if (System.IO.Path.IsPathRooted(iconName) && System.IO.File.Exists(iconName))
            {
                return iconName;
            }

            string fileName = System.IO.Path.GetFileName(iconName);

            var candidateDirs = new List<string>
            {
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"),
                AppDomain.CurrentDomain.BaseDirectory,
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "MyDoujinBot", "Resources"),
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Resources"),
                System.IO.Directory.GetCurrentDirectory()
            };

            try
            {
                var dir = new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                for (int i = 0; i < 4 && dir != null; i++)
                {
                    var sub = System.IO.Path.Combine(dir.FullName, "Resources");
                    if (System.IO.Directory.Exists(sub)) candidateDirs.Add(sub);
                    var projectSub = System.IO.Path.Combine(dir.FullName, "MyDoujinBot", "Resources");
                    if (System.IO.Directory.Exists(projectSub)) candidateDirs.Add(projectSub);
                    dir = dir.Parent;
                }
            }
            catch { }

            foreach (var dir in candidateDirs)
            {
                if (!System.IO.Directory.Exists(dir)) continue;
                string fullPath = System.IO.Path.Combine(dir, fileName);
                if (System.IO.File.Exists(fullPath)) return fullPath;
            }

            // 若找不到指定圖示，嘗試回退至預設 app.ico
            if (!string.Equals(fileName, "app.ico", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveAppIconPath("app.ico");
            }

            return null;
        }

        private void ApplyAppIcon(string iconPathOrName)
        {
            var resolved = ResolveAppIconPath(iconPathOrName);
            if (resolved == null || !System.IO.File.Exists(resolved)) return;

            try
            {
                using var fs = new System.IO.FileStream(resolved, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                var newIcon = new Icon(fs);

                this.Icon = newIcon;
                if (_notifyIcon != null)
                {
                    _notifyIcon.Icon = newIcon;
                }

                string fileName = System.IO.Path.GetFileName(resolved);
                AppSettingsManager.Current.SelectedAppIcon = fileName;
                AppSettingsManager.Save();

                // 若該圖示不在執行目錄下的 Resources，複製一份進去以便日後持久使用
                try
                {
                    string targetDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
                    if (!System.IO.Directory.Exists(targetDir)) System.IO.Directory.CreateDirectory(targetDir);
                    string targetFile = System.IO.Path.Combine(targetDir, fileName);
                    if (!string.Equals(resolved, targetFile, StringComparison.OrdinalIgnoreCase) && !System.IO.File.Exists(targetFile))
                    {
                        System.IO.File.Copy(resolved, targetFile, true);
                    }
                }
                catch { }

                AppendLog($"[設定] 已更換應用程式圖示為「{fileName}」。", Color.FromArgb(140, 200, 255));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"載入圖示失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                SaveCurrentUiSettings();
            }
            catch
            {
                // 忽略退出存檔異常
            }

            CleanUpNotifyIcon();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CleanUpNotifyIcon();
                _statusUpdateTimer?.Dispose();
                _cts?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
