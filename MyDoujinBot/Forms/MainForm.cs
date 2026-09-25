using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Toolkit.Uwp.Notifications;
using MyDoujinBot.Models;
using MyDoujinBot.Services;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 主視窗。
    /// 負責：UI 建立、使用者操作事件、顯示狀態與 LOG。
    /// 業務邏輯全部委託給 Service 層。
    /// </summary>
    public partial class MainForm : Form
    {
        // =====================================================================
        // 控制項宣告
        // =====================================================================

        // --- 訓練行動 ---
        private ComboBox cmbAction = null!;

        // --- 執行模式 ---
        private RadioButton rbCount = null!;
        private RadioButton rbTime = null!;
        private NumericUpDown nudCount = null!;
        private NumericUpDown nudMinutes = null!;
        private Label lblCountUnit = null!;
        private Label lblTimeUnit = null!;

        // --- 冷卻延遲 ---
        private NumericUpDown nudExtraDelay = null!;

        // --- 事件應對 ---
        private RadioButton rbAutoEvent = null!;
        private RadioButton rbManualEvent = null!;
        // 手動模式的子選項
        private RadioButton rbPopupMode = null!;
        private RadioButton rbInlineMode = null!;
        private Panel pnlManualSub = null!;

        // --- 內嵌事件 Overlay（覆蓋 LOG 欄） ---
        private Panel pnlEventOverlay = null!;

        // --- 系統托盤通知 ---
        private NotifyIcon _notifyIcon = null!;

        // --- 控制按鈕 ---
        private Button btnStart = null!;
        private Button btnStop = null!;
        private Button btnSettings = null!;

        // --- Token 狀態顯示 ---
        private Label lblTokenStatus = null!;

        // --- LOG ---
        private RichTextBox rtbLog = null!;
        private Label lblLogHeader = null!;

        // --- 即時狀態 ---
        private Label lblStatusValue = null!;
        private Label lblRunCount = null!;
        private Label lblSuccessCount = null!;
        private Label lblFailCount = null!;
        private Label lblTitleRunCount = null!;
        private Label lblTitleSuccessCount = null!;
        private Label lblTitleFailCount = null!;
        private Label lblEventCount = null!;
        private Label lblEventSuccessCount = null!;
        private Label lblEventFailCount = null!;
        private Label lblLevel = null!;
        private Label lblTotalExp = null!;
        private Label lblGainedCharacters = null!;
        private Label lblNextRun = null!;
        private Label lblElapsed = null!;
        private Label lblChadoSuccessText = null!;

        // --- 執行模式切換 ---
        private RadioButton rbModeTraining = null!;
        private RadioButton rbModeBattle = null!;

        // --- 戰鬥專屬 ---
        private TextBox txtTargetPlayerId = null!;
        private Button btnGetPlayerInfo = null!;
        private Label lblPlayerInfo = null!;
        private RadioButton rbBattleChallenge = null!;
        private RadioButton rbBattleChado = null!;
        private Panel pnlTrainingSettings = null!;
        private Panel pnlBattleSettings = null!;
        private Button btnShowLastReport = null!;
        private BattleResult? _lastBattleResult;
        private string _targetPlayerName = string.Empty;

        // =====================================================================
        // 狀態與 Service
        // =====================================================================

        // Token：啟動時從 AppSettings 讀取，使用者儲存設定時更新
        private string _currentToken = string.Empty;

        private readonly TrainingService _trainingService = new();
        private readonly EventService _eventService = new();
        private readonly BattleService _battleService = new();
        private TrainingLoop? _trainingLoop;
        private BattleLoop? _battleLoop;
        private EventSelectionForm? _currentEventForm;

        private CancellationTokenSource? _cts;
        private readonly System.Windows.Forms.Timer _elapsedTimer = new() { Interval = 1000 };
        private DateTime _loopStartTime;

        // =====================================================================
        // 建構子
        // =====================================================================
        public MainForm()
        {
            this.Text = $"MyDoujin Bot v{Application.ProductVersion}";
            this.Icon = new Icon(@"Resources\app.ico");

            this.Size = new Size(1050, 800);
            this.MinimumSize = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(22, 22, 30);
            this.ForeColor = Color.FromArgb(220, 220, 230);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);

            // ── DPI 自動縮放：讓 WinForms 依目前 DPI 自動換算所有絕對座標
            // AutoScaleDimensions 宣告原始設計基準（96 DPI = 100% 縮放）
            // 啟動時若偵測到不同 DPI，WinForms 會等比例縮放所有 Location / Size
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            // 啟動時讀取持久化設定
            AppSettingsManager.Load();

            InitializeControls();
            InitializeNotifyIcon();

            // 從持久化設定還原 Token
            _currentToken = AppSettingsManager.Current.Token;
            UpdateTokenStatus();

            // 依儲存的設定還原事件應對模式
            // EventMode: "auto" = 自動；"popup" = 手動+彈窗；"inline" = 手動+內嵌
            bool isManual = AppSettingsManager.Current.EventMode != "auto";
            if (isManual)
            {
                rbManualEvent.Checked = true; // 觸發 CheckedChanged → 子選項啟用
                if (AppSettingsManager.Current.EventMode == "inline")
                    rbInlineMode.Checked = true;
                else
                    rbPopupMode.Checked = true;
            }
            else
            {
                rbAutoEvent.Checked = true;
            }
        }

        // =====================================================================
        // 建立所有控制項（使用 TableLayoutPanel 確保 resize 正確）
        // =====================================================================
        private void InitializeControls()
        {
            this.SuspendLayout();

            // ── 根容器：TableLayoutPanel（3欄） ──
            // TableLayoutPanel 說明：
            // 把視窗分成固定的欄位，每個欄位內的 Panel 用 Dock=Fill 自動填滿。
            // 這樣 resize 視窗時，只有第 3 欄（LOG）會變寬，前兩欄固定不動。
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(4),
                BackColor = Color.FromArgb(22, 22, 30)
            };
            // 比例式欄寬：SizeType.Absolute 欄寬不隨 DPI 縮放，是跑版根本原因之一
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));  // 設定欄
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));  // 狀態欄
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));  // LOG 欄
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            this.Controls.Add(table);

            // ── 左欄：設定面板 ──
            var pnlLeft = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 45),
                Padding = new Padding(2)
            };
            // 捲動由內部 FlowLayoutPanel 負責，外部 Panel 不需要 AutoScroll
            table.Controls.Add(pnlLeft, 0, 0);

            BuildLeftPanel(pnlLeft);

            // ── 中欄：即時狀態面板 ──
            var pnlMiddle = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 45),
                Padding = new Padding(2)
            };
            table.Controls.Add(pnlMiddle, 1, 0);

            BuildMiddlePanel(pnlMiddle);

            // ── 右欄：LOG 面板 ──
            var pnlLog = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 16, 22),
                Padding = new Padding(4, 4, 4, 4)
            };
            table.Controls.Add(pnlLog, 2, 0);

            BuildLogPanel(pnlLog);

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        // =====================================================================
        // 左欄：設定面板（單欄 TableLayoutPanel → 真正的 HTML block 堆疊）
        // =====================================================================
        private void BuildLeftPanel(Panel outerPanel)
        {
            // 將外部 Panel 設為可捲動
            outerPanel.AutoScroll = true;

            // 單欄 TableLayoutPanel：設定為 AutoSize，高度由內容决定，像 HTML <div> 堆疊
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 0,
                Padding = new Padding(10, 8, 6, 12),
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            outerPanel.Controls.Add(tbl);

            // ── API 設定 ──
            TblAddRow(tbl, MakeSectionHeader("連線設定"));

            btnSettings = new Button
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 4, 12, 4),
                Text = "⚙  設定",
                BackColor = Color.FromArgb(60, 70, 110), ForeColor = Color.FromArgb(200, 210, 255),
                FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 0, 4)
            };
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.Click += OnSettingsClicked;
            TblAddRow(tbl, btnSettings);

            lblTokenStatus = new Label
            {
                Text = "⚠  尚未設定 Token，請點擊「設定」填入",
                ForeColor = Color.FromArgb(210, 80, 80),
                AutoSize = true, Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 6)
            };
            TblAddRow(tbl, lblTokenStatus);

            // ── 執行類型 ──
            TblAddRow(tbl, MakeSectionHeader("執行類型"));

            rbModeTraining = new RadioButton
            {
                Text = "自動訓練", Checked = true, ForeColor = Color.White,
                Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat,
                AutoSize = true, Padding = new Padding(20, 6, 20, 6),
                TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand
            };
            rbModeTraining.FlatAppearance.BorderSize = 0;
            rbModeTraining.FlatAppearance.CheckedBackColor = Color.FromArgb(60, 70, 110);
            rbModeBattle = new RadioButton
            {
                Text = "自動戰鬥", ForeColor = Color.White,
                Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat,
                AutoSize = true, Padding = new Padding(20, 6, 20, 6),
                TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand,
                Margin = new Padding(6, 0, 0, 0)
            };
            rbModeBattle.FlatAppearance.BorderSize = 0;
            rbModeBattle.FlatAppearance.CheckedBackColor = Color.FromArgb(60, 70, 110);
            rbModeTraining.CheckedChanged += OnModeTypeChanged;
            rbModeBattle.CheckedChanged += OnModeTypeChanged;
            TblAddRow(tbl, MakeHRow(rbModeTraining, rbModeBattle));

            // ── 執行設定 ──
            TblAddRow(tbl, MakeSectionHeader("執行設定"));
            TblAddRow(tbl, MakeLabel("執行模式："));

            rbCount = new RadioButton { Text = "執行指定次數", Checked = true, ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true };
            rbTime  = new RadioButton { Text = "執行指定時間", ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true, Margin = new Padding(12, 0, 0, 0) };
            rbCount.CheckedChanged += OnExecutionModeChanged;
            rbTime.CheckedChanged  += OnExecutionModeChanged;
            TblAddRow(tbl, MakeHRow(rbCount, rbTime));

            // 次數 / 時間輸入
            nudCount    = new NumericUpDown { Width = 110, Minimum = 1, Maximum = 99999, Value = 100, BackColor = Color.FromArgb(45, 45, 58), ForeColor = Color.FromArgb(220, 220, 230) };
            lblCountUnit = MakeLabel("次");
            nudMinutes  = new NumericUpDown { Width = 110, Minimum = 1, Maximum = 1440,  Value = 30,  BackColor = Color.FromArgb(45, 45, 58), ForeColor = Color.FromArgb(220, 220, 230), Visible = false };
            lblTimeUnit  = MakeLabel("分鐘", visible: false);
            TblAddRow(tbl, MakeHRow(nudCount, lblCountUnit, nudMinutes, lblTimeUnit));

            TblAddRow(tbl, MakeLabel("額外冷卻延遲："));
            nudExtraDelay = new NumericUpDown { Width = 110, Minimum = 0, Maximum = 60, Value = 2, BackColor = Color.FromArgb(45, 45, 58), ForeColor = Color.FromArgb(220, 220, 230) };
            TblAddRow(tbl, MakeHRow(nudExtraDelay, MakeLabel("秒（0～此值）")));

            // ── 遭遇事件應對 ──
            TblAddRow(tbl, MakeSectionHeader("遭遇事件應對"));

            rbAutoEvent = new RadioButton { Text = "自動選擇（最高成功率）", Checked = true, ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true };
            rbAutoEvent.CheckedChanged += OnManualSubModeChanged;
            TblAddRow(tbl, rbAutoEvent);

            rbManualEvent = new RadioButton { Text = "手動選擇", ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true };
            rbManualEvent.CheckedChanged += OnManualSubModeChanged;
            TblAddRow(tbl, rbManualEvent);

            // 手動子選項（縮排）
            var subTbl = new TableLayoutPanel
            {
                ColumnCount = 1, RowCount = 0,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent, Enabled = false,
                Margin = new Padding(24, 2, 0, 6),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            subTbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            rbPopupMode  = new RadioButton { Text = "彈出視窗（奪取焦點）",   Checked = true, ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true };
            rbInlineMode = new RadioButton { Text = "內嵌於主畫面（零干擾）", ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            TblAddRow(subTbl, rbPopupMode);
            TblAddRow(subTbl, rbInlineMode);
            pnlManualSub = subTbl;
            TblAddRow(tbl, pnlManualSub);

            // ── 訓練 / 戰鬥 專屬設定容器 ──
            BuildTrainingSettingsPanel(tbl);
            BuildBattleSettingsPanel(tbl);

            // ── 開始 / 停止 ──
            TblAddRow(tbl, MakeSeparator());

            btnStart = new Button
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(24, 6, 24, 6),
                Text = "▶  開始",
                BackColor = Color.FromArgb(38, 155, 75), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold),
                Cursor = Cursors.Hand, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 0, 4)
            };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += OnStartClicked;

            btnStop = new Button
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(24, 6, 24, 6),
                Text = "⏹  停止",
                BackColor = Color.FromArgb(155, 45, 45), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold),
                Cursor = Cursors.Hand, Enabled = false, Margin = new Padding(8, 4, 0, 4)
            };
            btnStop.FlatAppearance.BorderSize = 0;
            btnStop.Click += OnStopClicked;
            TblAddRow(tbl, MakeHRow(btnStart, btnStop));

            // 初始化模式顯示
            OnModeTypeChanged(null, EventArgs.Empty);
        }

        private void BuildTrainingSettingsPanel(TableLayoutPanel parent)
        {
            // 單欄 TableLayoutPanel，跟左欄外層一樣的堆疊方式
            pnlTrainingSettings = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1, RowCount = 0,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent, Visible = true,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            ((TableLayoutPanel)pnlTrainingSettings).ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            TblAddRow(parent, (Control)pnlTrainingSettings, topPad: 0);

            var t = (TableLayoutPanel)pnlTrainingSettings;
            TblAddRow(t, MakeSectionHeader("訓練行動"));
            TblAddRow(t, MakeLabel("選擇訓練："));

            cmbAction = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 58), ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 2, 0, 6)
            };
            foreach (var action in TrainingActions.All) cmbAction.Items.Add(action);
            if (cmbAction.Items.Count > 0) cmbAction.SelectedIndex = 0;
            TblAddRow(t, cmbAction);
        }

        private void BuildBattleSettingsPanel(TableLayoutPanel parent)
        {
            pnlBattleSettings = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1, RowCount = 0,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent, Visible = false,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            ((TableLayoutPanel)pnlBattleSettings).ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            TblAddRow(parent, (Control)pnlBattleSettings, topPad: 0);

            var t = (TableLayoutPanel)pnlBattleSettings;
            TblAddRow(t, MakeSectionHeader("戰鬥對象"));

            txtTargetPlayerId = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 58), ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.FixedSingle, Text = AppSettingsManager.Current.LastBattleTargetId
            };
            btnGetPlayerInfo = new Button
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(6, 2, 6, 2),
                Text = "驗證 ID", BackColor = Color.FromArgb(60, 70, 110), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = new Padding(6, 0, 0, 0)
            };
            btnGetPlayerInfo.FlatAppearance.BorderSize = 0;
            btnGetPlayerInfo.Click += OnGetPlayerInfoClicked;
            var idRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, RowCount = 1,
                Margin = Padding.Empty, Padding = Padding.Empty
            };
            idRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            idRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            idRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            
            txtTargetPlayerId.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            btnGetPlayerInfo.Anchor = AnchorStyles.Left;
            idRow.Controls.Add(txtTargetPlayerId, 0, 0);
            idRow.Controls.Add(btnGetPlayerInfo, 1, 0);

            TblAddRow(t, idRow);

            lblPlayerInfo = new Label
            {
                Text = "請輸入玩家 ID 並驗證", ForeColor = Color.FromArgb(170, 170, 190),
                AutoSize = true, Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 6)
            };
            TblAddRow(t, lblPlayerInfo);

            TblAddRow(t, MakeSectionHeader("戰鬥模式"));

            rbBattleChallenge = new RadioButton { Text = "友好切磋", Checked = AppSettingsManager.Current.BattleMode == "Challenge", ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true };
            rbBattleChado    = new RadioButton { Text = "我要茶渡你", Checked = AppSettingsManager.Current.BattleMode == "Chado",     ForeColor = Color.FromArgb(210, 210, 225), AutoSize = true, Margin = new Padding(12, 0, 0, 0) };
            if (!rbBattleChallenge.Checked && !rbBattleChado.Checked) rbBattleChallenge.Checked = true;
            TblAddRow(t, MakeHRow(rbBattleChallenge, rbBattleChado));
        }

        // =====================================================================
        // 中欄：即時狀態面板（單欄 TableLayoutPanel 堆疊 + 兩欄狀態格線）
        // =====================================================================
        private void BuildMiddlePanel(Panel panel)
        {
            // 將外部 Panel 設為可捲動
            panel.AutoScroll = true;

            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1, RowCount = 0,
                Padding = new Padding(8),
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.Controls.Add(tbl);

            TblAddRow(tbl, MakeSectionHeader("即時狀態"));

            // 兩欄 TableLayoutPanel：左欄標籤、右欄數值
            var grid = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 0,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Margin = new Padding(0, 2, 0, 0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            TblAddRow(tbl, grid, topPad: 0);

            int row = 0;
            (_, lblStatusValue)                              = AddMiddleStatRow(grid, row++, "狀態：",     "已停止",    Color.FromArgb(170, 170, 180));
            (lblTitleRunCount,     lblRunCount)              = AddMiddleStatRow(grid, row++, "已執行：",   "0 次",      Color.FromArgb(210, 210, 225));
            (lblTitleSuccessCount, lblSuccessCount)          = AddMiddleStatRow(grid, row++, "成功：",     "0 次",      Color.FromArgb(90,  215, 110));
            (lblTitleFailCount,    lblFailCount)             = AddMiddleStatRow(grid, row++, "失敗：",     "0 次",      Color.FromArgb(215, 90,  90));
            AddMiddleSpacer(grid, row++);
            (_, lblEventCount)                              = AddMiddleStatRow(grid, row++, "觸發事件：", "0 次",      Color.FromArgb(190, 150, 255));
            (_, lblEventSuccessCount)                       = AddMiddleStatRow(grid, row++, "事件成功：", "0 次",      Color.FromArgb(90,  215, 110));
            (_, lblEventFailCount)                          = AddMiddleStatRow(grid, row++, "事件失敗：", "0 次",      Color.FromArgb(215, 90,  90));
            AddMiddleSpacer(grid, row++);
            (_, lblLevel)                                   = AddMiddleStatRow(grid, row++, "目前等級：", "—",         Color.FromArgb(255, 215, 70));
            (_, lblTotalExp)                                = AddMiddleStatRow(grid, row++, "累積 EXP：", "0",         Color.FromArgb(210, 210, 225));
            AddMiddleSpacer(grid, row++);
            (_, lblNextRun)                                 = AddMiddleStatRow(grid, row++, "下次執行：", "—",         Color.FromArgb(170, 215, 255));
            (_, lblElapsed)                                 = AddMiddleStatRow(grid, row++, "運作時間：", "00:00:00",  Color.FromArgb(210, 210, 225));
            AddMiddleSpacer(grid, row++);
            (_, lblGainedCharacters)                        = AddMiddleStatRow(grid, row++, "獲得角色：", "無",        Color.Gold);
            lblGainedCharacters.AutoSize = true;
            lblGainedCharacters.MaximumSize = new Size(120, 0);

            // 茶渡潑屎成功標語
            lblChadoSuccessText = new Label
            {
                Text = "此輪戰鬥螺旋下指小零食成功！",
                ForeColor = Color.FromArgb(200, 155, 106),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Visible = false,
                Margin = new Padding(0, 12, 0, 0)
            };
            grid.Controls.Add(lblChadoSuccessText, 0, row++);
            grid.SetColumnSpan(lblChadoSuccessText, 2);
        }

        // =====================================================================
        // 右欄：LOG 面板內容（含事件 Overlay）
        // =====================================================================
        private void BuildLogPanel(Panel panel)
        {
            // ── 標題與戰報按鈕容器 ──
            var pnlLogTop = new Panel
            {
                Dock = DockStyle.Top, Height = 26
            };
            
            lblLogHeader = new Label
            {
                Text = "LOG",
                Dock = DockStyle.Left,
                Width = 100,
                ForeColor = Color.FromArgb(160, 160, 195),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0)
            };
            pnlLogTop.Controls.Add(lblLogHeader);

            btnShowLastReport = new Button
            {
                Text = "📄 顯示最後戰報",
                Dock = DockStyle.Right,
                Width = 120,
                BackColor = Color.FromArgb(50, 60, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = false
            };
            btnShowLastReport.FlatAppearance.BorderSize = 0;
            btnShowLastReport.Click += (s, e) => {
                if (_lastBattleResult != null)
                {
                    using var f = new BattleReportForm(_lastBattleResult);
                    f.ShowDialog(this);
                }
            };
            pnlLogTop.Controls.Add(btnShowLastReport);
            
            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(14, 14, 20),
                ForeColor = Color.FromArgb(200, 200, 215),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9.5f),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            // ── 事件 Overlay Panel（覆蓋整個 LOG 欄，平時隱藏）──
            pnlEventOverlay = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 28),
                Visible = false,
                Padding = new Padding(0)
            };

            panel.Controls.Add(rtbLog);
            panel.Controls.Add(pnlLogTop);
            panel.Controls.Add(pnlEventOverlay);
        }

        // =====================================================================
        // NotifyIcon 初始化（右下角系統托盤圖示）
        // =====================================================================
        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = this.Icon,
                Text = "MyDoujin Bot",
                Visible = true
            };
            // 雙擊托盤圖示時還原視窗
            _notifyIcon.DoubleClick += (_, _) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            };
        }

        // =====================================================================
        // Helper：區塊標題（TableLayoutPanel 2列：AutoSize 標題 + 1px 分隔線）
        // 標題高度隨字體自動成長，分隔線自動占滿欄寬——像 HTML <h3> + <hr>
        // =====================================================================
        private static Control MakeSectionHeader(string title)
        {
            var t = new TableLayoutPanel
            {
                ColumnCount = 1, RowCount = 2,
                Dock = DockStyle.Fill,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Padding = Padding.Empty,
                Margin = new Padding(0, 8, 0, 4)
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // 標題列：自動高度
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 1)); // 分隔線：固定 1px

            t.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = title,
                ForeColor = Color.FromArgb(130, 170, 255),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Padding = new Padding(0, 0, 0, 3)
            }, 0, 0);

            t.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(65, 65, 85),
                AutoSize = false, Text = ""
            }, 0, 1);

            return t;
        }

        // =====================================================================
        // Helper：純粗分隔線（用於開始按鈕上方）
        // =====================================================================
        private static Control MakeSeparator()
        {
            return new Label
            {
                Dock = DockStyle.Fill, Height = 1,
                BackColor = Color.FromArgb(55, 55, 70),
                AutoSize = false, Text = "",
                Margin = new Padding(0, 6, 0, 6)
            };
        }

        // =====================================================================
        // Helper：單欄 TableLayoutPanel 加列（相當於 HTML appendChild）
        // topPad 預設 4px，小標題設 2px
        // =====================================================================
        private static void TblAddRow(TableLayoutPanel tbl, Control ctrl, int topPad = 4)
        {
            int row = tbl.RowCount;
            tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tbl.RowCount = row + 1;
            ctrl.Margin = new Padding(ctrl.Margin.Left, topPad, ctrl.Margin.Right, ctrl.Margin.Bottom);
            tbl.Controls.Add(ctrl, 0, row);
        }

        // =====================================================================
        // Helper：水平排列容器（FlowLayoutPanel LeftToRight，用於並排控制項）
        // =====================================================================
        private static FlowLayoutPanel MakeHRow(params Control[] controls)
        {
            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 2, 0, 4)
            };
            foreach (var c in controls)
            {
                c.Anchor = AnchorStyles.Left; // 垂直置中
                row.Controls.Add(c);
            }
            return row;
        }

        // =====================================================================
        // Helper：說明/小標題 Label（AutoSize 、排定列位置用）
        // =====================================================================
        private static Label MakeLabel(string text, Color? color = null, bool visible = true)
        {
            return new Label
            {
                Text = text,
                ForeColor = color ?? Color.FromArgb(185, 185, 205),
                AutoSize = true,
                Visible = visible,
                Margin = new Padding(0, 0, 0, 2)
            };
        }

        // =====================================================================
        // Helper：中欄 TableLayoutPanel 狀態列（標籤 + 數值，兩欄對齊）
        // =====================================================================
        private static (Label Title, Label Value) AddMiddleStatRow(TableLayoutPanel grid, int row,
            string labelText, string valueText, Color valueColor)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var title = new Label
            {
                Text = labelText,
                ForeColor = Color.FromArgb(140, 140, 160),
                AutoSize = true,
                Padding = new Padding(0, 3, 8, 3)
            };
            var value = new Label
            {
                Text = valueText,
                ForeColor = valueColor,
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Padding = new Padding(0, 3, 0, 3)
            };
            grid.Controls.Add(title, 0, row);
            grid.Controls.Add(value, 1, row);
            return (title, value);
        }

        // =====================================================================
        // Helper：中欄 TableLayoutPanel 分組間距列
        // =====================================================================
        private static void AddMiddleSpacer(TableLayoutPanel grid, int row)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
            var spacer = new Label { Height = 8, AutoSize = false, BackColor = Color.Transparent };
            grid.Controls.Add(spacer, 0, row);
            grid.SetColumnSpan(spacer, 2);
        }

        // =====================================================================
        // 設定按鈕：開啟 SettingsForm 對話框
        // ShowDialog() 說明：
        // ShowDialog 會阻塞當前 Thread 直到對話框關閉，
        // 對話框本身是 Modal（使用者必須先關閉它才能操作主視窗）。
        // =====================================================================
        private void OnSettingsClicked(object? sender, EventArgs e)
        {
            using var settingsForm = new SettingsForm(_currentToken);
            if (settingsForm.ShowDialog(this) == DialogResult.OK)
            {
                _currentToken = settingsForm.Token;
                UpdateTokenStatus();
            }
        }

        private void UpdateTokenStatus()
        {
            if (string.IsNullOrWhiteSpace(_currentToken))
            {
                lblTokenStatus.Text = "⚠  尚未設定 Token，請點擊「設定」填入";
                lblTokenStatus.ForeColor = Color.FromArgb(210, 80, 80);
            }
            else
            {
                lblTokenStatus.Text = "✓  Token 已設定";
                lblTokenStatus.ForeColor = Color.FromArgb(90, 210, 100);
            }
        }

        // =====================================================================
        // 模式切換 (訓練 / 戰鬥)
        // =====================================================================
        private void OnModeTypeChanged(object? sender, EventArgs e)
        {
            bool isBattle = rbModeBattle.Checked;
            if (pnlTrainingSettings != null) pnlTrainingSettings.Visible = !isBattle;
            if (pnlBattleSettings != null) pnlBattleSettings.Visible = isBattle;
            if (btnStart != null) btnStart.Text = isBattle ? "▶  開始戰鬥" : "▶  開始訓練";
            if (lblLogHeader != null) lblLogHeader.Text = isBattle ? "戰鬥 LOG" : "訓練 LOG";

            if (lblTitleRunCount != null) lblTitleRunCount.Text = isBattle ? "戰鬥總次數：" : "已執行：";
            if (lblTitleSuccessCount != null) lblTitleSuccessCount.Text = isBattle ? "勝利次數：" : "成功：";
            if (lblTitleFailCount != null) lblTitleFailCount.Text = isBattle ? "失敗次數：" : "失敗：";

            rbModeTraining.BackColor = isBattle ? Color.FromArgb(40, 40, 50) : Color.FromArgb(60, 70, 110);
            rbModeBattle.BackColor = isBattle ? Color.FromArgb(60, 70, 110) : Color.FromArgb(40, 40, 50);
        }

        // =====================================================================
        // 取得戰鬥對象資訊
        // =====================================================================
        private async void OnGetPlayerInfoClicked(object? sender, EventArgs e)
        {
            string targetId = txtTargetPlayerId.Text.Trim();
            if (string.IsNullOrEmpty(targetId))
            {
                MessageBox.Show("請輸入玩家 ID", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentToken))
            {
                MessageBox.Show("請先設定 Token", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnGetPlayerInfo.Enabled = false;
            lblPlayerInfo.Text = "讀取中...";
            lblPlayerInfo.ForeColor = Color.FromArgb(170, 170, 190);

            try
            {
                var result = await _battleService.GetPlayerProfileAsync(targetId, _currentToken);
                if (result.IsSuccess && result.Data != null)
                {
                    var data = result.Data;
                    string cName = data.Character != null ? data.Character.Name : "無";
                    int cLevel = data.Character != null ? data.Character.Level : 0;
                    lblPlayerInfo.Text = $"暱稱: {data.Nickname}\n角色: Lv.{cLevel} {cName}";
                    lblPlayerInfo.ForeColor = Color.FromArgb(80, 220, 120);
                    _targetPlayerName = data.Nickname ?? string.Empty;
                }
                else
                {
                    lblPlayerInfo.Text = $"讀取失敗: {result.ErrorMessage}";
                    lblPlayerInfo.ForeColor = Color.FromArgb(220, 80, 80);
                    _targetPlayerName = string.Empty;
                }
            }
            finally
            {
                btnGetPlayerInfo.Enabled = true;
            }
        }

        // =====================================================================
        // 手動模式子選項顯示切換（自動→停用子面板；手動→啟用子面板）
        // =====================================================================
        private void OnManualSubModeChanged(object? sender, EventArgs e)
        {
            bool isManual = rbManualEvent.Checked;
            pnlManualSub.Enabled = isManual;
            // 視覺回饋：停用時降低子選項亮度
            rbPopupMode.ForeColor = isManual
                ? Color.FromArgb(210, 210, 225)
                : Color.FromArgb(120, 120, 140);
            rbInlineMode.ForeColor = isManual
                ? Color.FromArgb(210, 210, 225)
                : Color.FromArgb(120, 120, 140);
        }

        // =====================================================================
        // 執行模式切換
        // =====================================================================
        private void OnExecutionModeChanged(object? sender, EventArgs e)
        {
            bool isCount = rbCount.Checked;
            nudCount.Visible   = isCount;
            lblCountUnit.Visible = isCount;
            nudMinutes.Visible  = !isCount;
            lblTimeUnit.Visible = !isCount;
        }

        // =====================================================================
        // 開始按鈕
        // async void：UI 事件不能回傳 Task，只能用 async void。
        // 內部必須自己 try/catch，因為 async void 的 exception 無法被外部捕獲。
        // =====================================================================
        private async void OnStartClicked(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentToken))
            {
                AppendLog("[錯誤] 請先點擊「設定」輸入 Bearer Token。",
                    Color.FromArgb(220, 80, 80));
                return;
            }

            bool isBattleMode = rbModeBattle.Checked;
            TrainingAction? selectedAction = null;

            if (!isBattleMode)
            {
                if (cmbAction.SelectedItem is not TrainingAction action)
                {
                    AppendLog("[錯誤] 請選擇訓練行動。", Color.FromArgb(220, 80, 80));
                    return;
                }
                selectedAction = action;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(txtTargetPlayerId.Text))
                {
                    AppendLog("[錯誤] 請輸入戰鬥對象 ID。", Color.FromArgb(220, 80, 80));
                    return;
                }
            }

            _cts = new CancellationTokenSource();
            SetControlsEnabled(false);
            btnStop.Enabled = true;
            lblChadoSuccessText.Visible = false;
            btnShowLastReport.Visible = false;

            _loopStartTime = DateTime.Now;
            _elapsedTimer.Tick += OnElapsedTick;
            _elapsedTimer.Start();

            try
            {
                if (isBattleMode)
                {
                    var battleSettings = new BattleLoopSettings
                    {
                        Token = _currentToken,
                        TargetPlayerId = txtTargetPlayerId.Text.Trim(),
                        TargetPlayerName = string.IsNullOrEmpty(_targetPlayerName) ? txtTargetPlayerId.Text.Trim() : _targetPlayerName,
                        Type = rbBattleChallenge.Checked ? BattleType.Challenge : BattleType.Chado,
                        Mode = rbCount.Checked ? ExecutionMode.Count : ExecutionMode.Time,
                        CountLimit = (int)nudCount.Value,
                        TimeLimitMinutes = (int)nudMinutes.Value,
                        ExtraDelaySeconds = (double)nudExtraDelay.Value
                    };

                    // 儲存設定
                    AppSettingsManager.Current.LastBattleTargetId = battleSettings.TargetPlayerId;
                    AppSettingsManager.Current.BattleMode = rbBattleChallenge.Checked ? "Challenge" : "Chado";
                    AppSettingsManager.Save();

                    bool isInline = rbManualEvent.Checked && rbInlineMode.Checked;
                    
                    _battleLoop = new BattleLoop(_battleService, _eventService)
                    {
                        OnLog = AppendLog,
                        OnStatusChanged = UpdateStatus,
                        OnStatsUpdated = UpdateBattleStats,
                        IsAutoEventMode = rbAutoEvent.Checked,
                        OnManualEventSelect = rbAutoEvent.Checked ? null :
                                              isInline ? HandleInlineEventAsync : HandleManualEventAsync,
                        OnManualEventResult = rbAutoEvent.Checked ? null :
                                              isInline ? ShowInlineEventResultAsync : HandleManualEventResultAsync,
                        OnCloseManualUi = () => { _currentEventForm?.Close(); HideEventOverlay(); },
                        OnLastBattleResultUpdated = (result) => 
                        {
                            _lastBattleResult = result;
                            if (btnShowLastReport.InvokeRequired)
                                btnShowLastReport.Invoke(() => btnShowLastReport.Visible = true);
                            else
                                btnShowLastReport.Visible = true;
                        }
                    };

                    AppendLog($"開始自動戰鬥：{(battleSettings.Type == BattleType.Challenge ? "友好切磋" : "我要茶渡你")}，目標: {battleSettings.TargetPlayerName}", Color.FromArgb(140, 200, 255));
                    await Task.Run(() => _battleLoop.RunAsync(battleSettings, _cts.Token), _cts.Token);
                }
                else
                {
                    var trainingSettings = new LoopSettings
                    {
                        Token = _currentToken,
                        ActionId = selectedAction!.ActionId,
                        Mode = rbCount.Checked ? ExecutionMode.Count : ExecutionMode.Time,
                        CountLimit = (int)nudCount.Value,
                        TimeLimitMinutes = (int)nudMinutes.Value,
                        ExtraDelaySeconds = (double)nudExtraDelay.Value
                    };

                    bool isInline = rbManualEvent.Checked && rbInlineMode.Checked;
                    bool isPopup  = rbManualEvent.Checked && !rbInlineMode.Checked;
                    AppSettingsManager.Current.EventMode = rbAutoEvent.Checked ? "auto" :
                                                           isInline ? "inline" : "popup";
                    AppSettingsManager.Save();

                    _trainingLoop = new TrainingLoop(_trainingService, _eventService)
                    {
                        IsAutoEventMode = rbAutoEvent.Checked,
                        OnLog = AppendLog,
                        OnStatusChanged = UpdateStatus,
                        OnStatsUpdated = UpdateStats,
                        OnManualEventSelect = rbAutoEvent.Checked ? null :
                                              isInline ? HandleInlineEventAsync : HandleManualEventAsync,
                        OnManualEventResult = rbAutoEvent.Checked ? null :
                                              isInline ? ShowInlineEventResultAsync : HandleManualEventResultAsync,
                        OnCloseManualUi = () => { _currentEventForm?.Close(); HideEventOverlay(); }
                    };

                    AppendLog($"開始訓練：{selectedAction.DisplayName}（{selectedAction.ActionId}）", Color.FromArgb(140, 200, 255));
                    await Task.Run(() => _trainingLoop.RunAsync(trainingSettings, _cts.Token), _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppendLog($"[錯誤] {ex.GetType().Name}: {ex.Message}", Color.FromArgb(220, 80, 80));
            }
            finally
            {
                _elapsedTimer.Stop();
                _elapsedTimer.Tick -= OnElapsedTick;
                SetControlsEnabled(true);
                btnStop.Enabled = false;
                lblNextRun.Text = "—";
            }
        }

        // =====================================================================
        // 停止按鈕
        // =====================================================================
        private void OnStopClicked(object? sender, EventArgs e)
        {
            _cts?.Cancel();
            _currentEventForm?.Close(); // 如果事件視窗開著，一起關掉
            HideEventOverlay();          // 如果 Overlay 展開著，一起關掉
            AppendLog("使用者按下停止，正在中止訓練…", Color.FromArgb(220, 160, 80));
            btnStop.Enabled = false;
        }

        // =====================================================================
        // 更新狀態標籤
        // =====================================================================
        private void UpdateStatus(LoopStatus status)
        {
            if (lblStatusValue.InvokeRequired)
            {
                lblStatusValue.Invoke(() => UpdateStatus(status));
                return;
            }
            (lblStatusValue.Text, lblStatusValue.ForeColor) = status switch
            {
                LoopStatus.Running         => ("執行中",     Color.FromArgb(80, 220, 120)),
                LoopStatus.WaitingCooldown => ("等待冷卻",   Color.FromArgb(170, 215, 255)),
                LoopStatus.WaitingEvent    => ("等待事件選擇", Color.FromArgb(190, 150, 255)),
                LoopStatus.Completed       => ("已完成",     Color.FromArgb(140, 200, 255)),
                LoopStatus.Error           => ("發生錯誤",   Color.FromArgb(220, 80,  80)),
                _                          => ("已停止",     Color.FromArgb(170, 170, 180)),
            };
        }

        // =====================================================================
        // 更新統計數字
        // =====================================================================
        private void UpdateStats(LoopStats stats)
        {
            if (lblRunCount.InvokeRequired)
            {
                lblRunCount.Invoke(() => UpdateStats(stats));
                return;
            }
            lblRunCount.Text          = $"{stats.RunCount} 次";
            lblSuccessCount.Text      = $"{stats.SuccessCount} 次";
            lblFailCount.Text         = $"{stats.FailCount} 次";
            lblEventCount.Text        = $"{stats.EventCount} 次";
            lblEventSuccessCount.Text = $"{stats.EventSuccessCount} 次";
            lblEventFailCount.Text    = $"{stats.EventFailCount} 次";

            if (stats.CurrentLevel > 0) lblLevel.Text = stats.CurrentLevel.ToString();
            lblTotalExp.Text = stats.TotalExp.ToString("N0");

            if (stats.GainedCharacters.Count == 0)
            {
                lblGainedCharacters.Text = "無";
            }
            else
            {
                lblGainedCharacters.Text = string.Join("、", stats.GainedCharacters);
            }

            lblNextRun.Text = stats.NextRunCountdownSeconds > 0
                ? $"{stats.NextRunCountdownSeconds:F1} 秒"
                : "—";
        }

        private void UpdateBattleStats(BattleStats stats)
        {
            if (lblRunCount.InvokeRequired)
            {
                lblRunCount.Invoke(() => UpdateBattleStats(stats));
                return;
            }

            lblRunCount.Text = $"{stats.RunCount} 次";
            lblSuccessCount.Text = $"{stats.WinCount} 次";
            lblFailCount.Text = $"{stats.LossCount} 次";
            lblTotalExp.Text = $"{stats.TotalExp:N0}";
            
            lblEventCount.Text = $"{stats.EventCount} 次";
            lblEventSuccessCount.Text = $"{stats.EventSuccessCount} 次";
            lblEventFailCount.Text = $"{stats.EventFailCount} 次";
            
            lblLevel.Text = stats.CurrentLevel > 0 ? $"Lv.{stats.CurrentLevel}" : "—";
            
            if (stats.GainedCharacters.Count > 0)
                lblGainedCharacters.Text = string.Join(", ", stats.GainedCharacters);
            else
                lblGainedCharacters.Text = "無";

            if (stats.NextRunCountdownSeconds > 0)
                lblNextRun.Text = $"{stats.NextRunCountdownSeconds:F1} 秒後";
            else
                lblNextRun.Text = "—";

            if (stats.ChadoSuccessCount > 0)
            {
                lblChadoSuccessText.Text = "此輪戰鬥螺旋下指小零食成功！";
                lblChadoSuccessText.Visible = true;
            }
            else
            {
                lblChadoSuccessText.Visible = false;
            }
        }

        // =====================================================================
        // 運作時間計時器
        // =====================================================================
        private void OnElapsedTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _loopStartTime;
            lblElapsed.Text = elapsed.ToString(@"hh\:mm\:ss");
        }

        // =====================================================================
        // 鎖定/解鎖設定控制項
        // =====================================================================
        private void SetControlsEnabled(bool enabled)
        {
            rbModeTraining.Enabled = enabled;
            rbModeBattle.Enabled   = enabled;
            btnSettings.Enabled    = enabled;
            cmbAction.Enabled      = enabled;
            txtTargetPlayerId.Enabled = enabled;
            btnGetPlayerInfo.Enabled = enabled;
            rbCount.Enabled        = enabled;
            rbTime.Enabled         = enabled;
            nudCount.Enabled       = enabled;
            nudMinutes.Enabled     = enabled;
            nudExtraDelay.Enabled  = enabled;
            rbAutoEvent.Enabled    = enabled;
            rbManualEvent.Enabled  = enabled;
            // pnlManualSub 整組控制 — 只有手動模式且 enabled=true 時才可操作
            pnlManualSub.Enabled   = enabled && rbManualEvent.Checked;
            btnStart.Enabled       = enabled;
        }

        // =====================================================================
        // 人工選擇事件 UI
        //
        // 流程：
        // 1. TrainingLoop（ThreadPool Thread）呼叫此方法
        // 2. 建立 TaskCompletionSource<string?>
        // 3. Invoke 到 UI Thread 顯示 EventSelectionForm
        // 4. TrainingLoop await tcs.Task（掛起等待）
        // 5. 使用者點選選項 → OptionSelected 事件 → tcs.SetResult(optionId)
        // 6. 視窗關閉，TrainingLoop 繼續執行
        //
        // TaskCompletionSource 說明：
        // 這是「手動控制的 Task」。在任意時間呼叫 tcs.SetResult(value)，
        // 所有 await tcs.Task 的地方就會收到結果並繼續執行。
        // =====================================================================
        private Task<string?> HandleManualEventAsync(PendingEvent pendingEvent, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<string?>();

            // 如果使用者按停止（CancellationToken 觸發），關閉事件視窗並回傳 null
            var reg = ct.Register(() =>
            {
                this.BeginInvoke(() => _currentEventForm?.Close());
                tcs.TrySetResult(null);
            });

            this.Invoke(() =>
            {
                _currentEventForm = new EventSelectionForm(pendingEvent);

                // 使用者點選選項：設定結果
                _currentEventForm.OptionSelected += optionId =>
                {
                    reg.Dispose();
                    tcs.TrySetResult(optionId);
                };

                // 視窗被關閉（或選擇完按下關閉）：清理引用
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

        // =====================================================================
        // 內嵌事件 UI（零干擾模式）
        //
        // 流程：
        // 1. TrainingLoop（ThreadPool Thread）呼叫此方法
        // 2. 建立 TaskCompletionSource<string?>
        // 3. Invoke 到 UI Thread，在 pnlEventOverlay 渲染事件選項
        // 4. 若設定開啟，發送 Windows BalloonTip 通知
        // 5. TrainingLoop await tcs.Task（掛起等待，不阻塞 UI）
        // 6. 使用者點選選項 → tcs.SetResult(optionId) → Overlay 隱藏
        // =====================================================================
        private Task<string?> HandleInlineEventAsync(PendingEvent pendingEvent, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<string?>();

            // CancellationToken 觸發時（使用者按停止）：隱藏 Overlay 並回傳 null
            var reg = ct.Register(() =>
            {
                this.BeginInvoke(() =>
                {
                    HideEventOverlay();
                    tcs.TrySetResult(null);
                });
            });

            this.Invoke(() =>
            {
                ShowInlineEvent(pendingEvent, optionId =>
                {
                    reg.Dispose();
                    tcs.TrySetResult(optionId);
                });

                // 系統通知（若設定開啟，不含 emoji 避免相容性問題）
                if (AppSettingsManager.Current.EnableSystemNotify)
                {
                    var title = $"事件觸發：{pendingEvent.Name}";
                    var content = string.IsNullOrWhiteSpace(pendingEvent.Description)
                        ? "請切換至 MyDoujin Bot 視窗選擇應對選項。"
                        : pendingEvent.Description;

                    try { ToastNotificationManagerCompat.History.Clear(); } catch { }

                    new ToastContentBuilder()
                        .AddText(title)
                        .AddText(content)
                        .AddAudio(new ToastAudio() { Silent = !AppSettingsManager.Current.EnableSystemNotifySound })
                        .Show();
                }
            });

            return tcs.Task;
        }

        // =====================================================================
        // 處理彈出視窗模式下的事件結果顯示
        // =====================================================================
        private Task HandleManualEventResultAsync(EventResult er, CancellationToken ct)
        {
            var form = _currentEventForm;
            if (form != null && !form.IsDisposed)
            {
                var tcs = new TaskCompletionSource<bool>();
                this.Invoke(() =>
                {
                    if (form != null && !form.IsDisposed)
                    {
                        var task = form.ShowResultAsync(er, ct);
                        task.ContinueWith(_ => tcs.TrySetResult(true));
                    }
                    else
                    {
                        tcs.TrySetResult(true);
                    }
                });
                return tcs.Task;
            }
            return Task.CompletedTask;
        }

        // =====================================================================
        // 處理內嵌模式下的事件結果顯示
        // =====================================================================
        private Task ShowInlineEventResultAsync(EventResult er, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            var reg = ct.Register(() =>
            {
                this.BeginInvoke(() =>
                {
                    HideEventOverlay();
                    tcs.TrySetResult(false);
                });
            });

            Action buildAction = () =>
            {
                pnlEventOverlay.Controls.Clear();
                pnlEventOverlay.Visible = true;
                pnlEventOverlay.BringToFront();
                if (lblLogHeader != null) lblLogHeader.Visible = false;

                const int pad = 14;

                // ── Header Bar ──
                var pnlHeader = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 42,
                    BackColor = Color.FromArgb(40, 30, 60)
                };
                var lblHeader = new Label
                {
                    Text = "⚡  事件結果",
                    Dock = DockStyle.Fill,
                    ForeColor = Color.FromArgb(200, 160, 255),
                    Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(pad, 0, 0, 0)
                };
                pnlHeader.Controls.Add(lblHeader);
                pnlEventOverlay.Controls.Add(pnlHeader);

                // ── 內容區 ──
                var pnlContent = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoScroll = true,
                    Padding = new Padding(pad, 14, pad, 10),
                    BackColor = Color.FromArgb(20, 20, 28)
                };
                pnlEventOverlay.Controls.Add(pnlHeader);
                pnlEventOverlay.Controls.Add(pnlContent);
                pnlHeader.SendToBack();
                pnlContent.BringToFront();

                // ── 標題：成功 / 失敗 ──
                string resultTitle = er.IsSuccess ? "成功" : "失敗";
                Color titleColor = er.IsSuccess 
                    ? Color.FromArgb(80, 220, 120) 
                    : Color.FromArgb(240, 80, 80);

                var lblTitle = new Label
                {
                    Text = resultTitle,
                    Font = new Font("Microsoft JhengHei UI", 15f, FontStyle.Bold),
                    ForeColor = titleColor,
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, 14)
                };
                pnlContent.Controls.Add(lblTitle);

                // ── 判定結果區塊 ──
                if (!string.IsNullOrEmpty(er.Stat) || er.Roll.HasValue)
                {
                    // Width doesn't matter initially, the Resize handler will fix it
                    var pnlCheck = MyDoujinBot.Utilities.EventUiHelper.CreateCheckResultPanel(er, 200);
                    pnlCheck.Margin = new Padding(0, 0, 0, 14);
                    pnlContent.Controls.Add(pnlCheck);
                }

                // ── 故事內文 ──
                if (!string.IsNullOrWhiteSpace(er.Text))
                {
                    var lblStory = new Label
                    {
                        Text = er.Text,
                        AutoSize = true,
                        ForeColor = Color.FromArgb(220, 220, 235),
                        Font = new Font("Microsoft JhengHei UI", 10.5f),
                        Margin = new Padding(0, 0, 0, 14)
                    };
                    pnlContent.Controls.Add(lblStory);
                }

                // ── 戰鬥結果 ──
                if (er.BattleResult != null)
                {
                    var btnBattle = new Button
                    {
                        Text = "⚔ 查看戰報",
                        Width = 120,
                        Height = 36,
                        FlatStyle = FlatStyle.Flat,
                        BackColor = Color.FromArgb(50, 50, 70),
                        ForeColor = Color.White,
                        Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                        Cursor = Cursors.Hand,
                        Margin = new Padding(0, 0, 0, 14)
                    };
                    btnBattle.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 130);
                    btnBattle.Click += (_, _) =>
                    {
                        using var form = new MyDoujinBot.Forms.BattleReportForm(er.BattleResult);
                        form.ShowDialog(this);
                    };
                    pnlContent.Controls.Add(btnBattle);
                }

                // ── 獲得獎勵 ──
                var bonusParts = MyDoujinBot.Utilities.EventUiHelper.BuildBonusStatsParts(er.Rewards?.BonusStats);
                if (bonusParts.Count > 0)
                {
                    var lblReward = new Label
                    {
                        Text = $"獲得獎勵：{string.Join("、", bonusParts)}",
                        AutoSize = true,
                        ForeColor = Color.FromArgb(255, 220, 80),
                        Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                        Margin = new Padding(0, 0, 0, 16)
                    };
                    pnlContent.Controls.Add(lblReward);
                }

                // ── 關閉按鈕 ──
                var btnClose = new Button
                {
                    Text = "關閉",
                    Width = 120,
                    Height = 36,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(60, 60, 80),
                    ForeColor = Color.White,
                    Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    Tag = "CenterCloseBtn",
                    Margin = new Padding(0)
                };
                btnClose.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 130);
                btnClose.Click += (_, _) =>
                {
                    reg.Dispose();
                    HideEventOverlay();
                    tcs.TrySetResult(true);
                };
                pnlContent.Controls.Add(btnClose);

                // Resize handle for dynamic layout
                pnlContent.Resize += (s, ev) =>
                {
                    int w = pnlContent.ClientSize.Width - pnlContent.Padding.Horizontal;
                    if (w < 200) w = 200;
                    
                    pnlContent.SuspendLayout();
                    foreach (Control ctrl in pnlContent.Controls)
                    {
                        if (ctrl.Tag?.ToString() == "CenterCloseBtn")
                        {
                            ctrl.Margin = new Padding((w - ctrl.Width) / 2, ctrl.Margin.Top, 0, 24);
                        }
                        else
                        {
                            ctrl.Width = w;
                            if (ctrl is Label lbl && lbl.AutoSize)
                            {
                                lbl.MaximumSize = new Size(w, 0);
                            }
                        }
                    }
                    pnlContent.ResumeLayout();
                };

                pnlEventOverlay.BringToFront();
            };

            if (this.InvokeRequired)
                this.Invoke(buildAction);
            else
                buildAction();

            return tcs.Task;
        }

        // =====================================================================
        // 渲染事件 Overlay 內容
        // onOptionSelected：使用者點選某個選項後呼叫，帶入選擇的 optionId
        // =====================================================================
        private void ShowInlineEvent(PendingEvent pe, Action<string?> onOptionSelected)
        {
            // 清空上一次的 Overlay 內容
            pnlEventOverlay.Controls.Clear();

            const int pad = 14;

            // ── Header Bar ──
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Color.FromArgb(40, 30, 60)
            };
            var lblHeader = new Label
            {
                Text = "⚡  遭遇事件",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(200, 160, 255),
                Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(pad, 0, 0, 0)
            };
            pnlHeader.Controls.Add(lblHeader);
            pnlEventOverlay.Controls.Add(pnlHeader);

            // ── 內容區（FlowLayoutPanel 可自動適應並重排）──
            var pnlContent = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(pad, 18, pad, 10),
                BackColor = Color.FromArgb(20, 20, 28)
            };
            pnlEventOverlay.Controls.Add(pnlHeader);
            pnlEventOverlay.Controls.Add(pnlContent);
            pnlHeader.SendToBack();
            pnlContent.BringToFront();

            // ── 事件名稱 ──
            var lblName = new Label
            {
                Text = pe.Name,
                Font = new Font("Microsoft JhengHei UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 215, 80),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            pnlContent.Controls.Add(lblName);

            // ── 事件描述 ──
            if (!string.IsNullOrWhiteSpace(pe.Description))
            {
                var lblDesc = new Label
                {
                    Text = pe.Description,
                    AutoSize = true,
                    ForeColor = Color.FromArgb(195, 195, 215),
                    Font = new Font("Microsoft JhengHei UI", 10.5f),
                    Margin = new Padding(0, 0, 0, 14)
                };
                pnlContent.Controls.Add(lblDesc);
            }

            // ── 分隔線 ──
            var lblSep = new Label
            {
                Height = 1,
                BackColor = Color.FromArgb(70, 60, 100),
                AutoSize = false,
                Text = "",
                Margin = new Padding(0, 0, 0, 14)
            };
            pnlContent.Controls.Add(lblSep);

            // ── 選項標題 ──
            pnlContent.Controls.Add(new Label
            {
                Text = "請選擇一個選項：",
                ForeColor = Color.FromArgb(150, 150, 195),
                Font = new Font("Microsoft JhengHei UI", 9f),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            });

            // ── 選項按鈕 ──
            foreach (var option in pe.Options)
            {
                string rateText = option.SuccessChance.HasValue ? $"{option.SuccessChance}%" : "未知";
                string mainText = option.Name;
                string subText = string.IsNullOrEmpty(option.CheckStat)
                    ? " (直接行動)"
                    : $" ({MyDoujinBot.Utilities.StatHelper.GetDisplayName(option.CheckStat)}：- / 成功率：{rateText})";

                Color btnBack = option.SuccessChance.HasValue
                    ? (option.SuccessChance.Value >= 70
                        ? Color.FromArgb(40, 70, 50)
                        : option.SuccessChance.Value >= 40
                            ? Color.FromArgb(60, 55, 40)
                            : Color.FromArgb(70, 40, 40))
                    : Color.FromArgb(50, 50, 70);

                Color btnBackHover = Color.FromArgb(
                    Math.Min(btnBack.R + 30, 255),
                    Math.Min(btnBack.G + 30, 255),
                    Math.Min(btnBack.B + 30, 255));

                var btn = new Button
                {
                    Height = 46,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = btnBack,
                    Cursor = Cursors.Hand,
                    Tag = option.Id,
                    Text = "",
                    Margin = new Padding(0, 0, 0, 6)
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(90, 75, 130);
                btn.FlatAppearance.BorderSize = 1;

                var mainFont = new Font("Microsoft JhengHei UI", 9.5f);
                var subFont  = new Font("Microsoft JhengHei UI", 8.2f);

                btn.Paint += (s, e) =>
                {
                    const int leftPad = 12;
                    Size mainSize = TextRenderer.MeasureText(e.Graphics, mainText, mainFont,
                        new Size(btn.Width, btn.Height), TextFormatFlags.NoPadding);
                    Size subSize = TextRenderer.MeasureText(e.Graphics, subText, subFont,
                        new Size(btn.Width, btn.Height), TextFormatFlags.NoPadding);

                    int mainY = (btn.Height - mainSize.Height) / 2;
                    int subY  = (btn.Height - subSize.Height)  / 2;

                    TextRenderer.DrawText(e.Graphics, mainText, mainFont,
                        new Point(leftPad, mainY),
                        Color.FromArgb(235, 230, 255), TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(e.Graphics, subText, subFont,
                        new Point(leftPad + mainSize.Width + 2, subY + 1),
                        Color.FromArgb(165, 165, 185), TextFormatFlags.NoPadding);
                };

                btn.Disposed += (s, e) =>
                {
                    mainFont.Dispose();
                    subFont.Dispose();
                };

                var capturedOption = option;
                btn.MouseEnter += (_, _) => btn.BackColor = btnBackHover;
                btn.MouseLeave += (_, _) => btn.BackColor = btnBack;

                btn.Click += (_, _) =>
                {
                    foreach (Control ctrl in pnlContent.Controls)
                    {
                        ctrl.Enabled = false;
                    }
                    onOptionSelected(capturedOption.Id);
                };

                pnlContent.Controls.Add(btn);
            }

            // ── 底部留白 ──
            pnlContent.Controls.Add(new Label { Height = 24, AutoSize = false, Text = "", BackColor = Color.Transparent });

            pnlContent.Resize += (s, e) =>
            {
                int w = pnlContent.ClientSize.Width - pnlContent.Padding.Horizontal;
                if (w < 200) w = 200;

                pnlContent.SuspendLayout();
                foreach (Control ctrl in pnlContent.Controls)
                {
                    ctrl.Width = w;
                    if (ctrl is Label lbl && lbl.AutoSize)
                    {
                        lbl.MaximumSize = new Size(w, 0);
                    }
                }
                pnlContent.ResumeLayout();
            };

            pnlEventOverlay.Visible = true;
            pnlEventOverlay.BringToFront();
            if (lblLogHeader != null) lblLogHeader.Visible = false;
        }

        /// <summary>隱藏並清空事件 Overlay</summary>
        private void HideEventOverlay()
        {
            if (pnlEventOverlay.InvokeRequired)
            {
                pnlEventOverlay.Invoke(HideEventOverlay);
                return;
            }
            pnlEventOverlay.Visible = false;
            pnlEventOverlay.Controls.Clear();
            if (lblLogHeader != null) lblLogHeader.Visible = true;
        }

        // =====================================================================
        // 向 LOG 追加彩色文字（執行緒安全）
        //
        // LOG 上限：2000 行。超過時自動刪除最舊的 500 行。
        // =====================================================================
        internal void AppendLog(string message, Color color)
        {
            if (rtbLog.InvokeRequired)
            {
                rtbLog.Invoke(() => AppendLog(message, color));
                return;
            }

            // LOG 行數上限管理
            const int maxLines = 2000;
            const int trimLines = 500;
            if (rtbLog.Lines.Length > maxLines)
            {
                int trimToIndex = rtbLog.GetFirstCharIndexFromLine(trimLines);
                rtbLog.Select(0, trimToIndex);
                rtbLog.SelectedText = "";
            }

            rtbLog.SelectionStart = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor = color;
            rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            rtbLog.SelectionColor = rtbLog.ForeColor;
            rtbLog.ScrollToCaret();
        }

        // =====================================================================
        // 視窗關閉：取消背景工作，釋放資源
        // =====================================================================
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            _cts?.Cancel();
            _currentEventForm?.Close();
            HideEventOverlay();
            _elapsedTimer.Dispose();
            _trainingService.Dispose();
            _eventService.Dispose();
            _notifyIcon.Visible = false; // 關閉前隱藏托盤圖示，避免殘留
            _notifyIcon.Dispose();
        }
    }
}
