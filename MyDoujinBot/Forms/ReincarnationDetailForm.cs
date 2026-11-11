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
    /// 檢視本次轉生配點視窗（呼叫 GET /api/characters 讀取 reincarnation.allocated 與傳承資訊）。
    /// </summary>
    public sealed class ReincarnationDetailForm : Form
    {
        private static readonly Color ActivePointColor = Color.FromArgb(255, 215, 70);  // 金黃色（有配點 > 0）
        private static readonly Color ZeroPointColor   = Color.FromArgb(120, 128, 148); // 暗灰（未配點 0）
        private static readonly Color BonusPointColor  = Color.FromArgb(110, 215, 255); // 亮青藍（特殊加成配點 > 0）

        private readonly UserService _userService;
        private readonly string _token;
        private readonly CancellationTokenSource _cts = new();

        private Label _lblTotalPoints = null!;
        private Label _lblSpentAvailable = null!;
        private Label _lblLoadingStatus = null!;

        // 基礎九圍配點標籤
        private Label _valHp = null!;
        private Label _valAtk = null!;
        private Label _valDef = null!;
        private Label _valSta = null!;
        private Label _valAgi = null!;
        private Label _valSpd = null!;
        private Label _valTec = null!;
        private Label _valInt = null!;
        private Label _valLuk = null!;

        // 特殊加成配點標籤
        private Label _valExpBonus = null!;
        private Label _valEventBonus = null!;
        private Label _valEffortBonus = null!;
        private Label _valAggroInc = null!;
        private Label _valAggroDec = null!;

        private float DpiScale => (float)this.DeviceDpi / 96f;
        private int ScaleDpi(int value) => (int)Math.Round(value * DpiScale);

        public ReincarnationDetailForm(UserService userService, string token)
        {
            _userService = userService;
            _token = token;

            this.Text = "檢視轉生配點";
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(22, 22, 30);
            this.ForeColor = Color.FromArgb(225, 225, 238);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            BuildUI();

            this.Shown += async (_, _) => await LoadReincarnationAsync();
            this.FormClosing += (_, _) =>
            {
                _cts.Cancel();
                _cts.Dispose();
            };
        }

        private void BuildUI()
        {
            var tblRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = ScaleDpi(330),
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(ScaleDpi(14), ScaleDpi(12), ScaleDpi(14), ScaleDpi(12)),
                BackColor = Color.Transparent
            };
            tblRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 0: 點數摘要卡
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 1: 狀態提示
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 2: 九圍配點
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 3: 特殊加成配點
            tblRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Row 4: 底部關閉按鈕

            // ═══════════════════════════════════════════════════════════════════
            // Row 0: 頂部轉生點數總覽卡
            // ═══════════════════════════════════════════════════════════════════
            var pnlSummaryCard = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.FromArgb(30, 32, 44),
                Padding = new Padding(ScaleDpi(14), ScaleDpi(10), ScaleDpi(14), ScaleDpi(10)),
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };
            pnlSummaryCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            _lblTotalPoints = new Label
            {
                Text = "總點數：—",
                Font = new Font("Microsoft JhengHei UI", 11.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(240, 242, 250),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(4))
            };
            pnlSummaryCard.Controls.Add(_lblTotalPoints, 0, 0);

            _lblSpentAvailable = new Label
            {
                Text = "已分配：— ｜ 剩餘：—",
                Font = new Font("Microsoft JhengHei UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(195, 205, 225),
                AutoSize = true,
                Margin = Padding.Empty
            };
            pnlSummaryCard.Controls.Add(_lblSpentAvailable, 0, 1);

            tblRoot.Controls.Add(pnlSummaryCard, 0, 0);

            // ═══════════════════════════════════════════════════════════════════
            // Row 1: 載入提示
            // ═══════════════════════════════════════════════════════════════════
            _lblLoadingStatus = new Label
            {
                Text = "⏳ 正在讀取轉生配點資料...",
                ForeColor = Color.FromArgb(235, 185, 80),
                Font = new Font("Microsoft JhengHei UI", 10f),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(8))
            };
            tblRoot.Controls.Add(_lblLoadingStatus, 0, 1);

            // ═══════════════════════════════════════════════════════════════════
            // Row 2: 基礎屬性配點 (直式單欄 1x9)
            // ═══════════════════════════════════════════════════════════════════
            var grpBaseStats = CreateSectionCard("基礎能力配點", out var tblBaseGrid, columns: 1, rows: 9);
            _valHp  = AddStatCell(tblBaseGrid, 0, 0, "HP");
            _valAtk = AddStatCell(tblBaseGrid, 0, 1, "攻擊");
            _valDef = AddStatCell(tblBaseGrid, 0, 2, "防禦");
            _valSta = AddStatCell(tblBaseGrid, 0, 3, "體力");
            _valAgi = AddStatCell(tblBaseGrid, 0, 4, "敏捷");
            _valSpd = AddStatCell(tblBaseGrid, 0, 5, "反應速度");
            _valTec = AddStatCell(tblBaseGrid, 0, 6, "技巧");
            _valInt = AddStatCell(tblBaseGrid, 0, 7, "智力");
            _valLuk = AddStatCell(tblBaseGrid, 0, 8, "幸運");
            tblRoot.Controls.Add(grpBaseStats, 0, 2);

            // ═══════════════════════════════════════════════════════════════════
            // Row 3: 特殊加成配點 (直式單欄 1x5)
            // ═══════════════════════════════════════════════════════════════════
            var grpSpecial = CreateSectionCard("特殊加成配點", out var tblSpecialGrid, columns: 1, rows: 5);
            _valExpBonus    = AddStatCell(tblSpecialGrid, 0, 0, "經驗加成");
            _valEventBonus  = AddStatCell(tblSpecialGrid, 0, 1, "事件加成");
            _valEffortBonus = AddStatCell(tblSpecialGrid, 0, 2, "努力加成");
            _valAggroInc    = AddStatCell(tblSpecialGrid, 0, 3, "仇恨增加");
            _valAggroDec    = AddStatCell(tblSpecialGrid, 0, 4, "仇恨減少");
            tblRoot.Controls.Add(grpSpecial, 0, 3);

            // ═══════════════════════════════════════════════════════════════════
            // Row 4: 底部關閉按鈕
            // ═══════════════════════════════════════════════════════════════════
            var pnlBottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, ScaleDpi(8), 0, 0)
            };

            var btnClose = new Button
            {
                Text = "關閉",
                DialogResult = DialogResult.Cancel,
                Size = new Size(ScaleDpi(96), ScaleDpi(34)),
                BackColor = Color.FromArgb(55, 58, 75),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 10.5f)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (_, _) => this.Close();
            pnlBottom.Controls.Add(btnClose);

            tblRoot.Controls.Add(pnlBottom, 0, 4);
            this.Controls.Add(tblRoot);
            this.CancelButton = btnClose;
        }

        private Panel CreateSectionCard(string title, out TableLayoutPanel grid, int columns, int rows)
        {
            var card = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = Color.FromArgb(30, 32, 44),
                Padding = new Padding(ScaleDpi(12), ScaleDpi(8), ScaleDpi(12), ScaleDpi(10)),
                Margin = new Padding(0, 0, 0, ScaleDpi(10))
            };

            var tblWrapper = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };
            tblWrapper.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var lblTitle = new Label
            {
                Text = $"▎{title}",
                Font = new Font("Microsoft JhengHei UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(145, 180, 255),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, ScaleDpi(6))
            };
            tblWrapper.Controls.Add(lblTitle, 0, 0);

            grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = columns,
                RowCount = rows,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                BackColor = Color.FromArgb(38, 40, 54),
                Margin = Padding.Empty
            };
            for (int c = 0; c < columns; c++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            }
            for (int r = 0; r < rows; r++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            tblWrapper.Controls.Add(grid, 0, 1);
            card.Controls.Add(tblWrapper);
            return card;
        }

        private Label AddStatCell(TableLayoutPanel grid, int col, int row, string labelText)
        {
            var pnlCell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.FromArgb(26, 28, 38),
                Padding = new Padding(ScaleDpi(12), ScaleDpi(5), ScaleDpi(12), ScaleDpi(5)),
                Margin = Padding.Empty
            };
            pnlCell.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlCell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var lblKey = new Label
            {
                Text = labelText,
                ForeColor = Color.FromArgb(175, 182, 202),
                Font = new Font("Microsoft JhengHei UI", 9.5f),
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };

            var lblVal = new Label
            {
                Text = "—",
                ForeColor = ZeroPointColor,
                Font = new Font("Consolas", 11f, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Right
            };

            pnlCell.Controls.Add(lblKey, 0, 0);
            pnlCell.Controls.Add(lblVal, 1, 0);
            grid.Controls.Add(pnlCell, col, row);
            return lblVal;
        }

        private static void AddEmptyCell(TableLayoutPanel grid, int col, int row)
        {
            var pnlEmpty = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(26, 28, 38),
                Margin = Padding.Empty
            };
            grid.Controls.Add(pnlEmpty, col, row);
        }

        private async Task LoadReincarnationAsync()
        {
            try
            {
                var cached = UserContext.Current.Reincarnation;
                if (cached != null)
                {
                    RenderReincarnation(cached);
                    _lblLoadingStatus.Visible = false;
                    return;
                }

                var res = await _userService.GetCharactersAsync(_token, _cts.Token);
                if (_cts.IsCancellationRequested || this.IsDisposed) return;

                if (!res.IsSuccess || res.Data == null)
                {
                    _lblLoadingStatus.Text = $"❌ 無法取得轉生配點資料：{res.ErrorMessage ?? "未知錯誤"}";
                    _lblLoadingStatus.ForeColor = Color.FromArgb(235, 95, 95);
                    _lblLoadingStatus.Visible = true;
                    return;
                }

                if (res.Data.Reincarnation != null)
                {
                    RenderReincarnation(res.Data.Reincarnation);
                }

                _lblLoadingStatus.Visible = false;
            }
            catch (Exception ex) when (!_cts.IsCancellationRequested)
            {
                if (!this.IsDisposed)
                {
                    _lblLoadingStatus.Text = $"❌ 讀取轉生配點發生例外：{ex.Message}";
                    _lblLoadingStatus.ForeColor = Color.FromArgb(235, 95, 95);
                    _lblLoadingStatus.Visible = true;
                }
            }
        }

        private void RenderReincarnation(ReincarnationInfo re)
        {
            _lblTotalPoints.Text = $"總點數：{re.Total}";
            _lblSpentAvailable.Text = $"已分配：{re.Spent} ｜ 剩餘：{re.Available}";

            var alloc = re.Allocated;
            SetPointLabel(_valHp,  alloc?.HP ?? 0,  ActivePointColor);
            SetPointLabel(_valAtk, alloc?.Atk ?? 0, ActivePointColor);
            SetPointLabel(_valDef, alloc?.Def ?? 0, ActivePointColor);
            SetPointLabel(_valSta, alloc?.Sta ?? 0, ActivePointColor);
            SetPointLabel(_valAgi, alloc?.Agi ?? 0, ActivePointColor);
            SetPointLabel(_valSpd, alloc?.Spd ?? 0, ActivePointColor);
            SetPointLabel(_valTec, alloc?.Tec ?? 0, ActivePointColor);
            SetPointLabel(_valInt, alloc?.Int ?? 0, ActivePointColor);
            SetPointLabel(_valLuk, alloc?.Luk ?? 0, ActivePointColor);

            SetPointLabel(_valExpBonus,    alloc?.ExpBonus ?? 0,      BonusPointColor);
            SetPointLabel(_valEventBonus,  alloc?.EventBonus ?? 0,    BonusPointColor);
            SetPointLabel(_valEffortBonus, alloc?.EffortBonus ?? 0,   BonusPointColor);
            SetPointLabel(_valAggroInc,    alloc?.AggroIncrease ?? 0, BonusPointColor);
            SetPointLabel(_valAggroDec,    alloc?.AggroDecrease ?? 0, BonusPointColor);
        }

        private static void SetPointLabel(Label lbl, int value, Color activeColor)
        {
            lbl.Text = value > 0 ? $"+{value}" : "0";
            lbl.ForeColor = value > 0 ? activeColor : ZeroPointColor;
        }
    }
}

