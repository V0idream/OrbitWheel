using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OrbitWheelLite;

// Real controls and the production save path, in an isolated config directory.
// DPI cases use WinForms Control.Scale; the host display setting is unchanged.
class SettingsUsabilityTests
{
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static string evidence;
    static int cases;
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

    static object Field(SettingsForm form, string name) { return typeof(SettingsForm).GetField(name, PrivateInstance).GetValue(form); }
    static object Invoke(SettingsForm form, string name, params object[] args) { return typeof(SettingsForm).GetMethod(name, PrivateInstance).Invoke(form, args); }
    static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void RedirectConfig(string path)
    {
        Directory.CreateDirectory(path);
        typeof(ConfigStore).GetField("Folder").SetValue(null, path);
        typeof(ConfigStore).GetField("FilePath").SetValue(null, Path.Combine(path, "config.json"));
    }

    [STAThread]
    static int Main(string[] args)
    {
        Native.SetThreadDpiAwarenessContext(new IntPtr(-4));
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try {
            if (args.Length == 2 && args[0] == "--reload") {
                RedirectConfig(args[1]);
                VerifySaved(ConfigStore.Load());
                Console.WriteLine("PASS independent process reloaded settings and page edits");
                return 0;
            }
            evidence = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(evidence);
            CheckWindowEffects(args.Length == 1 && args[0] == "--native-window");
            CheckApplicationPicker();
            if (args.Length == 1 && args[0] == "--native-window") {
                Console.WriteLine("Evidence: " + evidence);
                return 0;
            }
            using (StreamWriter log = new StreamWriter(Path.Combine(evidence, "geometry.log"))) {
                foreach (float scale in new float[] { 1f, 1.5f, 2f }) {
                    // Nominal minimum and common desktop / laptop work areas.
                    CheckLayout(scale, new Size(5000, 5000), "minimum", log);
                    CheckLayout(scale, new Size(1920, 1040), "desktop", log);
                    CheckLayout(scale, new Size(1366, 728), "laptop", log);
                    CheckEditing(scale);
                }
                // Windows DPI compatibility also presents a reduced logical work
                // area to unaware .NET Framework apps at high display scaling.
                CheckLayout(1f, new Size(960, 520), "compatibility-200", log);
                using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero)) {
                    float hostScale = graphics.DpiX / 96f;
                    Console.WriteLine("Native host DPI: " + graphics.DpiX + " (" + (int)(hostScale * 100) + "%)");
                    CheckLayout(hostScale, Screen.PrimaryScreen.WorkingArea.Size, "native-host", log);
                }
            }
            Console.WriteLine("PASS " + cases + " section/layout cases at 100%, 150%, 200%; edits, auto-save and restart");
            Console.WriteLine("Evidence: " + evidence);
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    static AppConfig FixtureConfig()
    {
        AppConfig config = AppConfig.Default();
        config.Pages.Clear();
        for (int page = 0; page < 2; page++) {
            WheelPage item = new WheelPage { Name = "测试页面 " + page, Actions = new List<ActionItem>() };
            for (int slot = 0; slot < 6; slot++) item.Actions.Add(new ActionItem { Name = "扇区 " + slot, Type = "Command", Target = "echo page-" + page + "-slot-" + slot });
            config.Pages.Add(item);
        }
        return config;
    }

