using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace HuoXingWen
{
    /// <summary>
    /// 命令行模式，用于自动化回归测试：
    ///   HuoXingWen.exe -cli &lt;mode&gt; &lt;输入文件&gt; &lt;输出文件&gt;
    /// mode: hx（中文-&gt;火星文）、cn（火星文-&gt;中文，首个匹配）、
    ///       smart（火星文-&gt;中文，智能消歧）、ft（中文-&gt;繁体）、jt（繁体-&gt;简体）、
    ///       roundtrip（对整个对照表做往返自检，输出统计）
    /// 输入文件为 "-" 时读取标准输入。
    /// </summary>
    public static class TestCli
    {
        public static void Run(string[] args)
        {
            try
            {
                if (args.Length >= 4)
                {
                    string mode = args[1];
                    string text = args[2] == "-" ? Console.In.ReadToEnd() : File.ReadAllText(args[2], new UTF8Encoding(false));
                    string result;
                    switch (mode)
                    {
                        case "hx": result = Conv.ToMartian(text); break;
                        case "cn": result = Conv.ToSimplified(text); break;
                        case "smart": result = Conv.ToSimplifiedSmart(text); break;
                        case "ft": result = Conv.ToTraditional(text); break;
                        case "jt": result = Conv.TraditionalToSimplified(text); break;
                        case "roundtrip": result = RoundTrip(text); break;
                        default: throw new ArgumentException("未知模式：" + mode);
                    }
                    File.WriteAllText(args[3], result, new UTF8Encoding(false));
                    return;
                }
                Console.Error.WriteLine("用法: -cli <hx|cn|smart|ft|jt|roundtrip> <输入文件|-> <输出文件>");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("错误: " + ex.Message);
                Environment.ExitCode = 2;
            }
        }

        /// <summary>把输入当作“简体基准文本”，做 中文-&gt;火星文-&gt;中文 往返，输出逐行统计。</summary>
        private static string RoundTrip(string text)
        {
            StringBuilder sb = new StringBuilder();
            int total = 0, okPlain = 0, okSmart = 0;
            foreach (char c in text)
            {
                if (!Conv.IsChinese(c)) continue;
                string hx = Conv.ToMartian(c.ToString());
                total++;
                if (Conv.ToSimplified(hx) == c.ToString()) okPlain++;
                if (Conv.ToSimplifiedSmart(hx) == c.ToString()) okSmart++;
            }
            sb.AppendLine("表内汉字数\t" + total);
            sb.AppendLine("首个匹配还原正确\t" + okPlain);
            sb.AppendLine("智能消歧还原正确\t" + okSmart);
            sb.AppendLine("智能消歧准确率\t" + (total == 0 ? "0" : (okSmart * 100.0 / total).ToString("0.00", CultureInfo.InvariantCulture)) + "%");
            sb.AppendLine("首个匹配准确率\t" + (total == 0 ? "0" : (okPlain * 100.0 / total).ToString("0.00", CultureInfo.InvariantCulture)) + "%");
            return sb.ToString();
        }

#if CLI
        public static void Main(string[] args)
        {
            Run(args);
        }
#endif
    }
}
