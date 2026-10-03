using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace HuoXingWen
{
    /// <summary>
    /// 火星文转换引擎。
    /// 简体/繁体/火星文三张对照表来自公开的火星文转换规则，索引一一对应。
    /// 简体 &lt;-&gt; 火星文、繁体 &lt;-&gt; 火星文均与常见在线工具保持一致；
    /// 火星文 -&gt; 中文额外提供“智能消歧”（字频 + 常用词组统计 + 动态规划），
    /// 解决同一火星文字对应多个汉字时的还原歧义。
    /// </summary>
    public static class Conv
    {
        private static string jt = "";   // 简体表
        private static string ft = "";   // 繁体表
        private static string hx = "";   // 火星文表

        private static readonly Dictionary<char, int> jtIdx = new Dictionary<char, int>();
        private static readonly Dictionary<char, int> ftIdx = new Dictionary<char, int>();
        private static readonly Dictionary<char, int> hxFirst = new Dictionary<char, int>();
        private static readonly Dictionary<char, List<char>> hxCand = new Dictionary<char, List<char>>();
        private static readonly Dictionary<char, List<char>> candCache = new Dictionary<char, List<char>>();

        private static Dictionary<char, float> charFreq;
        private static Dictionary<int, float> pairFreq;
        private static readonly object smartLock = new object();
        private const float WChar = 1f / 4f;
        private const float WPair = 1f / 2f;

        /// <summary>该字符在简体或繁体对照表中（即可以转成火星文）。</summary>
        public static bool IsChinese(char c) { return jtIdx.ContainsKey(c) || ftIdx.ContainsKey(c); }

        /// <summary>该字符在火星文对照表中（即可能是火星文写法）。</summary>
        public static bool IsMartian(char c) { return hxCand.ContainsKey(c); }

        /// <summary>该字符可以反查成中文（含繁体字与本身）。</summary>
        public static bool IsReverseKnown(char c) { return Candidates(c).Count > 0; }

        public static bool Ready { get; private set; }
        public static string LoadError { get; private set; }
        public static int TableSize { get { return jt.Length; } }

        static Conv()
        {
            try
            {
                Load();
                Ready = true;
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
            }
        }

        private static void Load()
        {
            string[] parts = ReadResourceText("dict.tsv").Replace("\r", "").TrimEnd('\n').Split('\n');
            if (parts.Length < 3) throw new InvalidDataException("对照表数据不完整");
            jt = parts[0];
            ft = parts[1];
            hx = parts[2];
            if (ft.Length != jt.Length || hx.Length != jt.Length) throw new InvalidDataException("对照表长度不一致");

            for (int i = 0; i < jt.Length; i++)
            {
                if (!jtIdx.ContainsKey(jt[i])) jtIdx[jt[i]] = i;
                if (!ftIdx.ContainsKey(ft[i])) ftIdx[ft[i]] = i;
                if (!hxFirst.ContainsKey(hx[i])) hxFirst[hx[i]] = i;

                List<char> list;
                if (!hxCand.TryGetValue(hx[i], out list))
                {
                    list = new List<char>();
                    hxCand[hx[i]] = list;
                }
                if (!list.Contains(jt[i])) list.Add(jt[i]);
            }
        }

        private static string ReadResourceText(string name)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string full = null;
            foreach (string n in asm.GetManifestResourceNames())
            {
                if (n == name || n.EndsWith("." + name, StringComparison.OrdinalIgnoreCase)) { full = n; break; }
            }
            if (full == null) throw new FileNotFoundException("找不到内嵌数据：" + name);
            using (Stream s = asm.GetManifestResourceStream(full))
            using (StreamReader r = new StreamReader(s, new UTF8Encoding(false), true))
                return r.ReadToEnd();
        }

        private static void EnsureSmart()
        {
            if (pairFreq != null) return;
            lock (smartLock)
            {
                if (pairFreq != null) return;
                Dictionary<char, float> cf = new Dictionary<char, float>();
                Dictionary<int, float> pf = new Dictionary<int, float>();
                Assembly asm = Assembly.GetExecutingAssembly();
                string full = null;
                foreach (string n in asm.GetManifestResourceNames())
                {
                    if (n == "smart.gz" || n.EndsWith(".smart.gz", StringComparison.OrdinalIgnoreCase)) { full = n; break; }
                }
                if (full == null) throw new FileNotFoundException("找不到内嵌数据：smart.gz");
                using (Stream raw = asm.GetManifestResourceStream(full))
                using (GZipStream gz = new GZipStream(raw, CompressionMode.Decompress))
                using (StreamReader r = new StreamReader(gz, new UTF8Encoding(false)))
                {
                    int mode = 0;
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        if (line.Length == 0) continue;
                        if (line == "#FREQ") { mode = 1; continue; }
                        if (line == "#PAIR") { mode = 2; continue; }
                        int t = line.IndexOf('\t');
                        if (t <= 0) continue;
                        if (mode == 1)
                        {
                            float v;
                            if (float.TryParse(line.Substring(t + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                                cf[line[0]] = (float)Math.Log(1.0 + v) * WChar;
                        }
                        else if (mode == 2)
                        {
                            if (line.Length < 3) continue;
                            float v;
                            if (float.TryParse(line.Substring(t + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                                pf[((int)line[0] << 16) | (int)line[1]] = (float)Math.Log(1.0 + v) * WPair;
                        }
                    }
                }
                charFreq = cf;
                pairFreq = pf;
            }
        }

        /// <summary>后台预加载智能消歧数据，避免首次转换出现延迟。</summary>
        public static void PreloadSmart()
        {
            try { EnsureSmart(); }
            catch { }
        }

        /// <summary>返回某个字符在“火星文/繁体 -> 简体”方向上的候选汉字，顺序与原站一致（繁体表 -&gt; 火星文表 -&gt; 原字）。</summary>
        public static List<char> Candidates(char ch)
        {
            List<char> list;
            if (candCache.TryGetValue(ch, out list)) return list;
            list = new List<char>(3);
            int i;
            if (ftIdx.TryGetValue(ch, out i)) list.Add(jt[i]);
            List<char> hc;
            if (hxCand.TryGetValue(ch, out hc))
            {
                for (int k = 0; k < hc.Count; k++) if (!list.Contains(hc[k])) list.Add(hc[k]);
            }
            if (jtIdx.ContainsKey(ch) && !list.Contains(ch)) list.Add(ch);
            candCache[ch] = list;
            return list;
        }

        // ---------- 与原站算法一致的方向 ----------

        /// <summary>简体/繁体 -&gt; 火星文</summary>
        public static string ToMartian(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                int k;
                if (jtIdx.TryGetValue(c, out k)) sb.Append(hx[k]);
                else if (ftIdx.TryGetValue(c, out k)) sb.Append(hx[k]);
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>火星文/繁体 -&gt; 简体（首个匹配，与在线工具结果一致）</summary>
        public static string ToSimplified(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                int k;
                if (ftIdx.TryGetValue(c, out k)) sb.Append(jt[k]);
                else
                {
                    List<char> hc;
                    if (hxCand.TryGetValue(c, out hc)) sb.Append(hc[0]);
                    else sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>简体/火星文 -&gt; 繁体</summary>
        public static string ToTraditional(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                int k;
                if (jtIdx.TryGetValue(c, out k)) sb.Append(ft[k]);
                else if (hxFirst.TryGetValue(c, out k)) sb.Append(ft[k]);
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>繁体 -&gt; 简体（保留火星文字符时使用，等价于 ToSimplified 的确定性版本）</summary>
        public static string TraditionalToSimplified(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                int k;
                if (ftIdx.TryGetValue(c, out k)) sb.Append(jt[k]);
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // ---------- 智能反查 ----------

        /// <summary>
        /// 火星文 -&gt; 简体（智能消歧）：对每个字取全部候选，用字频与常用词组统计打分，
        /// 通过动态规划求全局最优解。孤立单字退化为“最常用候选”。
        /// </summary>
        public static string ToSimplifiedSmart(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            EnsureSmart();
            int n = s.Length;

            List<char>[] cand = new List<char>[n];
            for (int i = 0; i < n; i++)
            {
                List<char> l = Candidates(s[i]);
                cand[i] = (l.Count > 0) ? l : One(s[i]);
            }

            float[] prev = new float[cand[0].Count];
            for (int j = 0; j < prev.Length; j++) prev[j] = Score(cand[0][j]);

            int[][] back = new int[n][];
            back[0] = new int[cand[0].Count];
            for (int j = 0; j < back[0].Length; j++) back[0][j] = -1;

            for (int i = 1; i < n; i++)
            {
                List<char> cur = cand[i];
                List<char> pre = cand[i - 1];
                float[] now = new float[cur.Count];
                int[] bk = new int[cur.Count];
                for (int j = 0; j < cur.Count; j++)
                {
                    char cj = cur[j];
                    float best = float.MinValue;
                    int bi = 0;
                    for (int k = 0; k < pre.Count; k++)
                    {
                        float v = prev[k] + PairScore(pre[k], cj);
                        if (v > best) { best = v; bi = k; }
                    }
                    now[j] = best + Score(cj);
                    bk[j] = bi;
                }
                back[i] = bk;
                prev = now;
            }

            int bi2 = 0;
            for (int j = 1; j < prev.Length; j++) if (prev[j] > prev[bi2]) bi2 = j;

            char[] outChars = new char[n];
            int idx = bi2;
            for (int i = n - 1; i >= 0; i--)
            {
                outChars[i] = cand[i][idx];
                idx = back[i][idx];
                if (idx < 0) idx = 0;
            }
            return new string(outChars);
        }

        /// <summary>智能模式下判定为“多义”并被改写的位置数量（用于界面提示）。</summary>
        public static int CountAmbiguity(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int c = 0;
            for (int i = 0; i < s.Length; i++) if (Candidates(s[i]).Count > 1) c++;
            return c;
        }

        private static readonly Dictionary<char, List<char>> oneCache = new Dictionary<char, List<char>>();
        private static List<char> One(char c)
        {
            List<char> l;
            if (!oneCache.TryGetValue(c, out l))
            {
                l = new List<char>(1);
                l.Add(c);
                oneCache[c] = l;
            }
            return l;
        }

        private static float Score(char c)
        {
            float v;
            return charFreq.TryGetValue(c, out v) ? v : 0f;
        }

        private static float PairScore(char a, char b)
        {
            float v;
            return pairFreq.TryGetValue(((int)a << 16) | (int)b, out v) ? v : 0f;
        }

        /// <summary>按方向批量转换。</summary>
        public static string Convert(string text, Direction dir, bool smart)
        {
            switch (dir)
            {
                case Direction.ToMartian: return ToMartian(text);
                case Direction.ToChinese:
                    return smart ? ToSimplifiedSmart(text) : ToSimplified(text);
                case Direction.ToTraditional: return ToTraditional(text);
                case Direction.ToSimplified: return TraditionalToSimplified(text);
                default: return text;
            }
        }
    }

    public enum Direction
    {
        ToMartian,
        ToChinese,
        ToTraditional,
        ToSimplified
    }
}
