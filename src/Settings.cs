using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace NovelOverlay
{
    /// <summary>全局设置 + 阅读进度持久化（exe 同目录 settings.ini，纯文本，无依赖）</summary>
    public class AppSettings
    {
        // ---- 外观 ----
        public string FontName = "Microsoft YaHei";
        public float FontSize = 17f;
        public Color TextColor = Color.FromArgb(255, 240, 242, 245);
        public Color BgColor = Color.FromArgb(255, 16, 18, 22);
        public float BgAlpha = 0f;            // 底色浓度：0 = 完全透明（默认）
        public bool AutoColor = true;         // 文字颜色自动跟随屏幕背景
        public bool TextShadow = true;        // 文字投影
        public float Opacity = 0.95f;         // 整体不透明度
        public float LineSpacing = 1.45f;
        public int PadX = 14;
        public int PadY = 10;
        public bool EdgeFade = true;
        public bool IndentFirst = true;

        // ---- 行为 ----
        public bool TopMost = true;
        public bool HoverHide = true;
        public bool AutoScroll = true;
        public float ScrollSpeed = 22f;      // 像素/秒
        public int RevealMode = 1;           // 0=瞬间 1=逐行 2=逐字
        public float RevealSpeed = 1f;       // 速度倍率（0.4 ~ 6）
        public bool Locked = false;
        public bool MouseThrough = false;    // 鼠标穿透
        public int HoverPad = 26;            // 隐藏时的"看不见的热区"外扩像素

        // ---- 窗口 ----
        public int CfgVer = 2;
        public int WinW = 330;
        public int WinH = 240;
        public int WinX = -1;
        public int WinY = -1;

        // ---- 阅读进度 ----
        public string LastFile = "";
        public int LastChapter = 0;
        public float LastScroll = 0f;
        public List<string> Recent = new List<string>();

        const string FileName = "settings.ini";

        public static string ConfigPath
        {
            get
            {
                string dir = Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath);
                return Path.Combine(dir, FileName);
            }
        }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            Dictionary<string, string> d = new Dictionary<string, string>();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    foreach (string raw in File.ReadAllLines(ConfigPath, Encoding.UTF8))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                        int k = line.IndexOf('=');
                        if (k <= 0) continue;
                        d[line.Substring(0, k).Trim()] = line.Substring(k + 1).Trim();
                    }
                }
            }
            catch { }

            s.FontName = GStr(d, "FontName", s.FontName);
            s.FontSize = GFloat(d, "FontSize", s.FontSize);
            s.TextColor = GColor(d, "TextColor", s.TextColor);
            s.BgColor = GColor(d, "BgColor", s.BgColor);
            s.BgAlpha = GFloat(d, "BgAlpha", s.BgAlpha);
            s.AutoColor = GBool(d, "AutoColor", s.AutoColor);
            s.TextShadow = GBool(d, "TextShadow", s.TextShadow);
            s.Opacity = GFloat(d, "Opacity", s.Opacity);
            s.LineSpacing = GFloat(d, "LineSpacing", s.LineSpacing);
            s.PadX = GInt(d, "PadX", s.PadX);
            s.PadY = GInt(d, "PadY", s.PadY);
            s.EdgeFade = GBool(d, "EdgeFade", s.EdgeFade);
            s.IndentFirst = GBool(d, "IndentFirst", s.IndentFirst);

            s.TopMost = GBool(d, "TopMost", s.TopMost);
            s.HoverHide = GBool(d, "HoverHide", s.HoverHide);
            s.AutoScroll = GBool(d, "AutoScroll", s.AutoScroll);
            s.ScrollSpeed = GFloat(d, "ScrollSpeed", s.ScrollSpeed);
            s.RevealMode = GInt(d, "RevealMode", s.RevealMode);
            s.RevealSpeed = GFloat(d, "RevealSpeed", s.RevealSpeed);
            s.Locked = GBool(d, "Locked", s.Locked);
            s.MouseThrough = GBool(d, "MouseThrough", s.MouseThrough);
            s.HoverPad = GInt(d, "HoverPad", s.HoverPad);

            s.WinW = GInt(d, "WinW", s.WinW);
            s.WinH = GInt(d, "WinH", s.WinH);
            s.WinX = GInt(d, "WinX", s.WinX);
            s.WinY = GInt(d, "WinY", s.WinY);

            // v2 迁移：老版本是深色实体大窗口，一次性地换成透明小窗
            if (GInt(d, "CfgVer", 1) < 2)
            {
                s.CfgVer = 2;
                s.WinW = 330;
                s.WinH = 240;
                s.BgAlpha = 0f;
                s.AutoColor = true;
                s.TextShadow = true;
                s.PadX = 14;
                s.PadY = 10;
                s.LineSpacing = 1.45f;
                s.Opacity = 0.95f;
            }
            else s.CfgVer = GInt(d, "CfgVer", 2);

            s.LastFile = GStr(d, "LastFile", "");
            s.LastChapter = GInt(d, "LastChapter", 0);
            s.LastScroll = GFloat(d, "LastScroll", 0f);

            string rec = GStr(d, "Recent", "");
            if (rec.Length > 0)
            {
                foreach (string p in rec.Split('|'))
                    if (p.Trim().Length > 0) s.Recent.Add(p.Trim());
            }
            return s;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# NovelOverlay 阅读器配置");
                sb.AppendLine("FontName=" + FontName);
                sb.AppendLine("FontSize=" + FontSize.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("TextColor=" + C2S(TextColor));
                sb.AppendLine("BgColor=" + C2S(BgColor));
                sb.AppendLine("BgAlpha=" + BgAlpha.ToString("0.##", CultureInfo.InvariantCulture));
                sb.AppendLine("AutoColor=" + (AutoColor ? 1 : 0));
                sb.AppendLine("TextShadow=" + (TextShadow ? 1 : 0));
                sb.AppendLine("Opacity=" + Opacity.ToString("0.00", CultureInfo.InvariantCulture));
                sb.AppendLine("LineSpacing=" + LineSpacing.ToString("0.00", CultureInfo.InvariantCulture));
                sb.AppendLine("PadX=" + PadX);
                sb.AppendLine("PadY=" + PadY);
                sb.AppendLine("EdgeFade=" + (EdgeFade ? 1 : 0));
                sb.AppendLine("IndentFirst=" + (IndentFirst ? 1 : 0));
                sb.AppendLine("TopMost=" + (TopMost ? 1 : 0));
                sb.AppendLine("HoverHide=" + (HoverHide ? 1 : 0));
                sb.AppendLine("AutoScroll=" + (AutoScroll ? 1 : 0));
                sb.AppendLine("ScrollSpeed=" + ScrollSpeed.ToString("0.##", CultureInfo.InvariantCulture));
                sb.AppendLine("RevealMode=" + RevealMode);
                sb.AppendLine("RevealSpeed=" + RevealSpeed.ToString("0.##", CultureInfo.InvariantCulture));
                sb.AppendLine("Locked=" + (Locked ? 1 : 0));
                sb.AppendLine("MouseThrough=" + (MouseThrough ? 1 : 0));
                sb.AppendLine("HoverPad=" + HoverPad);
                sb.AppendLine("CfgVer=" + CfgVer);
                sb.AppendLine("WinW=" + WinW);
                sb.AppendLine("WinH=" + WinH);
                sb.AppendLine("WinX=" + WinX);
                sb.AppendLine("WinY=" + WinY);
                sb.AppendLine("LastFile=" + LastFile);
                sb.AppendLine("LastChapter=" + LastChapter);
                sb.AppendLine("LastScroll=" + LastScroll.ToString("0.####", CultureInfo.InvariantCulture));
                StringBuilder r = new StringBuilder();
                for (int i = 0; i < Recent.Count && i < 8; i++)
                {
                    if (i > 0) r.Append("|");
                    r.Append(Recent[i]);
                }
                sb.AppendLine("Recent=" + r.ToString());
                File.WriteAllText(ConfigPath, sb.ToString(), new UTF8Encoding(true));
            }
            catch { }
        }

        public void PushRecent(string path)
        {
            Recent.RemoveAll(delegate(string x) { return string.Equals(x, path, StringComparison.OrdinalIgnoreCase); });
            Recent.Insert(0, path);
            while (Recent.Count > 8) Recent.RemoveAt(Recent.Count - 1);
        }

        // ---- 小工具 ----
        static string C2S(Color c) { return c.R + "," + c.G + "," + c.B; }

        static Color S2C(string s, Color def)
        {
            try
            {
                string[] p = s.Split(',');
                if (p.Length >= 3)
                    return Color.FromArgb(255, int.Parse(p[0].Trim()), int.Parse(p[1].Trim()), int.Parse(p[2].Trim()));
            }
            catch { }
            return def;
        }

        static string GStr(Dictionary<string, string> d, string k, string def)
        {
            string v;
            if (d.TryGetValue(k, out v) && v.Length > 0) return v;
            return def;
        }
        static int GInt(Dictionary<string, string> d, string k, int def)
        {
            string v; int r;
            if (d.TryGetValue(k, out v) && int.TryParse(v, out r)) return r;
            return def;
        }
        static float GFloat(Dictionary<string, string> d, string k, float def)
        {
            string v; float r;
            if (d.TryGetValue(k, out v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out r)) return r;
            return def;
        }
        static bool GBool(Dictionary<string, string> d, string k, bool def)
        {
            string v;
            if (d.TryGetValue(k, out v)) return v == "1" || v.ToLowerInvariant() == "true";
            return def;
        }
        static Color GColor(Dictionary<string, string> d, string k, Color def)
        {
            string v;
            if (d.TryGetValue(k, out v)) return S2C(v, def);
            return def;
        }
    }
}
