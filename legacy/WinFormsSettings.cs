using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Automation;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OrbitWheelLite
{
    class ApplicationPicker : Form
    {
        private TextBox search;
        private ListBox list;
        private List<ApplicationChoice> all;
        public ApplicationChoice SelectedApplication { get; private set; }

        public ApplicationPicker()
        {
            Text = "选择应用 - Applications";
            Icon = IconFactory.AppIcon();
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(680, 720);
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
            Font = new Font("Microsoft YaHei UI", 10f);
            Panel shell = new Panel { Left = 18, Top = 18, Width = 644, Height = 684 };
            Controls.Add(shell);
            Label title = new Label { Text = "选择应用", Left = 28, Top = 22, Width = 400, Height = 38, Font = new Font("Microsoft YaHei UI", 20f, FontStyle.Bold), ForeColor = SystemColors.ControlText, BackColor = Color.Transparent };
            Label hint = new Label { Text = "Applications 中的所有应用，也可切换为普通文件选择", Left = 30, Top = 64, Width = 540, Height = 24, ForeColor = SystemColors.ControlText, BackColor = Color.Transparent };
            search = new TextBox { Left = 28, Top = 104, Width = 588, Height = 36, BackColor = SystemColors.Control, ForeColor = SystemColors.ControlText, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 11f) };
            list = new ListBox { Left = 28, Top = 158, Width = 588, Height = 430, BackColor = SystemColors.Control, ForeColor = SystemColors.ControlText, BorderStyle = BorderStyle.Fixed3D, ItemHeight = 58, Font = new Font("Microsoft YaHei UI", 11f), DrawMode = DrawMode.Normal };
            Button browse = new Button { Text = "浏览文件…", Left = 28, Top = 610, Width = 160, Height = 44, FlatStyle = FlatStyle.System, BackColor = SystemColors.Control, ForeColor = SystemColors.ControlText };
            Button choose = new Button { Text = "选择应用", Left = 456, Top = 610, Width = 160, Height = 44, FlatStyle = FlatStyle.System, BackColor = SystemColors.Control, ForeColor = SystemColors.ControlText };
            browse.UseVisualStyleBackColor = true;
            choose.UseVisualStyleBackColor = true;
            shell.Controls.AddRange(new Control[] { title, hint, search, list, browse, choose });
            search.TextChanged += delegate { Filter(); };
            list.DoubleClick += delegate { Accept(); };
            browse.Click += delegate { BrowseFile(); };
            choose.Click += delegate { Accept(); };
            all = ApplicationCatalog.Load();
            Filter();
        }

        private void Filter()
        {
            string query = search.Text.Trim();
            list.BeginUpdate(); list.Items.Clear();
            foreach (ApplicationChoice app in all)
                if (query.Length == 0 || app.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0) list.Items.Add(app);
            list.EndUpdate();
        }

        private void Accept()
        {
            SelectedApplication = list.SelectedItem as ApplicationChoice;
            if (SelectedApplication == null) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BrowseFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog { Title = "选择应用程序或快捷方式", Filter = "应用与快捷方式 (*.exe;*.lnk)|*.exe;*.lnk|所有文件 (*.*)|*.*" }) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                SelectedApplication = new ApplicationChoice { Name = Path.GetFileNameWithoutExtension(dialog.FileName), Target = dialog.FileName };
                DialogResult = DialogResult.OK;
                Close();
            }
        }

    }

    class SettingsActionGrid : DataGridView
    {
        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);
            // WinForms scales the grid bounds, but not these pixel-based metrics.
            ColumnHeadersHeight = (int)Math.Round(ColumnHeadersHeight * factor.Height);
            RowTemplate.Height = (int)Math.Round(RowTemplate.Height * factor.Height);
            foreach (DataGridViewRow row in Rows) row.Height = (int)Math.Round(row.Height * factor.Height);
            foreach (DataGridViewColumn column in Columns)
                column.MinimumWidth = (int)Math.Round(column.MinimumWidth * factor.Width);
        }
    }

    // Use only inbox DWM APIs: no Windows App SDK or additional runtime.
    static class SettingsWindowEffects
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Margins { public int Left, Right, Top, Bottom; }
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);

        internal static bool Apply(IntPtr window, bool highContrast, bool transparency, int frameWidth = 18)
        {
            try {
                int dark = 0; // Standard .NET Framework WinForms controls use a light system theme.
                DwmSetWindowAttribute(window, 20, ref dark, sizeof(int));
                int corner = highContrast ? 0 : 2; // DEFAULT / ROUND
                DwmSetWindowAttribute(window, 33, ref corner, sizeof(int));
                int backdrop = !highContrast && transparency ? 2 : 1; // MAINWINDOW / NONE
                bool material = DwmSetWindowAttribute(window, 38, ref backdrop, sizeof(int)) >= 0 && backdrop == 2;
                Margins margins = new Margins();
                // Opaque settings cards remain readable; only the surrounding
                // window surface exposes the real system material.
                // Do not extend glass underneath GDI controls: their black text
                // has no alpha channel and would become transparent on glass.
                if (material) margins.Left = margins.Right = margins.Top = margins.Bottom = Math.Max(0, frameWidth);
                if (DwmExtendFrameIntoClientArea(window, ref margins) < 0) {
                    backdrop = 1;
                    DwmSetWindowAttribute(window, 38, ref backdrop, sizeof(int));
                    return false;
                }
                return material;
            } catch (DllNotFoundException) { return false; }
              catch (EntryPointNotFoundException) { return false; }
        }

        internal static bool TransparencyEnabled()
        {
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return key == null || Convert.ToInt32(key.GetValue("EnableTransparency", 1)) != 0;
            } catch (System.Security.SecurityException) { return false; }
              catch (UnauthorizedAccessException) { return false; }
              catch (IOException) { return false; }
              catch (FormatException) { return false; }
              catch (InvalidCastException) { return false; }
              catch (OverflowException) { return false; }
        }
    }

    class SettingsForm : Form
    {
        private AppConfig config;
        private ListBox pages;
        private DataGridView grid;
        private TextBox pageName;
        private ComboBox mode, style;
        private TextBox hotkeyRecorder;
        private int recordedModifiers;
        private int recordedKey;
        private CheckBox startup;
        private CheckBox mouseGestures;
        private Label effectDescription;
        private TabControl tabs;
        private bool loadingGrid;
        private bool choosingApp;
        private bool showingPage;
        private bool initializing;
        private int editingPage = -1;
        private float layoutScale = 1f;
        private bool systemMaterial;
        public event EventHandler ConfigSaved;

        public SettingsForm(AppConfig c)
        {
            SuspendLayout();
            config = c;
            Text = "OrbitWheel 设置";
            Icon = IconFactory.AppIcon();
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1180, 760);
            MinimumSize = new Size(1080, 700);
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;
            Font = new Font("Microsoft YaHei UI", 9.5f);
            Padding = new Padding(18);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            initializing = true;
            Build();
            LoadPageList();
            ResumeLayout(false);
            PerformAutoScale();
            foreach (TabPage page in tabs.TabPages) page.ResumeLayout(true);
            initializing = false;
            FormClosing += delegate { if (grid != null) { grid.EndEdit(); AutoSave(); } };
        }

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            layoutScale *= factor.Width;
            base.ScaleControl(factor, specified);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RefreshWindowEffects();
        }

        protected override void OnPaddingChanged(EventArgs e)
        {
            base.OnPaddingChanged(e);
            RefreshWindowEffects();
        }

        private void RefreshWindowEffects()
        {
            if (!IsHandleCreated) return;
            systemMaterial = SettingsWindowEffects.Apply(Handle, SystemInformation.HighContrast,
                SettingsWindowEffects.TransparencyEnabled(), Padding.Left);
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Black in the extended DWM frame exposes the system backdrop.
            // Never use TransparencyKey or a layered window for this form.
            if (systemMaterial) e.Graphics.Clear(Color.Black);
            else base.OnPaintBackground(e);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Settings, visual theme and DWM composition can change while open.
            if (m.Msg == 0x001A || m.Msg == 0x031A || m.Msg == 0x031E)
                RefreshWindowEffects();
        }

        protected override void OnLoad(EventArgs e)
        {
            FitToWorkingArea(Screen.FromControl(this).WorkingArea);
            base.OnLoad(e);
        }

        private void FitToWorkingArea(Rectangle workingArea)
        {
            // Screen coordinates share the window's DPI context, including the
            // Windows compatibility scaling used by this .NET Framework app.
            Size trackLimit = SystemInformation.MaxWindowTrackSize;
            MinimumSize = new Size(Math.Min((int)Math.Round(1080 * layoutScale), Math.Min(workingArea.Width, trackLimit.Width)),
                                   Math.Min((int)Math.Round(700 * layoutScale), Math.Min(workingArea.Height, trackLimit.Height)));
            Size = new Size(Math.Min(Width, workingArea.Width), Math.Min(Height, workingArea.Height));
            if (StartPosition == FormStartPosition.CenterScreen)
                Location = new Point(workingArea.Left + (workingArea.Width - Width) / 2,
                                     workingArea.Top + (workingArea.Height - Height) / 2);
        }

        private void Build()
        {
            TableLayoutPanel windowLayout = Rows();
            windowLayout.Name = "settingsLayout";
            windowLayout.Dock = DockStyle.Fill;
            windowLayout.AutoSize = false;
            windowLayout.RowCount = 2;
            windowLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            windowLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(windowLayout);
            tabs = new TabControl { Name = "settingsTabs", Dock = DockStyle.Fill, Multiline = true,
                DrawMode = TabDrawMode.Normal, Margin = Padding.Empty };
            windowLayout.Controls.Add(tabs, 0, 0);
            windowLayout.Controls.Add(Note("所有更改都会自动保存；关闭设置后，软件仍在系统托盘运行。"), 0, 1);
            tabs.Selecting += delegate(object sender, TabControlCancelEventArgs e) {
                if (grid != null) {
                    if (!grid.EndEdit()) { e.Cancel = true; return; }
                    CommitPage();
                }
            };

            TableLayoutPanel general = AddTab("常规");
            startup = new CheckBox { Text = "随 Windows 自动启动", AutoSize = true, FlatStyle = FlatStyle.System };
            general.Controls.Add(Group("启动与托盘", startup,
                Note("关闭设置窗口后，OrbitWheel 仍会留在系统托盘。")));
            mode = Choice(new object[] { "点击模式", "按住并松开执行" });
            general.Controls.Add(Group("触发模式", mode,
                Note("默认按住快捷键，移动到目标后松开执行。")));
            general.Controls.Add(Group("页面切换", Note("圆环打开时，滚动鼠标滚轮切换页面。")));

            TableLayoutPanel actions = AddTab("页面与动作");
            TableLayoutPanel editor = Rows();
            editor.ColumnCount = 2;
            editor.ColumnStyles.Clear();
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
            TableLayoutPanel pageList = Rows();
            pageList.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
            pageList.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pages = new ListBox { Dock = DockStyle.Fill,
                IntegralHeight = false, BorderStyle = BorderStyle.Fixed3D, DrawMode = DrawMode.Normal };
            pages.SelectedIndexChanged += delegate { ShowPage(); };
            pageList.Controls.Add(pages);
            TableLayoutPanel pageButtons = Rows();
            Button add = Command("添加页面"); add.Click += delegate { AddPage(); };
            Button remove = Command("删除页面"); remove.Click += delegate { DeletePage(); };
            pageButtons.Controls.Add(add); pageButtons.Controls.Add(remove);
            pageList.Controls.Add(pageButtons);
            editor.Controls.Add(pageList, 0, 0);
            TableLayoutPanel actionList = Rows();
            actionList.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionList.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionList.RowStyles.Add(new RowStyle(SizeType.Absolute, 340));
            actionList.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            actionList.Controls.Add(Note("页面名称"));
            pageName = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            pageName.TextChanged += delegate {
                if (!showingPage && pages.SelectedIndex >= 0) {
                    config.Pages[pages.SelectedIndex].Name = pageName.Text;
                    if (Convert.ToString(pages.Items[pages.SelectedIndex]) != pageName.Text)
                        pages.Items[pages.SelectedIndex] = pageName.Text;
                    AutoSave();
                }
            };
            actionList.Controls.Add(pageName);
            grid = new SettingsActionGrid { Dock = DockStyle.Fill,
                BackgroundColor = SystemColors.Window, BorderStyle = BorderStyle.Fixed3D,
                RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 32, RowTemplate = { Height = 44 }, EnableHeadersVisualStyles = true };
            grid.DefaultCellStyle.BackColor = SystemColors.Window;
            grid.DefaultCellStyle.ForeColor = SystemColors.WindowText;
            grid.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
            grid.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;
            grid.Columns.Add("slot", "位置"); grid.Columns.Add("name", "名称");
            DataGridViewComboBoxColumn typeCol = new DataGridViewComboBoxColumn {
                Name = "type", HeaderText = "动作类型", FlatStyle = FlatStyle.Standard };
            typeCol.Items.AddRange(ActionNames.AllChinese());
            grid.Columns.Add(typeCol);
            grid.Columns.Add("target", "程序路径 / 文件夹 / 命令");
            foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns[0].ReadOnly = true;
            grid.Columns[0].FillWeight = 45; grid.Columns[1].FillWeight = 80;
            grid.Columns[2].FillWeight = 90; grid.Columns[3].FillWeight = 170;
            grid.Columns[0].MinimumWidth = 54; grid.Columns[1].MinimumWidth = 90;
            grid.Columns[2].MinimumWidth = 110; grid.Columns[3].MinimumWidth = 190;
            grid.CurrentCellDirtyStateChanged += delegate { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += delegate { HandleCellChange(); };
            grid.SelectionChanged += delegate { UpdateActionEditor(); };
            actionList.Controls.Add(grid);
            actionList.Controls.Add(Note("选择“打开程序”会打开应用选择器；选择“打开文件夹”会打开文件夹选择器。"));
            editor.Controls.Add(actionList, 1, 0);
            actions.Controls.Add(Group("页面与六个扇区", editor));

            TableLayoutPanel appearance = AddTab("外观效果");
            style = Choice(new object[] { "液态玻璃", "高斯模糊", "亚克力" });
            effectDescription = Note("");
            appearance.Controls.Add(Group("圆环视觉效果", Note("效果样式"), style, effectDescription));
            appearance.Controls.Add(Group("效果说明",
                Note("视觉效果仅应用于圆环本身，不影响整个屏幕。"),
                Note("液态玻璃强调折射高光；高斯模糊强调背景虚化；亚克力带有磨砂颗粒。")));

            TableLayoutPanel hotkeys = AddTab("快捷键");
            hotkeyRecorder = new TextBox { ReadOnly = true, Anchor = AnchorStyles.Left | AnchorStyles.Right };
            hotkeyRecorder.KeyDown += RecordHotkey;
            hotkeyRecorder.Enter += delegate { hotkeyRecorder.Text = "请按下新的快捷键…"; };
            Button record = Command("录制快捷键");
            record.Click += delegate { hotkeyRecorder.Focus(); hotkeyRecorder.Text = "请按下新的快捷键…"; };
            hotkeys.Controls.Add(Group("快捷键", hotkeyRecorder, record,
                Note("点击“录制快捷键”并按下组合键。")));

            TableLayoutPanel advanced = AddTab("高级");
            mouseGestures = new CheckBox { Text = "启用左右键组合手势", AutoSize = true, FlatStyle = FlatStyle.System };
            advanced.Controls.Add(Group("鼠标手势", mouseGestures,
                Note("同时按下鼠标左右键并移动，全部松开后执行：上滑开始菜单，下滑桌面，左右滑切换窗口。"),
                Note("组合手势期间会屏蔽原点击；普通单击仅在 110 毫秒识别窗口内短暂延后。"),
                Note("圆环中心取打开瞬间的鼠标位置；点击模式下重复快捷键无效，按 Esc 关闭。")));
            TableLayoutPanel about = AddTab("关于");
            about.Controls.Add(Group("OrbitWheel", Note("鼠标中心的六等分径向快捷操作工具"), Note("OrbitWheel 1.3")));

            style.SelectedIndexChanged += delegate { UpdateEffectDescription(); AutoSave(); };
            mode.SelectedIndexChanged += delegate { AutoSave(); };
            startup.CheckedChanged += delegate { AutoSave(); };
            mouseGestures.CheckedChanged += delegate { AutoSave(); };
            recordedModifiers = config.Modifiers;
            recordedKey = config.KeyCode;
            hotkeyRecorder.Text = HotkeyText(recordedModifiers, recordedKey);
            mode.SelectedIndex = config.Mode == "Hold" ? 1 : 0;
            style.SelectedItem = config.Style;
            UpdateEffectDescription();
            startup.Checked = config.StartWithWindows;
            mouseGestures.Checked = config.MouseGestures;
            ShowSection(0);
        }

        private static TableLayoutPanel Rows()
        {
            TableLayoutPanel table = new TableLayoutPanel { ColumnCount = 1, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, GrowStyle = TableLayoutPanelGrowStyle.AddRows,
                Margin = new Padding(0), Padding = new Padding(0) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return table;
        }

        private TableLayoutPanel AddTab(string title)
        {
            TabPage page = new TabPage(title) { AutoScroll = true, UseVisualStyleBackColor = true, Padding = new Padding(12) };
            page.SuspendLayout();
            TableLayoutPanel content = Rows();
            // A docked/right-anchored child is excluded from WinForms horizontal
            // AutoScroll. Measure this table at the available width, while all
            // group and control positions are assigned by TableLayoutPanel.
            content.Dock = DockStyle.None;
            content.AutoSize = false;
            content.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            content.Location = new Point(page.Padding.Left, page.Padding.Top);
            content.MinimumSize = new Size(700, 0);
            bool arranging = false;
            page.Layout += delegate {
                if (arranging) return;
                arranging = true;
                try {
                    int width = Math.Max((int)Math.Round(700 * layoutScale), page.ClientSize.Width - page.Padding.Horizontal);
                    content.MinimumSize = new Size((int)Math.Round(700 * layoutScale), 0);
                    content.Width = width;
                    content.Height = content.GetPreferredSize(new Size(width, 0)).Height;
                } finally { arranging = false; }
            };
            page.Controls.Add(content);
            tabs.TabPages.Add(page);
            return content;
        }

        private static GroupBox Group(string title, params Control[] controls)
        {
            GroupBox group = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top, Padding = new Padding(12), Margin = new Padding(0, 0, 0, 12) };
            TableLayoutPanel layout = Rows();
            layout.Dock = DockStyle.Fill;
            foreach (Control control in controls) {
                control.Margin = new Padding(4, 6, 4, 6);
                layout.Controls.Add(control);
            }
            group.Controls.Add(layout);
            return group;
        }

        private static Label Note(string text)
        {
            return new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(4, 6, 4, 6), UseMnemonic = false };
        }

        private static Button Command(string text)
        {
            return new Button { Text = text, AutoSize = true, FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true, Anchor = AnchorStyles.Left };
        }

        private static ComboBox Choice(object[] values)
        {
            ComboBox combo = new ComboBox { Width = 320, DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.System, DrawMode = DrawMode.Normal, Anchor = AnchorStyles.Left };
            combo.Items.AddRange(values);
            return combo;
        }
        private void LoadPageList()
        {
            grid.EndEdit();
            CommitPage();
            editingPage = -1;
            pages.Items.Clear();
            foreach (WheelPage p in config.Pages) pages.Items.Add(p.Name);
            if (pages.Items.Count > 0) pages.SelectedIndex = 0;
        }

        private void ShowPage()
        {
            if (showingPage) return;
            grid.EndEdit();
            CommitPage();
            showingPage = true;
            loadingGrid = true;
            editingPage = pages.SelectedIndex;
            grid.Rows.Clear();
            if (pages.SelectedIndex < 0) { loadingGrid = false; showingPage = false; return; }
            string[] slots = { "右", "右下", "左下", "左", "左上", "右上" };
            WheelPage p = config.Pages[pages.SelectedIndex];
            pageName.Text = p.Name;
            for (int i = 0; i < 6; i++) grid.Rows.Add(slots[i], p.Actions[i].Name, ActionNames.Chinese(p.Actions[i].Type), p.Actions[i].Target);
            loadingGrid = false;
            showingPage = false;
            UpdateActionEditor();
        }

        private void HandleCellChange()
        {
            if (loadingGrid || choosingApp) return;
            CommitPage();
            UpdateActionEditor();
            if (grid.CurrentRow != null && grid.CurrentCell != null && grid.CurrentCell.ColumnIndex == 2) {
                string type = ActionNames.Id(Convert.ToString(grid.CurrentRow.Cells[2].Value));
                if (type == "App") BrowseApp();
                if (type == "Folder") BrowseFolder();
            }
            AutoSave();
        }

        private void UpdateActionEditor()
        {
            if (grid == null || grid.CurrentRow == null) return;
            string type = ActionNames.Id(Convert.ToString(grid.CurrentRow.Cells[2].Value));
            grid.CurrentRow.Cells[3].ReadOnly = type != "App" && type != "Folder" && type != "Command";
            if (type != "App" && type != "Folder" && type != "Command") grid.CurrentRow.Cells[3].Value = "";
        }

        private void UpdateEffectDescription()
        {
            if (effectDescription == null || style == null) return;
            string value = Convert.ToString(style.SelectedItem);
            effectDescription.Text = value == "高斯模糊" ? "强背景虚化，颜色保持自然" : value == "亚克力" ? "高遮罩、磨砂颗粒、低透视" : "圆形局部背景模糊、实时焦散与折射高光";
        }

        private void CommitPage()
        {
            if (editingPage < 0 || editingPage >= config.Pages.Count || grid.Rows.Count != 6) return;
            WheelPage p = config.Pages[editingPage];
            for (int i = 0; i < 6; i++) {
                p.Actions[i].Name = Convert.ToString(grid.Rows[i].Cells[1].Value);
                p.Actions[i].Type = ActionNames.Id(Convert.ToString(grid.Rows[i].Cells[2].Value));
                p.Actions[i].Target = Convert.ToString(grid.Rows[i].Cells[3].Value);
            }
        }

        private void AddPage()
        {
            grid.EndEdit();
            CommitPage();
            WheelPage p = new WheelPage { Name = "页面 " + (config.Pages.Count + 1), Actions = new List<ActionItem>() };
            for (int i = 0; i < 6; i++) p.Actions.Add(new ActionItem { Name = "空", Type = "None", Target = "" });
            config.Pages.Add(p);
            LoadPageList();
            pages.SelectedIndex = config.Pages.Count - 1;
            AutoSave();
        }

        private void DeletePage()
        {
            if (config.Pages.Count <= 1) { MessageBox.Show("至少保留一个页面。"); return; }
            grid.EndEdit();
            CommitPage();
            int i = pages.SelectedIndex;
            editingPage = -1;
            config.Pages.RemoveAt(i);
            LoadPageList();
            pages.SelectedIndex = Math.Min(i, config.Pages.Count - 1);
            AutoSave();
        }

        private void BrowseApp()
        {
            if (grid.CurrentRow == null || choosingApp) return;
            choosingApp = true;
            using (ApplicationPicker d = new ApplicationPicker()) {
                if (d.ShowDialog(this) == DialogResult.OK && d.SelectedApplication != null) {
                    grid.CurrentRow.Cells[1].Value = d.SelectedApplication.Name;
                    grid.CurrentRow.Cells[2].Value = ActionNames.Chinese("App");
                    grid.CurrentRow.Cells[3].Value = d.SelectedApplication.Target;
                }
            }
            choosingApp = false;
            CommitPage();
            AutoSave();
        }

        private void BrowseFolder()
        {
            if (grid.CurrentRow == null || choosingApp) return;
            choosingApp = true;
            using (FolderBrowserDialog d = new FolderBrowserDialog()) {
                d.Description = "选择要打开的文件夹";
                d.ShowNewFolderButton = true;
                if (d.ShowDialog(this) == DialogResult.OK && Directory.Exists(d.SelectedPath)) {
                    grid.CurrentRow.Cells[1].Value = Path.GetFileName(d.SelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (String.IsNullOrWhiteSpace(Convert.ToString(grid.CurrentRow.Cells[1].Value)))
                        grid.CurrentRow.Cells[1].Value = d.SelectedPath;
                    grid.CurrentRow.Cells[2].Value = ActionNames.Chinese("Folder");
                    grid.CurrentRow.Cells[3].Value = d.SelectedPath;
                }
            }
            choosingApp = false;
            CommitPage();
            AutoSave();
        }

        private void AutoSave()
        {
            if (initializing || loadingGrid || showingPage || choosingApp) return;
            CommitPage();
            config.Modifiers = recordedModifiers;
            config.KeyCode = recordedKey;
            config.Mode = mode.SelectedIndex == 1 ? "Hold" : "Click";
            config.Style = Convert.ToString(style.SelectedItem);
            bool startupChanged = config.StartWithWindows != startup.Checked;
            config.StartWithWindows = startup.Checked;
            config.MouseGestures = mouseGestures.Checked;
            if (startupChanged) Startup.Set(config.StartWithWindows);
            ConfigStore.Save(config);
            if (ConfigSaved != null) ConfigSaved(this, EventArgs.Empty);
        }

        private void RecordHotkey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin) return;
            bool win = (Native.GetAsyncKeyState((int)Keys.LWin) & 0x8000) != 0 || (Native.GetAsyncKeyState((int)Keys.RWin) & 0x8000) != 0;
            recordedModifiers = (e.Control ? Native.MOD_CONTROL : 0) | (e.Alt ? Native.MOD_ALT : 0) | (e.Shift ? Native.MOD_SHIFT : 0) | (win ? Native.MOD_WIN : 0);
            recordedKey = (int)e.KeyCode;
            hotkeyRecorder.Text = HotkeyText(recordedModifiers, recordedKey);
            e.SuppressKeyPress = true;
            AutoSave();
        }

        private string HotkeyText(int modifiers, int keyCode)
        {
            List<string> parts = new List<string>();
            if ((modifiers & Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifiers & Native.MOD_ALT) != 0) parts.Add("Alt");
            if ((modifiers & Native.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((modifiers & Native.MOD_WIN) != 0) parts.Add("Win");
            parts.Add(((Keys)keyCode).ToString());
            return String.Join(" + ", parts.ToArray());
        }

        private void ShowSection(int index)
        {
            if (index >= 0 && index < tabs.TabCount) tabs.SelectedIndex = index;
        }
    }
}
