using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace NovelOverlay
{
    public class Chapter
    {
        public string Title = "";
        public string Text = "";
    }

    public class Book
    {
        public string Path = "";
        public string Title = "";
        public List<Chapter> Chapters = new List<Chapter>();
    }

    /// <summary>电子书解析：txt / epub / mobi(azw) / html，全部零外部依赖</summary>
    public static class BookLoader
    {
        public static Book Load(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            Book b;
            if (ext == ".epub") b = LoadEpub(path);
            else if (ext == ".mobi" || ext == ".azw" || ext == ".azw3" || ext == ".prc") b = LoadMobi(path);
            else if (ext == ".html" || ext == ".htm" || ext == ".xhtml")
            {
                b = new Book();
                b.Chapters.Add(new Chapter { Title = "正文", Text = HtmlToText(File.ReadAllText(path)) });
            }
            else b = LoadTxt(path);

            b.Path = path;
            if (b.Title.Length == 0) b.Title = System.IO.Path.GetFileNameWithoutExtension(path);
            for (int i = 0; i < b.Chapters.Count; i++)
                if (b.Chapters[i].Title.Length == 0) b.Chapters[i].Title = "第 " + (i + 1) + " 节";
            if (b.Chapters.Count == 0) b.Chapters.Add(new Chapter { Title = "正文", Text = "" });
            return b;
        }

        // =====================================================================
        // TXT
        // =====================================================================
        static Book LoadTxt(string path)
        {
            string text = ReadTextSmart(path);
            Book b = new Book();
            b.Chapters = SplitChapters(text);
            return b;
        }

        /// <summary>编码识别：BOM → 严格 UTF8 → GBK(936) → 系统默认</summary>
        public static string ReadTextSmart(string path)
        {
            byte[] d = File.ReadAllBytes(path);
            if (d.Length >= 3 && d[0] == 0xEF && d[1] == 0xBB && d[2] == 0xBF)
                return Encoding.UTF8.GetString(d, 3, d.Length - 3);
            if (d.Length >= 2 && d[0] == 0xFF && d[1] == 0xFE)
                return Encoding.Unicode.GetString(d, 2, d.Length - 2);
            if (d.Length >= 2 && d[0] == 0xFE && d[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(d, 2, d.Length - 2);
            try
            {
                UTF8Encoding strict = new UTF8Encoding(false, true);
                return strict.GetString(d);
            }
            catch { }
            try { return Encoding.GetEncoding(936).GetString(d); }
            catch { }
            try { return Encoding.GetEncoding("GB18030").GetString(d); }
            catch { }
            return Encoding.Default.GetString(d);
        }

        static readonly Regex ChapterRe = new Regex(
            @"^\s*(?:第\s*[0-9０-９零一二三四五六七八九十百千万两]{1,12}\s*[章节回卷部篇集话幕]|" +
            @"Chapter\s+[0-9IVXLC]+|CHAPTER\s+[0-9IVXLC]+|" +
            @"(?:楔子|序章|序言|序|前言|引子|后记|尾声|终章|番外|正文))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<Chapter> SplitChapters(string text)
        {
            List<Chapter> list = new List<Chapter>();
            string t = Normalize(text);
            string[] lines = t.Split('\n');

            List<int> marks = new List<int>();
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i].Trim();
                if (s.Length < 2 || s.Length > 34) continue;
                if (s.IndexOf('。') >= 0 || s.IndexOf('，') >= 0) continue;
                if (ChapterRe.IsMatch(s)) marks.Add(i);
            }

            if (marks.Count < 2)
            {
                list.Add(new Chapter { Title = "正文", Text = t });
                return list;
            }

            // 第一个标记之前的序言
            if (marks[0] > 3)
            {
                StringBuilder pre = new StringBuilder();
                for (int i = 0; i < marks[0]; i++) pre.AppendLine(lines[i]);
                if (pre.ToString().Trim().Length > 0)
                    list.Add(new Chapter { Title = "开篇", Text = pre.ToString().Trim() });
            }

            for (int m = 0; m < marks.Count; m++)
            {
                int start = marks[m];
                int end = (m + 1 < marks.Count) ? marks[m + 1] : lines.Length;
                StringBuilder sb = new StringBuilder();
                for (int i = start + 1; i < end; i++) sb.AppendLine(lines[i]);
                Chapter c = new Chapter();
                c.Title = lines[start].Trim();
                c.Text = sb.ToString().Trim();
                if (c.Text.Length > 0) list.Add(c);
            }
            if (list.Count == 0) list.Add(new Chapter { Title = "正文", Text = t });
            return list;
        }

        static string Normalize(string s)
        {
            s = s.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ").Replace('\u3000', ' ');
            s = s.Replace("\u00A0", " ");
            s = Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.Trim();
        }

        // =====================================================================
        // HTML → 纯文本
        // =====================================================================
        public static string HtmlToText(string html)
        {
            if (html == null) return "";
            html = Regex.Replace(html, @"(?is)<!--.*?-->", "");
            html = Regex.Replace(html, @"(?is)<(script|style|head|title)[^>]*>.*?</\1>", "");
            html = Regex.Replace(html, @"(?i)<br\s*/?>", "\n");
            html = Regex.Replace(html, @"(?i)<(?:hr)[^>]*>", "\n\n");
            html = Regex.Replace(html, @"(?i)</(?:p|div|h[1-6]|li|tr|blockquote|section|article|dd|dt|pre)>", "\n");
            html = Regex.Replace(html, @"(?i)<(?:p|div|h[1-6]|li|tr|blockquote|section|article|dd|dt|pre)(\s[^>]*)?>", "\n");
            html = Regex.Replace(html, @"<[^>]+>", "");
            html = DecodeEntities(html);
            html = html.Replace('\u3000', ' ');
            html = Regex.Replace(html, @"[ \t\u00A0]+", " ");
            html = Regex.Replace(html, @" *\n *", "\n");
            html = Regex.Replace(html, @"\n{3,}", "\n\n");
            return html.Trim();
        }

        static string DecodeEntities(string s)
        {
            s = s.Replace("&nbsp;", " ").Replace("&lt;", "<").Replace("&gt;", ">")
                 .Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&mdash;", "—")
                 .Replace("&ldquo;", "“").Replace("&rdquo;", "”").Replace("&hellip;", "…")
                 .Replace("&amp;", "&");
            s = Regex.Replace(s, @"&#x([0-9a-fA-F]{1,6});", delegate(Match m)
            {
                try { return ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString(); }
                catch { return ""; }
            });
            s = Regex.Replace(s, @"&#([0-9]{1,7});", delegate(Match m)
            {
                try { return ((char)Convert.ToInt32(m.Groups[1].Value)).ToString(); }
                catch { return ""; }
            });
            return s;
        }

        // =====================================================================
        // EPUB
        // =====================================================================
        static Book LoadEpub(string path)
        {
            Book book = new Book();
            ZipReader zip = new ZipReader(File.ReadAllBytes(path));

            string opfPath = null;
            string container = zip.ReadText("META-INF/container.xml");
            if (container != null)
            {
                Match m = Regex.Match(container, "full-path\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                if (m.Success) opfPath = m.Groups[1].Value;
            }
            if (opfPath == null)
            {
                foreach (string n in zip.Names)
                    if (n.ToLowerInvariant().EndsWith(".opf")) { opfPath = n; break; }
            }
            if (opfPath == null) throw new Exception("不是有效的 EPUB：找不到 OPF 文件");

            string opf = zip.ReadText(opfPath);
            if (opf == null) throw new Exception("EPUB 内容损坏：无法读取 OPF");
            string baseDir = DirOf(opfPath);

            Match tm = Regex.Match(opf, @"<dc:title[^>]*>(.*?)</dc:title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (tm.Success) book.Title = HtmlToText(tm.Groups[1].Value);

            Dictionary<string, string> manifest = new Dictionary<string, string>();
            foreach (Match m in Regex.Matches(opf, @"<item\b[^>]*>", RegexOptions.IgnoreCase))
            {
                string tag = m.Value;
                string id = Attr(tag, "id");
                string href = Attr(tag, "href");
                if (id != null && href != null) manifest[id] = href;
            }

            List<string> spine = new List<string>();
            foreach (Match m in Regex.Matches(opf, @"<itemref\b[^>]*>", RegexOptions.IgnoreCase))
            {
                string idref = Attr(m.Value, "idref");
                if (idref != null && manifest.ContainsKey(idref)) spine.Add(manifest[idref]);
            }
            if (spine.Count == 0)
            {
                foreach (KeyValuePair<string, string> kv in manifest)
                    if (kv.Value.ToLowerInvariant().EndsWith(".html") || kv.Value.ToLowerInvariant().EndsWith(".xhtml") || kv.Value.ToLowerInvariant().EndsWith(".htm"))
                        spine.Add(kv.Value);
            }

            foreach (string href in spine)
            {
                string full = CombinePath(baseDir, href);
                string html = zip.ReadText(full);
                if (html == null) continue;
                string title = "";
                Match h = Regex.Match(html, @"(?is)<h[1-3][^>]*>(.*?)</h[1-3]>");
                if (h.Success) title = HtmlToText(h.Groups[1].Value).Replace("\n", " ").Trim();
                if (title.Length == 0)
                {
                    Match t2 = Regex.Match(html, @"(?is)<title[^>]*>(.*?)</title>");
                    if (t2.Success) title = HtmlToText(t2.Groups[1].Value).Trim();
                }
                string body = HtmlToText(html);
                if (body.Length < 2) continue;
                if (title.Length > 40) title = title.Substring(0, 40);
                Chapter c = new Chapter();
                c.Title = title;
                c.Text = body;
                book.Chapters.Add(c);
            }
            if (book.Chapters.Count == 0) throw new Exception("EPUB 中没有可读文本");
            return book;
        }

        static string Attr(string tag, string name)
        {
            Match m = Regex.Match(tag, "\\b" + name + "\\s*=\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
            if (!m.Success) m = Regex.Match(tag, "\\b" + name + "\\s*=\\s*'([^']*)'", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        static string DirOf(string p)
        {
            int i = p.LastIndexOf('/');
            return i < 0 ? "" : p.Substring(0, i + 1);
        }

        static string CombinePath(string baseDir, string href)
        {
            href = Uri.UnescapeDataString(href);
            if (href.IndexOf("://") > 0) return href; // 外链，直接忽略
            int hash = href.IndexOf('#');
            if (hash >= 0) href = href.Substring(0, hash);
            string full = href.StartsWith("/") ? href.Substring(1) : baseDir + href;
            List<string> parts = new List<string>();
            foreach (string s in full.Split('/'))
            {
                if (s.Length == 0 || s == ".") continue;
                if (s == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); continue; }
                parts.Add(s);
            }
            return string.Join("/", parts.ToArray());
        }

        // =====================================================================
        // MOBI / AZW (PalmDB + PalmDOC LZ77)
        // =====================================================================
        static Book LoadMobi(string path)
        {
            byte[] d = File.ReadAllBytes(path);
            if (d.Length < 200) throw new Exception("文件太小，不是有效的 MOBI");

            int numRec = BE16(d, 76);
            if (numRec < 2) throw new Exception("不是有效的 MOBI：记录数异常");
            long[] offs = new long[numRec + 1];
            for (int i = 0; i < numRec; i++) offs[i] = BE32(d, 78 + i * 8);
            offs[numRec] = d.Length;

            int r0 = (int)offs[0];
            int compression = BE16(d, r0);
            long textLen = BE32(d, r0 + 4);
            int textRecs = BE16(d, r0 + 8);

            bool isMobi = r0 + 132 < d.Length &&
                          Encoding.ASCII.GetString(d, r0 + 16, 4) == "MOBI";
            int encoding = 1252;
            if (isMobi) encoding = (int)BE32(d, r0 + 28);

            if (compression != 1 && compression != 2)
                throw new Exception("该 MOBI 使用了 HUFF/CDIC 压缩（多见于 azw3），暂不支持。建议转换为 epub 或 txt。");

            MemoryStream ms = new MemoryStream();
            for (int i = 1; i <= textRecs && i < numRec; i++)
            {
                byte[] rec = Slice(d, (int)offs[i], (int)(offs[i + 1] - offs[i]));
                byte[] outRec = (compression == 1) ? rec : PalmDocDecompress(rec);
                ms.Write(outRec, 0, outRec.Length);
            }
            byte[] all = ms.ToArray();
            int realLen = (int)Math.Min(textLen > 0 ? textLen : all.Length, all.Length);
            if (realLen <= 0) throw new Exception("MOBI 文本为空");

            Encoding enc;
            try { enc = Encoding.GetEncoding(encoding); }
            catch { enc = Encoding.UTF8; }
            if (encoding == 65001) enc = Encoding.UTF8;

            string raw = enc.GetString(all, 0, realLen);
            string text = HtmlToText(raw);

            // 按 <mbp:pagebreak> 或章节正则切分
            Book b = new Book();
            if (isMobi && r0 + 92 < d.Length)
            {
                int nameOff = (int)BE32(d, r0 + 84);
                int nameLen = (int)BE32(d, r0 + 88);
                int nameAbs = r0 + nameOff;          // 全名相对 record0 起始
                if (nameLen > 0 && nameOff > 0 && nameAbs + nameLen <= d.Length)
                {
                    try { b.Title = enc.GetString(d, nameAbs, nameLen).Trim(); }
                    catch { }
                }
            }
            b.Chapters = SplitChapters(text);
            return b;
        }

        static byte[] Slice(byte[] d, int off, int len)
        {
            if (off < 0) off = 0;
            if (off + len > d.Length) len = d.Length - off;
            if (len < 0) len = 0;
            byte[] r = new byte[len];
            Buffer.BlockCopy(d, off, r, 0, len);
            return r;
        }

        /// <summary>PalmDOC LZ77 解压</summary>
        static byte[] PalmDocDecompress(byte[] input)
        {
            int cap = input.Length * 4 + 64;
            byte[] buf = new byte[cap];
            int n = 0;
            int i = 0;
            while (i < input.Length)
            {
                int c = input[i++];
                if (c >= 1 && c <= 8)
                {
                    for (int k = 0; k < c && i < input.Length; k++)
                    {
                        if (n >= buf.Length) Array.Resize(ref buf, buf.Length * 2);
                        buf[n++] = input[i++];
                    }
                }
                else if (c < 0x80)
                {
                    if (n >= buf.Length) Array.Resize(ref buf, buf.Length * 2);
                    buf[n++] = (byte)c;
                }
                else if (c >= 0xC0)
                {
                    if (n + 1 >= buf.Length) Array.Resize(ref buf, buf.Length * 2);
                    buf[n++] = (byte)' ';
                    buf[n++] = (byte)(c ^ 0x80);
                }
                else
                {
                    if (i >= input.Length) break;
                    int pair = (c << 8) | input[i++];
                    int dist = (pair >> 3) & 0x07FF;
                    int len = (pair & 0x07) + 3;
                    if (dist == 0 || dist > n) continue;
                    int src = n - dist;
                    for (int k = 0; k < len; k++)
                    {
                        if (n >= buf.Length) Array.Resize(ref buf, buf.Length * 2);
                        buf[n++] = buf[src + k];
                    }
                }
            }
            byte[] r = new byte[n];
            Buffer.BlockCopy(buf, 0, r, 0, n);
            return r;
        }

        static int BE16(byte[] d, int o)
        {
            if (o + 1 >= d.Length) return 0;
            return (d[o] << 8) | d[o + 1];
        }
        static long BE32(byte[] d, int o)
        {
            if (o + 3 >= d.Length) return 0;
            return ((long)d[o] << 24) | ((long)d[o + 1] << 16) | ((long)d[o + 2] << 8) | d[o + 3];
        }
    }

    // =========================================================================
    // 最小 ZIP 读取器（只用 DeflateStream，兼容 .NET 2.0+，不依赖 System.IO.Compression.ZipFile）
    // =========================================================================
    public class ZipEntry
    {
        public string Name;
        public int Method;
        public long CompressedSize;
        public long Size;
        public long LocalOffset;
    }

    public class ZipReader
    {
        byte[] data;
        Dictionary<string, ZipEntry> map = new Dictionary<string, ZipEntry>(StringComparer.OrdinalIgnoreCase);
        List<string> names = new List<string>();

        public List<string> Names { get { return names; } }

        public ZipReader(byte[] bytes)
        {
            data = bytes;
            int eocd = -1;
            int min = Math.Max(0, data.Length - 66000);
            for (int i = data.Length - 22; i >= min; i--)
            {
                if (data[i] == 0x50 && data[i + 1] == 0x4B && data[i + 2] == 0x05 && data[i + 3] == 0x06) { eocd = i; break; }
            }
            if (eocd < 0) throw new Exception("不是有效的 ZIP/EPUB 文件");
            int count = data[eocd + 10] | (data[eocd + 11] << 8);
            long cdOff = (long)data[eocd + 16] | ((long)data[eocd + 17] << 8) | ((long)data[eocd + 18] << 16) | ((long)data[eocd + 19] << 24);
            int p = (int)cdOff;
            for (int i = 0; i < count; i++)
            {
                if (p + 46 > data.Length) break;
                if (!(data[p] == 0x50 && data[p + 1] == 0x4B && data[p + 2] == 0x01 && data[p + 3] == 0x02)) break;
                ZipEntry e = new ZipEntry();
                e.Method = data[p + 10] | (data[p + 11] << 8);
                e.CompressedSize = (long)data[p + 20] | ((long)data[p + 21] << 8) | ((long)data[p + 22] << 16) | ((long)data[p + 23] << 24);
                e.Size = (long)data[p + 24] | ((long)data[p + 25] << 8) | ((long)data[p + 26] << 16) | ((long)data[p + 27] << 24);
                int nameLen = data[p + 28] | (data[p + 29] << 8);
                int extraLen = data[p + 30] | (data[p + 31] << 8);
                int commentLen = data[p + 32] | (data[p + 33] << 8);
                e.LocalOffset = (long)data[p + 42] | ((long)data[p + 43] << 8) | ((long)data[p + 44] << 16) | ((long)data[p + 45] << 24);
                e.Name = Encoding.UTF8.GetString(data, p + 46, nameLen);
                if (!map.ContainsKey(e.Name)) { map[e.Name] = e; names.Add(e.Name); }
                p += 46 + nameLen + extraLen + commentLen;
            }
            if (map.Count == 0) throw new Exception("ZIP 目录为空或损坏");
        }

        public byte[] ReadBytes(string name)
        {
            ZipEntry e;
            if (!map.TryGetValue(name, out e)) return null;
            int lo = (int)e.LocalOffset;
            if (lo + 30 > data.Length) return null;
            int nameLen = data[lo + 26] | (data[lo + 27] << 8);
            int extraLen = data[lo + 28] | (data[lo + 29] << 8);
            int start = lo + 30 + nameLen + extraLen;
            int len = (int)e.CompressedSize;
            if (start + len > data.Length) len = data.Length - start;
            if (len <= 0) return new byte[0];
            if (e.Method == 0)
            {
                byte[] raw = new byte[len];
                Buffer.BlockCopy(data, start, raw, 0, len);
                return raw;
            }
            if (e.Method == 8)
            {
                using (MemoryStream input = new MemoryStream(data, start, len))
                using (DeflateStream ds = new DeflateStream(input, CompressionMode.Decompress))
                {
                    MemoryStream outMs = new MemoryStream(e.Size > 0 ? (int)e.Size : len * 4);
                    byte[] buf = new byte[8192];
                    int n;
                    while ((n = ds.Read(buf, 0, buf.Length)) > 0) outMs.Write(buf, 0, n);
                    return outMs.ToArray();
                }
            }
            throw new Exception("不支持的 ZIP 压缩方式：" + e.Method);
        }

        public string ReadText(string name)
        {
            byte[] b = ReadBytes(name);
            if (b == null) return null;
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
                return Encoding.UTF8.GetString(b, 3, b.Length - 3);
            try { return new UTF8Encoding(false, true).GetString(b); }
            catch { }
            try { return Encoding.GetEncoding(936).GetString(b); }
            catch { }
            return Encoding.UTF8.GetString(b);
        }
    }
}
