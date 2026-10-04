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
            if (args.Length > 0 && args[0] == "--peer") return Peer();
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
            return 0;
        } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    static int Peer()
    {
        AppConfig config = AppConfig.Default();
        for (int index = 0; index < 6; index++) config.Pages[0].Actions[index] = new ActionItem {
            Name = "扇区 " + index, Type = "Command", Target = "echo slot-" + index
        };
        ConfigStore.Save(config);
        OrbitContext host = new OrbitContext(true);
        string ack = Path.Combine(ConfigStore.Folder, "host-ack.json");
        string command = Path.Combine(ConfigStore.Folder, "peer-command.txt");
        Timer timer = new Timer { Interval = 100 };
        timer.Tick += delegate {
            try {
                host.ReloadConfiguration();
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
