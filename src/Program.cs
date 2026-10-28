using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace NovelOverlay
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
                {
                    Report(e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
                {
                    Report(e.ExceptionObject as Exception);
                };

                string file = null;
                if (args != null)
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i].StartsWith("-")) continue;
                        if (File.Exists(args[i])) { file = args[i]; break; }
                    }
                }

                if (!File.Exists(AppSettings.ConfigPath))
                {
                    MessageBox.Show(
                        "NovelOverlay 已启动。\n\n" +
                        "· 窗口是完全透明的，只会浮出文字（无边框、无标题栏、无底色）\n" +
                        "· 文字颜色会自动跟随它背后的屏幕内容，亮处用深色、暗处用浅色\n" +
                        "· 把鼠标移到那块区域文字就浮现，移开自动消失，位置停住不动\n" +
                        "· 左键拖动位置，拖四边改大小（最小能缩到只显示一行两三个字）\n" +
                        "· 滚轮滚动，双击暂停；右键菜单里可导书、改字号颜色速度、置顶…\n\n" +
                        "找不到窗口时，鼠标在屏幕右侧上下扫一下即可。",
                        "NovelOverlay · 首次使用",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                Application.Run(new MainForm(file));
            }
            catch (Exception ex)
            {
                Report(ex);
            }
        }

        static void Report(Exception ex)
        {
            if (ex == null) return;
            try
            {
                string p = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "error.log");
                File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + ex + "\r\n\r\n", Encoding.UTF8);
            }
            catch { }
            try
            {
                MessageBox.Show(ex.Message, "NovelOverlay 出错", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
