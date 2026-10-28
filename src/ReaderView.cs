using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace NovelOverlay
{
    public class Line
    {
        public string Text = "";
        public float X;
        public float Y;
        public int Start;
        public int Len;
    }

    /// <summary>
    /// 排版 + 浮现/滚动动画的纯逻辑层。不再继承 Control：
    /// 窗口是分层窗口（真透明），由 MainForm 每帧把内容画进 32bppPArgb 位图后提交。
    /// </summary>
    public class ReaderView
    {
        public MainForm Host;

        public Font TextFont = new Font("Microsoft YaHei", 17f);
        public Color TextColor = Color.FromArgb(255, 255, 255, 255);
        public Color ShadowColor = Color.FromArgb(255, 0, 0, 0);
        public bool Shadow = true;
        public float LineSpacing = 1.45f;
        public int PadX = 14;
        public int PadY = 10;
        public bool EdgeFade = true;
        public bool IndentFirst = true;
        public bool AutoScroll = true;
        public float ScrollSpeed = 22f;
        public int RevealMode = 1;        // 0=瞬间 1=逐行 2=逐字
        public float RevealSpeed = 1f;    // 倍率
        public bool AllowResize = true;
        public bool ManualPause;
        public bool Busy;                 // 拖动/拉伸中：暂停自动滑动

        Size size = new Size(330, 240);
        string text = "";
        bool hasBook;
        float lineH = 28f;
        int charTotal = 1;

        readonly List<Line> lines = new List<Line>();
        readonly Dictionary<char, float> cwCache = new Dictionary<char, float>();
        readonly Dictionary<string, float> wCache = new Dictionary<string, float>();
        readonly Dictionary<int, SolidBrush> textBrushes = new Dictionary<int, SolidBrush>();
        readonly Dictionary<int, SolidBrush> shadowBrushes = new Dictionary<int, SolidBrush>();
        readonly StringFormat typo = (StringFormat)StringFormat.GenericTypographic.Clone();
        readonly StringFormat center = new StringFormat();

        float scrollY, targetScrollY, maxScroll;
        float pendingFraction = -1f;
        float revealPos;
        bool revealing;
        float pauseLeft;

        Bitmap measBmp;
        Graphics measG;

        public ReaderView()
        {
            center.Alignment = StringAlignment.Center;
            center.LineAlignment = StringAlignment.Center;
            typo.FormatFlags |= StringFormatFlags.NoWrap;
        }

        // ======================= 对外接口 =======================
        public bool HasBook { get { return hasBook; } }
        public bool IsRevealing { get { return revealing; } }
        public Size ViewSize { get { return size; } }

        public int EffPadX
        {
            get
            {
                int cap = Math.Max(2, size.Width / 12);
                int p = PadX;
                if (p > cap) p = cap;
                if (p < 0) p = 0;
                return p;
            }
        }

        public int EffPadY
        {
            get
            {
                int cap = Math.Max(1, size.Height / 10);
                int p = PadY;
                if (p > cap) p = cap;
                if (p < 0) p = 0;
                return p;
            }
        }

        public float VisibleHeight { get { return Math.Max(12f, size.Height - EffPadY * 2); } }

        public int RevealTotal { get { return RevealMode == 2 ? charTotal : lines.Count; } }

        public float ScrollFraction
        {
            get { return maxScroll <= 0.5f ? 0f : Math.Max(0f, Math.Min(1f, scrollY / maxScroll)); }
        }

        public float TargetFraction
        {
            get { return maxScroll <= 0.5f ? 0f : Math.Max(0f, Math.Min(1f, targetScrollY / maxScroll)); }
        }

        public void SetSize(Size s)
        {
            if (s.Width < 1) s.Width = 1;
            if (s.Height < 1) s.Height = 1;
            if (s == size && lines.Count > 0) return;
            size = s;
            RelayoutInternal(false);
        }

        public void SetText(string t)
        {
            text = t == null ? "" : t.Replace("\r\n", "\n").Replace('\r', '\n');
            hasBook = true;
            scrollY = 0;
            targetScrollY = 0;
            ManualPause = false;
            RelayoutInternal(true);
        }

        public void ClearText()
        {
            text = "";
            hasBook = false;
            lines.Clear();
        }

        public void SetScrollFraction(float f)
        {
            if (f < 0f) f = 0f;
            if (f > 1f) f = 1f;
            if (maxScroll <= 0.5f)
            {
                pendingFraction = f;   // 排版还没完成，等排完再定位
                scrollY = 0f;
                targetScrollY = 0f;
                return;
            }
            scrollY = targetScrollY = maxScroll * f;
            ClampScroll();
        }

        public void SetFont(Font f)
        {
            if (f == null) return;
            int keep = -1;
            if (lines.Count > 0)
            {
                int fi = FirstVisibleIndex();
                if (fi >= 0 && fi < lines.Count) keep = lines[fi].Start;
            }
            TextFont = f;
            cwCache.Clear();
            wCache.Clear();
            ClearBrushes();
            RelayoutInternal(false);
            if (keep >= 0) ScrollToChar(keep);
        }

        public void SetTextColor(Color c)
        {
            TextColor = c;
            ClearBrushes();
        }

        public void SetShadowColor(Color c)
        {
            ShadowColor = c;
            ClearBrushes();
        }

        public void ScrollBy(float px)
        {
            targetScrollY += px;
            pauseLeft = 1.8f;
            ClampScroll();
        }

        public void WheelStep(int delta)
        {
            ScrollBy(-(delta / 120f) * (lineH * 2.6f));
        }

        public void ScrollToTop()
        {
            targetScrollY = 0;
            ClampScroll();
        }

        public void StopReveal()
        {
            revealing = false;
            revealPos = RevealTotal;
        }

        public void SetRevealMode(int mode)
        {
            RevealMode = mode;
            revealPos = RevealTotal;
            revealing = false;
        }

        /// <summary>每次浮现开始时调用：从当前可视位置开始逐行/逐字显现</summary>
        public void OnShownNow()
        {
            if (!hasBook) return;
            targetScrollY = Math.Max(0f, Math.Min(maxScroll, targetScrollY));
            if (RevealMode == 0 || lines.Count == 0)
            {
                revealPos = RevealTotal;
                revealing = false;
            }
            else
            {
                int fi = FirstVisibleIndex();
                if (fi < 0) fi = 0;
                if (RevealMode == 2 && fi < lines.Count) revealPos = lines[fi].Start;
                else revealPos = fi;
                revealing = true;
            }
        }

        // ======================= 排版 =======================
        Graphics MG
        {
            get
            {
                if (measG == null)
                {
                    measBmp = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);
                    measG = Graphics.FromImage(measBmp);
                }
                return measG;
            }
        }

        public void RelayoutInternal(bool resetScroll)
        {
            Graphics g = MG;
            lineH = TextFont.GetHeight(g) * LineSpacing;
            BuildLines(g);

            float contentH = lines.Count > 0 ? lines[lines.Count - 1].Y + lineH : 0f;
            maxScroll = Math.Max(0f, contentH + EffPadY * 2f - size.Height);
            if (resetScroll) { scrollY = 0; targetScrollY = 0; }
            else ClampScroll();
            if (pendingFraction >= 0f)
            {
                scrollY = targetScrollY = maxScroll * pendingFraction;
                pendingFraction = -1f;
                ClampScroll();
            }
        }

        /// <summary>逐字贪心折行：中文按字断，英文按单词断（单词太长就按字断）</summary>
        void BuildLines(Graphics g)
        {
            lines.Clear();
            int padx = EffPadX;
            float maxW = Math.Max(4f, size.Width - padx * 2f);
            float indentW = 0f;
            if (IndentFirst)
            {
                indentW = Measure(g, "　　");
                if (indentW > maxW * 0.42f) indentW = 0f;
            }

            float y = 0f;
            int cp = 0;
            string[] paras = text.Split('\n');
            for (int pi = 0; pi < paras.Length; pi++)
            {
                string p = paras[pi].TrimEnd();
                if (p.Length == 0)
                {
                    cp += 1;
                    y += lineH * 0.45f;
                    continue;
                }

                int paraStart = cp;
                int lineStart = 0;
                float curW = 0f;
                bool firstLine = true;
                float avail = (indentW > 0f) ? Math.Max(4f, maxW - indentW) : maxW;
                int wordStart = -1;
                int i = 0;

                while (i < p.Length)
                {
                    char c = p[i];

                    // 行首空白丢弃
                    if (curW <= 0f && i == lineStart && char.IsWhiteSpace(c)) { i++; lineStart = i; continue; }

                    float w = MeasureChar(g, c);

                    if (curW + w > avail + 0.02f && i > lineStart)
                    {
                        int cut = i;
                        // 尽量不把英文单词劈开：回退到单词开头
                        if (wordStart > lineStart && IsWordChar(c) && IsWordChar(p[i - 1])) cut = wordStart;

                        string seg = p.Substring(lineStart, cut - lineStart).TrimEnd();
                        AddLine(seg, paraStart + lineStart, cut - lineStart, firstLine ? indentW : 0f, y);
                        y += lineH;
                        firstLine = false;
                        avail = maxW;
                        lineStart = cut;
                        curW = 0f;
                        wordStart = -1;
                        continue;
                    }

                    if (IsWordChar(c)) { if (wordStart < 0) wordStart = i; }
                    else wordStart = -1;

                    curW += w;
                    i++;
                }

                if (lineStart < p.Length)
                {
                    string seg = p.Substring(lineStart).TrimEnd();
                    if (seg.Length > 0)
                    {
                        AddLine(seg, paraStart + lineStart, p.Length - lineStart, firstLine ? indentW : 0f, y);
                        y += lineH;
                    }
                }
                cp = paraStart + p.Length + 1;
            }

            charTotal = 1;
            for (int i = 0; i < lines.Count; i++)
            {
                int e = lines[i].Start + lines[i].Len;
                if (e > charTotal) charTotal = e;
            }
            charTotal += 1;
        }

        void AddLine(string s, int start, int len, float x, float y)
        {
            Line l = new Line();
            l.Text = s;
            l.Start = start;
            l.Len = Math.Max(1, len);
            l.X = x;
            l.Y = y;
            lines.Add(l);
        }

        static bool IsWordChar(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
                   c == '\'' || c == '-' || c == '_';
        }

        static bool IsCjk(char c)
        {
            return (c >= 0x2E80 && c <= 0x9FFF) || (c >= 0xF900 && c <= 0xFAFF) ||
                   (c >= 0xFE30 && c <= 0xFE4F) || (c >= 0xFF00 && c <= 0xFFEF) ||
                   (c >= 0x3000 && c <= 0x303F);
        }

        float MeasureChar(Graphics g, char c)
        {
            float w;
            if (cwCache.TryGetValue(c, out w)) return w;
            w = g.MeasureString(c.ToString(), TextFont, PointF.Empty, typo).Width;
            if (w < 0f) w = 0f;
            if (cwCache.Count < 40000) cwCache[c] = w;
            return w;
        }

        float Measure(Graphics g, string s)
        {
            if (s == null || s.Length == 0) return 0f;
            float w;
            if (wCache.TryGetValue(s, out w)) return w;
            w = g.MeasureString(s, TextFont, PointF.Empty, typo).Width;
            if (wCache.Count < 20000) wCache[s] = w;
            return w;
        }

        void ClampScroll()
        {
            if (targetScrollY < 0f) targetScrollY = 0f;
            if (targetScrollY > maxScroll) targetScrollY = maxScroll;
            if (scrollY < 0f) scrollY = 0f;
            if (scrollY > maxScroll) scrollY = maxScroll;
        }

        void ScrollToChar(int ci)
        {
            int idx = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Start <= ci) idx = i; else break;
            }
            targetScrollY = scrollY = lines.Count > 0 ? lines[idx].Y : 0f;
            ClampScroll();
        }

        int FirstVisibleIndex()
        {
            if (lines.Count == 0) return 0;
            int lo = 0, hi = lines.Count - 1, ans = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (lines[mid].Y + lineH > scrollY - 0.5f) { ans = mid; hi = mid - 1; }
                else lo = mid + 1;
            }
            return ans;
        }

        // ======================= 动画 =======================
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (dt > 0.12f) dt = 0.12f;

            if (revealing)
            {
                float ups = 26f * Math.Max(0.05f, RevealSpeed);            // 逐行：行/秒
                if (RevealMode == 2) ups = 130f * Math.Max(0.05f, RevealSpeed); // 逐字：字/秒
                revealPos += ups * dt;
                if (revealPos >= RevealTotal) { revealPos = RevealTotal; revealing = false; }
            }

            if (pauseLeft > 0f) pauseLeft -= dt;

            if (AutoScroll && !revealing && !ManualPause && pauseLeft <= 0f && !Busy && maxScroll > 0.5f)
            {
                if (targetScrollY < maxScroll)
                {
                    targetScrollY += ScrollSpeed * dt;
                    if (targetScrollY > maxScroll) targetScrollY = maxScroll;
                }
            }

            if (Math.Abs(targetScrollY - scrollY) > 0.05f)
            {
                float k = 1f - (float)Math.Exp(-dt * 13f);
                scrollY += (targetScrollY - scrollY) * k;
                if (Math.Abs(targetScrollY - scrollY) < 0.15f) scrollY = targetScrollY;
            }
        }

        // ======================= 颜色缓存 =======================
        void ClearBrushes()
        {
            foreach (KeyValuePair<int, SolidBrush> kv in textBrushes) kv.Value.Dispose();
            textBrushes.Clear();
            foreach (KeyValuePair<int, SolidBrush> kv in shadowBrushes) kv.Value.Dispose();
            shadowBrushes.Clear();
        }

        static int Quant(int a)
        {
            a = a - (a % 3);
            if (a < 3) a = 3;
            if (a > 255) a = 255;
            return a;
        }

        SolidBrush TextBrush(int a)
        {
            a = Quant(a);
            SolidBrush b;
            if (!textBrushes.TryGetValue(a, out b))
            {
                b = new SolidBrush(Color.FromArgb(a, TextColor));
                textBrushes[a] = b;
            }
            return b;
        }

        SolidBrush ShadowBrush(int a)
        {
            a = Quant(a);
            SolidBrush b;
            if (!shadowBrushes.TryGetValue(a, out b))
            {
                b = new SolidBrush(Color.FromArgb(a, ShadowColor));
                shadowBrushes[a] = b;
            }
            return b;
        }

        // ======================= 绘制 =======================
        /// <summary>把当前画面画进位图。ga = 全局透明度（淡入淡出 * 整体不透明度）</summary>
        public void Render(Graphics g, float ga)
        {
            if (ga <= 0.004f) return;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.None;

            if (hasBook && lines.Count == 0 && text.Length > 0) RelayoutInternal(false);

            if (!hasBook)
            {
                DrawHint(g, ga);
                return;
            }
            if (lines.Count == 0) return;

            int h = size.Height;
            int padx = EffPadX;
            float baseY = EffPadY - scrollY;

            float fh = Math.Min(42f, h * 0.22f);
            bool fadeTop = EdgeFade && fh > 5f && scrollY > 1.5f;
            bool fadeBot = EdgeFade && fh > 5f && maxScroll > 1.5f && scrollY < maxScroll - 1.5f;

            int i0 = FirstVisibleIndex() - 2;
            if (i0 < 0) i0 = 0;

            for (int i = i0; i < lines.Count; i++)
            {
                Line L = lines[i];
                float y = baseY + L.Y;
                if (y > h + lineH) break;
                if (y < -lineH * 2f) continue;

                string s = L.Text;
                float a = 1f;
                float dy = 0f;

                if (revealing || revealPos < RevealTotal)
                {
                    if (RevealMode == 2)
                    {
                        float shown = revealPos - L.Start;
                        if (shown <= 0f) continue;
                        if (shown < L.Len)
                        {
                            int n = (int)shown;
                            if (n <= 0) continue;
                            if (n < s.Length) s = s.Substring(0, n);
                            float sp = Math.Min(1f, shown / Math.Max(1f, L.Len * 0.6f));
                            dy = (1f - sp) * 9f;
                            a = 0.35f + 0.65f * sp;
                        }
                    }
                    else
                    {
                        float p = revealPos - i;
                        if (p <= 0f) continue;
                        if (p < 1f)
                        {
                            dy = (1f - p) * 13f;
                            a = 0.12f + 0.88f * p;
                        }
                    }
                }

                if (fadeTop)
                {
                    float d = y;
                    if (d < fh)
                    {
                        if (d < 0f) d = 0f;
                        a *= d / fh;
                    }
                }
                if (fadeBot)
                {
                    float d = h - (y + lineH);
                    if (d < fh)
                    {
                        if (d < 0f) d = 0f;
                        a *= d / fh;
                    }
                }

                if (a <= 0.02f) continue;
                int alpha = (int)(a * ga * 255f);
                if (alpha < 3) continue;
                if (alpha > 255) alpha = 255;

                if (Shadow)
                {
                    int sa = (int)(alpha * 0.62f);
                    if (sa > 3)
                        g.DrawString(s, TextFont, ShadowBrush(sa),
                            new PointF(padx + L.X + 1.1f, y + dy + 1.2f), typo);
                }
                g.DrawString(s, TextFont, TextBrush(alpha), new PointF(padx + L.X, y + dy), typo);
            }
        }

        void DrawHint(Graphics g, float ga)
        {
            int w = size.Width, h = size.Height;
            string t = "右键 · 导入 txt / epub / mobi";
            string t2 = "左键拖动 · 边缘拉伸 · 滚轮滚动";
            using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(150 * ga), TextColor)))
            using (SolidBrush b2 = new SolidBrush(Color.FromArgb((int)(96 * ga), TextColor)))
            using (Font f1 = new Font(TextFont.FontFamily, 13f))
            using (Font f2 = new Font(TextFont.FontFamily, 9.5f))
            {
                Rectangle r = new Rectangle(0, h / 2 - 26, w, 26);
                g.DrawString(t, f1, b, r, center);
                r.Y += 30;
                g.DrawString(t2, f2, b2, r, center);
            }
        }

        public void ClearBrushesPublic() { ClearBrushes(); }

        public void DisposeRes()
        {
            ClearBrushes();
            if (measG != null) { measG.Dispose(); measG = null; }
            if (measBmp != null) { measBmp.Dispose(); measBmp = null; }
        }
    }
}
