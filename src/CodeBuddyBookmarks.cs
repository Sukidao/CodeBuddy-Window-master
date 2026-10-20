
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace CodeBuddyBookmarks
{
    internal static class Theme
    {
        public static readonly Color Accent = Color.FromArgb(47, 107, 255);
        public static readonly Color AccentHover = Color.FromArgb(78, 130, 255);
        public static readonly Color AccentPress = Color.FromArgb(35, 88, 214);
        public static readonly Color HeaderTop = Color.FromArgb(27, 33, 48);
        public static readonly Color HeaderBottom = Color.FromArgb(38, 47, 69);
        public static readonly Color Card = Color.FromArgb(247, 248, 250);
        public static readonly Color Border = Color.FromArgb(226, 230, 238);
        public static readonly Color Muted = Color.FromArgb(123, 131, 148);
        public static readonly Color TextMain = Color.FromArgb(34, 37, 46);
        public static readonly Color Danger = Color.FromArgb(203, 62, 62);
        public static readonly Color HeaderText = Color.FromArgb(158, 169, 194);

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
                fill = pressed ? Color.FromArgb(232, 236, 244)
                     : (hovered ? Color.FromArgb(243, 246, 252) : Color.White);

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
                using (Pen p = new Pen(hovered && Enabled ? Color.FromArgb(198, 208, 228) : Theme.Border))
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

    // ---------------- IDE 产品 ----------------
    internal class Product
    {
        public string Id;
        public string Name;
        public string UserData;
        public string[] ExeCandidates;

        public string StoragePath
        {
            get { return Path.Combine(UserData, @"User\globalStorage\storage.json"); }
        }

        public string Exe
        {
            get
            {
                foreach (string p in ExeCandidates)
                {
                    try { if (File.Exists(p)) return p; }
                    catch { }
                }
                return null;
            }
        }

        public bool Running
        {
            get
            {
                string exe = Exe;
                if (exe == null) return false;
                try { return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)).Length > 0; }
                catch { return false; }
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
        private ListView listExtra;
        private Label lblExtraTitle;
        private List<Bookmark> extras = new List<Bookmark>();
        private Label lblEmptyTitle, lblEmptyHint;

        private bool busy = false;

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

        public MainForm()
        {
            workDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            storePath = Path.Combine(workDir, "bookmarks.json");
            snapshotPath = Path.Combine(workDir, @"snapshots\windows-latest.json");
            products = BuildProducts();

            Text = "CodeBuddy 窗口书签";
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9F);
            MinimumSize = new Size(820, 500);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            DoubleBuffered = true;

            BuildUi();
            Size wa = Screen.PrimaryScreen.WorkingArea.Size;
            ClientSize = new Size(Math.Min(1040, wa.Width - 60), Math.Min(640, wa.Height - 60));
            LayoutColumns();
            LayoutToolbar();

            LoadStore();
            RefreshRows();

            InitTimer();
            InitTray();
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
                try { RefreshStates(); }
                catch { }
            };
            chkAuto.CheckedChanged += delegate { autoTimer.Enabled = chkAuto.Checked; };
            autoTimer.Enabled = chkAuto.Checked;
        }

        // ==================== 托盘图标 ====================
        private void InitTray()
        {
            Icon ico = null;
            try { ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            tray = new NotifyIcon();
            tray.Icon = ico != null ? ico : SystemIcons.Application;
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
            trayMenu.Items.Add(new ToolStripSeparator());

            // 同步一次"已打开"状态
            try
            {
                List<Bookmark> live = CollectLive();
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

            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("全部打开", null, delegate { CheckAll(true); OpenChecked(false); });
            trayMenu.Items.Add("退出", null, delegate
            {
                exiting = true;
                Close();
            });
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

        private void HideToTray()
        {
            if (tray == null) return;
            ShowInTaskbar = false;
            Hide();
            if (!trayHintShown)
            {
                tray.ShowBalloonTip(2500, "仍在后台运行",
                    "窗口已最小化到托盘，双击图标可重新打开。\n每 5 秒自动刷新会暂停，恢复界面后继续。",
                    ToolTipIcon.Info);
                trayHintShown = true;
            }
        }

        private void ShowFromTray()
        {
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

            list = new ListView();
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
            list.Columns.Add("书签", 300);
            list.Columns.Add("路径", 500);
            list.Columns.Add("类型", 74);
            list.Columns.Add("状态", 88);
            list.ItemCheck += OnItemCheck;
            list.DoubleClick += delegate { OpenChecked(true); };
            list.MouseUp += OnListMouseUp;
            list.Resize += delegate { LayoutColumns(); };

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
            lblHint.Text = "F5 刷新    Ctrl+F 搜索    双击打开";
            lblHint.AutoSize = false;
            lblHint.TextAlign = ContentAlignment.MiddleRight;
            lblHint.ForeColor = Color.FromArgb(122, 134, 160);
            lblHint.BackColor = Color.Transparent;
            lblHint.Size = new Size(300, 22);
            lblHint.Location = new Point(600, 30);

            header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 86;
            header.Paint += OnHeaderPaint;
            header.Resize += delegate { lblHint.Location = new Point(header.Width - 324, 33); };
            header.Controls.Add(title);
            header.Controls.Add(lblSummary);
            header.Controls.Add(lblHint);

            // 列表外包一层，给表头留出上边距
            Panel listWrap = new Panel();
            listWrap.Dock = DockStyle.Fill;
            listWrap.Padding = new Padding(0, 4, 0, 0);
            listWrap.BackColor = Color.White;
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
            listExtra = new ListView();
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
            Panel extraListWrap = new Panel();
            extraListWrap.Dock = DockStyle.Fill;
            extraListWrap.Padding = new Padding(16, 42, 16, 10);
            extraListWrap.BackColor = Color.White;
            extraListWrap.Controls.Add(listExtra);
            panelExtra.Controls.Add(extraListWrap);
            panelExtra.Controls.Add(lblExtraTitle);
            panelExtra.Controls.Add(btnAddSel);
            panelExtra.Controls.Add(btnAddAll);
            // Controls[0] 在最上层，这里必须把标题和按钮提到前面
            extraListWrap.SendToBack();
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
            menu.Items.Add("全选（Ctrl+A）", null, delegate { CheckAll(true); });
            menu.Items.Add("全不选", null, delegate { CheckAll(false); });
            menu.Items.Add("重命名", null, delegate { RenameSelected(); });
            menu.Items.Add("在资源管理器中打开", null, delegate { RevealSelected(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("删除", null, delegate { DeleteSelected(); });
            list.ContextMenuStrip = menu;
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
            using (SolidBrush b = new SolidBrush(Color.FromArgb(250, 251, 253))) g.FillRectangle(b, e.Bounds);
            using (Pen p = new Pen(Theme.Border))
                g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            int indent = e.ColumnIndex == 0 ? 32 : 12;
            Rectangle r = new Rectangle(e.Bounds.X + indent, e.Bounds.Y, Math.Max(10, e.Bounds.Width - indent - 6), e.Bounds.Height);
            TextRenderer.DrawText(g, e.Header.Text, headerFont, r, Color.FromArgb(122, 131, 148),
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

        private void OnDrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            Graphics g = e.Graphics;
            bool selected = e.Item.Selected;
            bool hover = e.ItemIndex == hoverIndex && !selected;
            Color bg = selected ? Color.FromArgb(235, 242, 255)
                       : (hover ? Color.FromArgb(249, 251, 254) : Color.White);

            using (SolidBrush brush = new SolidBrush(bg)) g.FillRectangle(brush, e.Bounds);
            if (!selected)
            {
                using (Pen p = new Pen(Color.FromArgb(242, 244, 248)))
                    g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            Bookmark b = e.Item.Tag as Bookmark;
            if (b == null) return;

            Color nameColor = b.Missing ? Theme.Danger
                : (b.OpenNow ? Color.FromArgb(160, 166, 178) : Theme.TextMain);

            if (e.ColumnIndex == 0)
            {
                Rectangle box = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + (e.Bounds.Height - 16) / 2, 16, 16);
                DrawCheckBox(g, box, b.Selected);
                Rectangle tr = new Rectangle(box.Right + 10, e.Bounds.Y,
                    Math.Max(10, e.Bounds.Right - box.Right - 16), e.Bounds.Height);
                TextRenderer.DrawText(g, b.name == null ? "" : b.name, listFont, tr, nameColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }

            Rectangle r = new Rectangle(e.Bounds.X + 12, e.Bounds.Y,
                Math.Max(10, e.Bounds.Width - 18), e.Bounds.Height);

            if (e.ColumnIndex == 1)
            {
                string local = ToLocalPath(b.uri);
                string text = local != null ? local : "远程  " + Uri.UnescapeDataString(b.uri);
                TextRenderer.DrawText(g, text, listFont, r,
                    b.Missing ? Theme.Danger : Color.FromArgb(106, 114, 130),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                return;
            }

            if (e.ColumnIndex == 2)
            {
                string type = b.kind == "workspace" ? "工作区" : (ToLocalPath(b.uri) == null ? "远程" : "文件夹");
                TextRenderer.DrawText(g, type, listFont, r, Color.FromArgb(134, 141, 156),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                return;
            }

            if (b.Missing) DrawChip(g, e.Bounds, "路径失效", Color.FromArgb(190, 58, 58), Color.FromArgb(253, 236, 236));
            else if (b.OpenNow) DrawChip(g, e.Bounds, "已打开", Color.FromArgb(28, 138, 78), Color.FromArgb(232, 247, 238));
            else DrawChip(g, e.Bounds, "未打开", Color.FromArgb(110, 118, 132), Color.FromArgb(242, 244, 248));
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

        private void LayoutColumns()
        {
            if (list.Columns.Count != 4) return;
            int w = list.ClientSize.Width;
            if (w <= 0) return;
            int name = Math.Max(180, Math.Min(330, (int)(w * 0.28)));
            int type = 76;
            int state = 92;
            int path = Math.Max(160, w - name - type - state - 8);
            list.Columns[0].Width = name;
            list.Columns[1].Width = path;
            list.Columns[2].Width = type;
            list.Columns[3].Width = state;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5) { RefreshRows(); return true; }
            if (keyData == (Keys.Control | Keys.F)) { txtSearch.Focus(); txtSearch.SelectAll(); return true; }
            if (keyData == (Keys.Control | Keys.A) && ActiveControl == list) { CheckAll(true); return true; }
            if (ActiveControl == list)
            {
                if (keyData == Keys.Delete) { DeleteSelected(); return true; }
                if (keyData == Keys.Enter) { OpenChecked(true); return true; }
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
            ps.Add(code);

            return ps;
        }

        private Product ProductById(string id)
        {
            foreach (Product p in products)
                if (p.Id == id) return p;
            return products[0];
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
                catch { }
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
                File.WriteAllText(storePath, json.Serialize(store), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
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
            List<Bookmark> live = CollectLive();
            List<string> openUris = new List<string>();
            foreach (Bookmark b in live) openUris.Add(b.uri.ToLower());

            string filter = txtSearch == null ? "" : txtSearch.Text.Trim().ToLower();

            list.BeginUpdate();
            list.Items.Clear();
            int missing = 0;
            foreach (Bookmark b in store.items)
            {
                b.OpenNow = openUris.Contains(b.uri.ToLower());
                if (filter.Length > 0 && !(b.name != null && b.name.ToLower().Contains(filter))
                    && !(b.uri != null && b.uri.ToLower().Contains(filter)))
                    continue;

                string local = ToLocalPath(b.uri);
                bool exists = local == null || Directory.Exists(local) || File.Exists(local);
                b.Missing = !exists;
                if (!exists) missing++;

                ListViewItem row = new ListViewItem(b.name == null ? "" : b.name, 0);
                row.SubItems.Add(local != null ? local : "远程  " + Uri.UnescapeDataString(b.uri));
                row.SubItems.Add(b.kind == "workspace" ? "工作区" : (local == null ? "远程" : "文件夹"));
                row.SubItems.Add(!exists ? "路径不存在" : (b.OpenNow ? "已打开" : "未打开"));
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
            lastSignature = ComputeSignature();
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
                string local = ToLocalPath(b.uri);
                ListViewItem row = new ListViewItem(b.name == null ? "" : b.name, 0);
                row.SubItems.Add(local != null ? local : "远程  " + Uri.UnescapeDataString(b.uri));
                row.SubItems.Add(local == null ? "远程" : (b.kind == "workspace" ? "工作区" : "文件夹"));
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

        private bool RaiseExistingWindow(Bookmark b, out string message)
        {
            message = null;
            Product p = ProductById(b.product);
            string exe = p.Exe;
            string procName = exe == null ? null : Path.GetFileNameWithoutExtension(exe);
            string leaf = LeafName(b.uri);
            IntPtr found = IntPtr.Zero;

            if (procName != null && !string.IsNullOrEmpty(leaf))
            {
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
            }

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

        private void RefreshStates()
        {
            if (!StateChanged()) return;

            List<Bookmark> live = CollectLive();
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
                    if (row.SubItems[3].Text != stateText) row.SubItems[3].Text = stateText;
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
            try { if (!busy) RefreshStates(); }
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
                Rectangle box = new Rectangle(rb.X + 6, rb.Y + (rb.Height - 16) / 2, 16, 16);
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
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        problems.Add(b.name + "  →  " + ex.Message);
                    }

                    int delay = (int)numDelay.Value;
                    if (delay > 0 && i < targets.Count - 1) await Task.Delay(delay);
                }

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

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "CodeBuddy 窗口书签 - 错误");
            }
        }
    }
}
