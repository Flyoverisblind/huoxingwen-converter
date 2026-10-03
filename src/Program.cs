using System;
using System.Text;
using System.Windows.Forms;

namespace HuoXingWen
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // -cli 模式：供自动化测试调用，转换结果写入文件
            if (args != null && args.Length > 0 && args[0] == "-cli")
            {
                TestCli.Run(args);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
