using System;
using System.Windows.Forms;
using FontAwesome.Sharp;

namespace MyDoujinBot.Utilities
{
    /// <summary>
    /// 側邊導覽列專用按鈕。
    /// 以自訂的 Locked 狀態取代 Enabled = false：
    /// WinForms 的 Flat 按鈕在 Enabled = false 時，文字與圖示會被系統強制以內建的 disabled 色彩（偏黑）重繪，
    /// 且無法透過 ForeColor / IconColor 控制。保持 Enabled = true 並自行處理「不可點擊」與「變淡」，
    /// 即可維持與一般狀態完全相同的繪製流程，因此版面位置、大小、對齊不會有任何位移。
    /// </summary>
    public class NavButton : IconButton
    {
        private bool _locked;

        /// <summary>鎖定時：不可點擊、無 hover/pressed 效果、游標為一般箭頭。顏色由外部（MainForm）決定。</summary>
        public bool Locked
        {
            get => _locked;
            set
            {
                if (_locked == value) return;
                _locked = value;
                Cursor = value ? Cursors.Default : Cursors.Hand;
                Invalidate();
            }
        }

        protected override void OnClick(EventArgs e)
        {
            if (_locked) return;
            base.OnClick(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (_locked) return;   // 不進入 pressed 狀態，避免閃一下按下的底色
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            if (_locked) return;   // 不進入 hover 狀態
            base.OnMouseEnter(e);
        }
    }
}
