using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using OrbitWheelLite;
using System.Text.Json;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace OrbitWheel.Settings;

public sealed partial class MainWindow
{
    // Opt-in QA mode only; always requires an isolated configuration directory.
    private async Task RunSmokeAsync()
    {
        if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ORBITWHEEL_CONFIG_DIR"))) {
            Problem("原型测试必须指定隔离配置目录。"); return;
        }
        var steps = new List<string>();
        try {
            await Task.Delay(500);
            await Wait(() => File.Exists(Path.Combine(ConfigStore.Folder, "host-ack.json")), "WinForms peer start");
            if (SystemBackdrop is not MicaBackdrop) throw new Exception("Missing native Mica backdrop");
            for (int index = 0; index < 6; index++) {
                Navigation.SelectedItem = Navigation.MenuItems[index];
                if (PageContent.Children.Count == 0) throw new Exception("Empty settings page " + index);
                await Snapshot("page-" + index);
                await CheckScrollReachability("page " + index);
            }
            steps.Add("six native WinUI pages rendered");
            Navigation.SelectedItem = Navigation.MenuItems[0];
            ((ComboBox)Find("mode")).SelectedIndex = 0;
            if (!SavePending()) throw new Exception("UI save failed");
            await Wait(() => Peer().Mode == "Click", "WinUI -> WinForms mode");
            Navigation.SelectedItem = Navigation.MenuItems[1];
            await Task.Delay(120);
            AddPage();
            await Wait(() => Peer().Pages.Count == 2, "add page");
            RemovePageConfirmed();
            await Wait(() => Peer().Pages.Count == 1, "remove page");
            await Task.Delay(120);
            ((TextBox)Find("page-name")).Text = "WinUI 同步测试";
            ((Expander)Find("slot-5")).IsExpanded = true;
            await Task.Delay(120);
            ((TextBox)Find("slot-5-target")).Text = "echo slot-5-smoke";
            if (!SavePending()) throw new Exception("Sector save failed");
            await Wait(() => Peer().Pages[0].Actions[5].Target == "echo slot-5-smoke", "sector binding");
            for (int index = 0; index < 5; index++)
                if (Peer().Pages[0].Actions[index].Target != "echo slot-" + index) throw new Exception("Unexpected sector reorder");
            steps.Add("WinUI control events saved mode/name/sector; real WinForms peer loaded them");
            File.WriteAllText(Path.Combine(ConfigStore.Folder, "peer-command.txt"), "external");
            await Wait(() => _config.Pages[0].Name == "主程序同步回设置", "WinForms -> WinUI");
            steps.Add("WinForms atomic save propagated back to the visible WinUI editor");
            ((TextBox)Find("page-name")).Text = "待保存的本地编辑";
            _save.Stop();
            File.WriteAllText(Path.Combine(ConfigStore.Folder, "peer-command.txt"), "conflict");
            await Wait(() => ConfigStore.TryLoad(out var current, out _) && current.Pages[0].Name == "外部优先", "conflict setup");
            if (SavePending() || !_dirty || !_conflict) throw new Exception("Stale UI overwrote a newer revision");
            if (!ConfigStore.TryLoad(out var remote, out _) || remote.Pages[0].Name != "外部优先") throw new Exception("Conflict changed the disk config");
            // Models the user explicitly choosing to discard after confirmation;
            // the human-facing confirmation dialog is not counted as exercised.
            if (!ReloadFromDisk()) throw new Exception("Conflict reload failed");
            steps.Add("stale save rejected, local edit retained until explicit reload");
            string valid = File.ReadAllText(ConfigStore.FilePath);
            File.WriteAllText(ConfigStore.FilePath, "{invalid");
            await Task.Delay(700);
            if (_config.Pages[0].Name != "外部优先" || File.ReadAllText(ConfigStore.FilePath) != "{invalid")
                throw new Exception("Malformed configuration was overwritten or applied");
            ((TextBox)Find("page-name")).Text = "不能覆盖损坏文件"; _save.Stop();
            if (SavePending()) throw new Exception("Malformed configuration accepted an overwrite");
            File.WriteAllText(ConfigStore.FilePath, valid); ReloadFromDisk();
            steps.Add("malformed file retained and last valid state preserved in both processes");
            Navigation.SelectedItem = Navigation.MenuItems[0];
            ((FrameworkElement)Content).RequestedTheme = ElementTheme.Dark;
            await Snapshot("dark");
            ((FrameworkElement)Content).RequestedTheme = ElementTheme.Light;
            AppWindow.Resize(new Windows.Graphics.SizeInt32(480, 700));
            await Snapshot("narrow");
            if (PageScroll.ActualWidth <= 0 || PageContent.ActualWidth <= 0) throw new Exception("Empty narrow viewport");
            await CheckScrollReachability("narrow window");
            await Snapshot("narrow-bottom");
            steps.Add("native light/dark resources and narrow window rendered; page bottoms reached through native scrolling");
            File.WriteAllText(Path.Combine(ConfigStore.Folder, "ui-result.json"), JsonSerializer.Serialize(new {
                success = true, process = Environment.ProcessId, window = WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt64(), steps
            }));
        } catch (Exception error) {
            File.WriteAllText(Path.Combine(ConfigStore.Folder, "ui-result.json"), JsonSerializer.Serialize(new {
                success = false, error = error.ToString(), steps
            }));
        } finally {
            _dirty = false; _conflict = false; _choosing = false; Close();
        }
    }

    private AppConfig Peer()
    {
        using var stream = new FileStream(Path.Combine(ConfigStore.Folder, "host-ack.json"), FileMode.Open,
            FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return JsonSerializer.Deserialize<AppConfig>(stream);
    }
    private async Task CheckScrollReachability(string name)
    {
        PageScroll.ChangeView(null, PageScroll.ScrollableHeight, null, true);
        await Task.Delay(160);
        if (PageScroll.ScrollableHeight > 1 && Math.Abs(PageScroll.VerticalOffset - PageScroll.ScrollableHeight) > 2)
            throw new Exception("Page bottom unreachable: " + name);
        var last = (FrameworkElement)PageContent.Children.Last();
        var bottom = last.TransformToVisual(PageScroll).TransformPoint(new Windows.Foundation.Point(0, last.ActualHeight));
        if (last.ActualHeight <= 0 || bottom.Y > PageScroll.ActualHeight + 3)
            throw new Exception("Last control remains below viewport: " + name);
        PageScroll.ChangeView(null, 0, null, true);
        await Task.Delay(100);
    }
    private static async Task Wait(Func<bool> condition, string name)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(12)) {
            try { if (condition()) return; } catch (IOException) { }
            await Task.Delay(100);
        }
        throw new TimeoutException(name);
    }

    private DependencyObject Find(string id)
    {
        DependencyObject FindIn(DependencyObject node)
        {
            if (AutomationProperties.GetAutomationId(node) == id) return node;
            IEnumerable<DependencyObject> children = node switch {
                Panel panel => panel.Children.Cast<DependencyObject>(),
                Expander expander => new[] { expander.Content as DependencyObject },
                _ => Enumerable.Empty<DependencyObject>()
            };
            foreach (var child in children) if (child != null) { var result = FindIn(child); if (result != null) return result; }
            return null;
        }
        return FindIn(PageContent) ?? throw new Exception("Missing native control " + id);
    }

    private async Task Snapshot(string name)
    {
        await Task.Delay(160);
        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync((FrameworkElement)Content);
        var buffer = await bitmap.GetPixelsAsync();
        byte[] pixels = new byte[buffer.Length];
        using (var reader = DataReader.FromBuffer(buffer)) reader.ReadBytes(pixels);
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, pixels);
        await encoder.FlushAsync(); stream.Seek(0);
        using var output = new DataReader(stream.GetInputStreamAt(0));
        await output.LoadAsync((uint)stream.Size);
        byte[] png = new byte[(int)stream.Size]; output.ReadBytes(png);
        await File.WriteAllBytesAsync(Path.Combine(ConfigStore.Folder, name + ".png"), png);
    }
}
