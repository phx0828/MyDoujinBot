using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
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
        private Label lblEventCount = null!;
        private Label lblEventSuccessCount = null!;
        private Label lblEventFailCount = null!;
        private Label lblLevel = null!;
        private Label lblTotalExp = null!;
        private Label lblNextRun = null!;
        private Label lblElapsed = null!;

        // =====================================================================
        // 狀態與 Service
        // =====================================================================

        // Token：啟動時從 AppSettings 讀取，使用者儲存設定時更新
        private string _currentToken = string.Empty;

        private readonly TrainingService _trainingService = new();
        private readonly EventService _eventService = new();
        private TrainingLoop? _trainingLoop;
        private EventSelectionForm? _currentEventForm;

        private CancellationTokenSource? _cts;
        private readonly System.Windows.Forms.Timer _elapsedTimer = new() { Interval = 1000 };
        private DateTime _loopStartTime;

        // =====================================================================
        // 建構子
        // =====================================================================
        public MainForm()
        {
            this.Text = "MyDoujin Bot";
            this.Icon = new Icon(@"Resources\app.ico");

            this.Size = new Size(1100, 720);
            this.MinimumSize = new Size(900, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(22, 22, 30);
            this.ForeColor = Color.FromArgb(220, 220, 230);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);

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
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 375)); // 設定欄
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210)); // 狀態欄
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // LOG 欄（自動填滿）
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            this.Controls.Add(table);

            // ── 左欄：設定面板 ──
            var pnlLeft = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(35, 35, 45),
                Padding = new Padding(2)
            };
            // AutoScroll = false：移除滾動條。
            // 設定內容高度 ~500px，在最小視窗 650px 下完全可以容納。
            pnlLeft.AutoScroll = false;
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
        // 左欄：設定面板內容
        // =====================================================================
        private void BuildLeftPanel(Panel panel)
        {
            int y = 8;
            const int x = 10;
            const int ctrlW = 345;

            // ── API 設定 ──
            y = AddSectionHeader(panel, "API 設定", y, x);

            // 設定按鈕（開啟 SettingsForm 對話框）
            btnSettings = new Button
            {
                Location = new Point(x, y),
                Width = 100,
                Height = 32,
                Text = "⚙  設定",
                BackColor = Color.FromArgb(60, 70, 110),
                ForeColor = Color.FromArgb(200, 210, 255),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.Click += OnSettingsClicked;
            panel.Controls.Add(btnSettings);
            y += 36; // 按鈕高度 + 小間距

            // Token 狀態標籤 — 獨立一行，避免被截斷
            lblTokenStatus = new Label
            {
                Location = new Point(x, y),
                Width = ctrlW,
                Text = "⚠  尚未設定 Token，請點擊「設定」填入",
                ForeColor = Color.FromArgb(210, 80, 80),
                AutoSize = false,
                Height = 18
            };
            panel.Controls.Add(lblTokenStatus);
            y += 24;

            // ── 訓練行動 ──
            y = AddSectionHeader(panel, "訓練行動", y + 4, x);

            AddLabel(panel, "選擇訓練：", x, y);
            y += 22;

            cmbAction = new ComboBox
            {
                Location = new Point(x, y),
                Width = ctrlW,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 58),
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat
            };
            foreach (var action in TrainingActions.All)
                cmbAction.Items.Add(action);
            if (cmbAction.Items.Count > 0)
                cmbAction.SelectedIndex = 0;
            panel.Controls.Add(cmbAction);
            y += 34;

            // ── 執行設定 ──
            y = AddSectionHeader(panel, "執行設定", y + 4, x);

            AddLabel(panel, "執行模式：", x, y);
            y += 22;

            var pnlExecMode = new Panel
            {
                Location = new Point(0, y),
                Width = ctrlW + x,
                Height = 24,
                BackColor = Color.Transparent
            };
            panel.Controls.Add(pnlExecMode);

            rbCount = new RadioButton
            {
                Location = new Point(x, 0),
                Text = "執行指定次數",
                Checked = true,
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true
            };
            rbCount.CheckedChanged += OnExecutionModeChanged;
            pnlExecMode.Controls.Add(rbCount);

            rbTime = new RadioButton
            {
                Location = new Point(x + 145, 0),
                Text = "執行指定時間",
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true
            };
            rbTime.CheckedChanged += OnExecutionModeChanged;
            pnlExecMode.Controls.Add(rbTime);
            y += 28;

            nudCount = new NumericUpDown
            {
                Location = new Point(x, y),
                Width = 110,
                Minimum = 1,
                Maximum = 99999,
                Value = 100,
                BackColor = Color.FromArgb(45, 45, 58),
                ForeColor = Color.FromArgb(220, 220, 230)
            };
            panel.Controls.Add(nudCount);
            lblCountUnit = AddLabel(panel, "次", x + 118, y + 4);

            nudMinutes = new NumericUpDown
            {
                Location = new Point(x, y),
                Width = 110,
                Minimum = 1,
                Maximum = 1440,
                Value = 30,
                BackColor = Color.FromArgb(45, 45, 58),
                ForeColor = Color.FromArgb(220, 220, 230),
                Visible = false
            };
            panel.Controls.Add(nudMinutes);
            lblTimeUnit = AddLabel(panel, "分鐘", x + 118, y + 4, visible: false);
            y += 34;

            AddLabel(panel, "額外冷卻延遲：", x, y);
            y += 22;

            nudExtraDelay = new NumericUpDown
            {
                Location = new Point(x, y),
                Width = 110,
                Minimum = 0,
                Maximum = 60,
                Value = 2,
                BackColor = Color.FromArgb(45, 45, 58),
                ForeColor = Color.FromArgb(220, 220, 230)
            };
            panel.Controls.Add(nudExtraDelay);
            AddLabel(panel, "秒（0～此值，毫秒精度）", x + 118, y + 4);
            y += 34;

            // ── 遭遇事件應對 ──
            y = AddSectionHeader(panel, "遭遇事件應對", y + 4, x);

            // pnlEventMode 高度 = rbAutoEvent(28) + rbManualEvent(28) + pnlManualSub(60) = 116
            var pnlEventMode = new Panel
            {
                Location = new Point(0, y),
                Width = ctrlW + x,
                Height = 116,
                BackColor = Color.Transparent
            };
            panel.Controls.Add(pnlEventMode);

            rbAutoEvent = new RadioButton
            {
                Location = new Point(x, 0),
                Text = "自動選擇（最高成功率）",
                Checked = true,
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true
            };
            rbAutoEvent.CheckedChanged += OnManualSubModeChanged;
            pnlEventMode.Controls.Add(rbAutoEvent);

            rbManualEvent = new RadioButton
            {
                Location = new Point(x, 28),
                Text = "手動選擇",
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true
            };
            rbManualEvent.CheckedChanged += OnManualSubModeChanged;
            pnlEventMode.Controls.Add(rbManualEvent);

            // 手動選擇的子選項（縮排顯示）
            pnlManualSub = new Panel
            {
                Location = new Point(x + 22, 56),
                Width = ctrlW - 22,
                Height = 60,
                BackColor = Color.Transparent,
                Enabled = false // 預設 auto 選中時停用
            };
            pnlEventMode.Controls.Add(pnlManualSub);

            rbPopupMode = new RadioButton
            {
                Location = new Point(0, 0),
                Text = "彈出視窗（奪取焦點）",
                Checked = true,
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true
            };
            pnlManualSub.Controls.Add(rbPopupMode);

            rbInlineMode = new RadioButton
            {
                Location = new Point(0, 28),
                Text = "內嵌於主畫面（零干擾）",
                ForeColor = Color.FromArgb(210, 210, 225),
                AutoSize = true
            };
            pnlManualSub.Controls.Add(rbInlineMode);

            y += 116;

            // ── 開始 / 停止 ──
            btnStart = new Button
            {
                Location = new Point(x, y),
                Width = 163,
                Height = 38,
                Text = "▶  開始訓練",
                BackColor = Color.FromArgb(38, 155, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnStart.FlatAppearance.BorderSize = 0;
            btnStart.Click += OnStartClicked;
            panel.Controls.Add(btnStart);

            btnStop = new Button
            {
                Location = new Point(x + 175, y),
                Width = 163,
                Height = 38,
                Text = "⏹  停止",
                BackColor = Color.FromArgb(155, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnStop.FlatAppearance.BorderSize = 0;
            btnStop.Click += OnStopClicked;
            panel.Controls.Add(btnStop);
        }

        // =====================================================================
        // 中欄：即時狀態面板內容
        // =====================================================================
        private void BuildMiddlePanel(Panel panel)
        {
            int y = 8;
            const int x = 10;

            y = AddSectionHeader(panel, "即時狀態", y, x);

            lblStatusValue     = AddStatRow(panel, "狀態：",     "已停止",    Color.FromArgb(170, 170, 180), x, ref y);
            lblRunCount        = AddStatRow(panel, "已執行：",   "0 次",      Color.FromArgb(210, 210, 225), x, ref y);
            lblSuccessCount    = AddStatRow(panel, "成功：",     "0 次",      Color.FromArgb(90,  215, 110), x, ref y);
            lblFailCount       = AddStatRow(panel, "失敗：",     "0 次",      Color.FromArgb(215, 90,  90),  x, ref y);

            y += 6; // 小間距
            lblEventCount         = AddStatRow(panel, "觸發事件：",  "0 次", Color.FromArgb(190, 150, 255), x, ref y);
            lblEventSuccessCount  = AddStatRow(panel, "事件成功：",  "0 次", Color.FromArgb(90,  215, 110), x, ref y);
            lblEventFailCount     = AddStatRow(panel, "事件失敗：",  "0 次", Color.FromArgb(215, 90,  90),  x, ref y);

            y += 6;
            lblLevel    = AddStatRow(panel, "目前等級：", "—",        Color.FromArgb(255, 215, 70),  x, ref y);
            lblTotalExp = AddStatRow(panel, "累積 EXP：", "0",        Color.FromArgb(210, 210, 225), x, ref y);

            y += 6;
            lblNextRun  = AddStatRow(panel, "下次執行：", "—",        Color.FromArgb(170, 215, 255), x, ref y);
            lblElapsed  = AddStatRow(panel, "運作時間：", "00:00:00", Color.FromArgb(210, 210, 225), x, ref y);
        }

        // =====================================================================
        // 右欄：LOG 面板內容（含事件 Overlay）
        // =====================================================================
        private void BuildLogPanel(Panel panel)
        {
            lblLogHeader = new Label
            {
                Text = "訓練 LOG",
                Dock = DockStyle.Top,
                Height = 26,
                ForeColor = Color.FromArgb(160, 160, 195),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 0, 0)
            };

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
            // 原理：WinForms 中，後加入的控制項會顯示在前面（Z-Order）。
            // Overlay 最後加入，因此視覺上蓋在 rtbLog 上方。
            pnlEventOverlay = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 28),
                Visible = false,
                Padding = new Padding(0)
            };

            // Dock 順序：Fill 先加，Top 後加（WinForms Dock 從後往前佔位）
            panel.Controls.Add(rtbLog);
            panel.Controls.Add(lblLogHeader);
            // Overlay 最後加入 → 位於最上層（蓋住 rtbLog）
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
        // Helper：建立區塊標題 + 分隔線
        // =====================================================================
        private static int AddSectionHeader(Control parent, string text, int y, int x)
        {
            parent.Controls.Add(new Label
            {
                Location = new Point(x, y),
                Width = parent.Width - x * 2,
                Text = text,
                ForeColor = Color.FromArgb(130, 170, 255),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = false,
                Height = 22
            });
            parent.Controls.Add(new Label
            {
                Location = new Point(x, y + 22),
                Width = parent.Width - x * 2,
                Height = 1,
                BackColor = Color.FromArgb(65, 65, 85),
                AutoSize = false,
                Text = ""
            });
            return y + 32;
        }

        private static Label AddLabel(Control parent, string text, int x, int y,
            Color? color = null, bool visible = true)
        {
            var lbl = new Label
            {
                Location = new Point(x, y),
                Text = text,
                ForeColor = color ?? Color.FromArgb(170, 170, 190),
                AutoSize = true,
                Visible = visible
            };
            parent.Controls.Add(lbl);
            return lbl;
        }

        // =====================================================================
        // Helper：中欄狀態列（標籤 + 數值）
        // ref y：讓 helper 內部自動累加 y，呼叫方不用手動 y += 26
        // =====================================================================
        private static Label AddStatRow(Control parent, string labelText, string valueText,
            Color valueColor, int x, ref int y)
        {
            parent.Controls.Add(new Label
            {
                Location = new Point(x, y),
                Text = labelText,
                ForeColor = Color.FromArgb(140, 140, 160),
                AutoSize = true
            });
            var lblValue = new Label
            {
                Location = new Point(x + 95, y),
                Text = valueText,
                ForeColor = valueColor,
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold)
            };
            parent.Controls.Add(lblValue);
            y += 25;
            return lblValue;
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

            if (cmbAction.SelectedItem is not TrainingAction selectedAction)
            {
                AppendLog("[錯誤] 請選擇訓練行動。", Color.FromArgb(220, 80, 80));
                return;
            }

            var settings = new LoopSettings
            {
                Token = _currentToken,
                ActionId = selectedAction.ActionId,
                Mode = rbCount.Checked ? ExecutionMode.Count : ExecutionMode.Time,
                CountLimit = (int)nudCount.Value,
                TimeLimitMinutes = (int)nudMinutes.Value,
                ExtraDelaySeconds = (double)nudExtraDelay.Value
            };

            // 計算並儲存目前的事件模式
            bool isInline = rbManualEvent.Checked && rbInlineMode.Checked;
            bool isPopup  = rbManualEvent.Checked && !rbInlineMode.Checked;
            AppSettingsManager.Current.EventMode = rbAutoEvent.Checked ? "auto" :
                                                   isInline ? "inline" : "popup";
            AppSettingsManager.Save();

            _cts = new CancellationTokenSource();

            _trainingLoop = new TrainingLoop(_trainingService, _eventService)
            {
                IsAutoEventMode = rbAutoEvent.Checked,
                OnLog = AppendLog,
                OnStatusChanged = UpdateStatus,
                OnStatsUpdated = UpdateStats,
                // 自動模式：OnManualEventSelect = null（TrainingLoop 自動選擇）
                // 手動+彈窗：HandleManualEventAsync
                // 手動+內嵌：HandleInlineEventAsync
                OnManualEventSelect = rbAutoEvent.Checked ? null :
                                      isInline ? HandleInlineEventAsync : HandleManualEventAsync,
                OnManualEventResult = rbAutoEvent.Checked ? null :
                                      isInline ? ShowInlineEventResultAsync : HandleManualEventResultAsync,
                OnCloseManualUi = () => { _currentEventForm?.Close(); HideEventOverlay(); }
            };

            SetControlsEnabled(false);
            btnStop.Enabled = true;

            _loopStartTime = DateTime.Now;
            _elapsedTimer.Tick += OnElapsedTick;
            _elapsedTimer.Start();

            AppendLog($"開始訓練：{selectedAction.DisplayName}（{selectedAction.ActionId}）",
                Color.FromArgb(140, 200, 255));

            try
            {
                await Task.Run(() => _trainingLoop.RunAsync(settings, _cts.Token), _cts.Token);
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

            lblNextRun.Text = stats.NextRunCountdownSeconds > 0
                ? $"{stats.NextRunCountdownSeconds:F1} 秒"
                : "—";
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
            btnSettings.Enabled    = enabled;
            cmbAction.Enabled      = enabled;
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
                    _notifyIcon.BalloonTipTitle = $"事件觸發：{pendingEvent.Name}";
                    _notifyIcon.BalloonTipText = string.IsNullOrWhiteSpace(pendingEvent.Description)
                        ? "請切換至 MyDoujin Bot 視窗選擇應對選項。"
                        : pendingEvent.Description;
                    _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                    _notifyIcon.ShowBalloonTip(8000);
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
                var pnlContent = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    Padding = new Padding(pad, 14, pad + SystemInformation.VerticalScrollBarWidth + 2, 10),
                    BackColor = Color.FromArgb(20, 20, 28)
                };
                pnlEventOverlay.Controls.Add(pnlHeader);
                pnlEventOverlay.Controls.Add(pnlContent);
                pnlHeader.SendToBack();
                pnlContent.BringToFront();

                var logPanel = pnlEventOverlay.Parent as Panel;
                int logClientW = logPanel?.ClientSize.Width ?? 500;
                int logPadH    = logPanel?.Padding.Horizontal ?? 8;
                int innerW = Math.Max(200, logClientW - logPadH - pad * 2 - SystemInformation.VerticalScrollBarWidth - 2);

                int y = 14;

                // ── 標題：成功 / 失敗 ──
                string resultTitle = er.IsSuccess ? "成功" : "失敗";
                Color titleColor = er.IsSuccess 
                    ? Color.FromArgb(80, 220, 120) 
                    : Color.FromArgb(240, 80, 80);

                var lblTitle = new Label
                {
                    Text = resultTitle,
                    Location = new Point(pad, y),
                    Width = innerW,
                    Font = new Font("Microsoft JhengHei UI", 15f, FontStyle.Bold),
                    ForeColor = titleColor,
                    AutoSize = false,
                    Height = 34
                };
                pnlContent.Controls.Add(lblTitle);
                y += 38;

                // ── 判定結果區塊 ──
                if (!string.IsNullOrEmpty(er.Stat) || er.Roll.HasValue)
                {
                    var pnlCheck = MyDoujinBot.Utilities.EventUiHelper.CreateCheckResultPanel(er, innerW);
                    pnlCheck.Location = new Point(pad, y);
                    pnlContent.Controls.Add(pnlCheck);
                    y += pnlCheck.Height + 14;
                }

                // ── 故事內文 ──
                if (!string.IsNullOrWhiteSpace(er.Text))
                {
                    var lblStory = new Label
                    {
                        Text = er.Text,
                        Location = new Point(pad, y),
                        MaximumSize = new Size(innerW, 0),
                        AutoSize = true,
                        ForeColor = Color.FromArgb(220, 220, 235),
                        Font = new Font("Microsoft JhengHei UI", 10.5f)
                    };
                    pnlContent.Controls.Add(lblStory);
                    int prefH = lblStory.GetPreferredSize(new Size(innerW, 0)).Height;
                    lblStory.Size = new Size(innerW, Math.Max(prefH, 20));
                    y += lblStory.Height + 14;
                }

                // ── 戰鬥結果 ──
                if (er.BattleResult != null)
                {
                    var lblBattle = new Label
                    {
                        Text = $"[戰鬥結果] {er.BattleResult}",
                        Location = new Point(pad, y),
                        MaximumSize = new Size(innerW, 0),
                        AutoSize = true,
                        ForeColor = Color.FromArgb(255, 180, 100),
                        Font = new Font("Microsoft JhengHei UI", 9.5f)
                    };
                    pnlContent.Controls.Add(lblBattle);
                    int prefH = lblBattle.GetPreferredSize(new Size(innerW, 0)).Height;
                    lblBattle.Size = new Size(innerW, Math.Max(prefH, 20));
                    y += lblBattle.Height + 14;
                }

                // ── 獲得獎勵 ──
                var bonusParts = MyDoujinBot.Utilities.EventUiHelper.BuildBonusStatsParts(er.Rewards?.BonusStats);
                if (bonusParts.Count > 0)
                {
                    var lblReward = new Label
                    {
                        Text = $"獲得獎勵：{string.Join("、", bonusParts)}",
                        Location = new Point(0, y),
                        Width = innerW,
                        AutoSize = true,
                        ForeColor = Color.FromArgb(255, 220, 80),
                        Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold)
                    };
                    pnlContent.Controls.Add(lblReward);
                    y += lblReward.Height + 16;
                }

                // ── 關閉按鈕 ──
                var btnClose = new Button
                {
                    Text = "關閉",
                    Location = new Point(pad + (innerW - 120) / 2, y),
                    Width = 120,
                    Height = 36,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(60, 60, 80),
                    ForeColor = Color.White,
                    Font = new Font("Microsoft JhengHei UI", 10f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnClose.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 130);
                btnClose.Click += (_, _) =>
                {
                    reg.Dispose();
                    HideEventOverlay();
                    tcs.TrySetResult(true);
                };
                pnlContent.Controls.Add(btnClose);
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

            // ── 內容區（可捲動，容納大量選項）──
            // AutoScroll = true：選項過多時顯示捲軸
            var pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                // 右側 padding 加入捐軸寬度，防止内容溱出時出現水平捐軸
                Padding = new Padding(pad, 18, pad + SystemInformation.VerticalScrollBarWidth + 2, 10),
                BackColor = Color.FromArgb(20, 20, 28)
            };
            pnlEventOverlay.Controls.Add(pnlHeader);
            pnlEventOverlay.Controls.Add(pnlContent);
            pnlHeader.SendToBack();
            pnlContent.BringToFront();

            int y = 14;

            // 寬度計算說明：
            // pnlEventOverlay 在 Visible=false 時，ClientSize.Width 可能為 0（WinForms 不對隱藏控制項進行 layout）。
            // 改從父容器 pnlLog（永遠可見）取寬度，再減去 padding 與捐軸預留。
            var logPanel = pnlEventOverlay.Parent as Panel;
            int logClientW = logPanel?.ClientSize.Width ?? 500;
            int logPadH    = logPanel?.Padding.Horizontal ?? 8; // pnlLog 左右 padding 各 4px
            // innerW = 可用寬 − 内容 padding 左右 − 捐軸寬度預留
            int innerW = Math.Max(200,
                logClientW - logPadH - pad * 2 - SystemInformation.VerticalScrollBarWidth - 2);

            // ── 事件名稱 ──
            var lblName = new Label
            {
                Text = pe.Name,
                Location = new Point(pad, y),
                Width = innerW,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Microsoft JhengHei UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 215, 80),
                AutoSize = false,
                Height = 30
            };
            pnlContent.Controls.Add(lblName);
            y += 34;

            // ── 事件描述 ──
            if (!string.IsNullOrWhiteSpace(pe.Description))
            {
                var lblDesc = new Label
                {
                    Text = pe.Description,
                    Location = new Point(pad, y),
                    MaximumSize = new Size(innerW, 0),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(195, 195, 215),
                    Font = new Font("Microsoft JhengHei UI", 10.5f)
                };
                pnlContent.Controls.Add(lblDesc);
                int descH = lblDesc.GetPreferredSize(new Size(innerW, 0)).Height;
                lblDesc.Size = new Size(innerW, Math.Max(descH, 22));
                y += lblDesc.Height + 16; // 拉高與分隔線的間距
            }

            // ── 分隔線 ──
            pnlContent.Controls.Add(new Label
            {
                Location = new Point(pad, y),
                Width = innerW,
                Height = 1,
                BackColor = Color.FromArgb(70, 60, 100),
                AutoSize = false,
                Text = ""
            });
            y += 10;

            // ── 選項標題 ──
            pnlContent.Controls.Add(new Label
            {
                Text = "請選擇一個選項：",
                Location = new Point(pad, y),
                ForeColor = Color.FromArgb(150, 150, 195),
                Font = new Font("Microsoft JhengHei UI", 9f),
                AutoSize = true
            });
            y += 26;

            // ── 選項按鈕（與 EventSelectionForm 同設計風格）──
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
                    Location = new Point(pad, y),
                    Width = innerW,
                    Height = 46,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = btnBack,
                    Cursor = Cursors.Hand,
                    Tag = option.Id,
                    Text = ""
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
                y += 52;
            }

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
