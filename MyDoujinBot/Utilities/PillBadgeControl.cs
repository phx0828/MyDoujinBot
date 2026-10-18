using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyDoujinBot.Utilities
{
    /// <summary>
    /// 平滑膠囊風格的標籤元件（Pill Badge），支援點擊移除（✕）與唯讀顯示模式。
    /// 完整支援 High-DPI 縮放與防鋸齒圓角繪製。
    /// </summary>
    public sealed class PillBadgeControl : Control
    {
        private readonly Action? _onRemove;
        private readonly string _title;
        private bool _isHovered;
        private bool _isPressed;
        private readonly float _dpiScale;

        public PillBadgeControl(string text, Action? onRemove, float dpiScale)
        {
            _title = text;
            _onRemove = onRemove;
            _dpiScale = Math.Max(1.0f, dpiScale);

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            DoubleBuffered = true;
            Cursor = _onRemove != null ? Cursors.Hand : Cursors.Default;
            Font = new Font("Microsoft JhengHei UI", 8.5f);
            Margin = new Padding(0, 0, (int)(6 * _dpiScale), (int)(6 * _dpiScale));

            int h = Math.Max(26, (int)(26 * _dpiScale));
            string measureText = _onRemove != null ? _title + "  ✕" : _title;
            var textSize = TextRenderer.MeasureText(measureText, Font);
            int w = textSize.Width + (int)(18 * _dpiScale);
            Size = new Size(w, h);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (!Enabled || _onRemove == null) return;
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!Enabled || _onRemove == null) return;
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isPressed)
            {
                _isPressed = false;
                Invalidate();
            }
        }

        protected override void OnClick(EventArgs e)
        {
            if (!Enabled || _onRemove == null) return;
            base.OnClick(e);
            _onRemove?.Invoke();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = (Enabled && _onRemove != null) ? Cursors.Hand : Cursors.Default;
            if (!Enabled)
            {
                _isHovered = false;
                _isPressed = false;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Color parentBg = Parent?.BackColor ?? Color.FromArgb(24, 24, 30);
            if (parentBg == Color.Transparent && Parent?.Parent != null)
                parentBg = Parent.Parent.BackColor;
            using (var bgBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // 繪製平滑膠囊橢圓底色（無邊框、無描邊）
            float h = Height - 1;
            float w = Width - 1;
            float r = h;
            using (var path = new GraphicsPath())
            {
                path.AddArc(0, 0, r, r, 90, 180);
                path.AddArc(w - r, 0, r, r, 270, 180);
                path.CloseFigure();

                Color pillBg;
                if (!Enabled)
                {
                    pillBg = Color.FromArgb(28, 32, 42);
                }
                else if (_onRemove != null)
                {
                    pillBg = _isPressed
                        ? Color.FromArgb(60, 90, 140)
                        : (_isHovered ? Color.FromArgb(48, 72, 112) : Color.FromArgb(36, 52, 80));
                }
                else
                {
                    pillBg = Color.FromArgb(32, 45, 68);
                }

                using var pillBrush = new SolidBrush(pillBg);
                g.FillPath(pillBrush, path);
            }

            // 繪製文字與 ✕
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            if (_onRemove != null)
            {
                var mainText = _title + " ";
                var closeText = "✕";

                var mainSize = TextRenderer.MeasureText(g, mainText, Font, Size, flags);
                var closeSize = TextRenderer.MeasureText(g, closeText, Font, Size, flags);

                int totalTextWidth = mainSize.Width + closeSize.Width;
                int startX = Math.Max((int)(8 * _dpiScale), (Width - totalTextWidth) / 2);
                int textY = (Height - mainSize.Height) / 2;

                var mainRect = new Rectangle(startX, textY, mainSize.Width, mainSize.Height);
                var closeRect = new Rectangle(startX + mainSize.Width, textY, closeSize.Width, closeSize.Height);

                Color textColor = !Enabled ? Color.FromArgb(100, 110, 125) : Color.FromArgb(220, 235, 255);
                TextRenderer.DrawText(g, mainText, Font, mainRect, textColor, flags);

                Color closeColor = !Enabled ? Color.FromArgb(70, 75, 90) : (_isHovered ? Color.FromArgb(255, 120, 120) : Color.FromArgb(145, 175, 215));
                TextRenderer.DrawText(g, closeText, Font, closeRect, closeColor, flags);
            }
            else
            {
                var mainText = _title;
                var mainSize = TextRenderer.MeasureText(g, mainText, Font, Size, flags);
                int startX = Math.Max((int)(8 * _dpiScale), (Width - mainSize.Width) / 2);
                int textY = (Height - mainSize.Height) / 2;
                var mainRect = new Rectangle(startX, textY, mainSize.Width, mainSize.Height);

                Color textColor = !Enabled ? Color.FromArgb(100, 110, 125) : Color.FromArgb(200, 220, 250);
                TextRenderer.DrawText(g, mainText, Font, mainRect, textColor, flags);
            }
        }
    }
}
