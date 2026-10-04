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
            List<Panel> sections = (List<Panel>)Field(form, "sections");
            foreach (Panel section in sections) section.SuspendLayout();
            form.Scale(new SizeF(additionalScale, additionalScale));
            foreach (Panel section in sections) section.ResumeLayout(true);
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
            Size expectedMinimum = new Size(Math.Min((int)(1080 * scale), workArea.Width), Math.Min((int)(700 * scale), workArea.Height));
            Assert(form.MinimumSize == expectedMinimum, label + " minimum does not match its work area");
            form.Size = new Size(1, 1);
            Assert(form.Size == form.MinimumSize, label + " native minimum was not enforced");
            Control shell = form.Controls["settingsShell"];
            Control host = (Control)Field(form, "contentHost");
            GlassPanel sidebar = (GlassPanel)shell.Controls["settingsNavigation"];
            List<Button> navigation = (List<Button>)Field(form, "navigation");
            List<Panel> sections = (List<Panel>)Field(form, "sections");
            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++) {
                sidebar.ScrollControlIntoView(navigation[sectionIndex]);
                Pump(sidebar);
                Within(navigation[sectionIndex], sidebar, label + " navigation " + sectionIndex);
                navigation[sectionIndex].PerformClick();
                Pump(form);
                Within(shell, form, label + " shell");
                Within(host, shell, label + " content host");
                Within(sidebar, shell, label + " sidebar");
                Panel section = sections[sectionIndex];
                Assert(section.Visible, "Navigation did not show section " + sectionIndex);
                Assert(section.ClientSize.Width > 0 && section.ClientSize.Height > 0, label + " empty viewport");
                section.AutoScrollPosition = Point.Empty;
                if (section.VerticalScroll.Visible) {
                    SendMessage(section.Handle, 0x020A, new IntPtr(-120 << 16), IntPtr.Zero);
                    Assert(section.AutoScrollPosition.Y < 0, label + " native mouse wheel did not scroll section " + sectionIndex);
                    section.AutoScrollPosition = Point.Empty;
                }
                foreach (Control card in section.Controls) {
                    foreach (Control child in card.Controls) {
                        Within(child, card, label + " card child " + child.GetType().Name + ": " + child.Text);
                        if (child is Label && !String.IsNullOrEmpty(child.Text)) {
                            Size measured = TextRenderer.MeasureText(child.Text, child.Font, new Size(child.Width, 10000), TextFormatFlags.WordBreak);
                            Assert(measured.Height <= child.Height + 2, label + " text clipped: " + child.Text + " measured=" + measured + " control=" + child.Size);
                        }
                        // Scroll a small rectangle at each control's far corner
                        // into view, including controls taller than the viewport.
                        Rectangle corner = new Rectangle(child.Right - 2, child.Bottom - 2, 2, 2);
                        Point target = new Point(card.Left - section.AutoScrollPosition.X + corner.X,
                                                 card.Top - section.AutoScrollPosition.Y + corner.Y);
                        section.AutoScrollPosition = target;
                        Pump(section);
                        Rectangle visibleCorner = new Rectangle(section.PointToClient(card.PointToScreen(corner.Location)), corner.Size);
                        Assert(section.ClientRectangle.Contains(visibleCorner), label + " unreachable content " + child.Text + " at " + visibleCorner);
                    }
                }
                section.AutoScrollPosition = Point.Empty;
                if (sectionIndex == 1) {
                    DataGridView grid = (DataGridView)Field(form, "grid");
                    grid.CurrentCell = grid.Rows[5].Cells[3];
                    Rectangle target = grid.GetCellDisplayRectangle(3, 5, true);
                    Assert(target.Width >= Math.Min(150 * scale, grid.Columns[3].Width) && target.Height > 0,
                           label + " sixth sector target cannot be reached: " + target);
                }
                log.WriteLine(label + " section=" + sectionIndex + " outer=" + form.Size + " client=" + form.ClientSize +
                              " shell=" + shell.Bounds + " viewport=" + section.ClientSize + " virtual=" + section.DisplayRectangle.Size);
                if (name == "minimum" || name == "laptop" || name == "compatibility-200" || name == "native-host") {
                    using (Bitmap image = new Bitmap(form.Width, form.Height)) {
                        form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                        image.Save(Path.Combine(evidence, label + "-section-" + sectionIndex + ".png"), ImageFormat.Png);
                    }
                }
                cases++;
            }
            // Growing must expand the shell/cards and remove obsolete scrollbars.
            form.Size = new Size((int)(1480 * scale), (int)(980 * scale));
            navigation[1].PerformClick();
            Pump(form);
            Within(shell, form, label + " expanded shell");
            Panel expanded = sections[1];
            Assert(!expanded.HorizontalScroll.Visible && !expanded.VerticalScroll.Visible, label + " stale scrollbars after growing");
            Assert(expanded.Controls[0].Width == expanded.ClientSize.Width, label + " card did not stretch");
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
            List<Button> navigation = (List<Button>)Field(form, "navigation");
            ListBox pages = (ListBox)Field(form, "pages");
            int saves = 0;
            form.ConfigSaved += delegate { saves++; };
            navigation[1].PerformClick();
            BeginTargetEdit(form, 5, "echo saved-before-page-switch");
            pages.SelectedIndex = 1;
            Assert(ConfigStore.Load().Pages[0].Actions[5].Target == "echo saved-before-page-switch", "Page switch lost the old page's in-progress edit");
            BeginTargetEdit(form, 4, "echo saved-before-section-switch");
            navigation[4].PerformClick();
            Assert(ConfigStore.Load().Pages[1].Actions[4].Target == "echo saved-before-section-switch", "Section switch lost the edit");
            ((CheckBox)Field(form, "mouseGestures")).Checked = true;
            navigation[0].PerformClick();
            ((ComboBox)Field(form, "mode")).SelectedIndex = 0;
            navigation[2].PerformClick();
            ((ComboBox)Field(form, "style")).SelectedItem = "亚克力";
            navigation[3].PerformClick();
            Invoke(form, "RecordHotkey", Field(form, "hotkeyRecorder"), new KeyEventArgs(Keys.Control | Keys.Alt | Keys.K));
            navigation[1].PerformClick();
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
