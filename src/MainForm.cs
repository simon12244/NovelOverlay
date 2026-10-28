using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace NovelOverlay
{
    public class MainForm : Form
    {
        AppSettings S;
        Book book;
        ReaderView view;
        Timer timer;
        DateTime lastTick;

        Bitmap canvas;
        float fade;                 // 0..1 淡入淡出（替代 Form.Opacity，配合分层窗口）
        bool wantShow;
        bool suppressHide;
        int hideAt;
        int curChapter;
        int sampleTick;
        int showUntil;
        bool firstRun;

        bool dragging, resizing;
        int rzone;
        Point dragOrigin;
        Rectangle dragRect;
        Point resizeOrigin;
        Rectangle resizeRect;

        bool forceQuit;
        ContextMenuStrip lastMenu;

        public MainForm(string openPath)
        {
            firstRun = !File.Exists(AppSettings.ConfigPath);
            S = AppSettings.Load();
            Text = "NovelOverlay";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque, true);

            view = new ReaderView();
            view.Host = this;

            ApplyBounds();
            ApplyAll();

            timer = new Timer();
            timer.Interval = 33;
            timer.Tick += OnTick;
            lastTick = DateTime.Now;
            timer.Start();

            Application.AddMessageFilter(new WheelFilter(this));

            LoadInitial(openPath);
        }

        /// <summary>分层窗口：真透明 + 不激活 + 不进任务栏</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Overlay.WS_EX_LAYERED | Overlay.WS_EX_TOOLWINDOW;
                if (S != null && S.MouseThrough) cp.ExStyle |= 0x00000020 | 0x08000000;
                return cp;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e) { }

        /// <summary>全局滚轮过滤器：鼠标压在悬浮窗上时，滚的是小说而不是背后的软件</summary>
        class WheelFilter : IMessageFilter
        {
            MainForm f;
            public WheelFilter(MainForm form) { f = form; }
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != 0x020A) return false;          // WM_MOUSEWHEEL
                if (!f.Visible) return false;
                if (!f.Bounds.Contains(Cursor.Position)) return false;
                int delta = (short)(((long)m.WParam >> 16) & 0xFFFF);
                f.view.WheelStep(delta);
                return true;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        // ================= 初始化 =================
        void ApplyBounds()
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            int w = Math.Max(24, S.WinW);
            int h = Math.Max(16, S.WinH);
            int x, y;
            bool valid = S.WinX != -1 && S.WinY != -1 &&
                         S.WinX > vs.Left - w + 40 && S.WinX < vs.Right - 40 &&
                         S.WinY > vs.Top - 20 && S.WinY < vs.Bottom - 40;
            if (valid) { x = S.WinX; y = S.WinY; }
            else
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                x = wa.Right - w - 60;
                y = wa.Top + 80;
            }
            Bounds = new Rectangle(x, y, w, h);
        }

        void ApplyAll()
        {
            view.PadX = S.PadX;
            view.PadY = S.PadY;
            view.LineSpacing = S.LineSpacing;
            view.EdgeFade = S.EdgeFade;
            view.IndentFirst = S.IndentFirst;
            view.AutoScroll = S.AutoScroll;
            view.ScrollSpeed = S.ScrollSpeed;
            view.RevealSpeed = S.RevealSpeed;
            view.AllowResize = !S.Locked;
            view.Shadow = S.TextShadow;
            view.SetRevealMode(S.RevealMode);
            view.SetSize(Size);
            view.SetFont(MakeFont(S.FontName, S.FontSize));
            if (S.AutoColor) RefreshAutoColor();
            else ApplyManualColor();
            TopMost = S.TopMost;
            if (IsHandleCreated) Overlay.SetClickThrough(Handle, S.MouseThrough);
        }

        void ApplyFont()
        {
            view.SetFont(MakeFont(S.FontName, S.FontSize));
        }

        void ApplyManualColor()
        {
            view.SetTextColor(S.TextColor);
            double lum = (0.299 * S.TextColor.R + 0.587 * S.TextColor.G + 0.114 * S.TextColor.B) / 255.0;
            view.SetShadowColor(lum > 0.55 ? Color.White : Color.Black);
        }

        /// <summary>取窗口四周的屏幕颜色 → 自动选一个既显眼又带背景色调的文字色</summary>
        void RefreshAutoColor()
        {
            if (!S.AutoColor) return;
            if (!IsHandleCreated || Width < 4 || Height < 4) return;
            Color avg;
            double lum;
            if (!Overlay.SampleAround(Bounds, out avg, out lum)) return;
            Color text, shadow;
            Overlay.PickText(avg, lum, out text, out shadow);
            view.SetTextColor(text);
            view.SetShadowColor(shadow);
        }

        public static Font MakeFont(string name, float size)
        {
            if (size < 6f) size = 6f;
            if (size > 120f) size = 120f;
            string[] cand = new string[] { name, "微软雅黑", "Microsoft YaHei", "宋体", "SimSun", "Segoe UI" };
            for (int i = 0; i < cand.Length; i++)
            {
                if (cand[i] == null || cand[i].Length == 0) continue;
                try
                {
                    Font f = new Font(cand[i], size);
                    if (string.Equals(f.Name, cand[i], StringComparison.OrdinalIgnoreCase) || i == cand.Length - 1)
                        return f;
                    if (f.Name.Length > 0 && !f.Name.Equals("Microsoft Sans Serif", StringComparison.OrdinalIgnoreCase))
                        return f;
                    f.Dispose();
                }
                catch { }
            }
            return new Font(FontFamily.GenericSansSerif, size);
        }

        void LoadInitial(string openPath)
        {
            if (openPath != null && openPath.Length > 0 && File.Exists(openPath))
            {
                OpenBook(openPath, false);
                return;
            }
            if (S.LastFile.Length > 0 && File.Exists(S.LastFile))
            {
                OpenBook(S.LastFile, true);
                return;
            }
            view.ClearText();
            curChapter = 0;
        }

        // ================= 文件 =================
        public void OpenBook(string path, bool restore)
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                Book b = BookLoader.Load(path);
                book = b;
                S.LastFile = path;
                S.PushRecent(path);
                int ch = restore ? S.LastChapter : 0;
                if (ch < 0) ch = 0;
                if (ch >= b.Chapters.Count) ch = b.Chapters.Count - 1;
                curChapter = ch;
                S.LastChapter = ch;
                view.SetText(b.Chapters[ch].Text);
                view.SetScrollFraction(restore ? S.LastScroll : 0f);
                view.StopReveal();
                if (wantShow) view.OnShownNow();
                SaveProgress();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开失败：\n" + ex.Message, "NovelOverlay",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        void SaveProgress()
        {
            S.WinX = Left;
            S.WinY = Top;
            S.WinW = Width;
            S.WinH = Height;
            if (book != null)
            {
                S.LastChapter = curChapter;
                S.LastScroll = view.TargetFraction;
            }
            S.Save();
        }

        void GotoChapter(int i)
        {
            if (book == null || i < 0 || i >= book.Chapters.Count) return;
            curChapter = i;
            S.LastChapter = i;
            view.SetText(book.Chapters[i].Text);
            view.SetScrollFraction(0f);
            view.StopReveal();
            view.OnShownNow();
            SaveProgress();
        }

        // ================= 渲染 =================
        void Render()
        {
            if (!IsHandleCreated || !Visible) return;
            int w = Width, h = Height;
            if (w < 2) w = 2;
            if (h < 2) h = 2;
            try
            {
                if (canvas == null || canvas.Width != w || canvas.Height != h)
                {
                    if (canvas != null) canvas.Dispose();
                    canvas = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
                }
                float ga = fade * Math.Max(0.05f, S.Opacity);
                if (ga > 1f) ga = 1f;
                if (ga < 0f) ga = 0f;

                using (Graphics g = Graphics.FromImage(canvas))
                {
                    // alpha = 1：肉眼全透明，但仍可被鼠标命中（要能拖动/拉伸）
                    g.Clear(Color.FromArgb(1, 0, 0, 0));

                    int bgA = (int)(S.BgAlpha * 255f * ga);
                    if (S.BgAlpha > 0.004f && bgA > 1)
                    {
                        if (bgA > 255) bgA = 255;
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(bgA, S.BgColor)))
                            g.FillRectangle(b, 0, 0, w, h);
                    }
                    view.Render(g, ga);
                }
                Overlay.Apply(Handle, canvas, new Point(Left, Top));
            }
            catch { }
        }

        // ================= 心跳 =================
        void OnTick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            float dt = (float)(now - lastTick).TotalSeconds;
            lastTick = now;

            UpdateHover();
            if (Visible)
            {
                view.Busy = dragging || resizing;
                view.Tick(dt);
                UpdateFade(dt);

                if (S.AutoColor && wantShow)
                {
                    sampleTick++;
                    if (sampleTick >= 30) { sampleTick = 0; RefreshAutoColor(); }
                }
                Render();
            }
        }

        bool MouseInZone()
        {
            Point p = Cursor.Position;
            Rectangle b = Bounds;
            int pad = wantShow ? 4 : Math.Max(4, S.HoverPad);
            return p.X >= b.Left - pad && p.X <= b.Right + pad &&
                   p.Y >= b.Top - pad && p.Y <= b.Bottom + pad;
        }

        void UpdateHover()
        {
            bool inside = !S.HoverHide || MouseInZone();
            if (suppressHide || dragging || resizing) inside = true;
            if (showUntil != 0)
            {
                if (unchecked(Environment.TickCount - showUntil) < 0) inside = true;
                else showUntil = 0;
            }

            if (inside)
            {
                hideAt = 0;
                if (!wantShow)
                {
                    wantShow = true;
                    sampleTick = 0;
                    if (!Visible)
                    {
                        fade = 0f;
                        Show();
                    }
                    RefreshAutoColor();
                    view.OnShownNow();
                    Render();
                }
                else if (!Visible)
                {
                    Show();
                    Render();
                }
            }
            else if (wantShow)
            {
                if (hideAt == 0) hideAt = Environment.TickCount;
                else if (unchecked(Environment.TickCount - hideAt) > 380)
                {
                    wantShow = false;
                    hideAt = 0;
                }
            }
        }

        void UpdateFade(float dt)
        {
            float target = (wantShow || !S.HoverHide) ? 1f : 0f;
            float cur = fade;

            if (Math.Abs(target - cur) <= 0.012f) cur = target;
            else
            {
                float speed = target > cur ? 4.5f : 3.4f;
                float step = speed * (dt <= 0f ? 0.033f : dt);
                if (step > Math.Abs(target - cur)) step = Math.Abs(target - cur);
                cur += (target > cur ? step : -step);
                if (cur < 0f) cur = 0f;
                if (cur > 1f) cur = 1f;
            }
            fade = cur;

            if (cur <= 0.012f && !wantShow && Visible)
            {
                SaveProgress();
                Hide();
            }
        }

        // ================= 命中 / 拖动 / 拉伸 =================
        int HitZone(Point p)
        {
            if (!view.AllowResize || S.MouseThrough) return 0;
            int m = Math.Min(Width, Height);
            int e = Math.Max(2, Math.Min(8, m / 8));
            bool l = p.X <= e, r = p.X >= Width - e, t = p.Y <= e, b = p.Y >= Height - e;
            if (l && t) return 5;
            if (r && t) return 6;
            if (l && b) return 7;
            if (r && b) return 8;
            if (l) return 1;
            if (r) return 2;
            if (t) return 3;
            if (b) return 4;
            return 0;
        }

        static Cursor ZoneCursor(int z)
        {
            switch (z)
            {
                case 1: case 2: return Cursors.SizeWE;
                case 3: case 4: return Cursors.SizeNS;
                case 5: case 8: return Cursors.SizeNWSE;
                case 6: case 7: return Cursors.SizeNESW;
                default: return Cursors.Default;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (S.MouseThrough) return;

            if (e.Button == MouseButtons.Right)
            {
                ShowContextMenu(this, e.Location);
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            if (S.Locked) return;

            int z = HitZone(e.Location);
            if (z != 0 && view.AllowResize)
            {
                resizing = true;
                rzone = z;
                resizeOrigin = Cursor.Position;
                resizeRect = Bounds;
            }
            else
            {
                dragging = true;
                dragOrigin = Cursor.Position;
                dragRect = Bounds;
            }
            suppressHide = true;
            try { Capture = true; } catch { }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (S.MouseThrough) return;

            if (dragging)
            {
                Point p = Cursor.Position;
                Location = new Point(dragRect.X + (p.X - dragOrigin.X), dragRect.Y + (p.Y - dragOrigin.Y));
                return;
            }
            if (resizing)
            {
                Point p = Cursor.Position;
                int dx = p.X - resizeOrigin.X;
                int dy = p.Y - resizeOrigin.Y;
                int l = resizeRect.Left, t = resizeRect.Top, r = resizeRect.Right, b = resizeRect.Bottom;
                if (rzone == 1 || rzone == 5 || rzone == 7) l += dx;
                if (rzone == 2 || rzone == 6 || rzone == 8) r += dx;
                if (rzone == 3 || rzone == 5 || rzone == 6) t += dy;
                if (rzone == 4 || rzone == 7 || rzone == 8) b += dy;
                if (r - l < 24) { if (rzone == 1 || rzone == 5 || rzone == 7) l = r - 24; else r = l + 24; }
                if (b - t < 16) { if (rzone == 3 || rzone == 5 || rzone == 6) t = b - 16; else b = t + 16; }
                Bounds = new Rectangle(l, t, r - l, b - t);
                return;
            }

            Cursor c = ZoneCursor(HitZone(e.Location));
            if (Cursor != c) Cursor = c;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging || resizing)
            {
                dragging = false;
                resizing = false;
                suppressHide = false;
                try { Capture = false; } catch { }
                view.RelayoutInternal(false);
                RefreshAutoColor();
                SaveProgress();
                Render();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left) view.ManualPause = !view.ManualPause;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!view.HasBook) return;
            view.WheelStep(e.Delta);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (view != null) view.SetSize(Size);
        }

        // ================= 菜单 =================
        public void ShowContextMenu(Control src, Point pt)
        {
            suppressHide = true;
            wantShow = true;
            if (!Visible) { fade = 1f; Show(); }
            RefreshAutoColor();
            Render();

            ContextMenuStrip m = BuildMenu();
            m.Closed += delegate
            {
                suppressHide = false;
                hideAt = Environment.TickCount;
            };
            if (lastMenu != null) { try { lastMenu.Dispose(); } catch { } }
            lastMenu = m;
            m.Show(src, pt);
        }

        ToolStripMenuItem Check(string text, bool on, EventHandler h)
        {
            ToolStripMenuItem it = new ToolStripMenuItem((on ? "✓ " : "    ") + text);
            it.Click += h;
            return it;
        }

        static string Trunc(string s, int n)
        {
            if (s == null) return "";
            s = s.Replace("\n", " ").Trim();
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }

        static bool SameColor(Color a, Color b)
        {
            return a.R == b.R && a.G == b.G && a.B == b.B;
        }

        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Font = new Font("Microsoft YaHei", 9.5f);
            m.ShowImageMargin = false;
            m.BackColor = Color.FromArgb(32, 34, 40);
            m.ForeColor = Color.FromArgb(232, 234, 238);
            m.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
            m.Padding = new Padding(3, 4, 3, 4);

            // ---- 头部 ----
            ToolStripMenuItem hd = new ToolStripMenuItem(book == null ? "未打开文件" : "《" + Trunc(book.Title, 16) + "》");
            hd.Enabled = false;
            m.Items.Add(hd);
            if (book != null)
            {
                ToolStripMenuItem hd2 = new ToolStripMenuItem((curChapter + 1) + " / " + book.Chapters.Count + " · " + Trunc(book.Chapters[curChapter].Title, 16));
                hd2.Enabled = false;
                m.Items.Add(hd2);
            }
            m.Items.Add(new ToolStripSeparator());

            // ---- 文件 ----
            ToolStripMenuItem open = new ToolStripMenuItem("打开文件…", null, delegate { Defer(DoOpen); });
            open.ShortcutKeyDisplayString = "Ctrl+O";
            m.Items.Add(open);

            if (S.Recent.Count > 0)
            {
                ToolStripMenuItem rec = new ToolStripMenuItem("最近打开");
                for (int i = 0; i < S.Recent.Count; i++)
                {
                    string p = S.Recent[i];
                    ToolStripMenuItem r = new ToolStripMenuItem(Trunc(Path.GetFileNameWithoutExtension(p), 22), null,
                        delegate { OpenBook(p, false); });
                    r.ToolTipText = p;
                    rec.DropDownItems.Add(r);
                }
                m.Items.Add(rec);
            }

            // ---- 章节 ----
            if (book != null && book.Chapters.Count > 1)
            {
                ToolStripMenuItem nav = new ToolStripMenuItem("章节");
                ToolStripMenuItem p1 = new ToolStripMenuItem("上一章", null, delegate { GotoChapter(curChapter - 1); });
                p1.ShortcutKeyDisplayString = "PgUp";
                ToolStripMenuItem p2 = new ToolStripMenuItem("下一章", null, delegate { GotoChapter(curChapter + 1); });
                p2.ShortcutKeyDisplayString = "PgDn";
                nav.DropDownItems.Add(p1);
                nav.DropDownItems.Add(p2);
                nav.DropDownItems.Add(new ToolStripSeparator());
                nav.DropDownItems.Add(new ToolStripMenuItem("章节列表…", null, delegate { Defer(ShowChapterPicker); }));
                m.Items.Add(nav);
            }

            m.Items.Add(new ToolStripSeparator());

            // ---- 排版 ----
            ToolStripMenuItem sizeM = new ToolStripMenuItem("字号");
            float[] sizes = new float[] { 12, 14, 16, 18, 20, 22, 24, 26, 28, 32, 36, 42, 48, 56, 64 };
            for (int i = 0; i < sizes.Length; i++)
            {
                float v = sizes[i];
                sizeM.DropDownItems.Add(Check(v.ToString("0"), Math.Abs(S.FontSize - v) < 0.15f,
                    delegate { S.FontSize = v; ApplyFont(); S.Save(); }));
            }
            m.Items.Add(sizeM);

            float[] fs = new float[] { 0.9f, 1.05f, 1.2f, 1.4f };
            ToolStripMenuItem fsM = new ToolStripMenuItem("快速缩放");
            for (int i = 0; i < fs.Length; i++)
            {
                float k = fs[i];
                fsM.DropDownItems.Add(new ToolStripMenuItem("×" + k.ToString("0.00"), null,
                    delegate { S.FontSize = Math.Max(8f, Math.Min(90f, S.FontSize * k)); ApplyFont(); S.Save(); }));
            }
            fsM.DropDownItems.Add(new ToolStripSeparator());
            fsM.DropDownItems.Add(new ToolStripMenuItem("还原 19", null, delegate { S.FontSize = 19f; ApplyFont(); S.Save(); }));
            m.Items.Add(fsM);

            m.Items.Add(new ToolStripMenuItem("字体…", null, delegate { Defer(DoFont); }));

            ToolStripMenuItem lhM = new ToolStripMenuItem("行距");
            float[] lhs = new float[] { 1.2f, 1.35f, 1.55f, 1.8f, 2.1f, 2.5f };
            string[] lhn = new string[] { "紧凑", "较紧", "标准", "宽松", "很宽", "超宽" };
            for (int i = 0; i < lhs.Length; i++)
            {
                float v = lhs[i];
                lhM.DropDownItems.Add(Check(lhn[i] + " (" + v.ToString("0.00") + ")", Math.Abs(S.LineSpacing - v) < 0.02f,
                    delegate { S.LineSpacing = v; view.LineSpacing = v; view.RelayoutInternal(false); S.Save(); }));
            }
            m.Items.Add(lhM);

            // ---- 文字颜色 ----
            ToolStripMenuItem tcM = new ToolStripMenuItem("文字颜色");
            tcM.DropDownItems.Add(Check("自动融入背景（跟随屏幕）", S.AutoColor, delegate
            {
                S.AutoColor = !S.AutoColor;
                if (S.AutoColor) RefreshAutoColor();
                else ApplyManualColor();
                S.Save();
            }));
            tcM.DropDownItems.Add(new ToolStripSeparator());
            AddTextColorItem(tcM, "白", Color.FromArgb(240, 242, 245));
            AddTextColorItem(tcM, "米白", Color.FromArgb(237, 227, 200));
            AddTextColorItem(tcM, "暖黄", Color.FromArgb(245, 215, 110));
            AddTextColorItem(tcM, "浅绿", Color.FromArgb(159, 232, 176));
            AddTextColorItem(tcM, "天蓝", Color.FromArgb(156, 211, 245));
            AddTextColorItem(tcM, "粉", Color.FromArgb(244, 178, 196));
            AddTextColorItem(tcM, "灰", Color.FromArgb(150, 152, 158));
            AddTextColorItem(tcM, "墨黑", Color.FromArgb(26, 26, 26));
            tcM.DropDownItems.Add(new ToolStripSeparator());
            tcM.DropDownItems.Add(new ToolStripMenuItem("自定义…", null, delegate { Defer(delegate { PickColor(true); }); }));
            m.Items.Add(tcM);

            // ---- 底色（默认全透明）----
            ToolStripMenuItem bcM = new ToolStripMenuItem("底色");
            bcM.DropDownItems.Add(Check("完全透明（无底色）", S.BgAlpha <= 0.004f, delegate
            {
                S.BgAlpha = 0f; S.Save();
            }));
            bcM.DropDownItems.Add(new ToolStripSeparator());
            AddBgItem(bcM, "深色半透明", Color.FromArgb(16, 18, 22), 0.55f);
            AddBgItem(bcM, "墨蓝半透明", Color.FromArgb(19, 26, 38), 0.55f);
            AddBgItem(bcM, "米黄半透明", Color.FromArgb(243, 233, 210), 0.70f);
            AddBgItem(bcM, "白色半透明", Color.FromArgb(250, 250, 248), 0.70f);
            AddBgItem(bcM, "纯黑不透明", Color.Black, 0.92f);
            bcM.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem dens = new ToolStripMenuItem("底色浓度");
            float[] das = new float[] { 0f, 0.15f, 0.3f, 0.5f, 0.7f, 1f };
            for (int i = 0; i < das.Length; i++)
            {
                float v = das[i];
                dens.DropDownItems.Add(Check((int)(v * 100) + "%", Math.Abs(S.BgAlpha - v) < 0.03f,
                    delegate { S.BgAlpha = v; S.Save(); }));
            }
            bcM.DropDownItems.Add(dens);
            bcM.DropDownItems.Add(new ToolStripMenuItem("自定义颜色…", null, delegate { Defer(delegate { PickColor(false); }); }));
            m.Items.Add(bcM);

            ToolStripMenuItem opM = new ToolStripMenuItem("整体不透明度");
            float[] ops = new float[] { 0.35f, 0.5f, 0.65f, 0.8f, 0.9f, 1.0f };
            for (int i = 0; i < ops.Length; i++)
            {
                float v = ops[i];
                opM.DropDownItems.Add(Check(((int)(v * 100)).ToString() + "%", Math.Abs(S.Opacity - v) < 0.02f,
                    delegate { S.Opacity = v; S.Save(); }));
            }
            m.Items.Add(opM);

            m.Items.Add(Check("文字投影（更易读）", S.TextShadow, delegate
            {
                S.TextShadow = !S.TextShadow;
                view.Shadow = S.TextShadow;
                S.Save();
            }));

            m.Items.Add(new ToolStripSeparator());

            // ---- 动画 ----
            string[] rn = new string[] { "瞬间显示", "逐行浮现", "逐字浮现" };
            ToolStripMenuItem rvM = new ToolStripMenuItem("浮现方式");
            for (int i = 0; i < rn.Length; i++)
            {
                int v = i;
                rvM.DropDownItems.Add(Check(rn[i], S.RevealMode == i, delegate
                {
                    S.RevealMode = v;
                    view.SetRevealMode(v);
                    if (wantShow) view.OnShownNow();
                    S.Save();
                }));
            }
            m.Items.Add(rvM);

            string[] rsp = new string[] { "很慢", "慢", "标准", "快", "很快", "极快" };
            float[] rspv = new float[] { 0.4f, 0.7f, 1f, 1.6f, 2.4f, 4f };
            ToolStripMenuItem rsm = new ToolStripMenuItem("浮现速度");
            for (int i = 0; i < rspv.Length; i++)
            {
                float v = rspv[i];
                rsm.DropDownItems.Add(Check(rsp[i], Math.Abs(S.RevealSpeed - v) < 0.03f,
                    delegate { S.RevealSpeed = v; view.RevealSpeed = v; S.Save(); if (wantShow) view.OnShownNow(); }));
            }
            m.Items.Add(rsm);

            string[] scn = new string[] { "很慢", "慢", "标准", "快", "很快", "极快" };
            float[] scv = new float[] { 8f, 14f, 22f, 34f, 50f, 76f };
            ToolStripMenuItem scM = new ToolStripMenuItem("滑动速度");
            for (int i = 0; i < scv.Length; i++)
            {
                float v = scv[i];
                scM.DropDownItems.Add(Check(scn[i] + "  " + v.ToString("0") + " px/s", Math.Abs(S.ScrollSpeed - v) < 0.3f,
                    delegate { S.ScrollSpeed = v; view.ScrollSpeed = v; S.Save(); }));
            }
            m.Items.Add(scM);

            m.Items.Add(Check("自动滑动（悬停时）", S.AutoScroll, delegate
            {
                S.AutoScroll = !S.AutoScroll;
                view.AutoScroll = S.AutoScroll;
                S.Save();
            }));

            m.Items.Add(new ToolStripSeparator());

            // ---- 窗口 ----
            m.Items.Add(Check("窗口置顶", S.TopMost, delegate { S.TopMost = !S.TopMost; TopMost = S.TopMost; S.Save(); }));
            m.Items.Add(Check("悬停才显示（否则隐藏）", S.HoverHide, delegate
            {
                S.HoverHide = !S.HoverHide;
                if (!S.HoverHide) { wantShow = true; fade = 1f; if (!Visible) Show(); }
                S.Save();
            }));
            m.Items.Add(Check("边缘渐隐", S.EdgeFade, delegate { S.EdgeFade = !S.EdgeFade; view.EdgeFade = S.EdgeFade; S.Save(); }));
            m.Items.Add(Check("首行缩进", S.IndentFirst, delegate { S.IndentFirst = !S.IndentFirst; view.IndentFirst = S.IndentFirst; view.RelayoutInternal(false); S.Save(); }));
            m.Items.Add(Check("锁定位置/大小", S.Locked, delegate { S.Locked = !S.Locked; view.AllowResize = !S.Locked; S.Save(); }));
            m.Items.Add(Check("鼠标穿透（点击到下层窗口）", S.MouseThrough, delegate
            {
                S.MouseThrough = !S.MouseThrough;
                Overlay.SetClickThrough(Handle, S.MouseThrough);
                S.Save();
            }));

            ToolStripMenuItem szM = new ToolStripMenuItem("窗口尺寸");
            szM.DropDownItems.Add(new ToolStripMenuItem("极小 90×52（一行两三字）", null, delegate { PresetSize(90, 52); }));
            szM.DropDownItems.Add(new ToolStripMenuItem("单行 220×48", null, delegate { PresetSize(220, 48); }));
            szM.DropDownItems.Add(new ToolStripMenuItem("窄条 260×全高", null, delegate { PresetSize(260, Screen.FromControl(this).WorkingArea.Height - 120); }));
            szM.DropDownItems.Add(new ToolStripMenuItem("标准 330×240", null, delegate { PresetSize(330, 240); }));
            szM.DropDownItems.Add(new ToolStripMenuItem("宽幅 560×420", null, delegate { PresetSize(560, 420); }));
            szM.DropDownItems.Add(new ToolStripSeparator());
            szM.DropDownItems.Add(new ToolStripMenuItem("更宽 +40", null, delegate { ResizeBy(40, 0); }));
            szM.DropDownItems.Add(new ToolStripMenuItem("更窄 -40", null, delegate { ResizeBy(-40, 0); }));
            szM.DropDownItems.Add(new ToolStripMenuItem("更高 +60", null, delegate { ResizeBy(0, 60); }));
            szM.DropDownItems.Add(new ToolStripMenuItem("更矮 -60", null, delegate { ResizeBy(0, -60); }));
            szM.DropDownItems.Add(new ToolStripSeparator());
            szM.DropDownItems.Add(new ToolStripMenuItem("重置到屏幕右侧", null, delegate { ResetPosition(); }));
            m.Items.Add(szM);

            m.Items.Add(new ToolStripMenuItem("回到本章开头", null, delegate { view.SetScrollFraction(0f); }));
            m.Items.Add(new ToolStripMenuItem("隐藏（Esc）", null, delegate { wantShow = false; hideAt = 0; }));

            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("退出", null, delegate { forceQuit = true; Close(); }));

            Tint(m.Items, Color.FromArgb(230, 232, 238));
            return m;
        }

        void AddTextColorItem(ToolStripMenuItem parent, string name, Color c)
        {
            bool on = !S.AutoColor && SameColor(S.TextColor, c);
            ToolStripMenuItem it = new ToolStripMenuItem((on ? "✓ " : "    ") + name);
            it.ForeColor = c;
            it.Click += delegate
            {
                S.AutoColor = false;
                S.TextColor = c;
                ApplyManualColor();
                S.Save();
            };
            parent.DropDownItems.Add(it);
        }

        void AddBgItem(ToolStripMenuItem parent, string name, Color c, float alpha)
        {
            bool on = S.BgAlpha > 0.004f && SameColor(S.BgColor, c) && Math.Abs(S.BgAlpha - alpha) < 0.05f;
            ToolStripMenuItem it = new ToolStripMenuItem((on ? "✓ " : "    ") + name);
            it.Click += delegate
            {
                S.BgColor = c;
                S.BgAlpha = alpha;
                S.Save();
            };
            parent.DropDownItems.Add(it);
        }

        static void Tint(ToolStripItemCollection items, Color c)
        {
            foreach (ToolStripItem it in items)
            {
                it.ForeColor = c;
                ToolStripMenuItem mi = it as ToolStripMenuItem;
                if (mi != null && mi.DropDownItems.Count > 0) Tint(mi.DropDownItems, c);
            }
        }

        void Defer(MethodInvoker act)
        {
            BeginInvoke(act);
        }

        void ResizeBy(int dw, int dh)
        {
            int w = Math.Max(24, Width + dw);
            int h = Math.Max(16, Height + dh);
            Bounds = new Rectangle(Left, Top, w, h);
            SaveProgress();
        }

        void PresetSize(int w, int h)
        {
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            if (h > wa.Height - 40) h = wa.Height - 40;
            int x = Math.Min(Left, wa.Right - w - 10);
            int y = Math.Min(Top, wa.Bottom - h - 10);
            Bounds = new Rectangle(Math.Max(wa.Left, x), Math.Max(wa.Top, y), w, h);
            SaveProgress();
            RefreshAutoColor();
            Render();
        }

        void ResetPosition()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Bounds = new Rectangle(wa.Right - Width - 60, wa.Top + 80, Width, Height);
            SaveProgress();
        }

        // ================= 对话框 =================
        void DoOpen()
        {
            suppressHide = true;
            try
            {
                using (OpenFileDialog d = new OpenFileDialog())
                {
                    d.Title = "选择电子书";
                    d.Filter = "电子书 (*.txt;*.epub;*.mobi;*.azw;*.azw3)|*.txt;*.epub;*.mobi;*.azw;*.azw3;*.prc|" +
                               "文本文件 (*.txt)|*.txt|EPUB (*.epub)|*.epub|" +
                               "Kindle (*.mobi;*.azw;*.azw3)|*.mobi;*.azw;*.azw3|所有文件 (*.*)|*.*";
                    if (book != null)
                    {
                        try { d.InitialDirectory = Path.GetDirectoryName(book.Path); }
                        catch { }
                    }
                    if (d.ShowDialog(this) == DialogResult.OK) OpenBook(d.FileName, false);
                }
            }
            finally
            {
                suppressHide = false;
                hideAt = Environment.TickCount;
            }
        }

        void DoFont()
        {
            suppressHide = true;
            try
            {
                using (FontDialog fd = new FontDialog())
                {
                    fd.FontMustExist = true;
                    fd.ShowEffects = false;
                    fd.MinSize = 8;
                    fd.MaxSize = 80;
                    fd.Font = view.TextFont;
                    if (fd.ShowDialog(this) == DialogResult.OK)
                    {
                        S.FontName = fd.Font.Name;
                        S.FontSize = fd.Font.Size;
                        ApplyFont();
                        S.Save();
                    }
                }
            }
            finally
            {
                suppressHide = false;
                hideAt = Environment.TickCount;
            }
        }

        void PickColor(bool isText)
        {
            suppressHide = true;
            try
            {
                using (ColorDialog cd = new ColorDialog())
                {
                    cd.FullOpen = true;
                    cd.Color = isText ? S.TextColor : S.BgColor;
                    if (cd.ShowDialog(this) == DialogResult.OK)
                    {
                        Color c = Color.FromArgb(255, cd.Color.R, cd.Color.G, cd.Color.B);
                        if (isText)
                        {
                            S.AutoColor = false;
                            S.TextColor = c;
                            ApplyManualColor();
                        }
                        else
                        {
                            S.BgColor = c;
                            if (S.BgAlpha < 0.02f) S.BgAlpha = 0.55f;
                        }
                        S.Save();
                    }
                }
            }
            finally
            {
                suppressHide = false;
                hideAt = Environment.TickCount;
            }
        }

        void ShowChapterPicker()
        {
            if (book == null) return;
            suppressHide = true;
            List<string> titles = new List<string>();
            for (int i = 0; i < book.Chapters.Count; i++)
                titles.Add((i + 1) + ". " + book.Chapters[i].Title);
            using (PickerForm p = new PickerForm(titles, curChapter))
            {
                p.ShowDialog(this);
                if (p.Result >= 0 && p.Result != curChapter) GotoChapter(p.Result);
            }
            suppressHide = false;
            if (wantShow) view.OnShownNow();
        }

        // ================= 键盘 =================
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                if (S.HoverHide) { wantShow = false; hideAt = 0; }
                return true;
            }
            if (keyData == (Keys.Control | Keys.O)) { Defer(DoOpen); return true; }
            if (keyData == (Keys.Control | Keys.Oemplus) || keyData == (Keys.Control | Keys.Add))
            {
                S.FontSize = Math.Min(90f, S.FontSize + 2f); ApplyFont(); S.Save(); return true;
            }
            if (keyData == (Keys.Control | Keys.OemMinus) || keyData == (Keys.Control | Keys.Subtract))
            {
                S.FontSize = Math.Max(8f, S.FontSize - 2f); ApplyFont(); S.Save(); return true;
            }
            if (keyData == Keys.Space) { view.ManualPause = !view.ManualPause; return true; }
            if (keyData == Keys.PageDown) { view.ScrollBy(view.VisibleHeight * 0.85f); return true; }
            if (keyData == Keys.PageUp) { view.ScrollBy(-view.VisibleHeight * 0.85f); return true; }
            if (keyData == Keys.Right) { GotoChapter(curChapter + 1); return true; }
            if (keyData == Keys.Left) { GotoChapter(curChapter - 1); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (firstRun) showUntil = Environment.TickCount + 7000;

            if (showUntil != 0 || !S.HoverHide || MouseInZone())
            {
                wantShow = true;
                fade = 1f;
                RefreshAutoColor();
                view.OnShownNow();
                Render();
            }
            else
            {
                wantShow = false;
                fade = 0f;
                Hide();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveProgress();
            timer.Stop();
            base.OnFormClosing(e);
        }
    }

    // ============================================================
    // 深色菜单主题
    // ============================================================
    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(32, 34, 40); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(32, 34, 40); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(32, 34, 40); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(32, 34, 40); } }
        public override Color MenuBorder { get { return Color.FromArgb(72, 76, 88); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(70, 96, 140); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(52, 58, 70); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(52, 58, 70); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(52, 58, 70); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(60, 66, 80); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(60, 66, 80); } }
        public override Color SeparatorDark { get { return Color.FromArgb(56, 60, 70); } }
        public override Color SeparatorLight { get { return Color.FromArgb(56, 60, 70); } }
        public override Color CheckBackground { get { return Color.FromArgb(70, 96, 140); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(84, 112, 160); } }
        public override Color CheckPressedBackground { get { return Color.FromArgb(84, 112, 160); } }
    }

    // ============================================================
    // 章节选择器
    // ============================================================
    public class PickerForm : Form
    {
        public int Result = -1;
        TextBox filter;
        ListBox list;
        List<string> all;
        List<int> map = new List<int>();
        int current;

        public PickerForm(List<string> items, int cur)
        {
            all = items;
            current = cur;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(28, 30, 36);
            Size = new Size(340, 460);
            TopMost = true;
            KeyPreview = true;
            Padding = new Padding(1);

            filter = new TextBox();
            filter.BorderStyle = BorderStyle.FixedSingle;
            filter.BackColor = Color.FromArgb(44, 47, 56);
            filter.ForeColor = Color.FromArgb(230, 232, 238);
            filter.Font = new Font("Microsoft YaHei", 10f);
            filter.SetBounds(10, 10, 320, 26);
            filter.TextChanged += delegate { Refill(); };
            filter.KeyDown += delegate(object o, KeyEventArgs ke)
            {
                if (ke.KeyCode == Keys.Enter) { Accept(); ke.Handled = true; ke.SuppressKeyPress = true; }
                else if (ke.KeyCode == Keys.Down) { if (list.Items.Count > 0) { list.Focus(); if (list.SelectedIndex < 0) list.SelectedIndex = 0; } ke.Handled = true; }
                else if (ke.KeyCode == Keys.Escape) { Close(); ke.Handled = true; }
            };
            Controls.Add(filter);

            list = new ListBox();
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Color.FromArgb(28, 30, 36);
            list.ForeColor = Color.FromArgb(220, 222, 228);
            list.Font = new Font("Microsoft YaHei", 9.5f);
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = 24;
            list.SetBounds(10, 44, 320, 396);
            list.DrawItem += ListDrawItem;
            list.DoubleClick += delegate { Accept(); };
            list.KeyDown += ListKeyDown;
            Controls.Add(list);

            Refill();
            if (list.Items.Count > 0) list.SelectedIndex = 0;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            filter.Focus();
            filter.SelectAll();
        }

        void ListDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush b = new SolidBrush(sel ? Color.FromArgb(58, 78, 112) : Color.FromArgb(28, 30, 36)))
                e.Graphics.FillRectangle(b, e.Bounds);
            int realIdx = e.Index < map.Count ? map[e.Index] : -1;
            Color c = Color.FromArgb(220, 222, 228);
            if (realIdx == current) c = Color.FromArgb(120, 200, 255);
            using (SolidBrush b = new SolidBrush(c))
            {
                string s = list.Items[e.Index].ToString();
                if (s.Length > 34) s = s.Substring(0, 34) + "…";
                e.Graphics.DrawString(s, list.Font, b, e.Bounds.X + 6, e.Bounds.Y + 5);
            }
        }

        void Refill()
        {
            string q = filter.Text.Trim();
            list.BeginUpdate();
            list.Items.Clear();
            map.Clear();
            for (int i = 0; i < all.Count; i++)
            {
                if (q.Length == 0 || all[i].IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    list.Items.Add(all[i]);
                    map.Add(i);
                }
            }
            list.EndUpdate();
            if (list.Items.Count > 0)
            {
                list.SelectedIndex = 0;
                for (int i = 0; i < map.Count; i++)
                    if (map[i] == current) { list.SelectedIndex = i; break; }
            }
        }

        void Accept()
        {
            int i = list.SelectedIndex;
            if (i >= 0 && i < map.Count) { Result = map[i]; Close(); }
        }

        void ListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { Accept(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Color.FromArgb(76, 80, 92)))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }
}
