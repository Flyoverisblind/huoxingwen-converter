using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace HuoXingWen
{
    internal static class UI
    {
        public static readonly Color Bg = Color.FromArgb(237, 242, 248);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(213, 222, 235);
        public static readonly Color Accent = Color.FromArgb(45, 106, 233);
        public static readonly Color AccentSoft = Color.FromArgb(230, 239, 254);
        public static readonly Color Ink = Color.FromArgb(24, 36, 58);
        public static readonly Color Muted = Color.FromArgb(108, 123, 146);
        public static readonly Color SoftHead = Color.FromArgb(246, 249, 253);

        private static readonly string Family = PickFamily();

        public static readonly Font TitleFont = F(19f, FontStyle.Bold);
        public static readonly Font SubFont = F(9f);
        public static readonly Font CardTitleFont = F(10.5f, FontStyle.Bold);
        public static readonly Font HintFont = F(9f);
        public static readonly Font BodyFont = F(12f);
        public static readonly Font SmallFont = F(9f);
        public static readonly Font BtnFont = F(9.5f);
        public static readonly Font BtnBoldFont = F(10f, FontStyle.Bold);

        private static string PickFamily()
        {
            string[] want = new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑", "SimHei", "SimSun" };
            try
            {
                using (InstalledFontCollection col = new InstalledFontCollection())
                {
                    foreach (string w in want)
                        foreach (FontFamily f in col.Families)
                            if (string.Equals(f.Name, w, StringComparison.OrdinalIgnoreCase)) return f.Name;
                }
            }
            catch { }
            return SystemFonts.MessageBoxFont.FontFamily.Name;
        }

        public static Font F(float size) { return new Font(Family, size, FontStyle.Regular, GraphicsUnit.Point); }
        public static Font F(float size, FontStyle style) { return new Font(Family, size, style, GraphicsUnit.Point); }
    }

    internal static class Rounded
    {
        public static GraphicsPath Path(Rectangle r, int radius)
        {
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            GraphicsPath p = new GraphicsPath();
            if (d <= 2) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static GraphicsPath TopPath(Rectangle r, int radius)
        {
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            GraphicsPath p = new GraphicsPath();
            if (d <= 2) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            p.CloseFigure();
            return p;
        }
    }

    internal class CardPanel : Panel
    {
        public string Title = "";
        public string Hint = "";
        public TextBox Box;
        public const int HeaderHeight = 38;
        private bool focused;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Rounded.Path(r, 8))
            {
                using (SolidBrush b = new SolidBrush(Color.White)) g.FillPath(b, path);
                Rectangle head = new Rectangle(1, 1, Math.Max(1, Width - 3), HeaderHeight);
                using (GraphicsPath hp = Rounded.TopPath(head, 8))
                using (SolidBrush b = new SolidBrush(UI.SoftHead)) g.FillPath(b, hp);
                using (Pen pen = new Pen(focused ? UI.Accent : UI.Border, focused ? 1.6f : 1f)) g.DrawPath(pen, path);
            }
            using (Pen pen = new Pen(UI.Border)) g.DrawLine(pen, 1, HeaderHeight, Width - 2, HeaderHeight);
            TextRenderer.DrawText(g, Title, UI.CardTitleFont, new Point(13, 11), UI.Ink, TextFormatFlags.NoPrefix);
            if (Hint.Length > 0)
            {
                Rectangle hr = new Rectangle(13, 1, Math.Max(1, Width - 26), HeaderHeight);
                TextRenderer.DrawText(g, Hint, UI.HintFont, hr, UI.Muted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        public void SetFocusState(bool on)
        {
            if (focused != on) { focused = on; Invalidate(); }
        }
    }

    internal class FlatButton : Button
    {
        private readonly Color normal;
        private bool hover;
        private bool pressed;

        public FlatButton(string text, Color back, Color fore, Font font, bool border)
        {
            normal = back;
            Font = font;
            ForeColor = fore;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = back;
            Cursor = Cursors.Hand;
            AutoSize = false;
            TabStop = false;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            base.Text = text;
            FitWidth();
            if (border)
            {
                FlatAppearance.BorderSize = 1;
                FlatAppearance.BorderColor = UI.Border;
            }
        }

        public void FitWidth()
        {
            Width = TextRenderer.MeasureText(base.Text, Font).Width + 26;
        }

        private static Color Shift(Color c, double amount)
        {
            int r = amount >= 0 ? c.R + (int)((255 - c.R) * amount) : c.R + (int)(c.R * amount);
            int g = amount >= 0 ? c.G + (int)((255 - c.G) * amount) : c.G + (int)(c.G * amount);
            int b = amount >= 0 ? c.B + (int)((255 - c.B) * amount) : c.B + (int)(c.B * amount);
            return Color.FromArgb(c.A, Math.Max(0, Math.Min(255, r)), Math.Max(0, Math.Min(255, g)), Math.Max(0, Math.Min(255, b)));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color back = normal;
            if (pressed) back = Shift(normal, normal.GetBrightness() > 0.6 ? -0.10 : 0.18);
            else if (hover) back = Shift(normal, normal.GetBrightness() > 0.6 ? -0.05 : 0.12);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Rounded.Path(r, 6))
            {
                using (SolidBrush b = new SolidBrush(back)) g.FillPath(b, p);
                if (FlatAppearance.BorderSize > 0)
                    using (Pen pen = new Pen(FlatAppearance.BorderColor)) g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, base.Text, Font, new Rectangle(0, 0, Width, Height), ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    public class MainForm : Form
    {
        private readonly TextBox leftBox = new TextBox();
        private readonly TextBox rightBox = new TextBox();
        private readonly Label statusLeft = new Label();
        private readonly Label statusRight = new Label();
        private readonly CheckBox chkSmart = new CheckBox();
        private readonly CheckBox chkTraditional = new CheckBox();
        private readonly CheckBox chkLive = new CheckBox();
        private readonly Timer debounce = new Timer();
        private readonly ToolTip tips = new ToolTip();
        private readonly Label sampleLabel = new Label();

        private CardPanel leftCard;
        private CardPanel rightCard;
        private bool syncing;
        private TextBox lastEditor;
        private int sampleIndex;

        private static readonly string[] Samples = new string[]
        {
            "我爱你，你是我心中最美的风景。",
            "今天的天气真不错，我们一起去公园散步吧！",
            "无论走到哪里，都要记得最初的梦想。",
            "生日快乐！愿你每天都开心快乐。",
            "学习使我快乐，努力让我进步。"
        };

        public MainForm()
        {
            Text = "火星文转换器 - 简体 / 繁体 / 火星文 双向互转";
            BackColor = UI.Bg;
            Font = UI.SmallFont;
            ClientSize = new Size(1080, 660);
            MinimumSize = new Size(880, 580);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            BuildUi();

            debounce.Interval = 60;
            debounce.Tick += delegate { debounce.Stop(); DoConvert(); };

            leftBox.TextChanged += delegate { OnEdited(leftBox); };
            rightBox.TextChanged += delegate { OnEdited(rightBox); };

            AttachDrop(this);
            AttachDrop(leftBox);
            AttachDrop(rightBox);

            leftBox.Text = Samples[0];
            lastEditor = leftBox;
            DoConvert();
            leftBox.SelectAll();

            // 后台预加载智能消歧统计表，避免首次反向转换卡顿
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { Conv.PreloadSmart(); });
        }

        private void AttachDrop(Control c)
        {
            c.AllowDrop = true;
            c.DragEnter += delegate (object s, DragEventArgs e)
            {
                if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            c.DragDrop += delegate (object s, DragEventArgs e)
            {
                try
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        leftBox.Text = File.ReadAllText(files[0], DetectEncoding(files[0]));
                        lastEditor = leftBox;
                        DoConvert();
                        statusLeft.Text = "已载入文件：" + Path.GetFileName(files[0]);
                    }
                }
                catch (Exception ex) { MessageBox.Show(this, "读取文件失败：" + ex.Message, "火星文转换器"); }
            };
        }

        private static Encoding DetectEncoding(string path)
        {
            try
            {
                byte[] head = new byte[3];
                using (FileStream fs = File.OpenRead(path))
                {
                    int n = fs.Read(head, 0, 3);
                    if (n == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF) return new UTF8Encoding(true);
                }
            }
            catch { }
            return Encoding.UTF8;
        }

        // ---------------- 界面搭建 ----------------

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.BackColor = UI.Bg;
            root.Padding = new Padding(18, 14, 18, 6);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            Controls.Add(root);

            // 标题栏
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.Margin = new Padding(0);
            Label title = new Label();
            title.Text = "火星文转换器";
            title.Font = UI.TitleFont;
            title.ForeColor = UI.Ink;
            title.AutoSize = true;
            title.Location = new Point(0, 0);
            header.Controls.Add(title);
            Label sub = new Label();
            sub.Text = "简体 / 繁体 / 火星文 双向互转 · 智能消歧 · 实时转换 · 支持整段粘贴与文件拖入";
            sub.Font = UI.SubFont;
            sub.ForeColor = UI.Muted;
            sub.AutoSize = true;
            sub.Location = new Point(3, 33);
            header.Controls.Add(sub);
            FlatButton help = new FlatButton("使用说明", UI.AccentSoft, UI.Accent, UI.SmallFont, false);
            help.Height = 30;
            help.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            help.Location = new Point(Math.Max(0, header.Width - help.Width), 6);
            help.Click += delegate { ShowHelp(); };
            header.Controls.Add(help);
            header.Resize += delegate { help.Location = new Point(Math.Max(0, header.Width - help.Width), 6); };
            root.Controls.Add(header, 0, 0);

            // 工具条
            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.Margin = new Padding(0, 4, 0, 4);

            FlowLayoutPanel barRight = new FlowLayoutPanel();
            barRight.Dock = DockStyle.Right;
            barRight.AutoSize = true;
            barRight.WrapContents = false;
            barRight.FlowDirection = FlowDirection.LeftToRight;
            barRight.BackColor = UI.Bg;
            AddSmall(barRight, "交换 ⇅", delegate { SwapSides(); });
            AddSmall(barRight, "复制中文", delegate { CopyText(leftBox.Text, "中文"); });
            AddSmall(barRight, "复制火星文", delegate { CopyText(rightBox.Text, "火星文"); });
            AddSmall(barRight, "粘贴", delegate { PasteToFocused(); });
            AddSmall(barRight, "清空", delegate { ClearAll(); });
            AddSmall(barRight, "示例", delegate { LoadSample(); });

            FlowLayoutPanel barLeft = new FlowLayoutPanel();
            barLeft.Dock = DockStyle.Fill;
            barLeft.WrapContents = false;
            barLeft.FlowDirection = FlowDirection.LeftToRight;
            barLeft.BackColor = UI.Bg;
            FlatButton toHx = new FlatButton("中文 → 火星文", UI.Accent, Color.White, UI.BtnBoldFont, false);
            toHx.Height = 32;
            toHx.Margin = new Padding(0, 4, 10, 0);
            toHx.Click += delegate { ConvertToMartian(); };
            barLeft.Controls.Add(toHx);
            FlatButton toCn = new FlatButton("火星文 → 中文", Color.White, UI.Accent, UI.BtnBoldFont, true);
            toCn.Height = 32;
            toCn.Margin = new Padding(0, 4, 0, 0);
            toCn.Click += delegate { ConvertToChinese(); };
            barLeft.Controls.Add(toCn);

            bar.Controls.Add(barLeft);
            bar.Controls.Add(barRight);
            root.Controls.Add(bar, 0, 1);

            // 输入 / 输出卡片
            TableLayoutPanel cards = new TableLayoutPanel();
            cards.Dock = DockStyle.Fill;
            cards.ColumnCount = 2;
            cards.RowCount = 1;
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            cards.Margin = new Padding(0);
            leftCard = MakeCard("中文（简体 / 繁体）", "输入或粘贴中文，右侧即时得到火星文", leftBox);
            leftCard.Margin = new Padding(0, 0, 7, 0);
            rightCard = MakeCard("火星文", "粘贴火星文，左侧即时还原中文", rightBox);
            rightCard.Margin = new Padding(7, 0, 0, 0);
            cards.Controls.Add(leftCard, 0, 0);
            cards.Controls.Add(rightCard, 1, 0);
            root.Controls.Add(cards, 0, 2);

            // 选项
            FlowLayoutPanel opts = new FlowLayoutPanel();
            opts.Dock = DockStyle.Fill;
            opts.Margin = new Padding(0);
            opts.WrapContents = false;
            chkSmart.Text = "智能消歧（推荐）";
            chkSmart.Checked = true;
            chkSmart.AutoSize = true;
            chkSmart.ForeColor = UI.Ink;
            chkSmart.Margin = new Padding(0, 10, 20, 0);
            chkSmart.CheckedChanged += delegate { RefreshAll(); };
            opts.Controls.Add(chkSmart);

            chkTraditional.Text = "中文侧显示繁体";
            chkTraditional.AutoSize = true;
            chkTraditional.ForeColor = UI.Ink;
            chkTraditional.Margin = new Padding(0, 10, 20, 0);
            chkTraditional.CheckedChanged += delegate { ToggleTraditional(); };
            opts.Controls.Add(chkTraditional);

            chkLive.Text = "实时转换";
            chkLive.Checked = true;
            chkLive.AutoSize = true;
            chkLive.ForeColor = UI.Ink;
            chkLive.Margin = new Padding(0, 10, 20, 0);
            opts.Controls.Add(chkLive);

            sampleLabel.Text = "";
            sampleLabel.AutoSize = true;
            sampleLabel.ForeColor = UI.Muted;
            sampleLabel.Font = UI.SmallFont;
            sampleLabel.Margin = new Padding(4, 12, 0, 0);
            opts.Controls.Add(sampleLabel);
            root.Controls.Add(opts, 0, 3);

            tips.SetToolTip(chkSmart, "火星文里同一个字常对应多个汉字（如“妑”= 芭/吧/巴/把/爸），勾选后按字频与常用词组自动判断。");
            tips.SetToolTip(chkTraditional, "把中文一侧在简体与繁体之间切换。");
            tips.SetToolTip(chkLive, "输入时自动转换，无需点击按钮。");
            tips.SetToolTip(statusRight, "共 " + Conv.TableSize + " 个常用汉字与火星文对应。");

            // 状态栏
            Panel status = new Panel();
            status.Dock = DockStyle.Fill;
            status.Margin = new Padding(0);
            statusLeft.Dock = DockStyle.Fill;
            statusLeft.ForeColor = UI.Muted;
            statusLeft.Font = UI.SmallFont;
            statusLeft.TextAlign = ContentAlignment.MiddleLeft;
            statusLeft.AutoSize = false;
            statusLeft.Text = "就绪";
            statusRight.Dock = DockStyle.Right;
            statusRight.Width = 420;
            statusRight.ForeColor = UI.Muted;
            statusRight.Font = UI.SmallFont;
            statusRight.TextAlign = ContentAlignment.MiddleRight;
            statusRight.Text = "数据：" + Conv.TableSize + " 个常用汉字" + (Conv.Ready ? "" : "（引擎异常：" + Conv.LoadError + "）");
            status.Controls.Add(statusLeft);
            status.Controls.Add(statusRight);
            root.Controls.Add(status, 0, 4);
        }

        private void AddSmall(FlowLayoutPanel host, string text, EventHandler onClick)
        {
            FlatButton b = new FlatButton(text, Color.White, UI.Ink, UI.SmallFont, true);
            b.Height = 30;
            b.Margin = new Padding(8, 5, 0, 0);
            b.Click += onClick;
            host.Controls.Add(b);
        }

        private CardPanel MakeCard(string title, string hint, TextBox box)
        {
            CardPanel card = new CardPanel();
            card.Dock = DockStyle.Fill;
            card.Title = title;
            card.Hint = hint;
            card.Box = box;
            card.Padding = new Padding(14, CardPanel.HeaderHeight + 10, 14, 14);

            box.Dock = DockStyle.Fill;
            box.BorderStyle = BorderStyle.None;
            box.Multiline = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.Font = UI.BodyFont;
            box.BackColor = Color.White;
            box.ForeColor = UI.Ink;
            box.AcceptsReturn = true;
            box.WordWrap = true;

            box.Enter += delegate { card.SetFocusState(true); };
            box.Leave += delegate { card.SetFocusState(false); };
            box.KeyDown += OnBoxKeyDown;

            card.Controls.Add(box);
            return card;
        }

        private void OnBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                if (sender == leftBox) ConvertToMartian(); else ConvertToChinese();
            }
        }

        // ---------------- 转换逻辑 ----------------

        private void OnEdited(TextBox box)
        {
            if (syncing) return;
            lastEditor = box;
            if (chkLive.Checked) { debounce.Stop(); debounce.Start(); }
        }

        private void RefreshAll()
        {
            if (leftBox.Text.Length == 0 && rightBox.Text.Length == 0) return;
            lastEditor = leftBox;
            DoConvert();
        }

        private void DoConvert()
        {
            if (syncing) return;
            if (lastEditor == rightBox) ConvertToChinese();
            else ConvertToMartian();
        }

        private void ConvertToMartian()
        {
            if (syncing) return;
            Stopwatch sw = Stopwatch.StartNew();
            string src = leftBox.Text;
            string dst = Conv.ToMartian(src);
            sw.Stop();
            int hit = 0;
            for (int i = 0; i < src.Length; i++) if (Conv.IsChinese(src[i])) hit++;
            SetTextPreserve(rightBox, dst);
            Report(src, dst, hit, 0, sw.Elapsed.TotalMilliseconds, "中文 → 火星文");
        }

        private void ConvertToChinese()
        {
            if (syncing) return;
            Stopwatch sw = Stopwatch.StartNew();
            string src = rightBox.Text;
            bool smart = chkSmart.Checked;
            string dst = smart ? Conv.ToSimplifiedSmart(src) : Conv.ToSimplified(src);
            if (chkTraditional.Checked) dst = Conv.ToTraditional(dst);
            sw.Stop();
            int amb = 0, hit = 0;
            for (int i = 0; i < src.Length; i++)
            {
                int n = Conv.Candidates(src[i]).Count;
                if (n > 0) hit++;
                if (n > 1) amb++;
            }
            SetTextPreserve(leftBox, dst);
            Report(src, dst, hit, amb, sw.Elapsed.TotalMilliseconds, smart ? "火星文 → 中文（智能消歧）" : "火星文 → 中文（首个匹配）");
        }

        private void Report(string src, string dst, int hit, int amb, double ms, string mode)
        {
            string s = mode + "　·　输入 " + Count(src) + " 字，命中 " + hit + " 字";
            if (amb > 0) s += "，其中多义 " + amb + " 处";
            s += "　·　输出 " + Count(dst) + " 字　·　用时 " + ms.ToString("0.0") + " ms";
            statusLeft.Text = s;
        }

        private static int Count(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (!char.IsWhiteSpace(s[i])) n++;
            return n;
        }

        private void SetTextPreserve(TextBox box, string text)
        {
            if (box.Text == text) return;
            syncing = true;
            try
            {
                int start = box.SelectionStart, len = box.SelectionLength;
                box.Text = text;
                if (start <= text.Length)
                {
                    box.SelectionStart = start;
                    box.SelectionLength = Math.Min(len, text.Length - start);
                }
            }
            finally { syncing = false; }
        }

        // ---------------- 工具栏动作 ----------------

        private void SwapSides()
        {
            string a = leftBox.Text, b = rightBox.Text;
            syncing = true;
            try
            {
                leftBox.Text = b;
                rightBox.Text = a;
            }
            finally { syncing = false; }
            lastEditor = leftBox;
            DoConvert();
            statusLeft.Text = "已交换左右两侧内容";
        }

        private void ToggleTraditional()
        {
            if (syncing) return;
            syncing = true;
            try
            {
                leftBox.Text = chkTraditional.Checked
                    ? Conv.ToTraditional(leftBox.Text)
                    : Conv.TraditionalToSimplified(leftBox.Text);
            }
            finally { syncing = false; }
            statusLeft.Text = chkTraditional.Checked ? "中文侧已切换为繁体" : "中文侧已切换为简体";
        }

        private void CopyText(string text, string what)
        {
            if (string.IsNullOrEmpty(text)) { statusLeft.Text = "没有可复制的" + what + "内容"; return; }
            try
            {
                Clipboard.SetText(text);
                statusLeft.Text = "已复制" + what + "（" + Count(text) + " 字）到剪贴板";
            }
            catch (Exception ex) { statusLeft.Text = "复制失败：" + ex.Message; }
        }

        private void PasteToFocused()
        {
            try
            {
                if (!Clipboard.ContainsText()) { statusLeft.Text = "剪贴板中没有文本"; return; }
                string t = Clipboard.GetText();
                TextBox target = leftBox;
                if (leftBox.Focused) target = leftBox;
                else if (rightBox.Focused) target = rightBox;
                else if (lastEditor != null) target = lastEditor;

                syncing = true;
                try
                {
                    string before = target.Text.Substring(0, target.SelectionStart);
                    string after = target.Text.Substring(target.SelectionStart + target.SelectionLength);
                    target.Text = before + t + after;
                    target.SelectionStart = before.Length + t.Length;
                }
                finally { syncing = false; }
                lastEditor = target;
                DoConvert();
                target.Focus();
                statusLeft.Text = "已粘贴 " + Count(t) + " 字";
            }
            catch (Exception ex) { statusLeft.Text = "粘贴失败：" + ex.Message; }
        }

        private void ClearAll()
        {
            syncing = true;
            try { leftBox.Text = ""; rightBox.Text = ""; }
            finally { syncing = false; }
            leftBox.Focus();
            statusLeft.Text = "已清空";
        }

        private void LoadSample()
        {
            leftBox.Text = Samples[sampleIndex % Samples.Length];
            sampleIndex++;
            lastEditor = leftBox;
            DoConvert();
            leftBox.Focus();
        }

        private void ShowHelp()
        {
            string msg =
                "【怎么用】\r\n" +
                "1. 左边输入中文（简体或繁体），右边立刻得到火星文；\r\n" +
                "2. 右边粘贴火星文，左边立刻还原成中文；\r\n" +
                "3. 两侧都能直接编辑，互为输入输出，支持整段文字与多行；\r\n" +
                "4. 把 .txt 文件直接拖进窗口即可转换。\r\n\r\n" +
                "【两个关键选项】\r\n" +
                "· 智能消歧：火星文里同一个字往往对应好几个汉字（比如“妑”可以是芭/吧/巴/把/爸），\r\n" +
                "   勾选后按字频与常用词组做全局最优判断，还原更准；取消则与在线工具的首个匹配结果一致。\r\n" +
                "· 中文侧显示繁体：把中文一侧在简体与繁体之间切换。\r\n\r\n" +
                "【小技巧】\r\n" +
                "· Ctrl+Enter 立即转换，Ctrl+C / Ctrl+V 正常可用；\r\n" +
                "· 对照表里没有的生僻字、标点、emoji 会原样保留。\r\n\r\n" +
                "数据：共 " + Conv.TableSize + " 个常用汉字与火星文一一对应；" +
                (Conv.Ready ? "引擎加载正常。" : "引擎加载异常：" + (Conv.LoadError ?? "未知错误"));
            MessageBox.Show(this, msg, "火星文转换器 · 使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
