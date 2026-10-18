using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyDoujinBot.Utilities
{
    /// <summary>
    /// 自適應換行與容器寬度的膠囊標籤流式容器（FlowLayoutPanel）。
    /// 解決 WinForms 在 TableLayoutPanel（百分比或自動欄寬）中將可用寬度傳為最小子元件寬度（如 112px），
    /// 導致 FlowLayoutPanel 計算出過高高度（每項獨立換行）且留下大量垂直空白的版面問題。
    /// </summary>
    public class PillFlowLayoutPanel : FlowLayoutPanel
    {
        public PillFlowLayoutPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            // 決定可用折行寬度：
            // 若上層容器（如 TableLayoutPanel）傳入的寬度過小（<= 200px），則優先採用本控制項實際寬度、
            // 父容器客戶區寬度，或 Form 寬度，以獲得真實的折行寬度。
            int targetWidth = 0;
            if (proposedSize.Width > 200)
            {
                targetWidth = proposedSize.Width;
            }
            else if (this.Width > 200)
            {
                targetWidth = this.Width;
            }
            else if (Parent != null && Parent.ClientSize.Width > 200)
            {
                targetWidth = Parent.ClientSize.Width - Margin.Horizontal;
            }
            else if (FindForm() is Form f && f.ClientSize.Width > 200)
            {
                targetWidth = f.ClientSize.Width - (Padding.Horizontal + Margin.Horizontal + 60);
            }
            else
            {
                targetWidth = 480;
            }

            int availableContentWidth = targetWidth - Padding.Horizontal;
            if (availableContentWidth <= 0) availableContentWidth = 400;

            int curX = 0;
            int curRowHeight = 0;
            int totalHeight = 0;
            int childCount = 0;

            Form? hostForm = FindForm();
            bool formVisible = hostForm != null && hostForm.Visible;

            foreach (Control child in Controls)
            {
                // 若表單已顯示且子元件明確被設為不可見，則跳過
                if (formVisible && !child.Visible) continue;

                childCount++;
                var childMargin = child.Margin;
                var childSize = child.PreferredSize.IsEmpty ? child.Size : child.PreferredSize;
                int itemW = childSize.Width + childMargin.Horizontal;
                int itemH = childSize.Height + childMargin.Vertical;

                if (curX + itemW > availableContentWidth && curX > 0)
                {
                    totalHeight += curRowHeight;
                    curX = itemW;
                    curRowHeight = itemH;
                }
                else
                {
                    curX += itemW;
                    if (itemH > curRowHeight) curRowHeight = itemH;
                }
            }
            totalHeight += curRowHeight;

            if (childCount == 0)
            {
                return new Size(targetWidth, 0);
            }

            int finalH = totalHeight + Padding.Vertical;
            return new Size(targetWidth, finalH);
        }
    }
}
