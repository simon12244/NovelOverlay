using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NovelOverlay
{
    /// <summary>
    /// 分层窗口（per-pixel alpha）封装：让窗口真正透明，只有文字像素可见。
    /// 同时提供屏幕取样 / 对比色计算 / 鼠标穿透样式切换。
    /// </summary>
    public static class Overlay
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr h);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst,
            ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        const int ULW_ALPHA = 0x00000002;
        const byte AC_SRC_OVER = 0x00;
        const byte AC_SRC_ALPHA = 0x01;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_NOACTIVATE = 0x08000000;

        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;

        /// <summary>把 32bppPArgb 位图作为整个窗口的像素提交（含位置与大小）</summary>
        public static void Apply(IntPtr hwnd, Bitmap bmp, Point location)
        {
            if (hwnd == IntPtr.Zero || bmp == null) return;
            IntPtr screenDc = IntPtr.Zero, memDc = IntPtr.Zero, hBmp = IntPtr.Zero, oldBmp = IntPtr.Zero;
            try
            {
                screenDc = GetDC(IntPtr.Zero);
                memDc = CreateCompatibleDC(screenDc);
                hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                oldBmp = SelectObject(memDc, hBmp);

                SIZE sz = new SIZE();
                sz.cx = bmp.Width;
                sz.cy = bmp.Height;
                POINT src = new POINT();
                src.X = 0;
                src.Y = 0;
                POINT dst = new POINT();
                dst.X = location.X;
                dst.Y = location.Y;
                BLENDFUNCTION bf = new BLENDFUNCTION();
                bf.BlendOp = AC_SRC_OVER;
                bf.BlendFlags = 0;
                bf.SourceConstantAlpha = 255;
                bf.AlphaFormat = AC_SRC_ALPHA;

                UpdateLayeredWindow(hwnd, screenDc, ref dst, ref sz, memDc, ref src, 0, ref bf, ULW_ALPHA);
            }
            catch { }
            finally
            {
                if (oldBmp != IntPtr.Zero && memDc != IntPtr.Zero) SelectObject(memDc, oldBmp);
                if (hBmp != IntPtr.Zero) DeleteObject(hBmp);
                if (memDc != IntPtr.Zero) DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        /// <summary>鼠标穿透：点击直接落到下面的窗口（此时无法拖动、无法右键）</summary>
        public static void SetClickThrough(IntPtr hwnd, bool through)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
                if (through) ex |= WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
                else ex &= ~(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
                SetWindowLong(hwnd, GWL_EXSTYLE, ex);
            }
            catch { }
        }

        // ==================== 屏幕取样 & 配色 ====================

        static double Lum(Color c)
        {
            return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
        }

        static Color Mix(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(255,
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        /// <summary>
        /// 取窗口四周（不含窗口自身区域）的屏幕颜色，得到平均色与亮度。
        /// 采样环避开自己，不会被自己画出来的文字干扰。
        /// </summary>
        public static bool SampleAround(Rectangle inner, out Color avg, out double lum)
        {
            avg = Color.FromArgb(255, 128, 128, 128);
            lum = 0.5;
            Rectangle vs = SystemInformation.VirtualScreen;
            Rectangle r = inner;
            r.Inflate(44, 44);
            r.Intersect(vs);
            if (r.Width < 8 || r.Height < 8) return false;

            try
            {
                using (Bitmap bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(r.Width, r.Height), CopyPixelOperation.SourceCopy);

                    BitmapData bd = bmp.LockBits(new Rectangle(0, 0, r.Width, r.Height),
                        ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    long sr = 0, sg = 0, sb = 0, n = 0;
                    try
                    {
                        int stride = bd.Stride;
                        byte[] buf = new byte[stride * r.Height];
                        Marshal.Copy(bd.Scan0, buf, 0, buf.Length);

                        int ix0 = inner.Left - r.Left, ix1 = inner.Right - r.Left;
                        int iy0 = inner.Top - r.Top, iy1 = inner.Bottom - r.Top;
                        int step = 5;
                        for (int y = 0; y < r.Height; y += step)
                        {
                            if (y >= iy0 && y < iy1) continue;      // 跳过窗口自身覆盖的横向区域
                            int row = y * stride;
                            for (int x = 0; x < r.Width; x += step)
                            {
                                int o = row + x * 4;
                                sb += buf[o];
                                sg += buf[o + 1];
                                sr += buf[o + 2];
                                n++;
                            }
                        }
                    }
                    finally
                    {
                        bmp.UnlockBits(bd);
                    }
                    if (n < 16) return false;
                    avg = Color.FromArgb(255, (int)(sr / n), (int)(sg / n), (int)(sb / n));
                    lum = Lum(avg);
                    return true;
                }
            }
            catch { return false; }
        }

        /// <summary>根据背景平均色挑一个够显眼、又带一点背景互补色调的文字色 + 投影色</summary>
        public static void PickText(Color bg, double lum, out Color text, out Color shadow)
        {
            Color comp = Color.FromArgb(255, 255 - bg.R, 255 - bg.G, 255 - bg.B);
            if (lum < 0.5)
            {
                // 背景偏暗 → 亮字，掺一点背景的补色，视觉上更"贴"在一起
                text = Mix(Color.White, comp, 0.20);
                if (Lum(text) < 0.70) text = Mix(text, Color.White, 0.55);
                shadow = Color.Black;
            }
            else
            {
                // 背景偏亮 → 暗字
                text = Mix(Color.Black, comp, 0.22);
                if (Lum(text) > 0.30) text = Mix(text, Color.Black, 0.55);
                shadow = Color.White;
            }
            // 中等亮度时补一层反向投影，保证任何背景上都读得清
            if (lum > 0.32 && lum < 0.68)
            {
                if (Lum(text) < 0.5) shadow = Color.Black;
                else shadow = Color.White;
            }
        }
    }
}
