using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDoujinBot.Models;
using MyDoujinBot.Services;
using MyDoujinBot.Utilities;

namespace MyDoujinBot.Forms
{
    public class BattleStatsForm : Form
    {
        private readonly BattleService _battleService = new();
        private readonly Func<string> _getTokenFunc;

        private TextBox txtReportId = null!;
        private Button btnQuery = null!;
        private RichTextBox rtbOutput = null!;
        private Label lblStatus = null!;

        public BattleStatsForm(Func<string> getTokenFunc)
        {
            _getTokenFunc = getTokenFunc;

            this.Text = "戰鬥數據統計";
            this.Size = new Size(680, 580);
            this.MinimumSize = new Size(520, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(28, 28, 38);
            this.ForeColor = Color.FromArgb(220, 220, 230);
            this.Font = new Font("Microsoft JhengHei UI", 9.5f);
            this.ShowIcon = false;
            this.MinimizeBox = false;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);

            BuildUI();
        }

        private void BuildUI()
        {
            var pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14)
            };
            this.Controls.Add(pnlMain);

            // ── 頂部查詢區 ──
            var pnlTop = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 0, 10)
            };
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pnlTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lblReportId = new Label
            {
                Text = "戰報編號：",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = Color.FromArgb(190, 190, 210)
            };

            txtReportId = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 58),
                ForeColor = Color.FromArgb(230, 230, 240),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 10f)
            };
            txtReportId.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    btnQuery.PerformClick();
                }
            };

            btnQuery = new Button
            {
                Text = "🔍 取得數據",
                AutoSize = true,
                BackColor = Color.FromArgb(60, 70, 120),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(8, 0, 0, 0)
            };
            btnQuery.FlatAppearance.BorderSize = 0;
            btnQuery.Click += OnQueryClicked;

            pnlTop.Controls.Add(lblReportId, 0, 0);
            pnlTop.Controls.Add(txtReportId, 1, 0);
            pnlTop.Controls.Add(btnQuery, 2, 0);

            lblStatus = new Label
            {
                Text = "請輸入戰報編號，並點擊「取得數據」。",
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 160, 180),
                Margin = new Padding(0, 6, 0, 0)
            };
            pnlTop.Controls.Add(lblStatus, 0, 1);
            pnlTop.SetColumnSpan(lblStatus, 3);

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

            // ── 結果顯示區 (RichTextBox) ──
            rtbOutput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 16, 24),
                ForeColor = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10.5f),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = false
            };

            var pnlLogs = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 6, 0, 0)
            };
            pnlLogs.Controls.Add(rtbOutput);
            pnlMain.Controls.Add(pnlLogs);
            pnlLogs.BringToFront();
        }

        private async void OnQueryClicked(object? sender, EventArgs e)
        {
            string reportIdStr = txtReportId.Text.Trim();
            if (string.IsNullOrEmpty(reportIdStr))
            {
                SetStatus("請輸入有效的戰報編號。", Color.FromArgb(220, 90, 90));
                return;
            }

            string token = _getTokenFunc();
            if (string.IsNullOrWhiteSpace(token))
            {
                SetStatus("Token 未設定，請先點擊主畫面的「設定」填入 Bearer Token。", Color.FromArgb(220, 90, 90));
                return;
            }

            btnQuery.Enabled = false;
            SetStatus($"正透過 API 讀取戰報 #{reportIdStr}...", Color.FromArgb(140, 200, 255));
            rtbOutput.Clear();

            try
            {
                var response = await _battleService.GetBattleReportAsync(reportIdStr, token);

                if (!response.IsSuccess || response.Data == null)
                {
                    string msg = response.IsUnauthorized
                        ? "Token 無效或已過期 (HTTP 401)，請更新 Token 後重試。"
                        : (response.ErrorMessage ?? "取得戰報失敗，請確認戰報編號是否存在或網路連線。");
                    SetStatus(msg, Color.FromArgb(220, 90, 90));
                    return;
                }

                DisplayReport(response.Data);
            }
            catch (Exception ex)
            {
                SetStatus($"處理發生異常：{ex.Message}", Color.FromArgb(220, 90, 90));
            }
            finally
            {
                btnQuery.Enabled = true;
            }
        }

        private void DisplayReport(BattleReportResponse report)
        {
            if (!BattleStatsAnalyzer.TryAnalyze(report, out var analysis, out string error))
            {
                SetStatus($"戰報格式異常：{error}", Color.FromArgb(220, 90, 90));
                return;
            }

            var lines = BattleStatsChartBuilder.Build(analysis!);
            RenderChart(lines);
            SetStatus($"戰報 #{report.ReportId} 數據計算完成。", Color.FromArgb(90, 210, 110));
        }

        private void RenderChart(List<List<ChartSegment>> lines)
        {
            rtbOutput.Clear();
            rtbOutput.SelectionStart = 0;
            rtbOutput.SelectionLength = 0;

            foreach (var line in lines)
            {
                foreach (var seg in line)
                {
                    rtbOutput.SelectionStart = rtbOutput.TextLength;
                    rtbOutput.SelectionLength = 0;

                    var (color, fontStyle) = GetStyleProperties(seg.Style);
                    rtbOutput.SelectionColor = color;
                    rtbOutput.SelectionFont = new Font(rtbOutput.Font, fontStyle);
                    rtbOutput.AppendText(seg.Text);
                }
                rtbOutput.AppendText("\n");
            }
        }

        private (Color Color, FontStyle Style) GetStyleProperties(ChartStyle style) => style switch
        {
            ChartStyle.OutputBar => (Color.FromArgb(246, 135, 139), FontStyle.Regular),   // 輸出長條 (紅)
            ChartStyle.TakenBar  => (Color.FromArgb(129, 185, 255), FontStyle.Regular),   // 承傷長條 (藍)
            ChartStyle.HealBar   => (Color.FromArgb(123, 213, 166), FontStyle.Regular),   // 治療長條 (綠)
            ChartStyle.Fallen    => (Color.FromArgb(255, 160, 60),  FontStyle.Bold),      // 【倒下】 橘色粗體
            ChartStyle.Dead      => (Color.FromArgb(246, 100, 100), FontStyle.Bold),      // 【死亡】 紅色粗體
            ChartStyle.Heading   => (Color.FromArgb(255, 215, 70),  FontStyle.Bold),      // 標題 (金黃)
            ChartStyle.Muted     => (Color.FromArgb(160, 160, 180), FontStyle.Italic),    // 附註說明 (灰斜)
            _                    => (Color.FromArgb(220, 220, 230), FontStyle.Regular)    // 預設樣式
        };

        private void SetStatus(string text, Color color)
        {
            lblStatus.Text = text;
            lblStatus.ForeColor = color;
        }
    }
}