    static void CheckWindowEffects(bool captureNativeWindow)
    {
        float scale = 1f;
        if (captureNativeWindow) using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero)) scale = graphics.DpiX / 96f;
        using (PassiveSettingsForm form = Open(scale, Path.Combine(evidence, "window-effects"))) {
            CheckNativeControls(form);
            bool material = (bool)Field(form, "systemMaterial");
            int value;
            if (material) {
                Assert(DwmGetWindowAttribute(form.Handle, 38, out value, sizeof(int)) >= 0 && value == 2,
                    "The native window did not retain the requested main-window material");
                Assert(DwmGetWindowAttribute(form.Handle, 20, out value, sizeof(int)) >= 0 && value == 0,
                    "The native title bar does not match the light controls");
                Assert(DwmGetWindowAttribute(form.Handle, 33, out value, sizeof(int)) >= 0 && value == 2,
                    "The native window did not request rounded corners");
            }
            Assert(!SettingsWindowEffects.Apply(form.Handle, false, false), "Transparency-off must disable material");
            if (DwmGetWindowAttribute(form.Handle, 38, out value, sizeof(int)) >= 0)
                Assert(value == 1, "Transparency-off left the material enabled");
            Assert(!SettingsWindowEffects.Apply(form.Handle, true, true), "High contrast must disable material");
            if (DwmGetWindowAttribute(form.Handle, 20, out value, sizeof(int)) >= 0)
                Assert(value == 0, "High contrast left the dark title-bar override enabled");
            SendMessage(form.Handle, 0x031A, IntPtr.Zero, IntPtr.Zero);
            Assert((bool)Field(form, "systemMaterial") == material, "Theme change did not restore the current policy");
            typeof(Control).GetMethod("RecreateHandle", PrivateInstance).Invoke(form, null);
            Application.DoEvents();
            Assert((bool)Field(form, "systemMaterial") == material, "Handle recreation lost the current policy");
            Assert(form.FormBorderStyle == FormBorderStyle.Sizable && form.TransparencyKey == Color.Empty,
                "Native window resizing or opaque control rendering was changed");
            if (captureNativeWindow) {
                form.Location = Screen.PrimaryScreen.WorkingArea.Location;
                form.TopMost = true;
                form.BringToFront();
                TabControl tabs = (TabControl)Field(form, "tabs");
                for (int index = 0; index < tabs.TabCount; index++) {
                    tabs.SelectedIndex = index;
                    tabs.SelectedTab.AutoScrollPosition = Point.Empty;
                    form.Refresh();
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(150);
                    Application.DoEvents();
                    Rectangle capture = Rectangle.Intersect(form.Bounds, Screen.PrimaryScreen.Bounds);
                    using (Bitmap image = new Bitmap(capture.Width, capture.Height))
                    using (Graphics graphics = Graphics.FromImage(image)) {
                        graphics.CopyFromScreen(capture.Location, Point.Empty, capture.Size);
                        image.Save(Path.Combine(evidence, "native-tab-" + index + ".png"), ImageFormat.Png);
                    }
                }
            }
            Console.WriteLine("PASS native window effects: material=" + material + "; transparency-off, high-contrast, theme change, handle recreation");
            // Do not persist this fixture when disposing the test window.
        }
    }

    static void CheckNativeControls(Control parent)
    {
        foreach (Control control in parent.Controls) {
            Assert(control.GetType().Name != "GlassPanel", "Settings contains an owner-painted glass panel");
            Button button = control as Button;
            if (button != null)
                Assert(button.FlatStyle == FlatStyle.System && button.UseVisualStyleBackColor,
                    "Settings button does not use system rendering");
            ListBox list = control as ListBox;
            if (list != null) Assert(list.DrawMode == DrawMode.Normal, "Settings list uses owner drawing");
            ComboBox combo = control as ComboBox;
            if (combo != null) Assert(combo.DrawMode == DrawMode.Normal, "Settings combo uses owner drawing");
            CheckNativeControls(control);
        }
    }

    static void CheckApplicationPicker()
    {
        using (ApplicationPicker picker = new ApplicationPicker()) {
            CheckNativeControls(picker);
            List<ApplicationChoice> choices = new List<ApplicationChoice> {
                new ApplicationChoice { Name = "Alpha app", Target = @"C:\Alpha\app.exe" },
                new ApplicationChoice { Name = "Beta app", Target = "shell:AppsFolder\\Beta" }
            };
            typeof(ApplicationPicker).GetField("all", PrivateInstance).SetValue(picker, choices);
            TextBox search = (TextBox)typeof(ApplicationPicker).GetField("search", PrivateInstance).GetValue(picker);
            ListBox list = (ListBox)typeof(ApplicationPicker).GetField("list", PrivateInstance).GetValue(picker);
            search.Text = "beta";
            Assert(list.Items.Count == 1 && list.Items[0] == choices[1], "Native application list filtering lost its target");
            Assert(list.GetItemText(list.Items[0]) == "Beta app", "Native application list lost its display name");
            list.SelectedIndex = 0;
            typeof(ApplicationPicker).GetMethod("Accept", PrivateInstance).Invoke(picker, null);
            Assert(picker.DialogResult == DialogResult.OK && picker.SelectedApplication == choices[1],
                "Native application selection lost the original application identity");
        }
        Console.WriteLine("PASS native application picker filtering, display and selection");
    }

    static PassiveSettingsForm Open(float scale, string configPath)
    {
        RedirectConfig(configPath);
        PassiveSettingsForm form = new PassiveSettingsForm(FixtureConfig());
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-20000, -20000);
        form.Show();
        float hostScale = form.CurrentAutoScaleDimensions.Width / 96f;
        // Exercise the native recursive control-scaling machinery. Adjusting
        // AutoScaleDimensions after first load would skip already-scaled children.
        float additionalScale = scale / (float)Field(form, "layoutScale");
        if (Math.Abs(additionalScale - 1f) > 0.001f) {
            TabControl tabs = (TabControl)Field(form, "tabs");
            foreach (TabPage page in tabs.TabPages) page.SuspendLayout();
            form.Scale(new SizeF(additionalScale, additionalScale));
            foreach (TabPage page in tabs.TabPages) page.ResumeLayout(true);
        }
        // A synthetic layout scale does not change the host Graphics DPI. Adjust
        // point sizes so GDI renders the equivalent physical text size as well.
        List<Control> fontControls = new List<Control>();
        List<Font> fonts = new List<Font>();
        CaptureFonts(form, fontControls, fonts);
        for (int i = 0; i < fontControls.Count; i++)
            fontControls[i].Font = new Font(fonts[i].FontFamily, fonts[i].Size * scale / hostScale, fonts[i].Style);
        Application.DoEvents();
        Assert(Math.Abs((float)Field(form, "layoutScale") - scale) < 0.01f, "Requested scale was not applied");
        return form;
    }

    static void CaptureFonts(Control parent, List<Control> controls, List<Font> fonts)
    {
        controls.Add(parent);
        fonts.Add(parent.Font);
        foreach (Control child in parent.Controls) CaptureFonts(child, controls, fonts);
    }

    static void Pump(Control control)
    {
        control.PerformLayout();
        Application.DoEvents();
        control.PerformLayout();
    }

    static void Within(Control control, Control parent, string description)
    {
        Assert(parent.ClientRectangle.Contains(control.Bounds), description + " clipped: " + control.Bounds + " viewport=" + parent.ClientRectangle);
    }

    static void CheckLayout(float scale, Size workArea, string name, StreamWriter log)
    {
        string label = ((int)(scale * 100)) + "-" + name;
        using (PassiveSettingsForm form = Open(scale, Path.Combine(evidence, label))) {
            Invoke(form, "FitToWorkingArea", new Rectangle(Point.Empty, workArea));
            Size trackLimit = SystemInformation.MaxWindowTrackSize;
            Size expectedMinimum = new Size(Math.Min((int)(1080 * scale), Math.Min(workArea.Width, trackLimit.Width)),
                                            Math.Min((int)(700 * scale), Math.Min(workArea.Height, trackLimit.Height)));
            // WinForms can further clamp MinimumSize to the real desktop even
            // when the test supplies a larger virtual work area (headless CI).
            Assert(form.MinimumSize.Width > 0 && form.MinimumSize.Height > 0 &&
                   form.MinimumSize.Width <= expectedMinimum.Width && form.MinimumSize.Height <= expectedMinimum.Height,
                   label + " minimum exceeds its work area: actual=" + form.MinimumSize + " expected upper limit=" + expectedMinimum);
            Size nativeWorkArea = Screen.FromControl(form).WorkingArea.Size;
            if (expectedMinimum.Width < nativeWorkArea.Width && expectedMinimum.Height < nativeWorkArea.Height)
                Assert(form.MinimumSize == expectedMinimum, label + " minimum changed within the available desktop");
            form.Size = new Size(1, 1);
            Assert(form.Size == form.MinimumSize, label + " native minimum was not enforced");
            TabControl tabs = (TabControl)Field(form, "tabs");
            Assert(tabs.TabCount == 6 && tabs.DrawMode == TabDrawMode.Normal, "Expected six system-rendered tabs");
            Assert(form.Controls["settingsShell"] == null, "Legacy shell is still present");
            for (int tabIndex = 0; tabIndex < tabs.TabCount; tabIndex++) {
                tabs.SelectedIndex = tabIndex;
                Pump(form);
                TabPage page = tabs.SelectedTab;
                Within(tabs, tabs.Parent, label + " tab control");
                Assert(page.Visible && page.ClientSize.Width > 0 && page.ClientSize.Height > 0, "Tab did not open");
                TableLayoutPanel content = (TableLayoutPanel)page.Controls[0];
                Assert(content.Controls.Count > 0, "Empty tab");
                foreach (Control group in content.Controls) Assert(group is GroupBox, "Settings must use native groups");
                page.AutoScrollPosition = Point.Empty;
                Pump(page);
                if (page.VerticalScroll.Visible) {
                    SendMessage(page.Handle, 0x020A, new IntPtr(-120 << 16), IntPtr.Zero);
                    Assert(page.AutoScrollPosition.Y < 0, label + " native wheel did not scroll tab " + tabIndex);
                    page.AutoScrollPosition = Point.Empty;
                }
                List<Control> targets = new List<Control>();
                VerifyTableContent(content, label, targets);
                foreach (Control control in targets) {
                    Point corner = control.PointToScreen(new Point(control.Width - 2, control.Height - 2));
                    Point viewport = page.PointToClient(corner);
                    page.AutoScrollPosition = new Point(Math.Max(0, viewport.X - page.AutoScrollPosition.X),
                                                        Math.Max(0, viewport.Y - page.AutoScrollPosition.Y));
                    Pump(page);
                    Point visibleCorner = page.PointToClient(control.PointToScreen(new Point(control.Width - 2, control.Height - 2)));
                    Assert(page.ClientRectangle.Contains(visibleCorner), label + " unreachable control " + control.GetType().Name +
                        " text=" + control.Text + " corner=" + visibleCorner + " viewport=" + page.ClientRectangle);
                }
                page.AutoScrollPosition = Point.Empty;
                if (tabIndex == 1) {
                    DataGridView grid = (DataGridView)Field(form, "grid");
                    grid.CurrentCell = grid.Rows[5].Cells[3];
                    Rectangle target = grid.GetCellDisplayRectangle(3, 5, true);
                    Assert(target.Width >= Math.Min(150 * scale, grid.Columns[3].Width) && target.Height > 0,
                           label + " sixth sector target cannot be reached: " + target);
                }
                log.WriteLine(label + " tab=" + tabIndex + " outer=" + form.Size + " client=" + form.ClientSize +
                              " viewport=" + page.ClientSize + " table=" + content.Bounds);
                if (name == "minimum" || name == "laptop" || name == "compatibility-200" || name == "native-host") {
                    using (Bitmap image = new Bitmap(form.Width, form.Height)) {
                        form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                        image.Save(Path.Combine(evidence, label + "-tab-" + tabIndex + ".png"), ImageFormat.Png);
                    }
                }
                cases++;
            }
            form.Size = new Size((int)(1480 * scale), (int)(980 * scale));
            tabs.SelectedIndex = 1;
            Pump(form);
            TableLayoutPanel expanded = (TableLayoutPanel)tabs.SelectedTab.Controls[0];
            Assert(expanded.Width >= expanded.MinimumSize.Width && expanded.Width <= Math.Max(expanded.MinimumSize.Width,
                tabs.SelectedTab.ClientSize.Width), label + " table failed to fit expanded viewport: table=" + expanded.Size +
                " minimum=" + expanded.MinimumSize + " viewport=" + tabs.SelectedTab.ClientSize);
        }
    }

    static void VerifyTableContent(Control parent, string label, List<Control> targets)
    {
        foreach (Control control in parent.Controls) {
            Within(control, parent, label + " layout child " + control.GetType().Name + ": " + control.Text);
            Label text = control as Label;
            if (text != null && !String.IsNullOrEmpty(text.Text)) {
                Size measured = TextRenderer.MeasureText(text.Text, text.Font, new Size(text.Width, 10000),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                Assert(measured.Height <= text.Height + 2, label + " wrapped text clipped: " + text.Text +
                    " measured=" + measured + " size=" + text.Size);
            }
            if (control is TableLayoutPanel || control is GroupBox) VerifyTableContent(control, label, targets);
            else targets.Add(control);
        }
    }
    static void BeginTargetEdit(SettingsForm form, int row, string target)
    {
        DataGridView grid = (DataGridView)Field(form, "grid");
        grid.CurrentCell = grid.Rows[row].Cells[3];
        Assert(grid.BeginEdit(false), "Target editor failed to open");
        TextBox editor = grid.EditingControl as TextBox;
        Assert(editor != null && editor.Visible && editor.Width > 0, "Native target editor is unavailable");
        editor.Text = target;
        Assert(grid.IsCurrentCellInEditMode, "Cell was committed before the navigation test");
    }

    static void CheckEditing(float scale)
    {
        string configPath = Path.Combine(evidence, ((int)(scale * 100)) + "-editing");
        using (PassiveSettingsForm form = Open(scale, configPath)) {
            Invoke(form, "FitToWorkingArea", new Rectangle(0, 0, 1366, 728));
            form.Size = form.MinimumSize;
            TabControl tabs = (TabControl)Field(form, "tabs");
            ListBox pages = (ListBox)Field(form, "pages");
            int saves = 0;
            form.ConfigSaved += delegate { saves++; };
            tabs.SelectedIndex = 1;
            BeginTargetEdit(form, 5, "echo saved-before-page-switch");
            pages.SelectedIndex = 1;
            Assert(ConfigStore.Load().Pages[0].Actions[5].Target == "echo saved-before-page-switch", "Page switch lost the old page's in-progress edit");
            BeginTargetEdit(form, 4, "echo saved-before-section-switch");
            SendMessage(tabs.Handle, 0x0100, new IntPtr(0x27), IntPtr.Zero); // Native right-arrow tab navigation.
            Assert(tabs.SelectedIndex == 2, "Native keyboard tab navigation failed");
            tabs.SelectedIndex = 4;
            Assert(ConfigStore.Load().Pages[1].Actions[4].Target == "echo saved-before-section-switch", "Section switch lost the edit");
            ((CheckBox)Field(form, "mouseGestures")).Checked = true;
            tabs.SelectedIndex = 0;
            ((ComboBox)Field(form, "mode")).SelectedIndex = 0;
            tabs.SelectedIndex = 2;
            ((ComboBox)Field(form, "style")).SelectedItem = "亚克力";
            tabs.SelectedIndex = 3;
            Invoke(form, "RecordHotkey", Field(form, "hotkeyRecorder"), new KeyEventArgs(Keys.Control | Keys.Alt | Keys.K));
            tabs.SelectedIndex = 1;
            ((TextBox)Field(form, "pageName")).Text = "已自动保存的页面";
            Invoke(form, "AddPage");
            Assert(pages.Items.Count == 3 && pages.SelectedIndex == 2, "Adding a page failed");
            Invoke(form, "DeletePage");
            Assert(pages.Items.Count == 2 && pages.SelectedIndex == 1, "Deleting a page failed");
            BeginTargetEdit(form, 3, "echo saved-before-close");
            form.Close();
            Assert(saves >= 9, "Expected production automatic saves were not fired");
        }
        VerifySaved(ConfigStore.Load());
        using (Process reload = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--reload \"" + configPath + "\"") {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        })) {
            string output = reload.StandardOutput.ReadToEnd();
            string error = reload.StandardError.ReadToEnd();
            Assert(reload.WaitForExit(15000) && reload.ExitCode == 0, "Restart verification failed: " + error);
            Console.Write(output);
        }
        Console.WriteLine("PASS " + ((int)(scale * 100)) + "% edit, page/section switch, add/delete, hotkey, close and auto-save");
    }

    static void VerifySaved(AppConfig config)
    {
        Assert(config.Pages.Count == 2 && config.Pages[1].Name == "已自动保存的页面", "Page list/name was not saved");
        Assert(config.Pages[0].Actions[5].Target == "echo saved-before-page-switch", "Page switch edit was not persisted");
        Assert(config.Pages[1].Actions[4].Target == "echo saved-before-section-switch", "Section switch edit was not persisted");
        Assert(config.Pages[1].Actions[3].Target == "echo saved-before-close", "Close edit was not persisted");
        Assert(config.Pages[0].Actions[4].Target == "echo page-0-slot-4", "Edit leaked into another page");
        Assert(config.Mode == "Click" && config.Style == "亚克力" && config.MouseGestures, "Other settings were not saved");
        Assert(config.Modifiers == (Native.MOD_CONTROL | Native.MOD_ALT) && config.KeyCode == (int)Keys.K, "Hotkey was not saved");
        using (PassiveSettingsForm reopened = new PassiveSettingsForm(config)) {
            ((ListBox)Field(reopened, "pages")).SelectedIndex = 1;
            DataGridView grid = (DataGridView)Field(reopened, "grid");
            Assert(Convert.ToString(grid.Rows[3].Cells[3].Value) == "echo saved-before-close", "Reopened editor differs from saved config");
        }
    }

    class PassiveSettingsForm : SettingsForm
    {
        public PassiveSettingsForm(AppConfig config) : base(config) { }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { CreateParams value = base.CreateParams; value.ExStyle |= 0x08000000; return value; }
        }
    }
}
