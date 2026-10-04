using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using OrbitWheelLite;

// Compile with the production source and a separate entry point. OrbitContext is never
// started: no global hooks, user config, startup entry, or configured actions are used.
class ActionReliabilityTests
{
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    const string FixtureName = "OrbitReliabilityTarget";
    const uint HideFixture = 0x8002;
    const uint HideTrayFixture = 0x8003;
    static string sandbox;
    static int passed;
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    static object Field(object instance, string name) { return (instance is SettingsForm ? typeof(SettingsForm) : instance.GetType()).GetField(name, PrivateInstance).GetValue(instance); }
    static void SetField(object instance, string name, object value) { typeof(SettingsForm).GetField(name, PrivateInstance).SetValue(instance, value); }
    static object Invoke(object instance, string name, params object[] args) { return (instance is SettingsForm ? typeof(SettingsForm) : instance.GetType()).GetMethod(name, PrivateInstance | BindingFlags.DeclaredOnly).Invoke(instance, args); }
    static object Runner(string name, params object[] args) { return typeof(ActionRunner).GetMethod(name, PrivateStatic).Invoke(null, args); }
    static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    [STAThread]
    static int Main(string[] args)
    {
        // UI Automation can initialize WPF DPI awareness on first use. Keep cursor and
        // fixture coordinates physical from the start, like the production tray call.
        Native.SetThreadDpiAwarenessContext(new IntPtr(-4));
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location) == FixtureName) {
            Application.Run(new FixtureForm());
            return 0;
        }
        sandbox = args.Length > 1 ? args[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "run-" + Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(sandbox);
            RedirectConfig();
            if (args.Length > 0 && args[0] == "--reload-config") {
                VerifyConfig(ConfigStore.Load());
                Console.WriteLine("PASS separate process reloaded all sector bindings");
                return 0;
            }
            if (Array.IndexOf(args, "--tray-only") >= 0) {
                Test("real Windows notification icon double-click wakeup", TestTray);
                return 0;
            }
            Test("fixed sector headers, editing, save and process restart", TestGrid);
            Test("hold release samples the current cursor", TestWheel);
            Test("identity mismatch and compatibility rules", TestIdentityRules);
            Test("real same-name EXEs, launch, reuse and hidden restore", TestProcesses);
            if (Array.IndexOf(args, "--include-tray") >= 0) Test("real Windows notification icon double-click wakeup", TestTray);
            else Console.WriteLine("SKIP real taskbar tray interaction (run with --include-tray on Windows desktop)");
            Console.WriteLine("PASS " + passed + " test groups; evidence: " + sandbox);
            return 0;
        } catch (Exception error) {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    static void Test(string name, Action test)
    {
        test();
        passed++;
        Console.WriteLine("PASS " + name);
    }

    static void RedirectConfig()
    {
        typeof(ConfigStore).GetField("Folder").SetValue(null, sandbox);
        typeof(ConfigStore).GetField("FilePath").SetValue(null, Path.Combine(sandbox, "config.json"));
    }

    static AppConfig FixtureConfig()
    {
        AppConfig config = AppConfig.Default();
        config.Style = "亚克力";
        config.Pages.Clear();
        for (int page = 0; page < 2; page++) {
            WheelPage item = new WheelPage { Name = "Page" + page, Actions = new List<ActionItem>() };
            for (int sector = 0; sector < 6; sector++) item.Actions.Add(new ActionItem { Name = "Action" + (6 - sector) + "-" + page, Type = "Command", Target = "echo sector-" + page + "-" + sector });
            config.Pages.Add(item);
        }
        return config;
    }

    static void VerifyConfig(AppConfig config)
    {
        Assert(config.Pages.Count == 2, "Page count changed");
        for (int page = 0; page < 2; page++) {
            using (PassiveSettingsForm settings = new PassiveSettingsForm(config)) {
                SetField(settings, "initializing", true); // suppress registry writes and automatic saves
                ((ListBox)Field(settings, "pages")).SelectedIndex = page;
                DataGridView grid = (DataGridView)Field(settings, "grid");
                for (int sector = 0; sector < 6; sector++) {
                    string expectedName = sector == 2 ? "Edited-" + page : "Action" + (6 - sector) + "-" + page;
                    string expectedTarget = "echo sector-" + page + "-" + sector + (sector == 2 ? "-edited" : "");
                    ActionItem action = config.Pages[page].Actions[sector];
                    Assert(action.Name == expectedName && action.Target == expectedTarget && action.Type == "Command", "Persisted binding changed at page " + page + ", sector " + sector);
                    Assert(Convert.ToString(grid.Rows[sector].Cells[1].Value) == expectedName, "Reopened UI binding changed");
                }
            }
        }
    }

    static void TestGrid()
    {
        AppConfig config = FixtureConfig();
        using (PassiveSettingsForm settings = new PassiveSettingsForm(config)) {
            SetField(settings, "initializing", true);
            settings.StartPosition = FormStartPosition.Manual;
            settings.Location = new Point(-20000, -20000);
            Invoke(settings, "ShowSection", 1);
            settings.Show();
            DataGridView grid = (DataGridView)Field(settings, "grid");
            for (int page = 0; page < 2; page++) {
                ((ListBox)Field(settings, "pages")).SelectedIndex = page;
                foreach (DataGridViewColumn column in grid.Columns) {
                    Assert(column.SortMode == DataGridViewColumnSortMode.NotSortable, "Sortable sector column: " + column.Name);
                    Rectangle bounds = grid.GetCellDisplayRectangle(column.Index, -1, true);
                    if (bounds.Width == 0) continue;
                    IntPtr point = new IntPtr(((bounds.Top + bounds.Height / 2) << 16) | (bounds.Left + bounds.Width / 2));
                    SendMessage(grid.Handle, 0x0201, new IntPtr(1), point);
                    SendMessage(grid.Handle, 0x0202, IntPtr.Zero, point);
                    SendMessage(grid.Handle, 0x0201, new IntPtr(1), point);
                    SendMessage(grid.Handle, 0x0202, IntPtr.Zero, point);
                }
                Assert(grid.SortedColumn == null, "Header clicks sorted the grid");
                for (int sector = 0; sector < 6; sector++) Assert(Convert.ToString(grid.Rows[sector].Cells[1].Value) == "Action" + (6 - sector) + "-" + page, "Header click moved a binding");
                grid.Rows[2].Cells[1].Value = "Edited-" + page;
                grid.Rows[2].Cells[3].Value = "echo sector-" + page + "-2-edited";
                Invoke(settings, "CommitPage");
            }
            ConfigStore.Save(config);
            settings.Close();
        }
        VerifyConfig(ConfigStore.Load());
        using (Process reload = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--reload-config \"" + sandbox + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })) {
            string output = reload.StandardOutput.ReadToEnd();
            string error = reload.StandardError.ReadToEnd();
            Assert(reload.WaitForExit(15000) && reload.ExitCode == 0, "Config restart failed: " + error);
            Console.Write(output);
        }
    }

    static void ReleaseCase(string name, Point offset, int expected, bool leave, bool changePage)
    {
        Point oldCursor = Cursor.Position;
        try {
            using (WheelForm wheel = new WheelForm(FixtureConfig())) {
                ActionItem executed = null;
                int dispatches = 0;
                int closed = 0;
                wheel.ExecuteRequested += delegate(ActionItem action) { executed = action; dispatches++; };
                wheel.CloseRequested += delegate { closed++; };
                Point center = (Point)Field(wheel, "center");
                // Intentionally leave a cached selection in a different sector.
                Invoke(wheel, "OnMove", wheel, new MouseEventArgs(MouseButtons.None, 0, center.X + 150, center.Y, 0));
                if (leave) {
                    typeof(Control).GetMethod("OnMouseLeave", PrivateInstance).Invoke(wheel, new object[] { EventArgs.Empty });
                    Assert((int)Field(wheel, "selected") == -1, "MouseLeave did not clear highlight");
                }
                if (changePage) Invoke(wheel, "ChangePage", 1);
                Cursor.Position = wheel.PointToScreen(new Point(center.X + offset.X, center.Y + offset.Y));
                wheel.ExecuteHoldSelection();
                wheel.ExecuteHoldSelection(); // repeated release must not dispatch again
                if (expected < 0) Assert(executed == null && closed == 1, name + " dispatched a cancelled action");
                else Assert(executed != null && dispatches == 1 && executed.Target == "echo sector-" + (changePage ? 1 : 0) + "-" + expected && closed == 0, name + " did not select the current sector/page");
            }
        } finally { Cursor.Position = oldCursor; }
        Console.WriteLine("  PASS " + name);
    }

    static void TestWheel()
    {
        ReleaseCase("leave then release outside", new Point(300, 0), -1, true, false);
        ReleaseCase("no MouseLeave or MouseMove before outside release", new Point(255, 0), -1, false, false);
        ReleaseCase("one pixel beyond the outer radius", new Point(236, 0), -1, false, false);
        ReleaseCase("release in center", Point.Empty, -1, false, false);
        ReleaseCase("inner boundary", new Point(67, 0), -1, false, false);
        ReleaseCase("outer boundary", new Point(235, 0), 0, false, false);
        Point[] sectors = { new Point(150, 0), new Point(75, 130), new Point(-75, 130), new Point(-150, 0), new Point(-75, -130), new Point(75, -130) };
        for (int sector = 0; sector < sectors.Length; sector++) ReleaseCase("current sector " + sector, sectors[sector], sector, false, false);
        ReleaseCase("reenter after leaving", sectors[5], 5, true, false);
        ReleaseCase("release uses current page", sectors[1], 1, false, true);
        Point oldCursor = Cursor.Position;
        try {
            using (WheelForm wheel = new WheelForm(FixtureConfig())) {
                ActionItem executed = null;
                wheel.ExecuteRequested += delegate(ActionItem action) { executed = action; };
                Invoke(wheel, "OnKey", wheel, new KeyEventArgs(Keys.D1));
                Assert(executed != null && executed.Target == "echo sector-0-5", "Number key mapping changed");
            }
        } finally { Cursor.Position = oldCursor; }
    }

    static void TestIdentityRules()
    {
        string expected = Path.Combine(sandbox, "B", FixtureName + ".exe");
        string other = Path.Combine(sandbox, "A", FixtureName + ".exe");
        Assert(!(bool)Runner("MatchesApplicationIdentity", other, "", FixtureName, expected, "", FixtureName), "Known path mismatch fell back to name");
        Assert((bool)Runner("MatchesApplicationIdentity", expected.ToUpperInvariant(), "", FixtureName, expected, "", FixtureName), "Case-insensitive path failed");
        Assert((bool)Runner("MatchesApplicationIdentity", "", "", FixtureName, expected, "", FixtureName), "Unavailable path compatibility failed");
        Assert(!(bool)Runner("MatchesApplicationIdentity", "", "Other_123!App", FixtureName, "", "Expected_123!App", FixtureName), "Known AUMID mismatch fell back to name");
        Assert((bool)Runner("MatchesApplicationIdentity", "", "Expected_123!App", "DifferentName", "", "Expected_123!App", FixtureName), "AUMID identity failed");
        Assert((bool)Runner("MatchesApplicationIdentity", "", "Expected_123!App", "DifferentName", "", "Expected_123", FixtureName), "Package-family compatibility failed");
        Environment.SetEnvironmentVariable("ORBIT_TEST_ROOT", sandbox);
        Assert((string)Runner("NormalizeExecutablePath", "\"%ORBIT_TEST_ROOT%\\B\\..\\B\\" + FixtureName + ".exe\"") == expected, "Quoted environment path did not normalize");
        Assert((string)Runner("NormalizeExecutablePath", "shell:AppsFolder\\Expected_123!App") == "", "AUMID was interpreted as an EXE path");
        Assert((string)Runner("NormalizeExecutablePath", expected) == expected, "Nonexistent known path was discarded");
        List<string> aliases = (List<string>)Runner("BuildTrayAliases", "Fixture display", FixtureName, expected);
        Assert((int)Runner("GetTrayNameMatchScore", FixtureName, aliases) > 0 && (int)Runner("GetTrayNameMatchScore", "Unrelated program", aliases) == 0, "Tray aliases changed");
    }

    static string FixturePath(string name)
    {
        string path = Path.Combine(sandbox, name, FixtureName + ".exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FixtureName + ".exe"), path, true);
        return path;
    }

    static void Wait(Func<bool> condition, string message)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 12000) {
            if (condition()) return;
            Thread.Sleep(50);
        }
        throw new InvalidOperationException(message);
    }

    static Process StartFixture(string path)
    {
        Process process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true });
        Wait(delegate { return File.Exists(Path.Combine(Path.GetDirectoryName(path), "ready-" + process.Id + ".txt")); }, "Fixture did not start");
        return process;
    }

    static IntPtr Window(string path, bool visible)
    {
        return (IntPtr)Runner("FindExistingWindow", path, "", FixtureName, visible);
    }

    static void AssertWindow(IntPtr hwnd, Process process, string message)
    {
        uint pid;
        Native.GetWindowThreadProcessId(hwnd, out pid);
        Assert(hwnd != IntPtr.Zero && pid == process.Id, message);
        string expectedHandle = File.ReadAllText(Path.Combine(Path.GetDirectoryName((string)Runner("GetProcessExecutablePath", process)), "ready-" + process.Id + ".txt"));
        Assert(hwnd.ToInt64() == Int64.Parse(expectedHandle), message + " (selected another window of the fixture process)");
    }

    static List<Process> OwnedFixtures(string path)
    {
        List<Process> result = new List<Process>();
        foreach (Process process in Process.GetProcessesByName(FixtureName)) {
            if (String.Equals((string)Runner("GetProcessExecutablePath", process), path, StringComparison.OrdinalIgnoreCase)) result.Add(process);
            else process.Dispose();
        }
        return result;
    }

    static void StopFixtures(string path)
    {
        foreach (Process process in OwnedFixtures(path)) {
            using (process) {
                IntPtr hwnd = Window(path, false);
                if (hwnd != IntPtr.Zero) Native.PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
                if (!process.WaitForExit(3000)) { process.Kill(); process.WaitForExit(3000); }
            }
        }
    }

    static void TestProcesses()
    {
        string pathA = FixturePath("identity A");
        string pathB = FixturePath("identity B");
        Process a = null, b = null;
        try {
            a = StartFixture(pathA);
            AssertWindow((IntPtr)Runner("FindVisibleProcessMainWindow", FixtureName, pathA, ""), a, "A main window was not found");
            Assert(!(bool)Runner("IsApplicationRunning", FixtureName, pathB, ""), "B incorrectly reported running while only A exists");
            Assert((IntPtr)Runner("FindVisibleProcessMainWindow", FixtureName, pathB, "") == IntPtr.Zero, "B main window lookup returned A");
            Assert(Window(pathB, true) == IntPtr.Zero && Window(pathB, false) == IntPtr.Zero, "B enumeration returned A");
            Assert((IntPtr)Runner("WaitForApplicationWindow", FixtureName, pathB, "", FixtureName, 100) == IntPtr.Zero, "Tray wait returned A for B");
            Runner("ActivateOrStart", pathB, FixtureName);
            Wait(delegate { List<Process> matches = OwnedFixtures(pathB); int count = matches.Count; foreach (Process process in matches) process.Dispose(); return count == 1 && Window(pathB, true) != IntPtr.Zero; }, "B was not launched separately");
            b = OwnedFixtures(pathB)[0];
            AssertWindow(Window(pathB, true), b, "B lookup returned wrong PID");
            AssertWindow((IntPtr)Runner("FindVisibleProcessMainWindow", FixtureName, pathB, ""), b, "B main window lookup returned wrong PID");
            Assert((bool)Runner("IsApplicationRunning", FixtureName, pathB, ""), "Running B not recognized");
            string equivalent = "\"" + Path.GetDirectoryName(pathB) + "\\..\\identity B\\" + FixtureName + ".exe\"";
            Runner("ActivateOrStart", equivalent, FixtureName);
            Wait(delegate { return File.Exists(Path.Combine(Path.GetDirectoryName(pathB), "restore-" + b.Id + ".txt")); }, "Existing B did not receive restore");
            List<Process> reused = OwnedFixtures(pathB);
            Assert(reused.Count == 1, "Existing B was launched again");
            foreach (Process process in reused) process.Dispose();
            Assert((bool)Runner("HasConflictingProcessIdentity", FixtureName, pathB, ""), "Tray identity conflict was missed");
            List<string> aliases = (List<string>)Runner("BuildTrayAliases", FixtureName, FixtureName, pathB);
            Assert(!(bool)Runner("TryActivateFromWindowsTray", aliases, FixtureName, pathB, "", FixtureName), "Ambiguous tray app was clicked");
            IntPtr bWindow = Window(pathB, true);
            Native.PostMessage(bWindow, HideFixture, IntPtr.Zero, IntPtr.Zero);
            Wait(delegate { return !Native.IsWindowVisible(bWindow); }, "B fixture did not hide");
            Runner("ActivateOrStart", pathB, FixtureName);
            Wait(delegate { return Native.IsWindowVisible(bWindow); }, "Hidden B did not restore");
            AssertWindow(Window(pathB, true), b, "Hidden restore selected A");
        } finally {
            StopFixtures(pathA);
            StopFixtures(pathB);
            if (a != null) a.Dispose();
            if (b != null) b.Dispose();
        }
    }

    static void TestTray()
    {
        string path = FixturePath("tray identity");
        Process process = null;
        Point oldCursor = Cursor.Position;
        try {
            process = StartFixture(path);
            IntPtr hwnd = Window(path, true);
            Native.PostMessage(hwnd, HideTrayFixture, IntPtr.Zero, IntPtr.Zero);
            Wait(delegate { return !Native.IsWindowVisible(hwnd); }, "Tray fixture did not hide");
            Assert(!(bool)Runner("HasConflictingProcessIdentity", FixtureName, path, ""), "Unambiguous fixture reported a conflict");
            List<string> aliases = (List<string>)Runner("BuildTrayAliases", FixtureName, FixtureName, path);
            // Exercise opening the popup, rather than only a panel left open by a prior run.
            if ((AutomationElement)Runner("FindTrayOverflowRoot") != null) {
                IntPtr taskbar = Native.FindWindow("Shell_TrayWnd", null);
                AutomationElement toggle = (AutomationElement)Runner("FindTrayOverflowButton", AutomationElement.FromHandle(taskbar));
                Assert(toggle != null && (bool)Runner("TryInvokeAutomationElement", toggle), "Could not close the fixture tray panel");
                Wait(delegate { return (AutomationElement)Runner("FindTrayOverflowRoot") == null; }, "Tray panel did not close before test");
            }
            Point beforeWake = Cursor.Position;
            bool woke = (bool)Runner("TryActivateFromWindowsTray", aliases, FixtureName, path, "", FixtureName);
            if (!woke) {
                IntPtr taskbar = Native.FindWindow("Shell_TrayWnd", null);
                Console.WriteLine("  Taskbar handle: " + taskbar + "; fixture hidden: " + !Native.IsWindowVisible(hwnd));
                if (taskbar != IntPtr.Zero) DumpTrayFixture(AutomationElement.FromHandle(taskbar), "taskbar");
                AutomationElement overflow = (AutomationElement)Runner("FindTrayOverflowRoot");
                Console.WriteLine("  Visible overflow root: " + (overflow != null));
                if (overflow != null) DumpTrayFixture(overflow, "overflow");
            }
            Assert(woke, "Windows tray wakeup did not return a matching window");
            Assert(File.Exists(Path.Combine(Path.GetDirectoryName(path), "tray-wakeup.txt")), "Fixture did not receive the actual tray double-click");
            AssertWindow(Window(path, true), process, "Tray wakeup returned wrong process");
            Assert(Cursor.Position == beforeWake, "Tray wakeup did not restore cursor: before=" + beforeWake + ", after=" + Cursor.Position);
        } finally {
            Cursor.Position = oldCursor;
            StopFixtures(path);
            if (process != null) process.Dispose();
        }
    }

    static void DumpTrayFixture(AutomationElement root, string label)
    {
        AutomationElementCollection buttons = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
        Console.WriteLine("  " + label + " button count: " + buttons.Count);
        int matches = 0;
        foreach (AutomationElement button in buttons) {
            if ((button.Current.Name ?? "").IndexOf(FixtureName, StringComparison.OrdinalIgnoreCase) < 0) continue;
            matches++;
            Console.WriteLine("  Fixture button: class=" + button.Current.ClassName + " id=" + button.Current.AutomationId + " bounds=" + button.Current.BoundingRectangle);
        }
        Console.WriteLine("  " + label + " fixture matches: " + matches);
    }

    class PassiveSettingsForm : SettingsForm
    {
        public PassiveSettingsForm(AppConfig config) : base(config) { }
        protected override bool ShowWithoutActivation { get { return true; } }
    }

    class FixtureForm : Form
    {
        NotifyIcon tray;
        readonly string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        readonly int pid = Process.GetCurrentProcess().Id;
        public FixtureForm()
        {
            Text = FixtureName;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-20000, -20000);
            Size = new Size(400, 300);
            ShowInTaskbar = true;
            Shown += delegate { File.WriteAllText(Path.Combine(folder, "ready-" + pid + ".txt"), Handle.ToString()); };
            FormClosed += delegate { if (tray != null) tray.Dispose(); };
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == HideFixture) { Hide(); return; }
            if (message.Msg == HideTrayFixture) {
                if (tray == null) {
                    tray = new NotifyIcon { Text = FixtureName, Icon = SystemIcons.Application };
                    tray.MouseDoubleClick += delegate { File.WriteAllText(Path.Combine(folder, "tray-wakeup.txt"), "double-click"); Show(); tray.Visible = false; };
                }
                tray.Visible = true;
                Hide();
                return;
            }
            if (message.Msg == 0x0112 && (message.WParam.ToInt64() & 0xFFF0) == 0xF120) {
                File.WriteAllText(Path.Combine(folder, "restore-" + pid + ".txt"), "restore");
                Show();
                if (tray != null) tray.Visible = false;
            }
            base.WndProc(ref message);
        }
    }
}
