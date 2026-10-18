using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using FontAwesome.Sharp;
using Icon = System.Drawing.Icon;
using MyDoujinBot.Models;
using MyDoujinBot.Services;
using MyDoujinBot.Utilities;

namespace MyDoujinBot.Forms
{
    /// <summary>
    /// 排程階段獨立編輯與新增對話框。
    /// 提供乾淨寬敞的編輯空間、自訂訓練循環清單與對手驗證機制。
    /// </summary>
    public class PipelineStageEditForm : Form
    {
        private readonly int _stageNumber;
        private readonly bool _isNew;
        private readonly string _currentToken;
        private readonly BattleService _battleService;

        private string _verifiedOpponentName = string.Empty;
        private string _verifiedOpponentDetail = string.Empty;

        // UI Controls
        private ComboBox _cmbActionType = null!;
        private TableLayoutPanel _pnlTraining = null!;
        private ComboBox _cmbTrainAction = null!;
        private Button _btnAddTrainAction = null!;
        private Button _btnClearTrainSequence = null!;
        private PillFlowLayoutPanel _pnlTrainingPills = null!;
        private Panel _pnlActionSection = null!;
        private readonly List<TrainingAction> _trainingSequenceList = new();

        private Panel _pnlBattle = null!;
        private ComboBox _cmbBattleType = null!;
        private TextBox _txtBattleTarget = null!;
        private Button _btnVerify = null!;
        private Label _lblBattleFeedback = null!;
        private ComboBox _cmbConditionType = null!;
        private NumericUpDown _nudConditionVal = null!;
        private Label _lblConditionUnit = null!;

        private Button _btnSave = null!;
        private Button _btnCancel = null!;
        private ToolTip _toolTip = null!;

        /// <summary>編輯或新增完成後的排程階段物件</summary>
        public PipelineStage Stage { get; private set; }

        public PipelineStageEditForm(PipelineStage stage, int stageNumber, bool isNew, string currentToken, BattleService battleService)
        {
            _stageNumber = stageNumber;
            _isNew = isNew;
            _currentToken = currentToken;
            _battleService = battleService;
            Stage = stage.Clone();

            // 載入自訂訓練清單 (僅在明確有設定自訂清單時載入，不預設塞入單項內容)
            if (Stage.TrainingActionSequence != null && Stage.TrainingActionSequence.Count > 0)
            {
                foreach (var actId in Stage.TrainingActionSequence)
                {
                    var act = TrainingActions.All.FirstOrDefault(a => a.ActionId == actId);
                    if (act != null) _trainingSequenceList.Add(act);
                }
            }

            if (Stage.ActionType == StageActionType.Battle)
            {
                _verifiedOpponentName = Stage.BattleTargetName;
                _verifiedOpponentDetail = Stage.BattleTargetDetail;
            }

            this.Text = _isNew ? $"新增階段 - #{_stageNumber}" : $"編輯階段 - #{_stageNumber}";
            this.Size = new Size(580, 480);
            this.MinimumSize = new Size(500, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(24, 24, 32);
            this.ForeColor = Color.FromArgb(220, 220, 230);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            _toolTip = new ToolTip
            {
                BackColor = Color.FromArgb(40, 42, 56),
                ForeColor = Color.FromArgb(235, 235, 245),
                OwnerDraw = true
            };
            _toolTip.Draw += (s, e) =>
            {
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(40, 42, 56)), e.Bounds);
                e.Graphics.DrawRectangle(new Pen(Color.FromArgb(70, 75, 100)), new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1));
                TextRenderer.DrawText(e.Graphics, e.ToolTipText, this.Font, e.Bounds, Color.FromArgb(235, 235, 245), TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            };

            BuildUI();
            UpdateSaveButtonState();
        }

        private int ScaleDpi(int value)
        {
            return (int)Math.Round(value * (this.DeviceDpi / 96F));
        }

