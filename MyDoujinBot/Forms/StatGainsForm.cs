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
    public class StatGainsForm : Form
    {
        private static readonly (string Key, string Name)[] StandardStats = new[]
        {
            ("hp", "HP"),
            ("atk", "攻擊"),
            ("def", "防禦"),
            ("sta", "體力"),
            ("agi", "敏捷"),
            ("spd", "反應速度"),
            ("tec", "技巧"),
            ("int", "智力"),
            ("luk", "幸運")
        };

        private readonly LoopStats _stats;
        private readonly string _mode;
        private readonly string _elapsedTime;

        private Button _btnTabTotal = null!;
        private Button _btnTabStages = null!;
        private Panel _pnlTotalView = null!;
        private Panel _pnlStageView = null!;

        private DataGridView _gridTotal = null!;
        private DataGridView _gridStage = null!;
        private ComboBox _cmbStages = null!;
        private Panel _pnlStageContent = null!;
        private Label _lblNoStageData = null!;
        private readonly bool _isChadoBattle;
        private Label? _lblHeaderLevel;
        private Label _lblStageLevel = null!;
        private FlowLayoutPanel _pnlStageChado = null!;
        private Label _lblStageChado = null!;

        public StatGainsForm(LoopStats stats, string mode, string elapsedTime = "00:00:00", bool isChadoBattle = false)
        {
            _stats = stats ?? new LoopStats();
            _mode = mode ?? string.Empty;
            _elapsedTime = string.IsNullOrWhiteSpace(elapsedTime) ? "00:00:00" : elapsedTime;
            _isChadoBattle = isChadoBattle;

            this.Text = "行動詳情";
            this.Size = new Size(860, 680);
            this.MinimumSize = new Size(760, 570);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(24, 24, 32);
            this.ForeColor = Color.FromArgb(220, 220, 230);
            this.Font = new Font("Microsoft JhengHei UI", 10.5f);
            this.ShowIcon = false;
            this.MinimizeBox = false;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            BuildUI();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try
            {
                var wa = Screen.FromControl(this)?.WorkingArea ?? Screen.PrimaryScreen?.WorkingArea;
                if (wa != null)
                {
                    if (this.Width > wa.Value.Width) this.Width = Math.Max(300, wa.Value.Width - 40);
                    if (this.Height > wa.Value.Height) this.Height = Math.Max(250, wa.Value.Height - 40);
                    this.CenterToParent();
                }
            }
            catch { }
        }

        private void BuildUI()
        {
            // 頂層使用 TableLayoutPanel，徹底避免 Control.Dock 與 BringToFront() 造成的覆蓋跑版問題
            var rootTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.FromArgb(24, 24, 32),
                Padding = new Padding(14, 12, 14, 12),
                Margin = Padding.Empty
            };
            rootTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // Row 0: 上方數據總覽區
            rootTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // Row 1: 深色分頁標籤列
            rootTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // Row 2: 表格內容展示區
            rootTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // Row 3: 底部提示與關閉按鈕
            this.Controls.Add(rootTable);

            // ── Row 0: 上方數據總覽區（依據行動模式顯示不同內容） ──
            var pnlTopBadges = BuildHeaderSummary();
            rootTable.Controls.Add(pnlTopBadges, 0, 0);

            // ── Row 1: 深色分頁切換列 ──
            var pnlTabBar = BuildTabBar();
            rootTable.Controls.Add(pnlTabBar, 0, 1);

            // ── Row 2: 表格內容容器 ──
            var pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 28),
                Margin = new Padding(0, 0, 0, 6),
                Padding = Padding.Empty
            };
            rootTable.Controls.Add(pnlContent, 0, 2);

            // View 1: 總計能力點視圖
            _pnlTotalView = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 28),
                Padding = Padding.Empty
            };
            _gridTotal = CreateStyledGrid();
            _pnlTotalView.Controls.Add(_gridTotal);
            pnlContent.Controls.Add(_pnlTotalView);

            // View 2: 排程各階段細項視圖
            _pnlStageView = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 28),
                Padding = Padding.Empty,
                Visible = false
            };
            BuildStagesContent(_pnlStageView);
            pnlContent.Controls.Add(_pnlStageView);

            // ── Row 3: 底部按鈕區 ──
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 38,
                Margin = new Padding(0, 4, 0, 0)
            };
            var lblHint = new Label
            {
                Text = "提示：能力點數於每次執行開始時自動重新累計。",
                Dock = DockStyle.Left,
                AutoSize = true,
                ForeColor = Color.FromArgb(140, 140, 160),
                Font = new Font("Microsoft JhengHei UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlBottom.Controls.Add(lblHint);

            var btnClose = new Button
            {
                Text = "關閉",
                Dock = DockStyle.Right,
                Width = 95,
                BackColor = Color.FromArgb(48, 52, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 10f)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();
            pnlBottom.Controls.Add(btnClose);
            rootTable.Controls.Add(pnlBottom, 0, 3);

            // 填入總計資料
            var totalStats = _stats.CurrentStats ?? UserContext.Current.ActiveCharacter?.Stats;
            PopulateGrid(_gridTotal, _stats.StatGains, totalStats);
            SwitchTab(0);
        }

        /// <summary>
        /// 上方數據面板：模式、執行時間、訓練次數 / 戰鬥次數、觸發事件 (依模式不同調整)
        /// </summary>
        private Control BuildHeaderSummary()
        {
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 6),
                Padding = Padding.Empty
            };

            bool isPipeline = string.Equals(_mode, "pipeline", StringComparison.OrdinalIgnoreCase);
            bool isBattle = string.Equals(_mode, "battle", StringComparison.OrdinalIgnoreCase);

            string evtSub = $"{_stats.EventSuccessCount} 成功 / {_stats.EventFailCount} 失敗";
            int currentLvl = _stats.CurrentLevel > 0 ? _stats.CurrentLevel : UserContext.Current.Level;
            string currentLvlText = currentLvl > 0 ? $"Lv. {currentLvl}" : "—";
            var cardLevel = CreateSummaryCard("當前等級", currentLvlText, Color.FromArgb(255, 215, 0), out _lblHeaderLevel);

            if (isPipeline)
            {
                // 行動排程：模式、執行時間、訓練次數、戰鬥次數、觸發事件、當前等級 (6格)
                tbl.ColumnCount = 6;
                for (int i = 0; i < 6; i++)
                    tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 6F));

                string trainSub = $"{_stats.TrainingSuccessCount} 成功 / {_stats.TrainingFailCount} 失敗";
                bool hasChadoInPipeline = _stats.ChadoSuccessCount > 0
                    || AppSettingsManager.Current.PipelineStages.Any(s => s.ActionType == StageActionType.Battle && s.BattleType == BattleType.Chado)
                    || _stats.StageStatGains.Values.Any(st => st.BattleType == BattleType.Chado || st.ChadoSuccessCount > 0);

                Control battleSubControl = hasChadoInPipeline
                    ? CreateChadoSubControl(_stats.BattleWinCount, _stats.ChadoSuccessCount, _stats.BattleLossCount)
                    : CreateTextSubControl($"{_stats.BattleWinCount} 成功 / {_stats.BattleLossCount} 失敗");

                tbl.Controls.Add(CreateSummaryCard("模式", "行動排程", Color.FromArgb(210, 215, 235)), 0, 0);
                tbl.Controls.Add(CreateSummaryCard("執行時間", _elapsedTime, Color.FromArgb(140, 205, 255)), 1, 0);
                tbl.Controls.Add(CreateSummaryCard("訓練次數", $"{_stats.TrainingRunCount} 次", Color.FromArgb(100, 220, 140), CreateTextSubControl(trainSub)), 2, 0);
                tbl.Controls.Add(CreateSummaryCard("戰鬥次數", $"{_stats.BattleRunCount} 次", Color.FromArgb(255, 185, 110), battleSubControl), 3, 0);
                tbl.Controls.Add(CreateSummaryCard("觸發事件", $"{_stats.EventCount} 次", Color.FromArgb(195, 160, 255), CreateTextSubControl(evtSub)), 4, 0);
                tbl.Controls.Add(cardLevel, 5, 0);
            }
            else if (isBattle)
            {
                // 自動戰鬥：模式、執行時間、戰鬥次數、觸發事件、當前等級 (5格)
                tbl.ColumnCount = 5;
                for (int i = 0; i < 5; i++)
                    tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

                bool isChado = _isChadoBattle || _stats.ChadoSuccessCount > 0 || AppSettingsManager.Current.BattleMode == "Chado";
                Control battleSubControl = isChado
                    ? CreateChadoSubControl(_stats.SuccessCount, _stats.ChadoSuccessCount, _stats.FailCount)
                    : CreateTextSubControl($"{_stats.SuccessCount} 成功 / {_stats.FailCount} 失敗");

                string modeSub = isChado ? "我要茶渡你" : "友好切磋";
                Color modeSubColor = isChado ? Color.FromArgb(220, 165, 110) : Color.FromArgb(180, 185, 200);

                tbl.Controls.Add(CreateSummaryCard("模式", "自動戰鬥", Color.FromArgb(210, 215, 235), CreateTextSubControl(modeSub, modeSubColor)), 0, 0);
                tbl.Controls.Add(CreateSummaryCard("執行時間", _elapsedTime, Color.FromArgb(140, 205, 255)), 1, 0);
                tbl.Controls.Add(CreateSummaryCard("戰鬥次數", $"{_stats.RunCount} 次", Color.FromArgb(100, 220, 140), battleSubControl), 2, 0);
                tbl.Controls.Add(CreateSummaryCard("觸發事件", $"{_stats.EventCount} 次", Color.FromArgb(195, 160, 255), CreateTextSubControl(evtSub)), 3, 0);
                tbl.Controls.Add(cardLevel, 4, 0);
            }
            else
            {
                // 自動訓練：模式、執行時間、訓練次數、觸發事件、當前等級 (5格)
                tbl.ColumnCount = 5;
                for (int i = 0; i < 5; i++)
                    tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

                string trainSub = $"{_stats.SuccessCount} 成功 / {_stats.FailCount} 失敗";

                tbl.Controls.Add(CreateSummaryCard("模式", "自動訓練", Color.FromArgb(210, 215, 235)), 0, 0);
                tbl.Controls.Add(CreateSummaryCard("執行時間", _elapsedTime, Color.FromArgb(140, 205, 255)), 1, 0);
                tbl.Controls.Add(CreateSummaryCard("訓練次數", $"{_stats.RunCount} 次", Color.FromArgb(100, 220, 140), CreateTextSubControl(trainSub)), 2, 0);
                tbl.Controls.Add(CreateSummaryCard("觸發事件", $"{_stats.EventCount} 次", Color.FromArgb(195, 160, 255), CreateTextSubControl(evtSub)), 3, 0);
                tbl.Controls.Add(cardLevel, 4, 0);
            }

            return tbl;
        }

        private Control CreateTextSubControl(string text, Color? color = null)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Bottom,
                Height = 22,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                ForeColor = color ?? Color.FromArgb(180, 185, 200),
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        /// <summary>
        /// 建立包含 FontAwesome Poo 圖示的 成功 / 💩 / 失敗 統計副標題控制項
        /// </summary>
        private Control CreateChadoSubControl(int successCount, int chadoCount, int failCount)
        {
            var pnlContainer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BackColor = Color.Transparent
            };

            var flow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            var lblSuccess = new Label
            {
                Text = $"{successCount} 成功",
                ForeColor = Color.FromArgb(80, 220, 120),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0)
            };
            var lblSlash1 = new Label
            {
                Text = " / ",
                ForeColor = Color.FromArgb(140, 140, 160),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0)
            };
            var picPoo = new IconPictureBox
            {
                IconChar = IconChar.Poo,
                IconColor = Color.FromArgb(200, 155, 106),
                IconSize = 14,
                Size = new Size(16, 16),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 3, 0, 0)
            };
            var lblChado = new Label
            {
                Text = $" {chadoCount}",
                ForeColor = Color.FromArgb(200, 155, 106),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0)
            };
            var lblSlash2 = new Label
            {
                Text = " / ",
                ForeColor = Color.FromArgb(140, 140, 160),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0)
            };
            var lblFail = new Label
            {
                Text = $"{failCount} 失敗",
                ForeColor = Color.FromArgb(240, 95, 95),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0)
            };

            flow.Controls.Add(lblSuccess);
            flow.Controls.Add(lblSlash1);
            flow.Controls.Add(picPoo);
            flow.Controls.Add(lblChado);
            flow.Controls.Add(lblSlash2);
            flow.Controls.Add(lblFail);

            pnlContainer.Controls.Add(flow);
            void CenterFlow()
            {
                flow.Location = new Point(
                    Math.Max(0, (pnlContainer.ClientSize.Width - flow.PreferredSize.Width) / 2),
                    Math.Max(0, (pnlContainer.ClientSize.Height - flow.PreferredSize.Height) / 2));
            }
            pnlContainer.Resize += (s, e) => CenterFlow();
            CenterFlow();

            return pnlContainer;
        }

        private Panel CreateSummaryCard(string title, string mainValue, Color mainColor, Control? subControl = null)
        {
            return CreateSummaryCard(title, mainValue, mainColor, out _, subControl);
        }

        private Panel CreateSummaryCard(string title, string mainValue, Color mainColor, out Label valueLabel, Control? subControl = null)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 76,
                BackColor = Color.FromArgb(32, 34, 46),
                Margin = new Padding(3, 0, 3, 0),
                Padding = new Padding(6, 6, 6, 6)
            };

            var lblTitle = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 18,
                Font = new Font("Microsoft JhengHei UI", 9f),
                ForeColor = Color.FromArgb(160, 160, 180),
                TextAlign = ContentAlignment.MiddleCenter
            };
            card.Controls.Add(lblTitle);

            if (subControl != null)
            {
                card.Controls.Add(subControl);
            }

            var lblVal = new Label
            {
                Text = mainValue,
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft JhengHei UI", 13.5f, FontStyle.Bold),
                ForeColor = mainColor,
                TextAlign = ContentAlignment.MiddleCenter
            };
            card.Controls.Add(lblVal);
            valueLabel = lblVal;

            return card;
        }

        /// <summary>
        /// 建立深色客製分頁標籤（取代原生的白色 TabControl）
        /// </summary>
        private Control BuildTabBar()
        {
            var pnl = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 6, 0, 6),
                Padding = Padding.Empty
            };

            _btnTabTotal = new Button
            {
                Text = "總計能力點",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 10.5f),
                Padding = new Padding(16, 5, 16, 5),
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnTabTotal.FlatAppearance.BorderSize = 0;
            _btnTabTotal.Click += (s, e) => SwitchTab(0);
            pnl.Controls.Add(_btnTabTotal);

            _btnTabStages = new Button
            {
                Text = "排程各階段細項",
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 10.5f),
                Padding = new Padding(16, 5, 16, 5),
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnTabStages.FlatAppearance.BorderSize = 0;
            _btnTabStages.Click += (s, e) => SwitchTab(1);
            pnl.Controls.Add(_btnTabStages);

            // 若非排程模式且無階段資料，隱藏階段標籤以維持介面簡潔
            bool hasStageData = _stats.StageStatGains != null && _stats.StageStatGains.Count > 0;
            bool isPipeline = string.Equals(_mode, "pipeline", StringComparison.OrdinalIgnoreCase);
            if (!isPipeline && !hasStageData)
            {
                _btnTabStages.Visible = false;
            }

            return pnl;
        }

        private void SwitchTab(int tabIndex)
        {
            if (tabIndex == 0)
            {
                _btnTabTotal.BackColor = Color.FromArgb(46, 58, 88);
                _btnTabTotal.ForeColor = Color.FromArgb(140, 205, 255);
                _btnTabTotal.Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold);

                _btnTabStages.BackColor = Color.FromArgb(30, 32, 42);
                _btnTabStages.ForeColor = Color.FromArgb(160, 160, 180);
                _btnTabStages.Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Regular);

                _pnlTotalView.Visible = true;
                _pnlStageView.Visible = false;

                if (_lblHeaderLevel != null)
                {
                    int curLvl = _stats.CurrentLevel > 0 ? _stats.CurrentLevel : UserContext.Current.Level;
                    _lblHeaderLevel.Text = curLvl > 0 ? $"Lv. {curLvl}" : "—";
                }

                _gridTotal.ClearSelection();
                _gridTotal.CurrentCell = null;
            }
            else
            {
                _btnTabStages.BackColor = Color.FromArgb(46, 58, 88);
                _btnTabStages.ForeColor = Color.FromArgb(140, 205, 255);
                _btnTabStages.Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold);

                _btnTabTotal.BackColor = Color.FromArgb(30, 32, 42);
                _btnTabTotal.ForeColor = Color.FromArgb(160, 160, 180);
                _btnTabTotal.Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Regular);

                _pnlTotalView.Visible = false;
                _pnlStageView.Visible = true;

                RefreshStageTab();
                UpdateHeaderLevelForStage();
                _gridStage.ClearSelection();
                _gridStage.CurrentCell = null;
            }
        }

        private void BuildStagesContent(Panel parent)
        {
            _lblNoStageData = new Label
            {
                Text = "目前尚無排程階段資料（執行行動排程時將自動記錄各階段獲得之能力點）。",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(150, 150, 170),
                Font = new Font("Microsoft JhengHei UI", 10.5f),
                Visible = false
            };
            parent.Controls.Add(_lblNoStageData);

            _pnlStageContent = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false
            };
            parent.Controls.Add(_pnlStageContent);

            // 頂部選單
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(0, 4, 0, 6),
                BackColor = Color.Transparent
            };

            var lblSelect = new Label
            {
                Text = "選擇階段：",
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 200, 220),
                Font = new Font("Microsoft JhengHei UI", 10.5f),
                Location = new Point(4, 10)
            };
            pnlTop.Controls.Add(lblSelect);

            _cmbStages = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(42, 42, 56),
                ForeColor = Color.FromArgb(230, 230, 240),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 10f),
                Location = new Point(90, 7),
                Width = 280
            };
            _cmbStages.SelectedIndexChanged += (s, e) => OnSelectedStageChanged();
            pnlTop.Controls.Add(_cmbStages);

            _lblStageLevel = new Label
            {
                Text = "階段等級：Lv. —",
                AutoSize = true,
                ForeColor = Color.FromArgb(255, 215, 0),
                Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold),
                Location = new Point(385, 10),
                Visible = false
            };
            pnlTop.Controls.Add(_lblStageLevel);

            _pnlStageChado = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Location = new Point(540, 8),
                Visible = false
            };

            var picStagePoo = new IconPictureBox
            {
                IconChar = IconChar.Poo,
                IconColor = Color.FromArgb(200, 155, 106),
                IconSize = 16,
                Size = new Size(18, 18),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 3, 0, 0)
            };
            _lblStageChado = new Label
            {
                Text = " 0 次",
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 155, 106),
                Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold),
                Margin = new Padding(0, 1, 0, 0)
            };
            _pnlStageChado.Controls.Add(picStagePoo);
            _pnlStageChado.Controls.Add(_lblStageChado);
            pnlTop.Controls.Add(_pnlStageChado);

            _pnlStageContent.Controls.Add(pnlTop);

            // 階段能力表格
            _gridStage = CreateStyledGrid();
            _pnlStageContent.Controls.Add(_gridStage);
            _gridStage.BringToFront();
        }

        private void RefreshStageTab()
        {
            if (_stats.StageStatGains == null || _stats.StageStatGains.Count == 0)
            {
                _lblNoStageData.Visible = true;
                _pnlStageContent.Visible = false;
                return;
            }

            _lblNoStageData.Visible = false;
            _pnlStageContent.Visible = true;

            _cmbStages.Items.Clear();
            var sortedStages = _stats.StageStatGains.Values.OrderBy(s => s.StageIndex).ToList();
            foreach (var st in sortedStages)
            {
                string itemText = $"第 {st.StageIndex} 階段：{st.StageName}";
                if (!string.IsNullOrWhiteSpace(st.StageSummary))
                {
                    itemText += $" ({st.StageSummary})";
                }
                _cmbStages.Items.Add(itemText);
            }

            if (_cmbStages.Items.Count > 0 && _cmbStages.SelectedIndex < 0)
            {
                _cmbStages.SelectedIndex = 0;
            }
        }

        private void OnSelectedStageChanged()
        {
            int idx = _cmbStages.SelectedIndex;
            if (idx < 0) return;

            var sortedStages = _stats.StageStatGains.Values.OrderBy(s => s.StageIndex).ToList();
            if (idx >= sortedStages.Count) return;

            var stageRecord = sortedStages[idx];
            var stageStats = stageRecord.LastStats ?? _stats.CurrentStats ?? UserContext.Current.ActiveCharacter?.Stats;
            PopulateGrid(_gridStage, stageRecord.Record, stageStats);
            _gridStage.ClearSelection();
            _gridStage.CurrentCell = null;

            int stageLvl = stageRecord.LastLevel > 0 ? stageRecord.LastLevel : (_stats.CurrentLevel > 0 ? _stats.CurrentLevel : UserContext.Current.Level);
            if (stageLvl > 0)
            {
                _lblStageLevel.Text = $"階段等級：Lv. {stageLvl}";
                _lblStageLevel.Visible = true;
            }
            else
            {
                _lblStageLevel.Text = "階段等級：—";
                _lblStageLevel.Visible = true;
            }
            _pnlStageChado.Location = new Point(_lblStageLevel.Right + 14, 8);

            UpdateHeaderLevelForStage();

            bool isStageChado = (stageRecord.ActionType == StageActionType.Battle && stageRecord.BattleType == BattleType.Chado)
                || stageRecord.StageSummary.Contains("茶渡")
                || stageRecord.ChadoSuccessCount > 0;

            if (isStageChado)
            {
                _lblStageChado.Text = $" {stageRecord.ChadoSuccessCount} 次";
                _pnlStageChado.Visible = true;
            }
            else
            {
                _pnlStageChado.Visible = false;
            }
        }

        private void UpdateHeaderLevelForStage()
        {
            if (_lblHeaderLevel == null || !_pnlStageView.Visible) return;
            int idx = _cmbStages.SelectedIndex;
            if (idx >= 0 && _stats.StageStatGains != null)
            {
                var sortedStages = _stats.StageStatGains.Values.OrderBy(s => s.StageIndex).ToList();
                if (idx < sortedStages.Count)
                {
                    int stLvl = sortedStages[idx].LastLevel;
                    if (stLvl <= 0) stLvl = _stats.CurrentLevel > 0 ? _stats.CurrentLevel : UserContext.Current.Level;
                    _lblHeaderLevel.Text = stLvl > 0 ? $"Lv. {stLvl}" : "—";
                }
            }
        }

        /// <summary>
        /// 建立具深色質感且標題與欄位不可點選、無藍色焦點框、數值靠右且支援列 Hover 的唯讀資料表格
        /// </summary>
        private DataGridView CreateStyledGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(20, 20, 28),
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(40, 42, 56),
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                EnableHeadersVisualStyles = false,
                AutoGenerateColumns = false,
                Font = new Font("Microsoft JhengHei UI", 11f),
                RowTemplate = { Height = 34 }
            };

            // 啟用雙緩衝避免 hover 時閃爍
            try
            {
                typeof(DataGridView).InvokeMember("DoubleBuffered",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                    null, grid, new object[] { true });
            }
            catch { }

            // 資料單元格預設樣式
            grid.DefaultCellStyle.BackColor = Color.FromArgb(24, 25, 34);
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(220, 220, 235);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(36, 40, 56);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 11f);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(28, 29, 40);

            // 欄位標題樣式（不可點擊選取、選取時維持深色背景，不會變為刺眼藍色）
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(32, 35, 48);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(210, 215, 235);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(32, 35, 48);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(210, 215, 235);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft JhengHei UI", 11f, FontStyle.Bold);
            grid.ColumnHeadersHeight = 40;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            var colAttr = new DataGridViewTextBoxColumn
            {
                HeaderText = "屬性名稱",
                Width = 160,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colAttr.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            colAttr.DefaultCellStyle.Padding = new Padding(12, 0, 0, 0);
            colAttr.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
            colAttr.HeaderCell.Style.Padding = new Padding(12, 0, 0, 0);

            var colLvl = new DataGridViewTextBoxColumn
            {
                HeaderText = "升級獲得",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colLvl.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colLvl.DefaultCellStyle.Padding = new Padding(0, 0, 24, 0);
            colLvl.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            colLvl.HeaderCell.Style.Padding = new Padding(0, 0, 24, 0);

            var colEvt = new DataGridViewTextBoxColumn
            {
                HeaderText = "事件獲得",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colEvt.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colEvt.DefaultCellStyle.Padding = new Padding(0, 0, 24, 0);
            colEvt.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            colEvt.HeaderCell.Style.Padding = new Padding(0, 0, 24, 0);

            var colTotal = new DataGridViewTextBoxColumn
            {
                HeaderText = "總計獲得",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colTotal.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colTotal.DefaultCellStyle.Padding = new Padding(0, 0, 24, 0);
            colTotal.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            colTotal.HeaderCell.Style.Padding = new Padding(0, 0, 24, 0);

            var colCurrent = new DataGridViewTextBoxColumn
            {
                HeaderText = "當前數值",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            colCurrent.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colCurrent.DefaultCellStyle.Padding = new Padding(0, 0, 24, 0);
            colCurrent.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            colCurrent.HeaderCell.Style.Padding = new Padding(0, 0, 24, 0);

            grid.Columns.AddRange(colAttr, colLvl, colEvt, colTotal, colCurrent);

            // 消除格內虛線焦點框與防止誤點選
            grid.CellPainting += (s, e) =>
            {
                if (e.RowIndex >= 0 && (e.PaintParts & DataGridViewPaintParts.Focus) != 0)
                {
                    e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.Focus);
                    e.Handled = true;
                }
            };
            grid.SelectionChanged += (s, e) =>
            {
                if (grid.Focused || grid.SelectedCells.Count > 0)
                {
                    grid.ClearSelection();
                }
            };

            // 滑鼠滑過列 Hover 效果
            int hoverRowIndex = -1;

            void SetRowHoverState(int rowIndex, bool hover)
            {
                if (rowIndex < 0 || rowIndex >= grid.RowCount) return;
                if (hover)
                {
                    grid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(40, 46, 66);
                }
                else
                {
                    grid.Rows[rowIndex].DefaultCellStyle.BackColor =
                        (rowIndex % 2 == 0 ? Color.FromArgb(24, 25, 34) : Color.FromArgb(28, 29, 40));
                }
            }

            grid.CellMouseEnter += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex != hoverRowIndex)
                {
                    if (hoverRowIndex >= 0) SetRowHoverState(hoverRowIndex, false);
                    hoverRowIndex = e.RowIndex;
                    SetRowHoverState(hoverRowIndex, true);
                }
            };

            grid.CellMouseLeave += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex == hoverRowIndex)
                {
                    SetRowHoverState(hoverRowIndex, false);
                    hoverRowIndex = -1;
                }
            };

            grid.MouseLeave += (s, e) =>
            {
                if (hoverRowIndex >= 0)
                {
                    SetRowHoverState(hoverRowIndex, false);
                    hoverRowIndex = -1;
                }
            };

            return grid;
        }

        private void PopulateGrid(DataGridView grid, StatPointsRecord record, CharacterStats? currentStats)
        {
            grid.Rows.Clear();

            var allKeys = new HashSet<string>(StandardStats.Select(s => s.Key), StringComparer.OrdinalIgnoreCase);
            foreach (var k in record.LevelUp.Stats.Keys) allKeys.Add(k);
            foreach (var k in record.Event.Stats.Keys) allKeys.Add(k);

            var orderedKeys = allKeys
                .OrderBy(k =>
                {
                    int idx = Array.FindIndex(StandardStats, s => string.Equals(s.Key, k, StringComparison.OrdinalIgnoreCase));
                    return idx >= 0 ? idx : 100;
                })
                .ThenBy(k => k)
                .ToList();

            foreach (var key in orderedKeys)
            {
                string displayName = StatHelper.GetDisplayName(key);
                int lvl = record.LevelUp.GetStat(key);
                int evt = record.Event.GetStat(key);
                int tot = lvl + evt;
                int cur = currentStats?.GetStat(key) ?? 0;
                string curText = currentStats != null ? cur.ToString() : (cur > 0 ? cur.ToString() : "—");

                int rowIdx = grid.Rows.Add(
                    displayName,
                    lvl > 0 ? $"+{lvl}" : "0",
                    evt > 0 ? $"+{evt}" : "0",
                    tot > 0 ? $"+{tot}" : "0",
                    curText
                );

                var row = grid.Rows[rowIdx];
                row.Cells[0].Style.Font = new Font("Microsoft JhengHei UI", 11f);

                // Col 1: 升級獲得
                row.Cells[1].Style.ForeColor = lvl > 0 ? Color.FromArgb(100, 220, 140) : Color.FromArgb(110, 110, 130);
                row.Cells[1].Style.Font = lvl > 0 ? new Font("Microsoft JhengHei UI", 11f, FontStyle.Bold) : new Font("Microsoft JhengHei UI", 11f);

                // Col 2: 事件獲得
                row.Cells[2].Style.ForeColor = evt > 0 ? Color.FromArgb(210, 170, 255) : Color.FromArgb(110, 110, 130);
                row.Cells[2].Style.Font = evt > 0 ? new Font("Microsoft JhengHei UI", 11f, FontStyle.Bold) : new Font("Microsoft JhengHei UI", 11f);

                // Col 3: 總計獲得
                if (tot == 0)
                {
                    row.Cells[3].Style.ForeColor = Color.FromArgb(110, 110, 130);
                    row.Cells[3].Style.Font = new Font("Microsoft JhengHei UI", 11f);
                }
                else
                {
                    row.Cells[3].Style.ForeColor = Color.FromArgb(140, 210, 255);
                    row.Cells[3].Style.Font = new Font("Microsoft JhengHei UI", 11.5f, FontStyle.Bold);
                }

                // Col 4: 當前數值
                if (cur == 0 && currentStats == null)
                {
                    row.Cells[4].Style.ForeColor = Color.FromArgb(110, 110, 130);
                    row.Cells[4].Style.Font = new Font("Microsoft JhengHei UI", 11f);
                }
                else
                {
                    row.Cells[4].Style.ForeColor = Color.FromArgb(245, 245, 255);
                    row.Cells[4].Style.Font = new Font("Microsoft JhengHei UI", 11.5f, FontStyle.Bold);
                }
            }

            grid.ClearSelection();
            grid.CurrentCell = null;
        }
    }
}
