using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OrbitWheelLite;

namespace OrbitWheel.Settings;
public partial class App : Application
{
    private Mutex _instance;
    private Window _window;
    private string _pidFile;
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(ConfigStore.FilePath).ToUpperInvariant())));
        _instance = new Mutex(false, "Local\\OrbitWheel.Settings." + key);
        bool owned;
        try { owned = _instance.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
        if (!owned) {
            try {
                if (Int32.TryParse(File.ReadAllText(Path.Combine(ConfigStore.Folder, "settings-instance.pid")), out int pid)) {
                    using var process = System.Diagnostics.Process.GetProcessById(pid);
                    if (process.ProcessName == "OrbitWheel.Settings" && process.MainWindowHandle != IntPtr.Zero) {
                        ShowWindow(process.MainWindowHandle, 9); SetForegroundWindow(process.MainWindowHandle);
                    }
                }
            } catch (IOException) { } catch (ArgumentException) { }
            Exit(); return;
        }
        Directory.CreateDirectory(ConfigStore.Folder);
        _pidFile = Path.Combine(ConfigStore.Folder, "settings-instance.pid");
        File.WriteAllText(_pidFile, Environment.ProcessId.ToString());
        _window = new MainWindow();
        _window.Closed += (_, _) => {
            try { File.Delete(_pidFile); } catch (IOException) { }
            _instance.ReleaseMutex(); _instance.Dispose();
        };
        _window.Activate();
    }
}
