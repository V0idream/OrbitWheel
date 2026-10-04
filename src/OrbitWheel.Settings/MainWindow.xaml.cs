using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using OrbitWheelLite;
using Windows.Storage.Pickers;
using Windows.System;
using System.Runtime.InteropServices;

namespace OrbitWheel.Settings;

public sealed partial class MainWindow : Window
{
    private AppConfig _config;
    private string _revision;
    private int _section, _page;
    private int _generation;
    private bool _rendering, _dirty, _conflict, _closing, _choosing;
    private readonly DispatcherTimer _save = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private TextBox _hotkey;
    internal static MainWindow Instance;
    private static readonly string[] Positions = { "右", "右下", "左下", "左", "左上", "右上" };
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        Title = "OrbitWheel 设置";
        var work = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id,
            Microsoft.UI.Windowing.DisplayAreaFallback.Nearest).WorkArea;
        double scale = Math.Max(96, GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this))) / 96.0;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(Math.Min(work.Width, (int)Math.Round(1100 * scale)),
            Math.Min(work.Height, (int)Math.Round(780 * scale))));
        try {
            _config = ConfigStore.Load();
            if (!ConfigStore.TryLoad(out _config, out _revision)) throw new IOException("配置加载失败。");
        } catch (Exception error) {
            _config = AppConfig.Default(); _revision = ""; _conflict = true;
            Problem("配置无法读取，原文件已保留。" + error.Message);
        }
        _save.Tick += (_, _) => { _save.Stop(); SavePending(); };
        _poll.Tick += (_, _) => Poll();
        AppWindow.Closing += (_, e) => {
            if (!SavePending()) { e.Cancel = true; return; }
            _closing = true; _save.Stop(); _poll.Stop();
        };
        Navigation.SelectedItem = Navigation.MenuItems[0];
        Render();
        _poll.Start();
        if (Environment.GetEnvironmentVariable("ORBITWHEEL_UI_SMOKE") == "1")
            _ = RunSmokeAsync();
    }

    private void Navigate(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_rendering || args.SelectedItemContainer == null || _config == null) return;
        int index = Int32.Parse((string)args.SelectedItemContainer.Tag);
        if (!SavePending()) {
            _rendering = true; Navigation.SelectedItem = Navigation.MenuItems[_section]; _rendering = false; return;
        }
        _section = index; Render();
    }

    private void Changed()
    {
        if (_rendering || _closing) return;
        _dirty = true;
        if (_conflict) { Problem("配置已在另一处更新，请重新加载后编辑。未保存的修改仍保留在窗口中。"); return; }
        Status.Title = "正在保存"; Status.Message = ""; Status.Severity = InfoBarSeverity.Informational;
        _save.Stop(); _save.Start();
    }

    private bool SavePending()
    {
        if (!_dirty) return true;
        if (_conflict || _choosing) return false;
        try {
            if (!ConfigStore.TrySave(_config, _revision, out string revision)) {
                _conflict = true;
                Problem("配置已在另一处更新。为避免覆盖，请先重新加载；窗口中的修改尚未保存。");
                return false;
            }
            _revision = revision; _dirty = false;
            Status.Title = "已自动保存"; Status.Message = "主程序会自动读取新配置。";
            Status.Severity = InfoBarSeverity.Success; Reload.Visibility = Visibility.Collapsed;
            return true;
        } catch (Exception error) { Problem("保存失败：" + error.Message); return false; }
    }

    private void Poll()
    {
        if (_closing || _choosing || _dirty || _conflict) return;
        if (!ConfigStore.TryLoad(out AppConfig config, out string revision)) {
            Problem("暂时无法读取配置，保留当前内容；原文件不会被覆盖。"); return;
        }
        if (revision == _revision) return;
        _config = config; _revision = revision;
        _page = Math.Min(_page, _config.Pages.Count - 1);
        Render();
        Status.Title = "已同步"; Status.Message = "已读取另一处保存的配置。"; Status.Severity = InfoBarSeverity.Success;
    }

    private void Problem(string text)
    {
        Status.Title = "需要处理"; Status.Message = text; Status.Severity = InfoBarSeverity.Warning;
        Reload.Visibility = Visibility.Visible;
    }

    private async void ReloadClick(object sender, RoutedEventArgs args)
    {
        if (_choosing) return;
        _choosing = true;
        try {
            if (_dirty) {
                var confirmation = new ContentDialog { XamlRoot = Content.XamlRoot, Title = "重新加载配置？",
                    Content = "窗口中未保存的修改将被丢弃。", PrimaryButtonText = "重新加载", CloseButtonText = "取消" };
                if (await confirmation.ShowAsync() != ContentDialogResult.Primary) return;
            }
            ReloadFromDisk();
        } catch (Exception error) { Problem("重新加载失败：" + error.Message); }
        finally { _choosing = false; }
    }

    private bool ReloadFromDisk()
    {
        if (!ConfigStore.TryLoad(out AppConfig config, out string revision)) { Problem("配置仍无法读取，请先修复配置文件。"); return false; }
        _save.Stop(); _config = config; _revision = revision; _dirty = false; _conflict = false;
        _page = Math.Min(_page, _config.Pages.Count - 1); Render();
        Status.Title = "已重新加载"; Status.Message = ""; Status.Severity = InfoBarSeverity.Success; Reload.Visibility = Visibility.Collapsed;
        return true;
    }

    private void Render()
    {
        _rendering = true;
        _generation++;
        try {
            PageContent.Children.Clear();
            _page = Math.Max(0, Math.Min(_page, _config.Pages.Count - 1));
            switch (_section) {
                case 0: General(); break;
                case 1: Actions(); break;
                case 2: Appearance(); break;
                case 3: Hotkeys(); break;
                case 4: Advanced(); break;
                case 5:
                    Add(Heading("OrbitWheel"));
                    Add(Text("WinUI 3 设置界面 · WinForms 托盘、热键与径向菜单"));
                    Add(Text("关闭设置窗口后，主程序仍在系统托盘运行。"));
                    Add(Text("配置位置：" + ConfigStore.FilePath)); break;
            }
            PageScroll.ChangeView(null, 0, null);
        } finally { _rendering = false; }
    }

    private void General()
    {
        int generation = _generation;
        Add(Heading("启动与托盘"));
        var startup = new ToggleSwitch { Header = "随 Windows 自动启动", IsOn = _config.StartWithWindows };
        Identify(startup, "startup", "随 Windows 自动启动");
        startup.Toggled += (_, _) => {
            if (_rendering || generation != _generation || _config.StartWithWindows == startup.IsOn) return;
            _config.StartWithWindows = startup.IsOn; Changed();
        };
        Add(startup); Add(Text("主程序运行时应用启动项；关闭设置窗口后，OrbitWheel 仍会留在系统托盘。"));
        Add(Heading("触发模式"));
        var mode = Choice("触发模式", new[] { "点击模式", "按住并松开执行" }, _config.Mode == "Hold" ? 1 : 0);
        Identify(mode, "mode", "触发模式");
        mode.SelectionChanged += (_, _) => {
            string value = mode.SelectedIndex == 1 ? "Hold" : "Click";
            if (_rendering || generation != _generation || _config.Mode == value) return;
            _config.Mode = value; Changed();
        };
        Add(mode); Add(Text("圆环打开时，滚动鼠标滚轮切换页面。"));
    }

    private void Actions()
    {
        int generation = _generation;
        var pages = Choice("页面", _config.Pages.Select(page => page.Name).ToArray(), _page);
        Identify(pages, "pages", "选择页面");
        pages.SelectionChanged += (_, _) => {
            if (_rendering || generation != _generation || pages.SelectedIndex < 0 || pages.SelectedIndex == _page) return;
            if (!SavePending()) { _rendering = true; pages.SelectedIndex = _page; _rendering = false; return; }
            _page = pages.SelectedIndex; Render();
        };
        Add(pages);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var add = Button("添加页面", AddPage);
        Identify(add, "add-page", "添加页面"); buttons.Children.Add(add);
        var remove = Button("删除页面", async () => {
            if (_choosing) return;
            if (_config.Pages.Count <= 1) { Problem("至少保留一个页面。"); return; }
            if (!SavePending()) return;
            var dialog = new ContentDialog { XamlRoot = Content.XamlRoot, Title = "删除当前页面？",
                Content = "该页面的六个动作也会删除。", PrimaryButtonText = "删除", CloseButtonText = "取消" };
            _choosing = true;
            try {
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
                _choosing = false; RemovePageConfirmed();
            } finally { _choosing = false; }
        });
        Identify(remove, "delete-page", "删除页面"); buttons.Children.Add(remove); Add(buttons);
        WheelPage page = _config.Pages[_page];
        var name = Edit("页面名称", page.Name); Identify(name, "page-name", "页面名称");
        name.TextChanging += (_, _) => { if (_rendering || generation != _generation || page.Name == name.Text) return; page.Name = name.Text; Changed(); }; Add(name);
        for (int slot = 0; slot < 6; slot++) {
            ActionItem action = page.Actions[slot]; int index = slot;
            var fields = new StackPanel { Spacing = 12 };
            var label = Edit("名称", action.Name); Identify(label, "slot-" + slot + "-name", Positions[slot] + "名称");
            label.TextChanging += (_, _) => { if (_rendering || generation != _generation || action.Name == label.Text) return; action.Name = label.Text; Changed(); }; fields.Children.Add(label);
            string[] names = ActionNames.AllChinese().Cast<string>().ToArray();
            var type = Choice("动作类型", names, Array.IndexOf(names, ActionNames.Chinese(action.Type)));
            Identify(type, "slot-" + slot + "-type", Positions[slot] + "动作类型");
            var target = Edit("程序路径 / 文件夹 / 命令", action.Target);
            Identify(target, "slot-" + slot + "-target", Positions[slot] + "目标");
            target.IsEnabled = action.Type == "App" || action.Type == "Folder" || action.Type == "Command";
            target.TextChanging += (_, _) => { if (_rendering || generation != _generation || action.Target == target.Text) return; action.Target = target.Text; Changed(); };
            var browse = Button("选择应用或文件夹", async () => await Browse(action, target, label));
            Identify(browse, "slot-" + slot + "-browse", Positions[slot] + "选择目标");
            browse.IsEnabled = action.Type == "App" || action.Type == "Folder";
            type.SelectionChanged += async (_, _) => {
                if (_rendering || generation != _generation || type.SelectedIndex < 0) return;
                string selected = ActionNames.Id((string)type.SelectedItem);
                if (action.Type == selected) return;
                action.Type = selected;
                target.IsEnabled = action.Type == "App" || action.Type == "Folder" || action.Type == "Command";
                browse.IsEnabled = action.Type == "App" || action.Type == "Folder";
                if (!target.IsEnabled) { action.Target = ""; target.Text = ""; }
                Changed();
                if (browse.IsEnabled) await Browse(action, target, label);
            };
            fields.Children.Add(type); fields.Children.Add(target); fields.Children.Add(browse);
            var expander = new Expander { Header = Positions[slot] + " · 扇区 " + (index + 1), Content = fields,
                IsExpanded = slot == 0, HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch };
            Identify(expander, "slot-" + slot, Positions[slot] + "扇区"); Add(expander);
        }
    }

    private void AddPage()
    {
        if (!SavePending()) return;
        _config.Pages.Add(new WheelPage { Name = "页面 " + (_config.Pages.Count + 1),
            Actions = Enumerable.Range(0, 6).Select(_ => new ActionItem { Name = "空", Type = "None", Target = "" }).ToList() });
        _page = _config.Pages.Count - 1; Changed(); SavePending(); Render();
    }

    private void RemovePageConfirmed()
    {
        if (_config.Pages.Count <= 1) return;
        _config.Pages.RemoveAt(_page); _page = Math.Min(_page, _config.Pages.Count - 1);
        Changed(); SavePending(); Render();
    }

    private async Task Browse(ActionItem action, TextBox target, TextBox label)
    {
        if (_choosing) return;
        _choosing = true; _save.Stop();
        try {
            string path = null;
            if (action.Type == "Folder") {
                var picker = new FolderPicker(); picker.FileTypeFilter.Add("*");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                var folder = await picker.PickSingleFolderAsync(); path = folder?.Path;
            } else if (action.Type == "App") {
                var all = ApplicationCatalog.Load();
                var list = new ListView { Height = 280, SelectionMode = ListViewSelectionMode.Single,
                    DisplayMemberPath = "Name", ItemsSource = all };
                var search = new AutoSuggestBox { PlaceholderText = "搜索应用" };
                search.TextChanged += (_, _) => {
                    list.ItemsSource = all.Where(app => app.Name.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)).ToList();
                };
                var panel = new StackPanel { Spacing = 12 }; panel.Children.Add(search); panel.Children.Add(list);
                var dialog = new ContentDialog { XamlRoot = Content.XamlRoot, Title = "选择应用", Content = panel,
                    PrimaryButtonText = "选择", SecondaryButtonText = "浏览文件", CloseButtonText = "取消" };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary) {
                    if (list.SelectedItem is ApplicationChoice app) { path = app.Target; action.Name = app.Name; }
                } else if (result == ContentDialogResult.Secondary) {
                    var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".exe"); picker.FileTypeFilter.Add(".lnk");
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                    var file = await picker.PickSingleFileAsync(); path = file?.Path;
                    if (file != null) action.Name = Path.GetFileNameWithoutExtension(file.Name);
                }
            }
            if (path != null) { action.Target = path; target.Text = path; label.Text = action.Name; Changed(); }
        } catch (Exception error) { Problem("选择目标失败：" + error.Message); }
        finally { _choosing = false; if (_dirty) _save.Start(); }
    }

    private void Appearance()
    {
        int generation = _generation;
        Add(Heading("圆环视觉效果"));
        string[] values = { "液态玻璃", "高斯模糊", "亚克力" };
        var choice = Choice("效果样式", values, Array.IndexOf(values, _config.Style));
        Identify(choice, "style", "圆环效果样式");
        choice.SelectionChanged += (_, _) => {
            string value = (string)choice.SelectedItem;
            if (_rendering || generation != _generation || _config.Style == value) return;
            _config.Style = value; Changed();
        };
        Add(choice); Add(Text("效果只用于径向菜单；设置界面使用系统 Fluent 控件和 Mica。"));
    }

    private void Hotkeys()
    {
        Add(Heading("快捷键"));
        _hotkey = Edit("点击后按下组合键", HotkeyText()); _hotkey.IsReadOnly = true;
        Identify(_hotkey, "hotkey", "录制快捷键");
        _hotkey.KeyDown += (_, args) => {
            if (args.Key == VirtualKey.Control || args.Key == VirtualKey.Menu || args.Key == VirtualKey.Shift ||
                args.Key == VirtualKey.LeftWindows || args.Key == VirtualKey.RightWindows) return;
            int modifiers = 0;
            if (Down(VirtualKey.Menu)) modifiers |= 1;
            if (Down(VirtualKey.Control)) modifiers |= 2;
            if (Down(VirtualKey.Shift)) modifiers |= 4;
            if (Down(VirtualKey.LeftWindows) || Down(VirtualKey.RightWindows)) modifiers |= 8;
            _config.Modifiers = modifiers; _config.KeyCode = (int)args.Key; _hotkey.Text = HotkeyText(); Changed(); args.Handled = true;
        };
        Add(_hotkey); Add(Text("组合键被其他程序占用时，主程序会通过托盘提示。"));
    }

    private static bool Down(VirtualKey key) => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key) &
        Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
    private string HotkeyText() => ((_config.Modifiers & 2) != 0 ? "Ctrl + " : "") +
        ((_config.Modifiers & 1) != 0 ? "Alt + " : "") + ((_config.Modifiers & 4) != 0 ? "Shift + " : "") +
        ((_config.Modifiers & 8) != 0 ? "Win + " : "") + (VirtualKey)_config.KeyCode;

    private void Advanced()
    {
        int generation = _generation;
        Add(Heading("鼠标手势"));
        var gestures = new ToggleSwitch { Header = "启用左右键组合手势", IsOn = _config.MouseGestures };
        Identify(gestures, "gestures", "启用鼠标手势");
        gestures.Toggled += (_, _) => {
            if (_rendering || generation != _generation || _config.MouseGestures == gestures.IsOn) return;
            _config.MouseGestures = gestures.IsOn; Changed();
        }; Add(gestures);
        Add(Text("左右键同时按下并移动，松开后执行：上滑开始菜单，下滑桌面，左右滑切换窗口。"));
        Add(Text("组合手势期间屏蔽原点击；普通单击仅在 110 毫秒识别窗口内短暂延后。"));
    }

    private void Add(UIElement element) => PageContent.Children.Add(element);
    private static TextBlock Heading(string text) => new() { Text = text, Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"] };
    private static TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
    private static TextBox Edit(string header, string value) => new() { Header = header, Text = value ?? "", HorizontalAlignment = HorizontalAlignment.Stretch };
    private static ComboBox Choice(string header, string[] values, int selected) => new() {
        Header = header, ItemsSource = values, SelectedIndex = Math.Max(0, selected), HorizontalAlignment = HorizontalAlignment.Stretch };
    private static Button Button(string text, Action action)
    {
        var button = new Button { Content = text }; button.Click += (_, _) => action(); return button;
    }
    private static void Identify(DependencyObject element, string id, string name)
    {
        AutomationProperties.SetAutomationId(element, id); AutomationProperties.SetName(element, name);
    }
}
