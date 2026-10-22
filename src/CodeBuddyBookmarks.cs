
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CodeBuddyBookmarks
{
    // 简易日志：出问题时写 error.log，方便排查
    internal static class Log
    {
        private static readonly object gate = new object();
        public static string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'), "error.log");

        public static void Write(string context, Exception ex)
        {
            try
            {
                lock (gate)
                {
                    string text = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}\r\n{2}\r\n\r\n",
                        DateTime.Now, context, ex == null ? "(null)" : ex.ToString());
                    File.AppendAllText(FilePath, text, new UTF8Encoding(false));
                }
            }
            catch { }
        }
    }

    internal static class Theme
    {
        public static readonly Color Accent = Color.FromArgb(47, 107, 255);
        public static readonly Color AccentHover = Color.FromArgb(78, 130, 255);
        public static readonly Color AccentPress = Color.FromArgb(35, 88, 214);
        public static readonly Color Danger = Color.FromArgb(203, 62, 62);

        public static bool IsDark { get; private set; }

        public static Color HeaderTop, HeaderBottom, HeaderText;
        public static Color Card, Border, Muted, TextMain;
        public static Color WindowBack, HeaderBack, RowBack, RowHover, RowSelected;
        public static Color RowLineColor, PathColor, TypeColor, NameMuted, EmptyTitle, EmptyHint;
        public static Color BtnHover, BtnPress, BtnBorder;
        public static Color ChipRedBack, ChipGreenBack, ChipGrayBack;

        // 复用的 GDI 对象（自绘时避免每次 Paint 都 new），切主题时重建
        public static SolidBrush BgWhite, BgHover, BgSelected, HeaderBg, WindowBrush;
        public static Pen RowLine, HeaderLine;

        static Theme() { Apply(false); }

        public static void Apply(bool dark)
        {
            IsDark = dark;

            HeaderTop = dark ? Color.FromArgb(20, 24, 34) : Color.FromArgb(27, 33, 48);
            HeaderBottom = dark ? Color.FromArgb(30, 36, 50) : Color.FromArgb(38, 47, 69);
            HeaderText = dark ? Color.FromArgb(150, 162, 186) : Color.FromArgb(158, 169, 194);

            Card = dark ? Color.FromArgb(38, 42, 52) : Color.FromArgb(247, 248, 250);
            Border = dark ? Color.FromArgb(62, 68, 82) : Color.FromArgb(226, 230, 238);
            Muted = dark ? Color.FromArgb(150, 158, 172) : Color.FromArgb(123, 131, 148);
            TextMain = dark ? Color.FromArgb(232, 236, 244) : Color.FromArgb(34, 37, 46);

            WindowBack = dark ? Color.FromArgb(30, 34, 43) : Color.White;
            HeaderBack = dark ? Color.FromArgb(36, 40, 50) : Color.FromArgb(250, 251, 253);
            RowBack = dark ? Color.FromArgb(30, 34, 43) : Color.White;
            RowHover = dark ? Color.FromArgb(40, 45, 56) : Color.FromArgb(249, 251, 254);
            RowSelected = dark ? Color.FromArgb(44, 62, 96) : Color.FromArgb(235, 242, 255);
            RowLineColor = dark ? Color.FromArgb(44, 48, 58) : Color.FromArgb(242, 244, 248);
            PathColor = dark ? Color.FromArgb(152, 160, 174) : Color.FromArgb(106, 114, 130);
            TypeColor = dark ? Color.FromArgb(140, 148, 162) : Color.FromArgb(134, 141, 156);
            NameMuted = dark ? Color.FromArgb(148, 156, 170) : Color.FromArgb(160, 166, 178);
            EmptyTitle = dark ? Color.FromArgb(170, 178, 192) : Color.FromArgb(150, 157, 172);
            EmptyHint = dark ? Color.FromArgb(130, 138, 152) : Color.FromArgb(178, 184, 196);

            BtnHover = dark ? Color.FromArgb(48, 53, 66) : Color.FromArgb(243, 246, 252);
            BtnPress = dark ? Color.FromArgb(40, 45, 57) : Color.FromArgb(232, 236, 244);
            BtnBorder = dark ? Color.FromArgb(92, 100, 118) : Color.FromArgb(198, 208, 228);

            ChipRedBack = dark ? Color.FromArgb(64, 34, 36) : Color.FromArgb(253, 236, 236);
            ChipGreenBack = dark ? Color.FromArgb(28, 52, 40) : Color.FromArgb(232, 247, 238);
            ChipGrayBack = dark ? Color.FromArgb(46, 50, 60) : Color.FromArgb(242, 244, 248);

            Swap(ref BgWhite, new SolidBrush(RowBack));
            Swap(ref BgHover, new SolidBrush(RowHover));
            Swap(ref BgSelected, new SolidBrush(RowSelected));
            Swap(ref HeaderBg, new SolidBrush(HeaderBack));
            Swap(ref WindowBrush, new SolidBrush(WindowBack));
            SwapPen(ref RowLine, new Pen(RowLineColor));
            SwapPen(ref HeaderLine, new Pen(Border));
        }

        private static void Swap(ref SolidBrush target, SolidBrush fresh)
        {
            if (target != null) target.Dispose();
            target = fresh;
        }

        private static void SwapPen(ref Pen target, Pen fresh)
        {
            if (target != null) target.Dispose();
            target = fresh;
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // 双缓冲列表，自绘时少闪烁
    internal class BufferedListView : ListView
    {
        public BufferedListView()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }
    }

    // 扁平圆角按钮
    internal class FlatButton : Button
    {
        public bool Primary;
        private bool hovered, pressed;

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 9F);
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // 圆角外的区域属于"父容器背景"，必须自己填一次，否则残留黑块
            Color behind = Parent != null ? Parent.BackColor : Color.White;
            using (SolidBrush bg = new SolidBrush(behind))
                g.FillRectangle(bg, ClientRectangle);

            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle fillRect = new Rectangle(0, 0, Width, Height);
            Color fill;
            if (!Enabled) fill = Theme.Card;
            else if (Primary)
                fill = pressed ? Theme.AccentPress : (hovered ? Theme.AccentHover : Theme.Accent);
            else
                fill = pressed ? Theme.BtnPress
                     : (hovered ? Theme.BtnHover : Theme.RowBack);

            using (GraphicsPath path = Theme.Round(fillRect, 6))
            using (SolidBrush b = new SolidBrush(fill))
            {
                g.FillPath(b, path);
            }

            if (!Primary)
            {
                SmoothingMode old = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.None;
                Rectangle borderRect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = Theme.Round(borderRect, 6))
                using (Pen p = new Pen(hovered && Enabled ? Theme.BtnBorder : Theme.Border))
                {
                    g.DrawPath(p, path);
                }
                g.SmoothingMode = old;
            }

            Color textColor = !Enabled ? Theme.Muted : (Primary ? Color.White : Theme.TextMain);
            TextRenderer.DrawText(g, Text, Font, fillRect, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // ---------------- 找 IDE 安装位置 ----------------
    internal static class IdeLocator
    {
        public static string Locate(string[] candidates)
        {
            foreach (string p in candidates)
            {
                try { if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p; }
                catch { }
            }

            string exeName = "app.exe";
            if (candidates != null && candidates.Length > 0)
            {
                string n = Path.GetFileName(candidates[0]);
                if (!string.IsNullOrEmpty(n)) exeName = n;
            }

            string hit = FromAppPaths(exeName);
            if (hit != null) return hit;

            hit = FromUninstall(exeName);
            if (hit != null) return hit;

            return FromStartMenu(exeName);
        }

        // 注册表 App Paths：正规安装包基本都会写这里
        private static string FromAppPaths(string exeName)
        {
            string[] subs = new string[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exeName,
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\" + exeName
            };
            RegistryKey[] roots = new RegistryKey[] { Registry.LocalMachine, Registry.CurrentUser };

            foreach (RegistryKey root in roots)
            {
                foreach (string sub in subs)
                {
                    try
                    {
                        using (RegistryKey k = root.OpenSubKey(sub))
                        {
                            if (k == null) continue;
                            string v = k.GetValue(null) as string;
                            if (string.IsNullOrEmpty(v)) continue;
                            v = v.Trim('"');
                            if (File.Exists(v)) return v;
                        }
                    }
                    catch { }
                }
            }
            return null;
        }

        // 卸载信息里的 InstallLocation / DisplayIcon
        private static string FromUninstall(string exeName)
        {
            string stem = Path.GetFileNameWithoutExtension(exeName);
            string[] subs = new string[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };
            RegistryKey[] roots = new RegistryKey[] { Registry.LocalMachine, Registry.CurrentUser };

            foreach (RegistryKey root in roots)
            {
                foreach (string sub in subs)
                {
                    try
                    {
                        using (RegistryKey k = root.OpenSubKey(sub))
                        {
                            if (k == null) continue;
                            foreach (string name in k.GetSubKeyNames())
                            {
                                try
                                {
                                    using (RegistryKey app = k.OpenSubKey(name))
                                    {
                                        if (app == null) continue;
                                        string dn = app.GetValue("DisplayName") as string;
                                        if (string.IsNullOrEmpty(dn) || dn.IndexOf(stem, StringComparison.OrdinalIgnoreCase) < 0) continue;

                                        string loc = app.GetValue("InstallLocation") as string;
                                        if (!string.IsNullOrEmpty(loc))
                                        {
                                            string cand = Path.Combine(loc.Trim('"'), exeName);
                                            if (File.Exists(cand)) return cand;
                                        }

                                        string icon = app.GetValue("DisplayIcon") as string;
                                        if (!string.IsNullOrEmpty(icon))
                                        {
                                            icon = icon.Trim('"');
                                            int comma = icon.LastIndexOf(',');
                                            if (comma > 0) icon = icon.Substring(0, comma).Trim('"');
                                            if (File.Exists(icon) &&
                                                string.Equals(Path.GetFileName(icon), exeName, StringComparison.OrdinalIgnoreCase))
                                                return icon;
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }
            return null;
        }

        // 开始菜单快捷方式（绿色版/自定义安装目录时最后兜底）
        private static string FromStartMenu(string exeName)
        {
            string[] roots = new string[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
            };

            foreach (string root in roots)
            {
                try
                {
                    string dir = Path.Combine(root, "Programs");
                    if (!Directory.Exists(dir)) continue;
                    string[] links = Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories);
                    foreach (string lnk in links)
                    {
                        string target = ResolveShortcut(lnk);
                        if (string.IsNullOrEmpty(target) || !File.Exists(target)) continue;
                        if (string.Equals(Path.GetFileName(target), exeName, StringComparison.OrdinalIgnoreCase))
                            return target;
                    }
                }
                catch { }
            }
            return null;
        }

        private static string ResolveShortcut(string lnkPath)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return null;
                object shell = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell,
                    new object[] { lnkPath });
                if (sc == null) return null;
                object target = sc.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, sc, null);
                return target as string;
            }
            catch { return null; }
        }
    }

    // 进程名快照：一次枚举所有进程，避免反复 GetProcessesByName
    internal static class ProcCache
    {
        private static HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static DateTime at = DateTime.MinValue;
        private static readonly object gate = new object();

        public static bool IsRunning(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            lock (gate)
            {
                if ((DateTime.UtcNow - at).TotalSeconds > 2) Refresh();
                return names.Contains(name);
            }
        }

        private static void Refresh()
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (Process p in Process.GetProcesses())
                {
                    try { set.Add(p.ProcessName); }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }
            names = set;
            at = DateTime.UtcNow;
        }
    }

    // ---------------- IDE 产品 ----------------
    internal class Product
    {
        public string Id;
        public string Name;
        public string UserData;
        public string[] ExeCandidates;
        public string[] ProcessNames;

        private string cachedExe;
        private bool resolved;
        private DateTime resolvedAt = DateTime.MinValue;

        public string StoragePath
        {
            get { return Path.Combine(UserData, @"User\globalStorage\storage.json"); }
        }

        public string Exe
        {
            get
            {
                // 结果缓存 30 秒，避免每 5 秒重复扫注册表/开始菜单
                if (resolved && (DateTime.UtcNow - resolvedAt).TotalSeconds < 30) return cachedExe;
                cachedExe = IdeLocator.Locate(ExeCandidates);
                resolved = true;
                resolvedAt = DateTime.UtcNow;
                return cachedExe;
            }
        }

        public bool Running
        {
            get
            {
                string exe = Exe;
                if (exe != null && ProcCache.IsRunning(Path.GetFileNameWithoutExtension(exe))) return true;

                string[] names = ProcessNames;
                if (names == null || names.Length == 0) names = new string[] { Name };
                foreach (string n in names)
                    if (ProcCache.IsRunning(n)) return true;

                return false;
            }
        }
    }

    // ---------------- 书签 ----------------
    internal class Bookmark
    {
        public string name;
        public string uri;
        public string kind;
        public string remoteAuthority;
        public string product;

        public bool OpenNow;
        public bool Missing;
        public bool Selected = true;
        public string lastOpened;    // 最近一次打开时间，用于"按最近打开"排序
    }

    // 记住窗口的位置 / 大小
    internal class UiState
    {
        public int x;
        public int y;
        public int width;
        public int height;
        public bool maximized;
        public bool dark;
        public bool darkSet;
        public int[] colWidths;
        public int sortMode;      // 0 手动 1 名称 2 最近打开
    }

    internal class BookmarkFile
    {
        public int version = 1;
        public string updatedAt;
        public List<Bookmark> items = new List<Bookmark>();
    }

    // ---------------- 重命名对话框 ----------------
    internal class PromptDialog : Form
    {
        private TextBox box;

        public string Value
        {
            get { return box.Text.Trim(); }
        }

        public PromptDialog(string title, string initial)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(430, 142);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.White;

            Label lbl = new Label();
            lbl.Text = "书签名称";
            lbl.AutoSize = true;
            lbl.ForeColor = Theme.Muted;
            lbl.Location = new Point(22, 18);
            Controls.Add(lbl);

            box = new TextBox();
            box.Text = initial;
            box.Location = new Point(22, 44);
            box.Width = 386;
            box.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(box);

            FlatButton ok = new FlatButton();
            ok.Text = "确定";
            ok.Primary = true;
            ok.Location = new Point(238, 90);
            ok.Size = new Size(84, 32);
            ok.DialogResult = DialogResult.OK;
            Controls.Add(ok);

            FlatButton cancel = new FlatButton();
            cancel.Text = "取消";
            cancel.Location = new Point(330, 90);
            cancel.Size = new Size(78, 32);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            box.SelectAll();
        }
    }

    // ---------------- 主窗口 ----------------
    public class MainForm : Form
    {
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private readonly string workDir;
        private readonly string storePath;
        private readonly string snapshotPath;
        private readonly string uiStatePath;

        private BookmarkFile store = new BookmarkFile();
        private List<Product> products;

        private Panel header;
        private Panel toolbar;
        private Panel options;
        private ListView list;
        private Label lblSummary;
        private Label lblHint;
        private ToolStripStatusLabel status;
        private ToolStripStatusLabel statusRight;
        private NumericUpDown numDelay;
        private CheckBox chkRaise;
        private TextBox txtSearch;

        private FlatButton btnOpen, btnAll, btnAdd, btnImport, btnDel, btnUp, btnDown, btnRefresh;
        private FlatButton btnAddSel, btnAddAll;
        private Panel panelEmpty;
        private Panel panelExtra;
        private Panel listWrap, extraWrap;
        private ListView listExtra;
        private Label lblExtraTitle;
        private List<Bookmark> extras = new List<Bookmark>();
        private Label lblEmptyTitle, lblEmptyHint;

        private bool busy = false;
        private Icon appIcon;
        private int[] userCols;     // 用户手动拖过的列宽
        private int sortMode;       // 0 手动 1 名称 2 最近打开
        private bool applyingCols;

        private int hoverIndex = -1;
        private readonly Font listFont = new Font("Microsoft YaHei UI", 9.5F);
        private readonly Font headerFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        private readonly Font chipFont = new Font("Microsoft YaHei UI", 8.25F);

        private CheckBox chkAuto;
        private Timer autoTimer;
        private NotifyIcon tray;
        private ContextMenuStrip trayMenu;
        private bool trayHintShown = false;
        private bool exiting = false;
        private bool startHidden = false;

        // 最近一次采集到的"正在运行的窗口"，刷新界面时直接复用，避免每次重建都去读文件
        private List<Bookmark> liveCache = new List<Bookmark>();
        private bool statesBusy = false;

        public MainForm()
        {
            workDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            storePath = Path.Combine(workDir, "bookmarks.json");
            snapshotPath = Path.Combine(workDir, @"snapshots\windows-latest.json");
            uiStatePath = Path.Combine(workDir, "ui-state.json");
            products = BuildProducts();

            foreach (string arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase)) startHidden = true;

            Text = "CodeBuddy 窗口书签";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9F);
            MinimumSize = new Size(820, 500);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            DoubleBuffered = true;

            // 窗口标题栏图标：直接用 exe 里嵌入的图标
            try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            if (appIcon != null) Icon = appIcon;

            BuildUi();
            ApplyUiState();
            LayoutColumns();
            LayoutToolbar();

            // 拖完窗口就把大小记下来，避免异常退出时丢
            ResizeEnd += delegate { if (!startHidden) SaveUiState(); };

            LoadStore();
            liveCache = CollectLive();
            RefreshRows();

            InitTimer();
            InitTray();
        }

        // ==================== 窗口大小 / 位置记忆 ====================
        private void ApplyUiState()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            UiState st = null;
            try
            {
                if (File.Exists(uiStatePath))
                {
                    string raw = File.ReadAllText(uiStatePath, Encoding.UTF8);
                    if (raw.Trim().Length > 0)
                    {
                        JavaScriptSerializer json = new JavaScriptSerializer();
                        st = json.Deserialize<UiState>(raw);
                    }
                }
            }
            catch { }

            if (st != null)
            {
                if (st.colWidths != null && st.colWidths.Length == 4) userCols = st.colWidths;
                sortMode = st.sortMode;
            }
            ApplyTheme(st != null && st.darkSet ? st.dark : SystemUsesDark());

            if (st != null && st.width >= 500 && st.height >= 380)
            {
                Rectangle r = new Rectangle(st.x, st.y, st.width, st.height);
                bool onScreen = false;
                foreach (Screen s in Screen.AllScreens)
                {
                    Rectangle part = Rectangle.Intersect(s.WorkingArea, r);
                    if (part.Width >= 120 && part.Height >= 80) { onScreen = true; break; }
                }

                if (onScreen)
                {
                    StartPosition = FormStartPosition.Manual;
                    Bounds = r;   // 恢复上次的位置和大小
                    if (st.maximized) WindowState = FormWindowState.Maximized;
                    return;
                }
            }

            // 没有记录（或记录的位置已经不在屏幕上）：用默认尺寸
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Math.Min(1040, wa.Width - 60), Math.Min(735, wa.Height - 60));
        }

        private void SaveUiState()
        {
            try
            {
                Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                if (b.Width < 300 || b.Height < 200) return;

                UiState st = new UiState();
                st.x = b.X;
                st.y = b.Y;
                st.width = b.Width;
                st.height = b.Height;
                st.maximized = WindowState == FormWindowState.Maximized;
                st.dark = Theme.IsDark;
                st.darkSet = true;
                st.sortMode = sortMode;
                if (userCols != null && userCols.Length == 4) st.colWidths = userCols;

                JavaScriptSerializer json = new JavaScriptSerializer();
                File.WriteAllText(uiStatePath, json.Serialize(st), new UTF8Encoding(false));
            }
            catch (Exception ex) { Log.Write("SaveUiState", ex); }
        }

        // ==================== 深色模式 ====================
        private static bool SystemUsesDark()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("AppsUseLightTheme");
                        if (v is int) return (int)v == 0;
                    }
                }
            }
            catch { }
            return false;
        }

        private void ApplyTheme(bool dark)
        {
            Theme.Apply(dark);

            BackColor = Theme.WindowBack;
            if (toolbar != null) toolbar.BackColor = Theme.WindowBack;
            if (options != null) options.BackColor = Theme.WindowBack;
            if (listWrap != null) listWrap.BackColor = Theme.WindowBack;
            if (extraWrap != null) extraWrap.BackColor = Theme.WindowBack;
            if (panelEmpty != null) panelEmpty.BackColor = Theme.WindowBack;
            if (panelExtra != null) panelExtra.BackColor = Theme.WindowBack;

            foreach (Control c in Controls)
            {
                if (c is Panel && c != header) c.BackColor = Theme.WindowBack;
                if (c is StatusStrip)
                {
                    c.BackColor = Theme.WindowBack;
                    foreach (ToolStripItem it in ((StatusStrip)c).Items) it.ForeColor = Theme.Muted;
                }
            }

            if (lblEmptyTitle != null) { lblEmptyTitle.BackColor = Theme.WindowBack; lblEmptyTitle.ForeColor = Theme.EmptyTitle; }
            if (lblEmptyHint != null) { lblEmptyHint.BackColor = Theme.WindowBack; lblEmptyHint.ForeColor = Theme.EmptyHint; }
            if (lblExtraTitle != null) lblExtraTitle.ForeColor = Theme.Muted;
            if (lblHint != null) lblHint.ForeColor = Theme.HeaderText;
            if (lblSummary != null) lblSummary.ForeColor = Theme.HeaderText;

            foreach (Control c in new Control[] { txtSearch, numDelay, chkRaise, chkAuto })
            {
                if (c == null) continue;
                c.BackColor = Theme.WindowBack;
                c.ForeColor = Theme.TextMain;
            }

            if (list != null)
            {
                list.BackColor = Theme.WindowBack;
                list.ForeColor = Theme.TextMain;
                list.Invalidate();
            }
            if (listExtra != null)
            {
                listExtra.BackColor = Theme.WindowBack;
                listExtra.ForeColor = Theme.TextMain;
                listExtra.Invalidate();
            }

            if (header != null) header.Invalidate();
            Invalidate(true);
        }

        // ==================== 自动刷新 ====================
        private void InitTimer()
        {
            autoTimer = new Timer();
            autoTimer.Interval = 5000;
            autoTimer.Tick += delegate
            {
                // 正在批量打开 / 有模态窗口 / 已最小化 时都不刷新，避免无谓开销
                if (busy || !Enabled || !Visible || WindowState == FormWindowState.Minimized) return;
                try { RefreshStatesAsync(); }
                catch { }
            };
            chkAuto.CheckedChanged += delegate { autoTimer.Enabled = chkAuto.Checked; };
            autoTimer.Enabled = chkAuto.Checked;
        }

        // ==================== 托盘图标 ====================
        private void InitTray()
        {
            tray = new NotifyIcon();
            tray.Icon = appIcon != null ? appIcon : SystemIcons.Application;
            tray.Text = "CodeBuddy 窗口书签";
            tray.Visible = true;
            tray.DoubleClick += delegate { ShowFromTray(); };

            trayMenu = new ContextMenuStrip();
            trayMenu.Font = new Font("Microsoft YaHei UI", 9F);
            trayMenu.ShowItemToolTips = true;
            trayMenu.Opening += delegate
            {
                try { BuildTrayMenu(); }
                catch (Exception ex)
                {
                    trayMenu.Items.Clear();
                    trayMenu.Items.Add("菜单构建失败：" + ex.Message);
                }
            };
            tray.ContextMenuStrip = trayMenu;
            BuildTrayMenu();
        }

        // 每次弹出托盘菜单时重建，保证"书签列表 + 是否已打开"是最新的
        private void BuildTrayMenu()
        {
            if (trayMenu == null) return;
            trayMenu.Items.Clear();

            trayMenu.Items.Add("打开主界面", null, delegate { ShowFromTray(); });
            trayMenu.Items.Add("刷新状态", null, delegate { RefreshStates(); });
            ToolStripMenuItem darkItem = new ToolStripMenuItem("深色模式");
            darkItem.Checked = Theme.IsDark;
            darkItem.ToolTipText = "切换深色 / 浅色界面";
            darkItem.Click += delegate
            {
                ApplyTheme(!Theme.IsDark);
                SaveUiState();
            };
            trayMenu.Items.Add(darkItem);
            trayMenu.Items.Add(new ToolStripSeparator());

            // 同步一次"已打开"状态
            List<Bookmark> live = new List<Bookmark>();
            try
            {
                live = CollectLive();
                List<string> openUris = new List<string>();
                foreach (Bookmark l in live) openUris.Add(l.uri.ToLower());
                foreach (Bookmark b in store.items) b.OpenNow = openUris.Contains(b.uri.ToLower());
            }
            catch { }

            if (store.items.Count == 0)
            {
                ToolStripMenuItem empty = new ToolStripMenuItem("（还没有书签）");
                empty.Enabled = false;
                trayMenu.Items.Add(empty);
            }
            else
            {
                int shown = 0;
                foreach (Bookmark b in store.items)
                {
                    if (shown >= 25)
                    {
                        trayMenu.Items.Add("更多…（打开主界面查看）", null, delegate { ShowFromTray(); });
                        break;
                    }
                    Bookmark bb = b;
                    ToolStripMenuItem item = new ToolStripMenuItem(bb.name == null ? "(未命名)" : bb.name);
                    item.Checked = bb.OpenNow;
                    item.ToolTipText = bb.OpenNow ? "已在运行 · 点击置顶" : "未打开 · 点击打开新窗口";
                    item.Click += delegate { ActivateBookmark(bb); };
                    trayMenu.Items.Add(item);
                    shown++;
                }
            }

            // ---- 未加入书签的窗口 ----
            List<Bookmark> extras = new List<Bookmark>();
            List<string> knownUris = new List<string>();
            foreach (Bookmark b in store.items) knownUris.Add(b.uri.ToLower());
            foreach (Bookmark l in live)
            {
                string u = l.uri.ToLower();
                if (knownUris.Contains(u)) continue;
                bool dup = false;
                foreach (Bookmark e in extras)
                    if (string.Equals(e.uri, l.uri, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                if (!dup) extras.Add(l);
            }

            if (extras.Count > 0)
            {
                trayMenu.Items.Add(new ToolStripSeparator());
                ToolStripMenuItem head = new ToolStripMenuItem("未加入书签的窗口（" + extras.Count + "）");
                head.Enabled = false;
                trayMenu.Items.Add(head);

                int shownX = 0;
                foreach (Bookmark b in extras)
                {
                    if (shownX >= 10)
                    {
                        trayMenu.Items.Add("更多…（打开主界面查看）", null, delegate { ShowFromTray(); });
                        break;
                    }
                    Bookmark bx = b;
                    ToolStripMenuItem item = new ToolStripMenuItem("· " + (bx.name == null ? "(未命名)" : bx.name));
                    item.ToolTipText = PathText(bx) + "\n点击置顶这个窗口";
                    item.Click += delegate { ActivateBookmark(bx); };
                    trayMenu.Items.Add(item);
                    shownX++;
                }

                List<Bookmark> toAdd = extras;
                trayMenu.Items.Add("把以上窗口加入书签", null, delegate { AddBookmarks(toAdd); });
            }

            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("全部打开", null, delegate { CheckAll(true); OpenChecked(false); });

            ToolStripMenuItem autoItem = new ToolStripMenuItem("开机自动启动");
            autoItem.Checked = IsAutoStartEnabled();
            autoItem.ToolTipText = "开机后自动启动，并缩到托盘后台运行";
            autoItem.Click += delegate { ToggleAutoStart(); };
            trayMenu.Items.Add(autoItem);

            trayMenu.Items.Add("退出", null, delegate
            {
                exiting = true;
                Close();
            });
        }

        // ==================== 开机自动启动 ====================
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "CodeBuddyWindowMaster";

        private static string AutoStartCommand()
        {
            return "\"" + Application.ExecutablePath + "\" --tray";
        }

        private static bool IsAutoStartEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (k == null) return false;
                    string v = k.GetValue(RunValueName) as string;
                    if (string.IsNullOrEmpty(v)) return false;
                    return v.IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { return false; }
        }

        // 返回 null 表示成功，否则是错误信息
        private static string SetAutoStart(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (k == null) return "打不开注册表启动项（权限不足？）";
                    if (on) k.SetValue(RunValueName, AutoStartCommand());
                    else k.DeleteValue(RunValueName, false);
                }
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        private void ToggleAutoStart()
        {
            bool on = !IsAutoStartEnabled();
            string err = SetAutoStart(on);
            if (err != null)
            {
                MessageBox.Show("设置开机自动启动失败：\r\n" + err, "CodeBuddy 窗口书签",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            status.Text = on ? "已开启：开机自动启动并缩到托盘" : "已关闭开机自动启动";
            if (tray != null)
                tray.ShowBalloonTip(1800, "CodeBuddy 窗口书签",
                    on ? "已加入开机启动，下次开机自动在托盘运行。" : "已从开机启动中移除。",
                    ToolTipIcon.Info);
        }

        // 把一批窗口加进书签（托盘菜单用）
        private void AddBookmarks(List<Bookmark> list)
        {
            if (list == null || list.Count == 0) return;
            int added = 0;
            foreach (Bookmark b in list)
                if (AddUnique(b)) added++;
            if (added > 0) SaveStore();
            RefreshRows();
            status.Text = added > 0 ? ("已加入 " + added + " 个书签") : "这些窗口已经在书签里了";
        }

        // 托盘里点某个书签：已打开就置顶，没打开就开新窗口
        private void ActivateBookmark(Bookmark b)
        {
            if (b == null) return;
            if (b.OpenNow)
            {
                string msg;
                if (!RaiseExistingWindow(b, out msg)) status.Text = "置顶失败：" + msg;
            }
            else
            {
                OpenSingle(b);
            }
        }

        private void HideToTray() { HideToTray(false); }

        private void HideToTray(bool silent)
        {
            if (tray == null) return;
            ShowInTaskbar = false;
            Hide();
            if (!trayHintShown && !silent)
            {
                tray.ShowBalloonTip(2500, "仍在后台运行",
                    "窗口已最小化到托盘，双击图标可重新打开。\n每 5 秒自动刷新会暂停，恢复界面后继续。",
                    ToolTipIcon.Info);
                trayHintShown = true;
            }
        }

        private void ShowFromTray()
        {
            startHidden = false;
            if (!Visible) Show();
            ShowInTaskbar = true;
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
            RefreshStates();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (WindowState == FormWindowState.Minimized) HideToTray();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                // 点右上角 × ：直接退出（托盘菜单里也有"退出"）
                exiting = true;
            }
            SaveUiState();
            if (autoTimer != null) autoTimer.Stop();
            if (tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
                tray = null;
            }
        }

        // ==================== 界面 ====================
        private void BuildUi()
        {
            // ---------- 列表 ----------
            ImageList il = new ImageList();
            il.ImageSize = new Size(1, 34);       // 行高
            il.ColorDepth = ColorDepth.Depth32Bit;
            il.Images.Add(new Bitmap(1, 34));

            list = new BufferedListView();
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.CheckBoxes = false;      // 勾选框自己画、自己处理，避免和系统绘制错位
            list.FullRowSelect = true;
            list.MultiSelect = true;
            list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            list.BorderStyle = BorderStyle.None;
            list.HideSelection = false;
            list.SmallImageList = il;
            list.Font = new Font("Microsoft YaHei UI", 9F);
            list.Columns.Add("状态", 92);
            list.Columns.Add("书签", 300);
            list.Columns.Add("路径", 500);
            list.Columns.Add("类型", 76);
            list.ItemCheck += OnItemCheck;
            list.DoubleClick += delegate { OpenChecked(true); };
            list.MouseUp += OnListMouseUp;
            list.Resize += delegate { LayoutColumns(); };
            list.ColumnWidthChanged += delegate
            {
                if (applyingCols || list.Columns.Count != 4) return;
                int[] w = new int[4];
                for (int i = 0; i < 4; i++) w[i] = list.Columns[i].Width;
                userCols = w;
                SaveUiState();
            };

            // 支持把文件夹从资源管理器拖进来
            list.AllowDrop = true;
            list.DragEnter += OnDragEnterFiles;
            list.DragDrop += OnDragDropFiles;
            AllowDrop = true;
            DragEnter += OnDragEnterFiles;
            DragDrop += OnDragDropFiles;

            // 自绘：自定义表头 / 行高亮 / 状态胶囊
            list.OwnerDraw = true;
            list.DrawColumnHeader += OnDrawColumnHeader;
            list.DrawItem += delegate (object s, DrawListViewItemEventArgs e) { e.DrawDefault = false; };
            list.DrawSubItem += OnDrawSubItem;
            list.MouseMove += OnListMouseMove;
            list.MouseLeave += delegate { SetHover(-1); };

            // ---------- 状态栏 ----------
            status = new ToolStripStatusLabel();
            status.Text = "就绪";
            status.Spring = true;
            status.TextAlign = ContentAlignment.MiddleLeft;

            statusRight = new ToolStripStatusLabel();
            statusRight.Text = "";
            statusRight.ForeColor = Theme.Muted;
            statusRight.TextAlign = ContentAlignment.MiddleRight;

            StatusStrip strip = new StatusStrip();
            strip.SizingGrip = false;
            strip.BackColor = Theme.Card;
            strip.Padding = new Padding(16, 0, 16, 0);
            strip.Items.Add(status);
            strip.Items.Add(statusRight);

            // ---------- 筛选行 ----------
            txtSearch = new TextBox();
            txtSearch.Location = new Point(88, 9);
            txtSearch.Width = 226;
            txtSearch.BorderStyle = BorderStyle.FixedSingle;
            txtSearch.TextChanged += delegate { RefreshRows(); };

            Label lblSearch = new Label();
            lblSearch.Text = "搜索";
            lblSearch.AutoSize = true;
            lblSearch.ForeColor = Theme.Muted;
            lblSearch.Location = new Point(42, 12);

            chkRaise = new CheckBox();
            chkRaise.Text = "已打开的窗口置顶";
            chkRaise.Checked = true;
            chkRaise.AutoSize = true;
            chkRaise.ForeColor = Theme.TextMain;
            chkRaise.Location = new Point(346, 11);

            Label lblDelay = new Label();
            lblDelay.Text = "打开间隔";
            lblDelay.AutoSize = true;
            lblDelay.ForeColor = Theme.Muted;
            lblDelay.Location = new Point(506, 12);

            numDelay = new NumericUpDown();
            numDelay.Minimum = 0;
            numDelay.Maximum = 15000;
            numDelay.Increment = 500;
            numDelay.Value = 1500;
            numDelay.Location = new Point(572, 8);
            numDelay.Width = 66;
            numDelay.BorderStyle = BorderStyle.FixedSingle;

            Label lblMs = new Label();
            lblMs.Text = "毫秒";
            lblMs.AutoSize = true;
            lblMs.ForeColor = Theme.Muted;
            lblMs.Location = new Point(644, 12);

            options = new Panel();
            options.Dock = DockStyle.Top;
            options.Height = 42;
            options.BackColor = Theme.Card;
            options.Paint += delegate (object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Border))
                    e.Graphics.DrawLine(p, 0, options.Height - 1, options.Width, options.Height - 1);
            };
            options.Controls.Add(lblSearch);
            options.Controls.Add(txtSearch);
            options.Controls.Add(chkRaise);
            options.Controls.Add(lblDelay);
            options.Controls.Add(numDelay);
            options.Controls.Add(lblMs);

            chkAuto = new CheckBox();
            chkAuto.Text = "5 秒自动刷新";
            chkAuto.Checked = true;
            chkAuto.AutoSize = true;
            chkAuto.ForeColor = Theme.TextMain;
            chkAuto.Location = new Point(700, 11);
            options.Controls.Add(chkAuto);

            // ---------- 工具条 ----------
            btnOpen = MakeButton("打开选中", 116, true);
            btnOpen.Click += delegate { OpenChecked(false); };

            btnAll = MakeButton("全部打开", 96, false);
            btnAll.Click += delegate
            {
                CheckAll(true);
                OpenChecked(false);
            };

            btnAdd = MakeButton("添加文件夹", 108, false);
            btnAdd.Click += delegate { AddFolder(); };

            btnImport = MakeButton("导入当前窗口", 118, false);
            btnImport.Click += delegate { ImportOpenWindows(); };

            btnDel = MakeButton("删除", 64, false);
            btnDel.Click += delegate { DeleteSelected(); };

            btnUp = MakeButton("↑", 36, false);
            btnUp.Click += delegate { MoveSelected(-1); };

            btnDown = MakeButton("↓", 36, false);
            btnDown.Click += delegate { MoveSelected(1); };

            btnRefresh = MakeButton("刷新", 64, false);
            btnRefresh.Click += delegate { RefreshRows(); };

            toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 56;
            toolbar.BackColor = Color.White;
            toolbar.Paint += delegate (object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Border))
                    e.Graphics.DrawLine(p, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
            };
            toolbar.Controls.Add(btnOpen);
            toolbar.Controls.Add(btnAll);
            toolbar.Controls.Add(btnAdd);
            toolbar.Controls.Add(btnImport);
            toolbar.Controls.Add(btnDel);
            toolbar.Controls.Add(btnUp);
            toolbar.Controls.Add(btnDown);
            toolbar.Controls.Add(btnRefresh);
            toolbar.Resize += delegate { LayoutToolbar(); };

            // ---------- 顶部标题 ----------
            Label title = new Label();
            title.Text = "CodeBuddy 窗口书签";
            title.ForeColor = Color.White;
            title.Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold);
            title.AutoSize = true;
            title.BackColor = Color.Transparent;
            title.Location = new Point(28, 10);

            lblSummary = new Label();
            lblSummary.AutoSize = true;
            lblSummary.ForeColor = Theme.HeaderText;
            lblSummary.BackColor = Color.Transparent;
            lblSummary.Location = new Point(30, 54);
            lblSummary.Text = "";

            lblHint = new Label();
            lblHint.Text = "F5 刷新    Ctrl+F 搜索    F2 改名    双击打开";
            lblHint.AutoSize = false;
            lblHint.TextAlign = ContentAlignment.MiddleRight;
            lblHint.ForeColor = Color.FromArgb(122, 134, 160);
            lblHint.BackColor = Color.Transparent;
            lblHint.Size = new Size(370, 22);
            lblHint.Location = new Point(560, 32);

            header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 86;
            header.Paint += OnHeaderPaint;
            header.Resize += delegate { lblHint.Location = new Point(header.Width - 394, 32); };
            header.Controls.Add(title);
            header.Controls.Add(lblSummary);
            header.Controls.Add(lblHint);

            // 列表外包一层，给表头留出上边距
            listWrap = new Panel();
            listWrap.Dock = DockStyle.Fill;
            listWrap.Padding = new Padding(0, 4, 0, 0);
            listWrap.BackColor = Theme.WindowBack;
            listWrap.Controls.Add(list);

            // 空状态提示
            lblEmptyTitle = new Label();
            lblEmptyTitle.AutoSize = false;
            lblEmptyTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblEmptyTitle.Font = new Font("Microsoft YaHei UI", 11F);
            lblEmptyTitle.ForeColor = Color.FromArgb(150, 157, 172);
            lblEmptyTitle.BackColor = Color.White;
            lblEmptyTitle.Text = "这里还没有书签";

            lblEmptyHint = new Label();
            lblEmptyHint.AutoSize = false;
            lblEmptyHint.TextAlign = ContentAlignment.MiddleCenter;
            lblEmptyHint.Font = new Font("Microsoft YaHei UI", 9F);
            lblEmptyHint.ForeColor = Color.FromArgb(178, 184, 196);
            lblEmptyHint.BackColor = Color.White;
            lblEmptyHint.Text = "用「导入当前窗口」把正在运行的窗口加进来，或用「添加文件夹」选择项目目录";

            panelEmpty = new Panel();
            panelEmpty.Dock = DockStyle.Fill;
            panelEmpty.BackColor = Color.White;
            panelEmpty.Visible = false;
            panelEmpty.Controls.Add(lblEmptyTitle);
            panelEmpty.Controls.Add(lblEmptyHint);
            panelEmpty.Resize += delegate { LayoutEmpty(); };
            listWrap.Controls.Add(panelEmpty);
            panelEmpty.BringToFront();

            // ---------- 底部：未加入书签的窗口 ----------
            listExtra = new BufferedListView();
            listExtra.Dock = DockStyle.Fill;
            listExtra.View = View.Details;
            listExtra.FullRowSelect = true;
            listExtra.MultiSelect = true;
            listExtra.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            listExtra.BorderStyle = BorderStyle.None;
            listExtra.HideSelection = false;
            listExtra.SmallImageList = il;
            listExtra.Font = new Font("Microsoft YaHei UI", 9F);
            listExtra.Columns.Add("窗口", 220);
            listExtra.Columns.Add("路径", 480);
            listExtra.Columns.Add("类型", 70);
            listExtra.DoubleClick += delegate { AddExtraSelected(); };
            listExtra.MouseUp += OnExtraMouseUp;
            listExtra.OwnerDraw = true;
            listExtra.DrawColumnHeader += OnDrawColumnHeader;
            listExtra.DrawItem += delegate (object s, DrawListViewItemEventArgs e) { e.DrawDefault = true; };
            listExtra.DrawSubItem += delegate (object s, DrawListViewSubItemEventArgs e) { e.DrawDefault = true; };

            lblExtraTitle = new Label();
            lblExtraTitle.AutoSize = true;
            lblExtraTitle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            lblExtraTitle.ForeColor = Color.FromArgb(96, 104, 120);
            lblExtraTitle.Location = new Point(24, 12);
            lblExtraTitle.Text = "未加入书签的窗口";

            btnAddSel = MakeButton("加入所选", 84, true);
            btnAddSel.Height = 28;
            btnAddSel.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            btnAddSel.Click += delegate { AddExtraSelected(); };

            btnAddAll = MakeButton("全部加入书签", 108, false);
            btnAddAll.Height = 28;
            btnAddAll.Click += delegate { AddAllExtras(); };

            panelExtra = new Panel();
            panelExtra.Dock = DockStyle.Bottom;
            panelExtra.Height = 184;
            panelExtra.BackColor = Color.White;
            panelExtra.Paint += delegate (object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.Border))
                    e.Graphics.DrawLine(p, 0, 0, panelExtra.Width, 0);
            };
            panelExtra.Resize += delegate { LayoutExtra(); };
            extraWrap = new Panel();
            extraWrap.Dock = DockStyle.Fill;
            extraWrap.Padding = new Padding(16, 42, 16, 10);
            extraWrap.BackColor = Theme.WindowBack;
            extraWrap.Controls.Add(listExtra);
            panelExtra.Controls.Add(extraWrap);
            panelExtra.Controls.Add(lblExtraTitle);
            panelExtra.Controls.Add(btnAddSel);
            panelExtra.Controls.Add(btnAddAll);
            // Controls[0] 在最上层，这里必须把标题和按钮提到前面
            extraWrap.SendToBack();
            lblExtraTitle.BringToFront();
            btnAddSel.BringToFront();
            btnAddAll.BringToFront();

            // Dock 顺序：先加的在外侧，因此倒序添加
            Controls.Add(listWrap);
            Controls.Add(panelExtra);
            Controls.Add(strip);
            Controls.Add(options);
            Controls.Add(toolbar);
            Controls.Add(header);

            // 右键菜单
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Microsoft YaHei UI", 9F);
            menu.Items.Add("打开这一个", null, delegate { OpenChecked(true); });
            menu.Items.Add("重命名（F2）", null, delegate { RenameSelected(); });
            menu.Items.Add("重新定位到…", null, delegate { RelocateSelected(); });
            menu.Items.Add("关闭这个窗口", null, delegate { CloseSelectedWindow(); });
            menu.Items.Add("在资源管理器中打开", null, delegate { RevealSelected(); });
            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem sortMenu = new ToolStripMenuItem("排序");
            ToolStripMenuItem sortManual = new ToolStripMenuItem("手动（拖动顺序）", null, delegate { SetSortMode(0); });
            ToolStripMenuItem sortName = new ToolStripMenuItem("按名称", null, delegate { SetSortMode(1); });
            ToolStripMenuItem sortRecent = new ToolStripMenuItem("按最近打开", null, delegate { SetSortMode(2); });
            sortManual.Checked = sortMode == 0;
            sortName.Checked = sortMode == 1;
            sortRecent.Checked = sortMode == 2;
            sortMenu.DropDownItems.Add(sortManual);
            sortMenu.DropDownItems.Add(sortName);
            sortMenu.DropDownItems.Add(sortRecent);
            menu.Items.Add(sortMenu);

            menu.Items.Add("重置列宽", null, delegate
            {
                userCols = null;
                LayoutColumns();
                SaveUiState();
                status.Text = "列宽已恢复自动布局";
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("全选（Ctrl+A）", null, delegate { CheckAll(true); });
            menu.Items.Add("全不选", null, delegate { CheckAll(false); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("删除", null, delegate { DeleteSelected(); });
            list.ContextMenuStrip = menu;
        }

        private void SetSortMode(int mode)
        {
            sortMode = mode;
            SaveUiState();
            RefreshRows();
            status.Text = mode == 0 ? "排序：手动" : (mode == 1 ? "排序：按名称" : "排序：按最近打开");
        }

        private FlatButton MakeButton(string text, int width, bool primary)
        {
            FlatButton b = new FlatButton();
            b.Text = text;
            b.Primary = primary;
            b.Size = new Size(width, primary ? 34 : 32);
            b.Font = new Font("Microsoft YaHei UI", 9F, primary ? FontStyle.Bold : FontStyle.Regular);
            return b;
        }

        private void LayoutToolbar()
        {
            if (toolbar == null) return;
            int cy = toolbar.Height / 2;

            int x = 20;
            Place(btnOpen, x, cy); x += btnOpen.Width + 8;
            Place(btnAll, x, cy); x += btnAll.Width + 20;
            Place(btnAdd, x, cy); x += btnAdd.Width + 8;
            Place(btnImport, x, cy);

            int rx = toolbar.Width - 20;
            rx -= btnRefresh.Width; Place(btnRefresh, rx, cy); rx -= 8;
            rx -= btnDel.Width; Place(btnDel, rx, cy); rx -= 14;
            rx -= btnDown.Width; Place(btnDown, rx, cy); rx -= 6;
            rx -= btnUp.Width; Place(btnUp, rx, cy);
        }

        private static void Place(Control c, int x, int centerY)
        {
            c.Location = new Point(x, centerY - c.Height / 2);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            SendMessage(txtSearch.Handle, EM_SETCUEBANNER, (IntPtr)1, "搜索名称或路径…");
            LayoutToolbar();
            LayoutColumns();
            LayoutExtra();
        }

        // 开机自启（--tray）时不显示主窗口，只留托盘图标
        protected override void SetVisibleCore(bool value)
        {
            if (startHidden)
            {
                value = false;
                if (!IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        private void OnHeaderPaint(object sender, PaintEventArgs e)
        {
            Rectangle r = new Rectangle(0, 0, header.Width, header.Height);
            using (LinearGradientBrush brush = new LinearGradientBrush(
                new Point(0, 0), new Point(r.Width, r.Height),
                Theme.HeaderTop, Theme.HeaderBottom))
            {
                e.Graphics.FillRectangle(brush, r);
            }
            // 顶部一条细高光
            using (SolidBrush b = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                e.Graphics.FillRectangle(b, new Rectangle(0, 0, r.Width, 1));
        }

        // ---------- 列表自绘 ----------
        private void OnListMouseMove(object sender, MouseEventArgs e)
        {
            ListViewHitTestInfo hit = list.HitTest(e.Location);
            SetHover(hit.Item == null ? -1 : hit.Item.Index);
        }

        private void SetHover(int idx)
        {
            if (idx == hoverIndex) return;
            int old = hoverIndex;
            hoverIndex = idx;
            if (old >= 0 && old < list.Items.Count) list.Invalidate(list.Items[old].Bounds);
            if (idx >= 0 && idx < list.Items.Count) list.Invalidate(list.Items[idx].Bounds);
        }

        private void OnDrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.FillRectangle(Theme.HeaderBg, e.Bounds);
            g.DrawLine(Theme.HeaderLine, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            int indent = (sender == list && e.ColumnIndex == 1) ? 32 : 12;
            Rectangle r = new Rectangle(e.Bounds.X + indent, e.Bounds.Y, Math.Max(10, e.Bounds.Width - indent - 6), e.Bounds.Height);
            TextRenderer.DrawText(g, e.Header.Text, headerFont, r, Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawCheckBox(Graphics g, Rectangle box, bool check)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (check)
            {
                using (GraphicsPath p = Theme.Round(box, 4))
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                    g.FillPath(b, p);
                using (Pen pen = new Pen(Color.White, 1.8f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, new PointF[]
                    {
                        new PointF(box.X + 4.0f, box.Y + 8.5f),
                        new PointF(box.X + 7.0f, box.Y + 11.5f),
                        new PointF(box.X + 12.0f, box.Y + 5.0f)
                    });
                }
            }
            else
            {
                using (GraphicsPath p = Theme.Round(box, 4))
                using (SolidBrush b = new SolidBrush(Color.White))
                    g.FillPath(b, p);
                using (GraphicsPath p = Theme.Round(box, 4))
                using (Pen pen = new Pen(Color.FromArgb(198, 205, 218)))
                    g.DrawPath(pen, p);
            }
            g.SmoothingMode = old;
        }

        private void DrawChip(Graphics g, Rectangle area, string text, Color fg, Color back)
        {
            Size sz = TextRenderer.MeasureText(text, chipFont);
            int h = 20;
            int w = Math.Min(sz.Width + 26, area.Width - 8);
            if (w < 30) return;
            Rectangle chip = new Rectangle(area.X + 4, area.Y + (area.Height - h) / 2, w, h);

            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Theme.Round(chip, h / 2))
            using (SolidBrush b = new SolidBrush(back))
                g.FillPath(b, p);
            g.SmoothingMode = old;

            using (SolidBrush dot = new SolidBrush(fg))
                g.FillEllipse(dot, chip.X + 8, chip.Y + h / 2 - 2, 5, 5);

            Rectangle tr = new Rectangle(chip.X + 17, chip.Y, chip.Width - 20, chip.Height);
            TextRenderer.DrawText(g, text, chipFont, tr, fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        // ---------- 文件夹图标（走系统 Shell，跟资源管理器一致） ----------
        private readonly Dictionary<string, Bitmap> iconCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW")]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private Bitmap GetItemIcon(Bookmark b)
        {
            string path = ToLocalPath(b.uri);
            string key;
            if (path == null) key = "remote";
            else if (Directory.Exists(path)) key = "dir";
            else if (File.Exists(path)) key = "file";
            else key = "missing";

            Bitmap cached;
            if (iconCache.TryGetValue(key, out cached)) return cached;

            Bitmap bmp = null;
            try
            {
                SHFILEINFO info = new SHFILEINFO();
                const uint SHGFI_ICON = 0x100;
                const uint SHGFI_SMALLICON = 0x1;
                const uint SHGFI_USEFILEATTRIBUTES = 0x10;
                const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
                const uint FILE_ATTRIBUTE_NORMAL = 0x80;

                string target = key == "file" && path != null ? path : "C:\\";
                uint attr = key == "file" ? FILE_ATTRIBUTE_NORMAL : FILE_ATTRIBUTE_DIRECTORY;

                IntPtr ok = SHGetFileInfo(target, attr, ref info, (uint)Marshal.SizeOf(info),
                    SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
                if (ok != IntPtr.Zero && info.hIcon != IntPtr.Zero)
                {
                    using (Icon ic = Icon.FromHandle(info.hIcon))
                        bmp = ic.ToBitmap();
                    DestroyIcon(info.hIcon);
                }
            }
            catch (Exception ex) { Log.Write("GetItemIcon", ex); }

            iconCache[key] = bmp;
            return bmp;
        }

        private void OnDrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            Graphics g = e.Graphics;
            bool selected = e.Item.Selected;
            bool hover = e.ItemIndex == hoverIndex && !selected;
            SolidBrush bg = selected ? Theme.BgSelected : (hover ? Theme.BgHover : Theme.BgWhite);
            g.FillRectangle(bg, e.Bounds);
            if (!selected)
                g.DrawLine(Theme.RowLine, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            Bookmark b = e.Item.Tag as Bookmark;
            if (b == null) return;

            Color nameColor = b.Missing ? Theme.Danger
                : (b.OpenNow ? Theme.NameMuted : Theme.TextMain);

            // 第 0 列：状态胶囊
            if (e.ColumnIndex == 0)
            {
                if (b.Missing) DrawChip(g, e.Bounds, "路径失效", Color.FromArgb(190, 58, 58), Theme.ChipRedBack);
                else if (b.OpenNow) DrawChip(g, e.Bounds, "已打开", Color.FromArgb(28, 138, 78), Theme.ChipGreenBack);
                else DrawChip(g, e.Bounds, "未打开", Theme.IsDark ? Color.FromArgb(170, 178, 192) : Color.FromArgb(110, 118, 132), Theme.ChipGrayBack);
                return;
            }

            Rectangle r = new Rectangle(e.Bounds.X + 12, e.Bounds.Y,
                Math.Max(10, e.Bounds.Width - 18), e.Bounds.Height);

            // 第 1 列：勾选框 + 图标 + 名称
            if (e.ColumnIndex == 1)
            {
                Rectangle box = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + (e.Bounds.Height - 16) / 2, 16, 16);
                DrawCheckBox(g, box, b.Selected);

                int iconX = box.Right + 8;
                Bitmap ico = GetItemIcon(b);
                if (ico != null)
                {
                    g.DrawImage(ico, new Rectangle(iconX, e.Bounds.Y + (e.Bounds.Height - 16) / 2, 16, 16));
                    iconX += 22;
                }

                Rectangle tr = new Rectangle(iconX, e.Bounds.Y,
                    Math.Max(10, e.Bounds.Right - iconX - 8), e.Bounds.Height);
                TextRenderer.DrawText(g, b.name == null ? "" : b.name, listFont, tr, nameColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }

            // 第 2 列：路径
            if (e.ColumnIndex == 2)
            {
                TextRenderer.DrawText(g, PathText(b), listFont, r,
                    b.Missing ? Theme.Danger : Theme.PathColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                return;
            }

            // 第 3 列：类型
            TextRenderer.DrawText(g, TypeText(b), listFont, r, Theme.TypeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void LayoutEmpty()
        {
            if (panelEmpty == null) return;
            int w = panelEmpty.ClientSize.Width;
            int h = panelEmpty.ClientSize.Height;
            int top = Math.Max(40, h / 2 - 46);
            lblEmptyTitle.Bounds = new Rectangle(24, top, Math.Max(100, w - 48), 26);
            lblEmptyHint.Bounds = new Rectangle(24, top + 30, Math.Max(100, w - 48), 24);
        }

        private void LayoutExtra()
        {
            if (panelExtra == null || btnAddAll == null) return;
            btnAddAll.Location = new Point(panelExtra.Width - 24 - btnAddAll.Width, 8);
            btnAddSel.Location = new Point(btnAddAll.Left - 8 - btnAddSel.Width, 8);
            lblExtraTitle.Location = new Point(24, 14);

            int w = listExtra.ClientSize.Width;
            if (w > 0 && listExtra.Columns.Count == 3)
            {
                listExtra.Columns[0].Width = Math.Max(140, Math.Min(280, (int)(w * 0.26)));
                listExtra.Columns[1].Width = Math.Max(160, w - listExtra.Columns[0].Width - 76);
                listExtra.Columns[2].Width = 70;
            }
        }

        private void OnExtraMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            ListViewItem hit = listExtra.GetItemAt(e.X, e.Y);
            if (hit == null) return;
            if (!hit.Selected)
            {
                listExtra.SelectedItems.Clear();
                hit.Selected = true;
            }
            ContextMenuStrip m = new ContextMenuStrip();
            m.Font = new Font("Microsoft YaHei UI", 9F);
            m.Items.Add("加入书签", null, delegate { AddExtraSelected(); });
            m.Items.Add("立即打开这个窗口", null, delegate { OpenSingle(hit.Tag as Bookmark); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("加入全部未加书签的窗口", null, delegate { AddAllExtras(); });
            m.Show(listExtra, e.Location);
        }

        // ==================== 拖拽 / 粘贴导入 ====================
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private void OnDragEnterFiles(object sender, DragEventArgs e)
        {
            e.Effect = (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDragDropFiles(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0) return;
            AddPathsToBookmarks(paths);
        }

        private void AddPathsToBookmarks(string[] paths)
        {
            int added = 0;
            foreach (string p in paths)
            {
                try
                {
                    if (Directory.Exists(p))
                    {
                        Bookmark b = new Bookmark();
                        b.uri = UriFromPath(p);
                        b.kind = "folder";
                        b.product = "codebuddy-cn";
                        b.name = Path.GetFileName(p.TrimEnd('\\'));
                        if (string.IsNullOrEmpty(b.name)) b.name = p;
                        if (AddUnique(b)) added++;
                    }
                    else if (File.Exists(p) && p.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase))
                    {
                        Bookmark b = new Bookmark();
                        b.uri = UriFromPath(p);
                        b.kind = "workspace";
                        b.product = "codebuddy-cn";
                        b.name = Path.GetFileNameWithoutExtension(p);
                        if (AddUnique(b)) added++;
                    }
                }
                catch (Exception ex) { Log.Write("AddPaths", ex); }
            }

            if (added > 0)
            {
                SaveStore();
                RefreshRows();
                status.Text = "已加入 " + added + " 个书签";
            }
            else
            {
                status.Text = "没有可加入的文件夹（拖入文件夹或 .code-workspace 文件）";
            }
        }

        private void PastePaths()
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    System.Collections.Specialized.StringCollection sc = Clipboard.GetFileDropList();
                    if (sc != null && sc.Count > 0)
                    {
                        string[] files = new string[sc.Count];
                        sc.CopyTo(files, 0);
                        AddPathsToBookmarks(files);
                        return;
                    }
                }

                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText().Trim().Trim('"');
                    if (text.Length > 0) { AddPathsToBookmarks(new string[] { text }); return; }
                }
                status.Text = "剪贴板里没有文件夹或路径";
            }
            catch (Exception ex)
            {
                Log.Write("PastePaths", ex);
                status.Text = "粘贴失败：" + ex.Message;
            }
        }

        // ==================== 重新定位 / 关闭窗口 ====================
        private void RelocateSelected()
        {
            if (list.SelectedItems.Count == 0) { status.Text = "请先点选一行"; return; }
            Bookmark b = list.SelectedItems[0].Tag as Bookmark;
            if (b == null) return;

            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择「" + b.name + "」的新位置";
                dlg.ShowNewFolderButton = false;
                string cur = ToLocalPath(b.uri);
                if (!string.IsNullOrEmpty(cur) && Directory.Exists(cur)) dlg.SelectedPath = cur;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string p = dlg.SelectedPath;
                b.uri = UriFromPath(p);
                b.kind = "folder";
                b.remoteAuthority = null;
                if (string.IsNullOrEmpty(b.name) || b.name.StartsWith("远程")) b.name = Path.GetFileName(p);
                SaveStore();
                RefreshRows();
                status.Text = "已重新定位到 " + p;
            }
        }

        private void CloseSelectedWindow()
        {
            if (list.SelectedItems.Count == 0) { status.Text = "请先点选一行"; return; }
            Bookmark b = list.SelectedItems[0].Tag as Bookmark;
            if (b == null) return;
            if (!b.OpenNow) { status.Text = "该窗口当前没有打开"; return; }

            IntPtr h = FindWindowFor(b);
            if (h == IntPtr.Zero) { status.Text = "没找到对应的窗口"; return; }

            PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero);   // WM_CLOSE
            status.Text = "已关闭：" + b.name;
            Task.Delay(800).ContinueWith(delegate { try { BeginInvoke(new Action(RefreshStatesAsync)); } catch { } });
        }

        private void LayoutColumns()
        {
            if (list.Columns.Count != 4) return;

            applyingCols = true;
            try
            {
                // 用户手动拖过列宽就尊重用户设置
                if (userCols != null && userCols.Length == 4)
                {
                    for (int i = 0; i < 4; i++)
                        list.Columns[i].Width = Math.Max(50, userCols[i]);
                    return;
                }

                int w = list.ClientSize.Width;
                if (w <= 0) return;
                int state = 92;
                int name = Math.Max(180, Math.Min(330, (int)((w - state) * 0.30)));
                int type = 76;
                int path = Math.Max(160, w - state - name - type - 8);
                list.Columns[0].Width = state;
                list.Columns[1].Width = name;
                list.Columns[2].Width = path;
                list.Columns[3].Width = type;
            }
            finally { applyingCols = false; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5) { RefreshRows(); return true; }
            if (keyData == (Keys.Control | Keys.F)) { txtSearch.Focus(); txtSearch.SelectAll(); return true; }
            if (keyData == (Keys.Control | Keys.V)) { PastePaths(); return true; }
            if (keyData == Keys.Escape && ActiveControl == txtSearch)
            {
                txtSearch.Text = "";
                list.Focus();
                return true;
            }
            if (keyData == (Keys.Control | Keys.A) && ActiveControl == list) { CheckAll(true); return true; }
            if (ActiveControl == list)
            {
                if (keyData == Keys.Delete) { DeleteSelected(); return true; }
                if (keyData == Keys.Enter) { OpenChecked(true); return true; }
                if (keyData == Keys.F2) { RenameSelected(); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ==================== 产品 ====================
        private static List<Product> BuildProducts()
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            List<Product> ps = new List<Product>();

            Product cb = new Product();
            cb.Id = "codebuddy-cn";
            cb.Name = "CodeBuddy CN";
            cb.UserData = Path.Combine(roaming, "CodeBuddy CN");
            cb.ExeCandidates = new string[]
            {
                Path.Combine(local, @"Programs\CodeBuddy CN\CodeBuddy CN.exe"),
                Path.Combine(pf, @"CodeBuddy CN\CodeBuddy CN.exe"),
                Path.Combine(pf86, @"CodeBuddy CN\CodeBuddy CN.exe")
            };
            cb.ProcessNames = new string[] { "CodeBuddy CN", "CodeBuddy" };
            ps.Add(cb);

            Product wb = new Product();
            wb.Id = "workbuddy";
            wb.Name = "WorkBuddy";
            wb.UserData = Path.Combine(roaming, "WorkBuddy");
            wb.ExeCandidates = new string[]
            {
                Path.Combine(local, @"Programs\WorkBuddy\WorkBuddy.exe"),
                Path.Combine(pf, @"WorkBuddy\WorkBuddy.exe")
            };
            wb.ProcessNames = new string[] { "WorkBuddy" };
            ps.Add(wb);

            Product trae = new Product();
            trae.Id = "trae-cn";
            trae.Name = "Trae CN";
            trae.UserData = Path.Combine(roaming, "Trae CN");
            trae.ExeCandidates = new string[]
            {
                Path.Combine(local, @"Programs\Trae CN\Trae CN.exe"),
                Path.Combine(pf, @"Trae CN\Trae CN.exe")
            };
            trae.ProcessNames = new string[] { "Trae CN", "Trae" };
            ps.Add(trae);

            Product code = new Product();
            code.Id = "vscode";
            code.Name = "VS Code";
            code.UserData = Path.Combine(roaming, "Code");
            code.ExeCandidates = new string[]
            {
                Path.Combine(local, @"Programs\Microsoft VS Code\Code.exe"),
                Path.Combine(pf, @"Microsoft VS Code\Code.exe")
            };
            code.ProcessNames = new string[] { "Code" };
            ps.Add(code);

            return ps;
        }

        private Product ProductById(string id)
        {
            foreach (Product p in products)
                if (p.Id == id) return p;
            return products[0];
        }

        // 按当前排序方式返回书签顺序（手动 = 存储顺序）
        private List<Bookmark> OrderedItems()
        {
            List<Bookmark> ordered = new List<Bookmark>(store.items);
            if (sortMode == 1)
            {
                ordered.Sort(delegate (Bookmark a, Bookmark b)
                {
                    return string.Compare(a.name, b.name, StringComparison.CurrentCultureIgnoreCase);
                });
            }
            else if (sortMode == 2)
            {
                ordered.Sort(delegate (Bookmark a, Bookmark b)
                {
                    return string.Compare(b.lastOpened, a.lastOpened, StringComparison.Ordinal);
                });
            }
            return ordered;
        }

        // file:///d%3A/xxx -> d:\xxx
        private static string ToLocalPath(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return null;
            if (!uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) return null;
            try
            {
                string rest = uri.Substring(7);
                bool unc = rest.Length > 0 && rest[0] != '/';
                rest = Uri.UnescapeDataString(rest);
                if (rest.StartsWith("/")) rest = rest.Substring(1);
                rest = rest.Replace('/', '\\');
                if (unc) rest = "\\\\" + rest;
                else if (rest.Length >= 2 && rest[1] == ':')
                    rest = char.ToUpper(rest[0]) + rest.Substring(1);
                return rest;
            }
            catch { return null; }
        }

        // vscode-remote://codebuddy-remote-ssh+win/e:/Android/xx
        //   ->  [SSH: win]  /e:/Android/xx
        private static string RemoteDisplay(Bookmark b)
        {
            string rest;
            string auth = b == null ? null : b.remoteAuthority;
            string uri = b == null || b.uri == null ? "" : b.uri;
            try
            {
                int idx = uri.IndexOf("://", StringComparison.Ordinal);
                if (idx > 0)
                {
                    string after = uri.Substring(idx + 3);
                    int slash = after.IndexOf('/');
                    if (slash > 0)
                    {
                        string a = Uri.UnescapeDataString(after.Substring(0, slash));
                        if (string.IsNullOrEmpty(auth)) auth = a;
                        rest = Uri.UnescapeDataString(after.Substring(slash));
                    }
                    else
                    {
                        rest = Uri.UnescapeDataString(after);
                    }
                }
                else
                {
                    rest = Uri.UnescapeDataString(uri);
                }
            }
            catch { rest = uri; }

            string label = RemoteLabel(auth);
            return label == null ? "远程  " + rest : "[" + label + "]  " + rest;
        }

        // codebuddy-remote-ssh+win -> SSH: win
        private static string RemoteLabel(string auth)
        {
            if (string.IsNullOrEmpty(auth)) return null;
            string a = auth;
            if (a.StartsWith("codebuddy-remote-", StringComparison.OrdinalIgnoreCase))
                a = a.Substring("codebuddy-remote-".Length);

            string kind = a;
            string target = null;
            int plus = a.IndexOf('+');
            if (plus > 0)
            {
                kind = a.Substring(0, plus);
                target = a.Substring(plus + 1);
            }

            if (kind.Equals("ssh", StringComparison.OrdinalIgnoreCase))
                return target == null ? "SSH" : "SSH: " + target;
            if (kind.Equals("wsl", StringComparison.OrdinalIgnoreCase))
                return target == null ? "WSL" : "WSL: " + target;
            if (kind.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0)
                return "容器";
            return target == null ? kind : kind + ": " + target;
        }

        private static string PathText(Bookmark b)
        {
            string local = ToLocalPath(b.uri);
            return local != null ? local : RemoteDisplay(b);
        }

        private static string TypeText(Bookmark b)
        {
            if (ToLocalPath(b.uri) != null)
                return b.kind == "workspace" ? "工作区" : "文件夹";

            string a = b.remoteAuthority ?? "";
            if (a.IndexOf("ssh", StringComparison.OrdinalIgnoreCase) >= 0) return "SSH";
            if (a.IndexOf("wsl", StringComparison.OrdinalIgnoreCase) >= 0) return "WSL";
            if (a.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0) return "容器";
            return "远程";
        }

        private static string UriFromPath(string path)
        {
            string p = path.Replace('\\', '/');
            if (!p.StartsWith("/")) p = "/" + p;
            return "file://" + Uri.EscapeUriString(p).Replace("%3A", ":");
        }

        private static string LeafName(string uri)
        {
            try
            {
                string p = ToLocalPath(uri);
                if (p != null)
                {
                    string n = Path.GetFileName(p.TrimEnd('\\'));
                    return string.IsNullOrEmpty(n) ? p : n;
                }
                string decoded = Uri.UnescapeDataString(uri);
                int i = decoded.LastIndexOf('/');
                string last = i >= 0 ? decoded.Substring(i + 1) : decoded;
                return string.IsNullOrEmpty(last) ? decoded : last;
            }
            catch { return uri; }
        }

        private static Dictionary<string, object> AsDict(object o) { return o as Dictionary<string, object>; }

        private static object GetVal(Dictionary<string, object> d, string key)
        {
            object v;
            if (d != null && d.TryGetValue(key, out v)) return v;
            return null;
        }

        private static string GetStr(Dictionary<string, object> d, string key)
        {
            object v = GetVal(d, key);
            return v == null ? null : v.ToString();
        }

        private List<Bookmark> CollectLive()
        {
            List<Bookmark> result = new List<Bookmark>();
            JavaScriptSerializer json = new JavaScriptSerializer();
            json.MaxJsonLength = 8 * 1024 * 1024;

            foreach (Product p in products)
            {
                if (!p.Running) continue;
                if (!File.Exists(p.StoragePath)) continue;
                Dictionary<string, object> root;
                try
                {
                    string raw = File.ReadAllText(p.StoragePath, Encoding.UTF8);
                    if (raw.Trim().Length == 0) continue;
                    root = AsDict(json.DeserializeObject(raw));
                }
                catch { continue; }
                if (root == null) continue;
                Dictionary<string, object> state = AsDict(GetVal(root, "windowsState"));
                if (state == null) continue;

                List<object> wins = new List<object>();
                object[] opened = GetVal(state, "openedWindows") as object[];
                if (opened != null) wins.AddRange(opened);
                object lastActive = GetVal(state, "lastActiveWindow");
                if (lastActive != null)
                {
                    Dictionary<string, object> ld = AsDict(lastActive);
                    if (ld != null && (GetStr(ld, "folder") != null || GetStr(ld, "workspace") != null))
                        wins.Add(lastActive);
                }

                foreach (object w in wins)
                {
                    Dictionary<string, object> d = AsDict(w);
                    if (d == null) continue;
                    string folder = GetStr(d, "folder");
                    string ws = GetStr(d, "workspace");
                    if (string.IsNullOrEmpty(folder) && string.IsNullOrEmpty(ws)) continue;

                    Bookmark b = new Bookmark();
                    b.uri = !string.IsNullOrEmpty(folder) ? folder : ws;
                    b.kind = !string.IsNullOrEmpty(folder) ? "folder" : "workspace";
                    b.remoteAuthority = GetStr(d, "remoteAuthority");
                    b.product = p.Id;
                    b.name = LeafName(b.uri);
                    b.OpenNow = true;
                    result.Add(b);
                }
            }
            return result;
        }

        // ==================== 书签存取 ====================
        private void LoadStore()
        {
            if (File.Exists(storePath))
            {
                try
                {
                    string raw = File.ReadAllText(storePath, Encoding.UTF8);
                    JavaScriptSerializer json = new JavaScriptSerializer();
                    json.MaxJsonLength = 8 * 1024 * 1024;
                    BookmarkFile f = json.Deserialize<BookmarkFile>(raw);
                    if (f != null && f.items != null) store = f;
                }
                catch (Exception ex)
                {
                    Log.Write("LoadStore", ex);
                    // 主文件坏了就退回备份
                    try
                    {
                        string bak = storePath + ".bak";
                        if (File.Exists(bak))
                        {
                            string raw = File.ReadAllText(bak, Encoding.UTF8);
                            JavaScriptSerializer json = new JavaScriptSerializer();
                            json.MaxJsonLength = 8 * 1024 * 1024;
                            BookmarkFile f = json.Deserialize<BookmarkFile>(raw);
                            if (f != null && f.items != null)
                            {
                                store = f;
                                status.Text = "书签文件损坏，已从 bookmarks.json.bak 恢复";
                            }
                        }
                    }
                    catch (Exception ex2) { Log.Write("LoadStore.bak", ex2); }
                }
            }

            if (store.items.Count == 0)
            {
                foreach (Bookmark b in CollectLive()) AddUnique(b);
                foreach (Bookmark b in LoadSnapshotBookmarks()) AddUnique(b);
                SaveStore();
                status.Text = "已根据当前窗口自动建立初始书签";
            }
        }

        private List<Bookmark> LoadSnapshotBookmarks()
        {
            List<Bookmark> result = new List<Bookmark>();
            if (!File.Exists(snapshotPath)) return result;
            try
            {
                string raw = File.ReadAllText(snapshotPath, Encoding.UTF8);
                JavaScriptSerializer json = new JavaScriptSerializer();
                json.MaxJsonLength = 8 * 1024 * 1024;
                Dictionary<string, object> root = AsDict(json.DeserializeObject(raw));
                if (root == null) return result;
                object[] wins = GetVal(root, "windows") as object[];
                if (wins == null) return result;
                foreach (object w in wins)
                {
                    Dictionary<string, object> d = AsDict(w);
                    if (d == null) continue;
                    string uri = GetStr(d, "uri");
                    if (string.IsNullOrEmpty(uri)) continue;
                    Bookmark b = new Bookmark();
                    b.uri = uri;
                    b.kind = GetStr(d, "kind");
                    if (b.kind == null) b.kind = "folder";
                    b.remoteAuthority = GetStr(d, "remoteAuthority");
                    b.product = GetStr(d, "product");
                    if (b.product == null) b.product = "codebuddy-cn";
                    b.name = LeafName(uri);
                    result.Add(b);
                }
            }
            catch { }
            return result;
        }

        private void SaveStore()
        {
            try
            {
                store.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                JavaScriptSerializer json = new JavaScriptSerializer();
                json.MaxJsonLength = 8 * 1024 * 1024;
                string text = json.Serialize(store);

                // 先写临时文件再替换，避免写一半断电把书签全毁了；旧文件留一份 .bak
                string tmp = storePath + ".tmp";
                File.WriteAllText(tmp, text, new UTF8Encoding(false));

                if (File.Exists(storePath))
                {
                    try { File.Copy(storePath, storePath + ".bak", true); }
                    catch { }
                    File.Replace(tmp, storePath, null);
                }
                else
                {
                    File.Move(tmp, storePath);
                }
            }
            catch (Exception ex)
            {
                Log.Write("SaveStore", ex);
                MessageBox.Show(this, "保存书签失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool AddUnique(Bookmark b)
        {
            foreach (Bookmark o in store.items)
                if (string.Equals(o.uri, b.uri, StringComparison.OrdinalIgnoreCase)) return false;
            store.items.Add(b);
            return true;
        }

        // ==================== 列表 ====================
        private void RefreshRows()
        {
            List<Bookmark> live = liveCache;
            List<string> openUris = new List<string>();
            foreach (Bookmark b in live) openUris.Add(b.uri.ToLower());

            string filter = txtSearch == null ? "" : txtSearch.Text.Trim().ToLower();

            list.BeginUpdate();
            list.Items.Clear();
            int missing = 0;
            foreach (Bookmark b in OrderedItems())
            {
                b.OpenNow = openUris.Contains(b.uri.ToLower());
                if (filter.Length > 0 && !(b.name != null && b.name.ToLower().Contains(filter))
                    && !(b.uri != null && b.uri.ToLower().Contains(filter)))
                    continue;

                string local = ToLocalPath(b.uri);
                bool exists = local == null || Directory.Exists(local) || File.Exists(local);
                b.Missing = !exists;
                if (!exists) missing++;

                string stateText = !exists ? "路径不存在" : (b.OpenNow ? "已打开" : "未打开");
                ListViewItem row = new ListViewItem(stateText, 0);
                row.SubItems.Add(b.name == null ? "" : b.name);
                row.SubItems.Add(PathText(b));
                row.SubItems.Add(TypeText(b));
                row.Tag = b;

                if (!exists) row.ForeColor = Theme.Danger;
                else if (b.OpenNow) row.ForeColor = Color.FromArgb(152, 158, 170);
                else row.ForeColor = Theme.TextMain;

                list.Items.Add(row);
            }
            list.EndUpdate();

            if (list.Items.Count == 0)
            {
                panelEmpty.Visible = true;
                if (filter.Length > 0)
                {
                    lblEmptyTitle.Text = "没有匹配「" + txtSearch.Text.Trim() + "」的书签";
                    lblEmptyHint.Text = "换个关键词试试，或清空搜索框";
                }
                else
                {
                    lblEmptyTitle.Text = "这里还没有书签";
                    lblEmptyHint.Text = "用「导入当前窗口」把正在运行的窗口加进来，或用「添加文件夹」选择项目目录";
                }
                LayoutEmpty();
                panelEmpty.BringToFront();
            }
            else
            {
                panelEmpty.Visible = false;
            }

            FillExtras(live);
            UpdateSummary(live, missing);
        }

        // 填充底部"未加入书签的窗口"
        private void FillExtras(List<Bookmark> live)
        {
            List<string> bookmarked = new List<string>();
            foreach (Bookmark b in store.items) bookmarked.Add(b.uri.ToLower());

            List<Bookmark> fresh = new List<Bookmark>();
            List<string> seen = new List<string>();
            foreach (Bookmark b in live)
            {
                string u = b.uri.ToLower();
                if (bookmarked.Contains(u) || seen.Contains(u)) continue;
                seen.Add(u);
                fresh.Add(b);
            }

            // 和上次完全一致就不重建列表，避免每 5 秒闪一下、丢失选中状态
            bool same = fresh.Count == extras.Count;
            if (same)
            {
                for (int i = 0; i < fresh.Count; i++)
                {
                    if (!string.Equals(fresh[i].uri, extras[i].uri, StringComparison.OrdinalIgnoreCase))
                    {
                        same = false;
                        break;
                    }
                }
            }
            if (same)
            {
                panelExtra.Visible = extras.Count > 0;
                return;
            }

            extras = fresh;
            listExtra.BeginUpdate();
            listExtra.Items.Clear();
            foreach (Bookmark b in extras)
            {
                ListViewItem row = new ListViewItem(b.name == null ? "" : b.name, 0);
                row.SubItems.Add(PathText(b));
                row.SubItems.Add(TypeText(b));
                row.Tag = b;
                listExtra.Items.Add(row);
            }
            listExtra.EndUpdate();

            lblExtraTitle.Text = "未加入书签的窗口（" + extras.Count + "）";
            panelExtra.Visible = extras.Count > 0;
            if (panelExtra.Visible) LayoutExtra();
        }

        private void AddExtraSelected()
        {
            if (listExtra.SelectedItems.Count == 0)
            {
                status.Text = "请先在下方点选一个窗口";
                return;
            }
            int added = 0;
            foreach (ListViewItem row in listExtra.SelectedItems)
            {
                Bookmark b = row.Tag as Bookmark;
                if (b != null && AddUnique(b)) added++;
            }
            if (added > 0) SaveStore();
            RefreshRows();
            status.Text = added > 0 ? ("已加入 " + added + " 个书签") : "这些窗口已经在书签里了";
        }

        private void AddAllExtras()
        {
            if (extras.Count == 0)
            {
                status.Text = "没有未加入书签的窗口";
                return;
            }
            int added = 0;
            foreach (Bookmark b in extras)
                if (AddUnique(b)) added++;
            if (added > 0) SaveStore();
            RefreshRows();
            status.Text = "已加入 " + added + " 个书签";
        }

        // ---- 把已经打开的窗口提到最前 ----
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        // 按标题 + 进程名找该项目的窗口
        private IntPtr FindWindowFor(Bookmark b)
        {
            Product p = ProductById(b.product);
            string exe = p.Exe;
            string procName = exe == null ? null : Path.GetFileNameWithoutExtension(exe);
            string leaf = LeafName(b.uri);
            IntPtr found = IntPtr.Zero;

            if (procName == null || string.IsNullOrEmpty(leaf)) return IntPtr.Zero;

            EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (!IsWindowVisible(h)) return true;
                StringBuilder sb = new StringBuilder(512);
                GetWindowText(h, sb, 512);
                string t = sb.ToString();
                if (t.Length == 0) return true;
                if (t.IndexOf(leaf, StringComparison.OrdinalIgnoreCase) < 0) return true;
                if (t.IndexOf(p.Name, StringComparison.OrdinalIgnoreCase) < 0) return true;

                uint pid;
                GetWindowThreadProcessId(h, out pid);
                try
                {
                    if (Process.GetProcessById((int)pid).ProcessName != procName) return true;
                }
                catch { return true; }

                found = h;
                return false;
            }, IntPtr.Zero);

            return found;
        }

        private bool RaiseExistingWindow(Bookmark b, out string message)
        {
            message = null;
            Product p = ProductById(b.product);
            string exe = p.Exe;
            IntPtr found = FindWindowFor(b);

            if (found != IntPtr.Zero)
            {
                if (IsIconic(found)) ShowWindow(found, 9);   // SW_RESTORE
                BringWindowToTop(found);
                SetForegroundWindow(found);
                return true;
            }

            // 兜底：让 IDE 自己去激活那个已打开该目录的窗口（不带 -n）
            if (exe != null)
            {
                string local = ToLocalPath(b.uri);
                string args = local != null ? "\"" + local + "\"" : "--folder-uri \"" + b.uri + "\"";
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                    psi.UseShellExecute = false;
                    Process.Start(psi);
                    return true;
                }
                catch (Exception ex)
                {
                    message = ex.Message;
                    return false;
                }
            }
            message = "找不到该窗口";
            return false;
        }

        private async void OpenSingle(Bookmark b)
        {
            if (busy || b == null) return;
            string local = ToLocalPath(b.uri);
            if (local != null && !Directory.Exists(local) && !File.Exists(local))
            {
                status.Text = "路径不存在：" + local;
                return;
            }
            Product p = ProductById(b.product);
            string exe = p.Exe;
            if (exe == null)
            {
                status.Text = "找不到 " + p.Name + " 的可执行文件";
                return;
            }
            string args = local != null ? "-n \"" + local + "\"" : "-n --folder-uri \"" + b.uri + "\"";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                Process.Start(psi);
                b.OpenNow = true;
                b.lastOpened = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                SaveStore();
                status.Text = "已打开：" + b.name;
                await Task.Delay(1200);
                RefreshStates();
            }
            catch (Exception ex)
            {
                status.Text = "打开失败：" + ex.Message;
            }
        }

        private void UpdateSummary(List<Bookmark> live, int missing)
        {
            int opened = 0;
            List<string> bookmarked = new List<string>();
            foreach (Bookmark b in store.items)
            {
                bookmarked.Add(b.uri.ToLower());
                if (b.OpenNow) opened++;
            }
            int extra = 0;
            List<string> seen = new List<string>();
            foreach (Bookmark b in live)
            {
                string u = b.uri.ToLower();
                if (bookmarked.Contains(u) || seen.Contains(u)) continue;
                seen.Add(u);
                extra++;
            }

            string s = string.Format("共 {0} 个书签   ·   {1} 个正打开", store.items.Count, opened);
            if (extra > 0) s += "   ·   " + extra + " 个窗口未加书签";
            if (missing > 0) s += "   ·   " + missing + " 个路径失效";
            if (lblSummary.Text != s) lblSummary.Text = s;
            string right = "书签文件  " + Path.GetFileName(storePath);
            if (statusRight.Text != right) statusRight.Text = right;
        }

        private string lastSignature = "";

        // 各 IDE 的 storage.json 修改时间 + 进程状态；没变化就不用重新解析
        private string ComputeSignature()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Product p in products)
            {
                string stamp = "-";
                try
                {
                    string path = p.StoragePath;
                    if (File.Exists(path)) stamp = File.GetLastWriteTimeUtc(path).Ticks.ToString();
                }
                catch { }
                sb.Append(p.Id).Append(':').Append(p.Running ? '1' : '0').Append('@').Append(stamp).Append(';');
            }
            return sb.ToString();
        }

        private bool StateChanged()
        {
            string sig = ComputeSignature();
            if (sig == lastSignature) return false;
            lastSignature = sig;
            return true;
        }

        // 同步刷新（打开/关闭窗口后立刻用）
        private void RefreshStates()
        {
            List<Bookmark> live = CollectLive();
            lastSignature = ComputeSignature();
            ApplyStates(live);
        }

        // 后台刷新：把读文件、枚举进程这些耗时活儿挪到线程池，界面不再卡顿
        private void RefreshStatesAsync()
        {
            if (statesBusy || IsDisposed) return;
            statesBusy = true;

            Task.Run((Action)delegate
            {
                List<Bookmark> live = null;
                bool changed;
                try { changed = StateChanged(); }
                catch { changed = false; }

                if (changed)
                {
                    try { live = CollectLive(); }
                    catch { live = null; }
                }

                try
                {
                    BeginInvoke((Action)delegate
                    {
                        statesBusy = false;
                        if (live != null && !IsDisposed) ApplyStates(live);
                    });
                }
                catch { statesBusy = false; }
            });
        }

        private void ApplyStates(List<Bookmark> live)
        {
            liveCache = live;
            List<string> openUris = new List<string>();
            foreach (Bookmark b in live) openUris.Add(b.uri.ToLower());

            int missing = 0;
            if (list.Items.Count > 0)
            {
                list.BeginUpdate();
                foreach (ListViewItem row in list.Items)
                {
                    Bookmark b = row.Tag as Bookmark;
                    if (b == null) continue;
                    b.OpenNow = openUris.Contains(b.uri.ToLower());
                    string local = ToLocalPath(b.uri);
                    bool exists = local == null || Directory.Exists(local) || File.Exists(local);
                    b.Missing = !exists;
                    if (!exists) missing++;

                    string stateText = !exists ? "路径不存在" : (b.OpenNow ? "已打开" : "未打开");
                    Color fore = !exists ? Theme.Danger
                        : (b.OpenNow ? Color.FromArgb(152, 158, 170) : Theme.TextMain);

                    // 只有变化时才写回，避免每 5 秒整表重绘
                    if (row.SubItems[0].Text != stateText) row.SubItems[0].Text = stateText;
                    if (row.ForeColor != fore) row.ForeColor = fore;
                }
                list.EndUpdate();
            }
            FillExtras(live);
            UpdateSummary(live, missing);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            try { if (!busy) RefreshStatesAsync(); }
            catch { }
        }

        private void CheckAll(bool value)
        {
            foreach (ListViewItem row in list.Items)
            {
                Bookmark b = row.Tag as Bookmark;
                if (b == null) continue;
                b.Selected = value;
            }
            list.Invalidate();
        }

        private void OnItemCheck(object sender, ItemCheckEventArgs e)
        {
            Bookmark b = list.Items[e.Index].Tag as Bookmark;
            if (b != null) b.Selected = (e.NewValue == CheckState.Checked);
        }

        private void OnListMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 点在自己画的勾选框上就切换勾选
                ListViewHitTestInfo h = list.HitTest(e.Location);
                if (h.Item == null) return;
                Rectangle rb = h.Item.Bounds;
                int nameColX = rb.X + (list.Columns.Count > 0 ? list.Columns[0].Width : 0);
                Rectangle box = new Rectangle(nameColX + 6, rb.Y + (rb.Height - 16) / 2, 16, 16);
                box.Inflate(5, 5);
                if (!box.Contains(e.Location)) return;
                Bookmark bm = h.Item.Tag as Bookmark;
                if (bm == null) return;
                bm.Selected = !bm.Selected;
                list.Invalidate(rb);
                return;
            }

            if (e.Button != MouseButtons.Right) return;
            ListViewItem hit = list.GetItemAt(e.X, e.Y);
            if (hit != null && !hit.Selected)
            {
                list.SelectedItems.Clear();
                hit.Selected = true;
            }
        }

        // ==================== 操作 ====================
        private async void OpenChecked(bool onlyCurrent)
        {
            if (busy) return;

            List<Bookmark> targets = new List<Bookmark>();
            if (onlyCurrent)
            {
                if (list.SelectedItems.Count == 0) return;
                Bookmark b = list.SelectedItems[0].Tag as Bookmark;
                if (b != null) targets.Add(b);
            }
            else
            {
                foreach (ListViewItem row in list.Items)
                {
                    Bookmark b = row.Tag as Bookmark;
                    if (b != null && b.Selected) targets.Add(b);
                }
            }
            if (targets.Count == 0)
            {
                status.Text = "没有勾选任何书签";
                return;
            }

            busy = true;
            btnOpen.Enabled = false;
            int ok = 0, raised = 0, skipped = 0, failed = 0;
            List<string> problems = new List<string>();
            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    Bookmark b = targets[i];
                    status.Text = string.Format("正在处理 {0}/{1}：{2}", i + 1, targets.Count, b.name);
                    Application.DoEvents();

                    // 已经打开的窗口：把那个窗口提到最前面，而不是再开一个
                    if (b.OpenNow)
                    {
                        if (!chkRaise.Checked) { skipped++; continue; }
                        string msg;
                        if (RaiseExistingWindow(b, out msg)) raised++;
                        else { failed++; problems.Add(b.name + "  →  " + msg); }
                        continue;
                    }

                    string local = ToLocalPath(b.uri);
                    if (local != null && !Directory.Exists(local) && !File.Exists(local))
                    {
                        failed++;
                        problems.Add(b.name + "  →  路径不存在：" + local);
                        continue;
                    }

                    Product p = ProductById(b.product);
                    string exe = p.Exe;
                    if (exe == null)
                    {
                        failed++;
                        problems.Add(b.name + "  →  找不到 " + p.Name + " 的可执行文件");
                        continue;
                    }

                    string args = local != null
                        ? "-n \"" + local + "\""
                        : "-n --folder-uri \"" + b.uri + "\"";

                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                        psi.UseShellExecute = false;
                        Process.Start(psi);
                        ok++;
                        b.OpenNow = true;
                        b.lastOpened = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        problems.Add(b.name + "  →  " + ex.Message);
                    }

                    int delay = (int)numDelay.Value;
                    if (delay > 0 && i < targets.Count - 1) await Task.Delay(delay);
                }

                SaveStore();
                status.Text = string.Format("完成：新开 {0} 个，置顶 {1} 个，跳过 {2} 个，失败 {3} 个",
                    ok, raised, skipped, failed);
                if (problems.Count > 0)
                {
                    MessageBox.Show(this, string.Join("\r\n", problems.ToArray()),
                        "有 " + problems.Count + " 个书签没能打开", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                await Task.Delay(1200);
                RefreshStates();
            }
            finally
            {
                busy = false;
                btnOpen.Enabled = true;
            }
        }

        private void AddFolder()
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.Description = "选择一个项目文件夹，添加为书签";
            dlg.ShowNewFolderButton = false;
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            Bookmark b = new Bookmark();
            b.uri = UriFromPath(dlg.SelectedPath);
            b.kind = "folder";
            b.product = "codebuddy-cn";
            b.name = LeafName(b.uri);
            if (!AddUnique(b))
            {
                status.Text = "该文件夹已经在书签里了";
                return;
            }
            SaveStore();
            RefreshRows();
            status.Text = "已添加书签：" + b.name;
        }

        private void ImportOpenWindows()
        {
            List<Bookmark> live = CollectLive();
            if (live.Count == 0)
            {
                status.Text = "没有读到正在运行的 IDE 窗口";
                return;
            }
            int added = 0;
            foreach (Bookmark b in live)
                if (AddUnique(b)) added++;
            if (added > 0) SaveStore();
            RefreshRows();
            status.Text = added > 0
                ? string.Format("已导入 {0} 个当前窗口（共 {1} 个）", added, live.Count)
                : "当前窗口都已经在书签里了";
        }

        private void DeleteSelected()
        {
            if (list.SelectedItems.Count == 0)
            {
                status.Text = "请先点选要删除的书签行";
                return;
            }
            List<Bookmark> doomed = new List<Bookmark>();
            foreach (ListViewItem row in list.SelectedItems)
            {
                Bookmark b = row.Tag as Bookmark;
                if (b != null) doomed.Add(b);
            }
            if (MessageBox.Show(this, "删除选中的 " + doomed.Count + " 个书签？（只删书签，不动文件夹）",
                    "确认删除", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;

            foreach (Bookmark b in doomed) store.items.Remove(b);
            SaveStore();
            RefreshRows();
            status.Text = "已删除 " + doomed.Count + " 个书签";
        }

        private void RenameSelected()
        {
            if (list.SelectedItems.Count == 0) return;
            Bookmark b = list.SelectedItems[0].Tag as Bookmark;
            if (b == null) return;
            PromptDialog dlg = new PromptDialog("重命名书签", b.name);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (dlg.Value.Length == 0) return;
            b.name = dlg.Value;
            SaveStore();
            RefreshRows();
        }

        private void RevealSelected()
        {
            if (list.SelectedItems.Count == 0) return;
            Bookmark b = list.SelectedItems[0].Tag as Bookmark;
            if (b == null) return;
            string local = ToLocalPath(b.uri);
            if (local != null && Directory.Exists(local))
                Process.Start("explorer.exe", "\"" + local + "\"");
            else
                status.Text = "这是远程窗口或路径不存在，无法在资源管理器中打开";
        }

        private void MoveSelected(int delta)
        {
            if (list.SelectedItems.Count != 1) return;
            Bookmark b = list.SelectedItems[0].Tag as Bookmark;
            int idx = store.items.IndexOf(b);
            int target = idx + delta;
            if (idx < 0 || target < 0 || target >= store.items.Count) return;

            store.items.RemoveAt(idx);
            store.items.Insert(target, b);
            SaveStore();
            RefreshRows();

            foreach (ListViewItem row in list.Items)
                if (row.Tag == b) { row.Selected = true; break; }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)
            {
                Log.Write("ThreadException", e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                Log.Write("UnhandledException", e.ExceptionObject as Exception);
            };

            // 只允许一个实例，避免两个进程互相覆盖 bookmarks.json
            bool createdNew;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, @"Local\CodeBuddyWindowMaster", out createdNew))
            {
                if (!createdNew)
                {
                    IntPtr h = FindWindow(null, "CodeBuddy 窗口书签");
                    if (h != IntPtr.Zero)
                    {
                        ShowWindow(h, 9);   // SW_RESTORE
                        SetForegroundWindow(h);
                    }
                    return;
                }

                try
                {
                    Application.Run(new MainForm());
                }
                catch (Exception ex)
                {
                    Log.Write("Main", ex);
                    MessageBox.Show(ex.Message + "\r\n\r\n详细信息已写入 error.log",
                        "CodeBuddy 窗口书签 - 错误");
                }
                GC.KeepAlive(mutex);
            }
        }
    }
}