        private void BuildUI()
        {
            var tblRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(ScaleDpi(16), ScaleDpi(12), ScaleDpi(16), ScaleDpi(12)),
                BackColor = Color.Transparent
            };
            tblRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.Controls.Add(tblRoot);

            // =================================================================
            // Row 0: 頂部標頭與說明
            // =================================================================
            var pnlHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };
            pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var lblHeaderTitle = new Label
            {
                Text = _isNew ? $"新增階段 (#{_stageNumber})" : $"編輯階段 (#{_stageNumber})",
                ForeColor = _isNew ? Color.FromArgb(130, 190, 255) : Color.FromArgb(255, 215, 80),
                Font = new Font("Microsoft JhengHei UI", 11.5f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(2))
            };

            var lblHeaderDesc = new Label
            {
                Text = "設定此階段欲執行的行動與達成切換條件，達標後將自動切換至下一階段。",
                ForeColor = Color.FromArgb(155, 160, 180),
                Font = new Font("Microsoft JhengHei UI", 9f),
                AutoSize = true,
                Margin = Padding.Empty
            };

            pnlHeader.Controls.Add(lblHeaderTitle, 0, 0);
            pnlHeader.Controls.Add(lblHeaderDesc, 0, 1);
            tblRoot.Controls.Add(pnlHeader, 0, 0);

            // =================================================================
            // Row 1: 中央滾動編輯區
            // =================================================================
            var pnlBody = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };

            var tblBody = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 0,
                AutoSize = true,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, ScaleDpi(4), 0)
            };
            tblBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlBody.Controls.Add(tblBody);
            tblRoot.Controls.Add(pnlBody, 0, 1);

            void AddSection(Control c)
            {
                tblBody.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                tblBody.Controls.Add(c, 0, tblBody.RowCount++);
            }

            // ─────────────────────────────────────────────────────────────
            // 區塊 1: 執行行動設定
            // ─────────────────────────────────────────────────────────────
            _pnlActionSection = CreateCardPanel();
            var tblAction = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent
            };
            tblAction.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblAction.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblAction.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblAction.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // 行動模式選擇列 (無 icon，純中文：自動訓練 / 自動戰鬥)
            var pnlTypeRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };

            var lblTypePrompt = new Label
            {
                Text = "行動模式：",
                ForeColor = Color.FromArgb(210, 215, 235),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(3), ScaleDpi(8), 0)
            };

            _cmbActionType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ScaleDpi(140),
                BackColor = Color.FromArgb(44, 48, 64),
                ForeColor = Color.FromArgb(235, 235, 245),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Margin = Padding.Empty
            };
            _cmbActionType.Items.Add("自動訓練");
            _cmbActionType.Items.Add("自動戰鬥");
            _cmbActionType.SelectedIndex = Stage.ActionType == StageActionType.Training ? 0 : 1;
            _cmbActionType.SelectedIndexChanged += (s, e) =>
            {
                bool isTrain = _cmbActionType.SelectedIndex == 0;
                _pnlTraining.Visible = isTrain;
                _pnlBattle.Visible = !isTrain;
                _pnlActionSection?.PerformLayout();
                this.PerformLayout();
                UpdateSaveButtonState();
            };

            pnlTypeRow.Controls.Add(lblTypePrompt);
            pnlTypeRow.Controls.Add(_cmbActionType);
            tblAction.Controls.Add(pnlTypeRow, 0, 0);

            // --- 訓練子面板 ---
            _pnlTraining = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Visible = Stage.ActionType == StageActionType.Training,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _pnlTraining.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _pnlTraining.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _pnlTraining.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var pnlTrainSelectRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, ScaleDpi(6))
            };

            var lblTrainActionPrompt = new Label
            {
                Text = "選擇訓練：",
                ForeColor = Color.FromArgb(180, 185, 205),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(5), ScaleDpi(6), 0)
            };

            _cmbTrainAction = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ScaleDpi(150),
                BackColor = Color.FromArgb(44, 48, 64),
                ForeColor = Color.FromArgb(235, 235, 245),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Margin = new Padding(0, 0, ScaleDpi(8), 0)
            };
            foreach (var act in TrainingActions.All)
            {
                _cmbTrainAction.Items.Add(act);
            }
            int selectedTrainIdx = 0;
            for (int i = 0; i < _cmbTrainAction.Items.Count; i++)
            {
                if (_cmbTrainAction.Items[i] is TrainingAction act && act.ActionId == Stage.TrainingActionId)
                {
                    selectedTrainIdx = i;
                    break;
                }
            }
            if (_cmbTrainAction.Items.Count > 0) _cmbTrainAction.SelectedIndex = selectedTrainIdx;

            _btnAddTrainAction = new IconButton
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
                ImageAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, ScaleDpi(6), 0)
            };
            _btnAddTrainAction.FlatAppearance.BorderSize = 0;
            _btnAddTrainAction.Click += (s, e) =>
            {
                if (_cmbTrainAction.SelectedItem is TrainingAction action)
                {
                    _trainingSequenceList.Add(action);
                    RefreshTrainingPills();
                    UpdateSaveButtonState();
                }
            };

            _btnClearTrainSequence = new IconButton
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
                ImageAlign = ContentAlignment.MiddleLeft,
                Margin = Padding.Empty
            };
            _btnClearTrainSequence.FlatAppearance.BorderSize = 0;
            _btnClearTrainSequence.Click += (s, e) =>
            {
                _trainingSequenceList.Clear();
                RefreshTrainingPills();
                UpdateSaveButtonState();
            };

            pnlTrainSelectRow.Controls.Add(lblTrainActionPrompt);
            pnlTrainSelectRow.Controls.Add(_cmbTrainAction);
            pnlTrainSelectRow.Controls.Add(_btnAddTrainAction);
            pnlTrainSelectRow.Controls.Add(_btnClearTrainSequence);
            _pnlTraining.Controls.Add(pnlTrainSelectRow, 0, 0);

            // 循環訓練項目橫列 (自適應寬度之 Pill Badge 容器，防止 TableLayoutPanel 誤算行高導致下方空白)
            _pnlTrainingPills = new PillFlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Color.FromArgb(20, 20, 26),
                Padding = new Padding(ScaleDpi(8), ScaleDpi(6), ScaleDpi(8), ScaleDpi(6)),
                Margin = new Padding(0, 0, 0, ScaleDpi(4)),
                Visible = true
            };
            _pnlTraining.Controls.Add(_pnlTrainingPills, 0, 1);
            RefreshTrainingPills();

            tblAction.Controls.Add(_pnlTraining, 0, 1);

            // --- 戰鬥子面板 ---
            _pnlBattle = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = Color.Transparent,
                Visible = Stage.ActionType == StageActionType.Battle,
                Margin = Padding.Empty
            };
            var tblBattle = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 3,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            tblBattle.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblBattle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var lblBattleTypePrompt = new Label
            {
                Text = "戰鬥類型：",
                ForeColor = Color.FromArgb(180, 185, 205),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(3), ScaleDpi(8), ScaleDpi(8))
            };

            _cmbBattleType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ScaleDpi(160),
                BackColor = Color.FromArgb(44, 48, 64),
                ForeColor = Color.FromArgb(235, 235, 245),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Margin = new Padding(0, 0, 0, ScaleDpi(8))
            };
            _cmbBattleType.Items.Add("友好切磋");
            _cmbBattleType.Items.Add("我要茶渡你");
            _cmbBattleType.SelectedIndex = Stage.BattleType == BattleType.Challenge ? 0 : 1;
            _cmbBattleType.SelectedIndexChanged += (s, e) =>
            {
                UpdateSaveButtonState();
            };

            var lblBattleTargetPrompt = new Label
            {
                Text = "對手 ID：",
                ForeColor = Color.FromArgb(180, 185, 205),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(3), ScaleDpi(8), 0)
            };

            var pnlTargetInputRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };

            _txtBattleTarget = new TextBox
            {
                Width = ScaleDpi(200),
                BackColor = Color.FromArgb(44, 48, 64),
                ForeColor = Color.FromArgb(235, 235, 245),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                PlaceholderText = "請輸入玩家 ID",
                Text = Stage.BattleTargetId,
                Margin = new Padding(0, ScaleDpi(1), ScaleDpi(8), 0)
            };

            _btnVerify = new IconButton
            {
                Text = " 驗證玩家",
                AutoSize = false,
                Width = ScaleDpi(100),
                Height = _txtBattleTarget.PreferredHeight,
                BackColor = Color.FromArgb(50, 75, 120),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 9f),
                IconChar = IconChar.MagnifyingGlass,
                IconColor = Color.White,
                IconSize = ScaleDpi(16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, ScaleDpi(1), 0, 0)
            };
            _btnVerify.FlatAppearance.BorderSize = 0;
            _btnVerify.Click += async (s, e) => await OnVerifyClickedAsync();

            pnlTargetInputRow.Controls.Add(_txtBattleTarget);
            pnlTargetInputRow.Controls.Add(_btnVerify);

            _lblBattleFeedback = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Font = new Font("Microsoft JhengHei UI", 9f),
                Margin = new Padding(0, ScaleDpi(4), 0, 0)
            };

            if (!string.IsNullOrEmpty(_verifiedOpponentName))
            {
                _lblBattleFeedback.Text = string.IsNullOrEmpty(_verifiedOpponentDetail)
                    ? $"✓ 對手：{_verifiedOpponentName}"
                    : $"✓ 對手：{_verifiedOpponentName} ｜ 角色：{_verifiedOpponentDetail}";
                _lblBattleFeedback.ForeColor = Color.FromArgb(80, 220, 120);
            }
            else
            {
                _lblBattleFeedback.Text = "請輸入對手玩家 ID 並點擊驗證";
                _lblBattleFeedback.ForeColor = Color.FromArgb(160, 160, 175);
            }

            _txtBattleTarget.TextChanged += (s, e) =>
            {
                string curText = _txtBattleTarget.Text.Trim();
                if (curText != Stage.BattleTargetId || string.IsNullOrEmpty(Stage.BattleTargetName))
                {
                    _verifiedOpponentName = string.Empty;
                    _verifiedOpponentDetail = string.Empty;
                    _lblBattleFeedback.Text = "⚠ 玩家 ID 已變更，請重新點擊「🔍 驗證玩家」";
                    _lblBattleFeedback.ForeColor = Color.FromArgb(235, 160, 80);
                }
                else
                {
                    _verifiedOpponentName = Stage.BattleTargetName;
                    _verifiedOpponentDetail = Stage.BattleTargetDetail;
                    _lblBattleFeedback.Text = string.IsNullOrEmpty(_verifiedOpponentDetail)
                        ? $"✓ 對手：{_verifiedOpponentName}"
                        : $"✓ 對手：{_verifiedOpponentName} ｜ 角色：{_verifiedOpponentDetail}";
                    _lblBattleFeedback.ForeColor = Color.FromArgb(80, 220, 120);
                }
                UpdateSaveButtonState();
            };

            tblBattle.Controls.Add(lblBattleTypePrompt, 0, 0);
            tblBattle.Controls.Add(_cmbBattleType, 1, 0);
            tblBattle.Controls.Add(lblBattleTargetPrompt, 0, 1);
            tblBattle.Controls.Add(pnlTargetInputRow, 1, 1);
            tblBattle.Controls.Add(_lblBattleFeedback, 1, 2);
            _pnlBattle.Controls.Add(tblBattle);
            tblAction.Controls.Add(_pnlBattle, 0, 2);

            _pnlActionSection.Controls.Add(tblAction);
            AddSection(_pnlActionSection);

            // ─────────────────────────────────────────────────────────────
            // 區塊 2: 達成切換條件設定
            // ─────────────────────────────────────────────────────────────
            var pnlConditionSection = CreateCardPanel();
            var tblCond = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            tblCond.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tblCond.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var lblCondPrompt = new Label
            {
                Text = "達成切換條件：",
                ForeColor = Color.FromArgb(210, 215, 235),
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(3), ScaleDpi(8), ScaleDpi(8))
            };

            _cmbConditionType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = ScaleDpi(160),
                BackColor = Color.FromArgb(44, 48, 64),
                ForeColor = Color.FromArgb(235, 235, 245),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Margin = new Padding(0, 0, 0, ScaleDpi(8))
            };
            _cmbConditionType.Items.Add("角色等級達到");
            _cmbConditionType.Items.Add("執行滿指定次數");
            _cmbConditionType.Items.Add("運行滿指定時間");
            _cmbConditionType.SelectedIndex = Stage.ConditionType switch
            {
                StageConditionType.RunCountReach => 1,
                StageConditionType.MinutesReach => 2,
                _ => 0
            };

            var lblValPrompt = new Label
            {
                Text = "目標數值：",
                ForeColor = Color.FromArgb(180, 185, 205),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(3), ScaleDpi(8), 0)
            };

            var pnlValRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };

            _nudConditionVal = new NumericUpDown
            {
                Width = ScaleDpi(110),
                Minimum = 1,
                Maximum = 99999,
                Value = Math.Max(1, Math.Min(99999, Stage.ConditionValue)),
                BackColor = Color.FromArgb(44, 48, 64),
                ForeColor = Color.FromArgb(235, 235, 245),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Margin = new Padding(0, 0, ScaleDpi(8), 0)
            };

            _lblConditionUnit = new Label
            {
                ForeColor = Color.FromArgb(160, 165, 190),
                Font = new Font("Microsoft JhengHei UI", 9f),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, ScaleDpi(3), 0, 0)
            };

            void UpdateConditionUnitText()
            {
                _lblConditionUnit.Text = _cmbConditionType.SelectedIndex switch
                {
                    1 => "次（達到指定執行次數後切換）",
                    2 => "分鐘（達到指定運行時間後切換）",
                    _ => "等（出戰角色達到或超過此等級時切換）"
                };
            }
            UpdateConditionUnitText();

            _cmbConditionType.SelectedIndexChanged += (s, e) =>
            {
                UpdateConditionUnitText();
                UpdateSaveButtonState();
            };
            _nudConditionVal.ValueChanged += (s, e) =>
            {
                UpdateSaveButtonState();
            };

            pnlValRow.Controls.Add(_nudConditionVal);
            pnlValRow.Controls.Add(_lblConditionUnit);

            tblCond.Controls.Add(lblCondPrompt, 0, 0);
            tblCond.Controls.Add(_cmbConditionType, 1, 0);
            tblCond.Controls.Add(lblValPrompt, 0, 1);
            tblCond.Controls.Add(pnlValRow, 1, 1);
            pnlConditionSection.Controls.Add(tblCond);
            AddSection(pnlConditionSection);

            // =================================================================
            // Row 2: 底部操作按鈕 (取消 / 確定儲存)
            // =================================================================
            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = new Padding(0, ScaleDpi(6), 0, 0)
            };

            _btnCancel = new Button
            {
                Text = "取消",
                AutoSize = true,
                BackColor = Color.FromArgb(55, 55, 70),
                ForeColor = Color.FromArgb(220, 220, 230),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                Cursor = Cursors.Hand,
                Padding = new Padding(ScaleDpi(16), ScaleDpi(6), ScaleDpi(16), ScaleDpi(6)),
                Margin = new Padding(ScaleDpi(8), 0, 0, 0)
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            _btnSave = new Button
            {
                Text = _isNew ? "確定新增" : "確定儲存",
                AutoSize = true,
                BackColor = Color.FromArgb(40, 140, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Padding = new Padding(ScaleDpi(20), ScaleDpi(6), ScaleDpi(20), ScaleDpi(6)),
                Margin = Padding.Empty
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += OnSaveClicked;

            pnlButtons.Controls.Add(_btnCancel);
            pnlButtons.Controls.Add(_btnSave);
            tblRoot.Controls.Add(pnlButtons, 0, 2);

            this.CancelButton = _btnCancel;
        }

        private Panel CreateCardPanel()
        {
            return new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.FromArgb(32, 34, 46),
                Padding = new Padding(ScaleDpi(14), ScaleDpi(12), ScaleDpi(14), ScaleDpi(12)),
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };
        }

        private async Task OnVerifyClickedAsync()
        {
            string targetId = _txtBattleTarget.Text.Trim();
            if (string.IsNullOrEmpty(targetId))
            {
                _lblBattleFeedback.Text = "⚠ 請輸入對手玩家 ID 再進行驗證";
                _lblBattleFeedback.ForeColor = Color.FromArgb(235, 100, 100);
                UpdateSaveButtonState();
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentToken))
            {
                _lblBattleFeedback.Text = "⚠ 請先至主畫面「設定」分頁填寫 Bearer Token";
                _lblBattleFeedback.ForeColor = Color.FromArgb(235, 100, 100);
                UpdateSaveButtonState();
                return;
            }

            _btnVerify.Enabled = false;
            _lblBattleFeedback.Text = "查詢中，正在連線取得玩家資訊...";
            _lblBattleFeedback.ForeColor = Color.FromArgb(170, 215, 255);

            try
            {
                var result = await _battleService.GetPlayerProfileAsync(targetId, _currentToken);
                if (result.IsSuccess && result.Data != null)
                {
                    var data = result.Data;
                    string cName = data.Character != null ? data.Character.Name : "無";
                    int cLevel = data.Character != null ? data.Character.Level : 0;
                    string nick = data.Nickname ?? targetId;
                    string detail = $"Lv.{cLevel} {cName}";

                    _verifiedOpponentName = nick;
                    _verifiedOpponentDetail = detail;
                    _lblBattleFeedback.Text = $"✓ 對手：{nick} ｜ 角色：{detail}";
                    _lblBattleFeedback.ForeColor = Color.FromArgb(80, 220, 120);
                }
                else
                {
                    _verifiedOpponentName = string.Empty;
                    _verifiedOpponentDetail = string.Empty;
                    _lblBattleFeedback.Text = $"✗ 查詢失敗：{result.ErrorMessage}";
                    _lblBattleFeedback.ForeColor = Color.FromArgb(235, 100, 100);
                }
            }
            catch (Exception ex)
            {
                _verifiedOpponentName = string.Empty;
                _verifiedOpponentDetail = string.Empty;
                _lblBattleFeedback.Text = $"✗ 連線錯誤：{ex.Message}";
                _lblBattleFeedback.ForeColor = Color.FromArgb(235, 100, 100);
            }
            finally
            {
                _btnVerify.Enabled = true;
                UpdateSaveButtonState();
            }
        }

        private void UpdateSaveButtonState()
        {
            if (_btnSave == null) return;

            bool isTrain = _cmbActionType.SelectedIndex == 0;
            bool canSave = true;
            string tipMsg = "儲存此階段設定";

            if (isTrain)
            {
                if (_cmbTrainAction.SelectedItem == null)
                {
                    canSave = false;
                    tipMsg = "請選擇欲執行的訓練項目";
                }
            }
            else
            {
                string targetId = _txtBattleTarget.Text.Trim();
                if (string.IsNullOrEmpty(targetId))
                {
                    canSave = false;
                    tipMsg = "請輸入對手玩家 ID";
                }
                else if (string.IsNullOrEmpty(_verifiedOpponentName))
                {
                    canSave = false;
                    tipMsg = "尚未完成對手玩家驗證，請先點擊「🔍 驗證玩家」確認存在";
                }
            }

            if (_nudConditionVal.Value <= 0)
            {
                canSave = false;
                tipMsg = "達成條件數值必須大於 0";
            }

            _btnSave.Enabled = canSave;
            _btnSave.BackColor = canSave ? Color.FromArgb(40, 140, 80) : Color.FromArgb(50, 55, 70);
            _btnSave.ForeColor = canSave ? Color.White : Color.FromArgb(140, 145, 160);
            _btnSave.Cursor = canSave ? Cursors.Hand : Cursors.Default;
            _toolTip.SetToolTip(_btnSave, tipMsg);
        }

        private float DpiScale => (float)this.DeviceDpi / 96f;

        private void RefreshTrainingPills()
        {
            if (_pnlTrainingPills == null) return;
            _pnlTrainingPills.SuspendLayout();
            _pnlTrainingPills.Controls.Clear();

            if (_trainingSequenceList.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "（目前無自訂循環清單，將僅重複執行上方選擇之訓練項目）",
                    ForeColor = Color.FromArgb(145, 150, 175),
                    Font = new Font("Microsoft JhengHei UI", 9f),
                    AutoSize = true,
                    Margin = new Padding(ScaleDpi(2), ScaleDpi(3), ScaleDpi(2), ScaleDpi(3))
                };
                _pnlTrainingPills.Controls.Add(lblEmpty);
            }
            else
            {
                for (int i = 0; i < _trainingSequenceList.Count; i++)
                {
                    int index = i;
                    var action = _trainingSequenceList[i];
                    var pill = new PillBadgeControl($"{index + 1}. {action.DisplayName}", () =>
                    {
                        if (index >= 0 && index < _trainingSequenceList.Count)
                        {
                            _trainingSequenceList.RemoveAt(index);
                            RefreshTrainingPills();
                            UpdateSaveButtonState();
                        }
                    }, DpiScale);

                    _toolTip.SetToolTip(pill, $"第 {index + 1} 順位：{action.DisplayName}\n點擊以自清單中移除");
                    _pnlTrainingPills.Controls.Add(pill);
                }
            }

            _pnlTrainingPills.Visible = true;
            _pnlTrainingPills.ResumeLayout(true);
            _pnlTrainingPills.PerformLayout();
            _pnlTraining?.PerformLayout();
            _pnlActionSection?.PerformLayout();
            this.PerformLayout();
        }

        private void OnSaveClicked(object? sender, EventArgs e)
        {
            bool isTrain = _cmbActionType.SelectedIndex == 0;

            Stage.Name = $"階段 #{_stageNumber}";
            Stage.ActionType = isTrain ? StageActionType.Training : StageActionType.Battle;

            if (isTrain)
            {
                Stage.TrainingActionSequence = _trainingSequenceList.Select(a => a.ActionId).ToList();
                if (_trainingSequenceList.Count > 0)
                {
                    Stage.TrainingActionId = _trainingSequenceList[0].ActionId;
                }
                else if (_cmbTrainAction.SelectedItem is TrainingAction act)
                {
                    Stage.TrainingActionId = act.ActionId;
                }
                Stage.BattleTargetId = string.Empty;
                Stage.BattleTargetName = string.Empty;
                Stage.BattleTargetDetail = string.Empty;
            }
            else
            {
                string targetId = _txtBattleTarget.Text.Trim();
                if (string.IsNullOrEmpty(targetId))
                {
                    MessageBox.Show("請輸入戰鬥對象玩家 ID", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.IsNullOrEmpty(_verifiedOpponentName))
                {
                    MessageBox.Show("戰鬥對象尚未通過驗證，請先點擊「🔍 驗證玩家」確認對手存在後再儲存。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Stage.BattleTargetId = targetId;
                Stage.BattleType = _cmbBattleType.SelectedIndex == 0 ? BattleType.Challenge : BattleType.Chado;
                Stage.BattleTargetName = _verifiedOpponentName;
                Stage.BattleTargetDetail = _verifiedOpponentDetail;
            }

            Stage.ConditionType = _cmbConditionType.SelectedIndex switch
            {
                1 => StageConditionType.RunCountReach,
                2 => StageConditionType.MinutesReach,
                _ => StageConditionType.LevelReach
            };
            Stage.ConditionValue = (int)_nudConditionVal.Value;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
