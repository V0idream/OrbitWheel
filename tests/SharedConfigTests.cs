using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using OrbitWheelLite;

class SharedConfigTests
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try {
            if (args.Length > 0 && args[0] == "--hotkey-holder") {
                using (var binding = new HotkeyWindow()) {
                    Assert(binding.Set(7, 135), "Test-only Ctrl+Alt+Shift+F24 unavailable");
                    File.WriteAllText(Path.Combine(ConfigStore.Folder, "hotkey-ready"), "");
                    while (!File.Exists(Path.Combine(ConfigStore.Folder, "hotkey-stop"))) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                }
                return 0;
            }
            if (args.Length > 0 && args[0] == "--hotkey-probe") {
                using (var binding = new HotkeyWindow()) return binding.Set(7, 134) ? 1 : 0;
            }
            if (args.Length > 0 && args[0] == "--peer") return Peer();
            if (args.Length > 0 && args[0] == "--live-peer") return Peer(true);
            if (args.Length > 0 && args[0] == "--writer") {
                for (int iteration = 0; iteration < 80; iteration++) {
                    AppConfig config = AppConfig.Default(); config.Pages[0].Name = "generation-" + iteration;
                    foreach (ActionItem action in config.Pages[0].Actions) action.Target = config.Pages[0].Name;
                    ConfigStore.Save(config);
                }
                return 0;
            }
            AppConfig initial = ConfigStore.Load(); string revision;
            Assert(ConfigStore.TryLoad(out initial, out revision), "Initial read failed");
            initial.Pages[0].Name = "中文与 Unicode \uD83D\uDE80";
            string updated;
            Assert(ConfigStore.TrySave(initial, revision, out updated), "Valid optimistic save failed");
            initial.Pages[0].Name = "stale";
            Assert(!ConfigStore.TrySave(initial, revision, out revision), "Stale save accepted");
            Assert(ConfigStore.Load().Pages[0].Name == "中文与 Unicode \uD83D\uDE80", "Unicode changed");
            Console.WriteLine("PASS shared contract, Unicode and stale revision rejection");
            string valid = File.ReadAllText(ConfigStore.FilePath);
            File.WriteAllText(ConfigStore.FilePath, "{invalid");
            AppConfig broken;
            Assert(!ConfigStore.TryLoad(out broken, out revision), "Malformed config accepted");
            bool rejected = false;
            try { ConfigStore.Load(); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected && File.ReadAllText(ConfigStore.FilePath) == "{invalid", "Load overwrote malformed file");
            Assert(!ConfigStore.TrySave(initial, updated, out revision), "Malformed file overwritten");
            File.WriteAllText(ConfigStore.FilePath, valid);
            Console.WriteLine("PASS malformed file preserved without default reset");
            // A slow writer may own the mutex for longer than the old 250ms
            // timeout. Snapshot reads must remain available during that period.
            string identity;
            using (var hash = System.Security.Cryptography.SHA256.Create()) identity = BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(ConfigStore.FilePath).ToUpperInvariant()))).Replace("-", "");
            using (var mutex = new System.Threading.Mutex(false, "Local\\OrbitWheel.Config." + identity)) {
                var entered = new System.Threading.ManualResetEvent(false);
                var release = new System.Threading.ManualResetEvent(false);
                var owner = new System.Threading.Thread(delegate() { mutex.WaitOne(); entered.Set(); release.WaitOne(); mutex.ReleaseMutex(); });
                owner.Start(); entered.WaitOne();
                try {
                    var elapsed = Stopwatch.StartNew();
                    Assert(ConfigStore.TryLoad(out initial, out revision), "Snapshot blocked by writer mutex");
                    Assert(elapsed.ElapsedMilliseconds < 250, "Reader still waits for writer mutex");
                } finally { release.Set(); owner.Join(); entered.Dispose(); release.Dispose(); }
            }
            Console.WriteLine("PASS snapshot read independent of held writer mutex");
            using (var writer = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--writer") {
                UseShellExecute = false, CreateNoWindow = true
            })) {
                int reads = 0;
                while (!writer.HasExited || reads < 50) {
                    AppConfig sample;
                    Assert(ConfigStore.TryLoad(out sample, out revision), "Atomic reader failed: " + ConfigStore.LastReadError);
                    if (sample.Pages[0].Name.StartsWith("generation-"))
                        foreach (ActionItem action in sample.Pages[0].Actions) Assert(action.Target == sample.Pages[0].Name, "Torn snapshot");
                    reads++;
                }
                Assert(writer.ExitCode == 0, "Concurrent writer failed");
                Console.WriteLine("PASS concurrent real-process atomic reader/writer; reads=" + reads);
            }
            using (var host = new OrbitContext(true)) {
                AppConfig config;
                ConfigStore.TryLoad(out config, out revision);
                config.Mode = "Click"; config.Modifiers = 7; config.KeyCode = 122;
                config.Style = "亚克力"; config.MouseGestures = true;
                Assert(ConfigStore.TrySave(config, revision, out revision), "Host fixture save failed");
                host.ReloadConfiguration();
                Assert(host.CurrentConfig.Modifiers == 7 && host.CurrentConfig.KeyCode == 122 && host.CurrentConfig.Mode == "Click" &&
                    host.CurrentConfig.Style == "亚克力" && host.CurrentConfig.MouseGestures, "Host failed to update its config");
                string before = host.ConfigRevision;
                File.WriteAllText(ConfigStore.FilePath, "{invalid"); host.ReloadConfiguration();
                Assert(host.ConfigRevision == before && host.CurrentConfig.Mode == "Click", "Host applied malformed config");
                typeof(OrbitContext).GetMethod("Exit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(host, null);
            }
            Console.WriteLine("PASS production WinForms reload preserves last valid snapshot (hardware hooks disabled)");
            if (args.Length > 0 && args[0] == "--real-hotkey") {
                using (var binding = new HotkeyWindow()) {
                    Assert(binding.Set(7, 134), "Test-only Ctrl+Alt+Shift+F23 unavailable");
                    using (var holder = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--hotkey-holder") { UseShellExecute = false, CreateNoWindow = true })) {
                        try {
                            var elapsed = Stopwatch.StartNew();
                            while (!File.Exists(Path.Combine(ConfigStore.Folder, "hotkey-ready"))) {
                                Assert(!holder.HasExited && elapsed.ElapsedMilliseconds < 5000, "Hotkey holder failed"); System.Threading.Thread.Sleep(10);
                            }
                            Assert(!binding.Set(7, 135), "Occupied candidate registered");
                            Assert(binding.ActiveKey == 134 && binding.ActiveModifiers == 7, "Conflict lost old key");
                            using (var probe = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--hotkey-probe") { UseShellExecute = false, CreateNoWindow = true })) {
                                Assert(probe.WaitForExit(5000) && probe.ExitCode == 0, "Old key no longer registered at OS level");
                            }
                        } finally { File.WriteAllText(Path.Combine(ConfigStore.Folder, "hotkey-stop"), ""); if (!holder.WaitForExit(3000)) holder.Kill(); }
                    }
                    Assert(binding.Set(7, 135), "Released candidate failed to register");
                }
                Console.WriteLine("PASS real OS hotkey conflict preserves old registration; replacement succeeds after release");
            }
            var runtime = RuntimeState.Identity(); runtime.Passive = true; runtime.Revision = "stale";
            RuntimeState.Write("runtime", runtime);
            Assert(RuntimeState.Read("runtime") != null, "Fresh runtime unavailable");
            runtime.Started++; RuntimeState.Write("runtime", runtime);
            Assert(RuntimeState.Read("runtime") == null, "Reused PID identity accepted");
            runtime = RuntimeState.Identity(); runtime.Passive = true; RuntimeState.Write("runtime", runtime);
            string runtimePath = Path.Combine(ConfigStore.Folder, "runtime.json");
            var serializer = new JavaScriptSerializer(); runtime.Updated = DateTime.UtcNow.AddSeconds(-5).Ticks;
            File.WriteAllText(runtimePath, serializer.Serialize(runtime));
            Assert(RuntimeState.Read("runtime") == null, "Expired heartbeat accepted");
            runtime.ProcessId = Int32.MaxValue; runtime.Updated = DateTime.UtcNow.Ticks; File.WriteAllText(runtimePath, serializer.Serialize(runtime));
            Assert(RuntimeState.Read("runtime") == null, "Exited runtime accepted");
            Console.WriteLine("PASS runtime identity, expired heartbeat and exited-process rejection");
            return 0;
        } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    static int Peer(bool live = false)
    {
        AppConfig config = AppConfig.Default();
        for (int index = 0; index < 6; index++) config.Pages[0].Actions[index] = new ActionItem {
            Name = "扇区 " + index, Type = live ? "None" : "Command", Target = "echo slot-" + index
        };
        if (live) { config.Modifiers = 7; config.KeyCode = 133; }
        ConfigStore.Save(config);
        OrbitContext host = new OrbitContext(!live, live);
        bool wheelWasOpen = false; int wheelCount = 0;
        string ack = Path.Combine(ConfigStore.Folder, "host-ack.json");
        string command = Path.Combine(ConfigStore.Folder, "peer-command.txt");
        Timer timer = new Timer { Interval = 100 };
        timer.Tick += delegate {
            try {
                host.ReloadConfiguration();
                if (live) {
                    bool wheelOpen = typeof(OrbitContext).GetField("wheel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host) != null;
                    if (wheelOpen && !wheelWasOpen) wheelCount++;
                    wheelWasOpen = wheelOpen;
                    File.WriteAllText(Path.Combine(ConfigStore.Folder, "wheel-count.txt"), wheelCount.ToString());
                }
                if (File.Exists(command)) {
                    string text = File.ReadAllText(command); File.Delete(command);
                    AppConfig changed; string revision, updated;
                    if (!ConfigStore.TryLoad(out changed, out revision)) throw new Exception("Peer read failed");
                    changed.Pages[0].Name = text == "external" ? "主程序同步回设置" : "外部优先";
                    if (!ConfigStore.TrySave(changed, revision, out updated)) throw new Exception("Peer save conflicted");
                    host.ReloadConfiguration();
                }
                string temporary = ack + ".tmp";
                File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(host.CurrentConfig));
                if (File.Exists(ack)) File.Replace(temporary, ack, null); else File.Move(temporary, ack);
                if (File.Exists(Path.Combine(ConfigStore.Folder, "stop-peer"))) {
                    timer.Stop(); timer.Dispose();
                    typeof(OrbitContext).GetMethod("Exit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(host, null);
                }
            } catch (IOException) { }
        };
        timer.Start(); Application.Run(host); host.Dispose(); return 0;
    }
}
