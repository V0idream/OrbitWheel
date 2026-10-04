using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
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
        ((UIElement)Content).AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RegionNavigation), true);
        Activated += (_, args) => { if (args.WindowActivationState == WindowActivationState.Deactivated) CancelRecording(); };
        AutomationProperties.SetLiveSetting(Status, AutomationLiveSetting.Polite);
        Instance = this;
        Title = "OrbitWheel 设置";
        InstallRecordingGuard();
        var work = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id,
            Microsoft.UI.Windowing.DisplayAreaFallback.Nearest).WorkArea;
        double scale = Math.Max(96, GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this))) / 96.0;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(Math.Min(work.Width, (int)Math.Round(1100 * scale)),
            Math.Min(work.Height, (int)Math.Round(780 * scale))));
        // Size alone can leave the lower edge beyond the work area at high DPI.
        AppWindow.Move(new Windows.Graphics.PointInt32(work.X + Math.Max(0, (work.Width - AppWindow.Size.Width) / 2),
            work.Y + Math.Max(0, (work.Height - AppWindow.Size.Height) / 2)));
        try {
            _config = ConfigStore.Load();
            if (!ConfigStore.TryLoad(out _config, out _revision)) throw new IOException("配置加载失败。");
        } catch (Exception error) {
            _config = AppConfig.Default(); _revision = ""; _conflict = true;
            Problem("配置无法读取，原文件已保留。" + error.Message);
        }
        _save.Tick += (_, _) => { _save.Stop(); SavePending(); };
        _poll.Tick += (_, _) => Poll();
        AppWindow.Closing += (window, e) => {
            if (!_closing && !SavePending()) { e.Cancel = true; _ = ResolveCloseAsync(); return; }
            CancelRecording();
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
        SavePending(); CancelRecording();
        _section = index; Render();
    }

    private void Changed()
    {
        if (_rendering || _closing) return;
        _dirty = true;
        if (_conflict) { Problem("配置已在另一处更新，请重新加载后编辑。未保存的修改仍保留在窗口中。"); return; }
        SaveStatus.Text = "正在保存…";
        _save.Stop(); _save.Start();
    }

    private bool SavePending()
    {
        if (!_dirty) return true;
        if (_conflict || _choosing) return false;
        if (!ValidateInputs()) return false;
        try {
            if (!ConfigStore.TrySave(_config, _revision, out string revision)) {
                _conflict = true;
                Problem("配置已在另一处更新。为避免覆盖，请先重新加载；窗口中的修改尚未保存。");
                return false;
            }
            _revision = revision; _dirty = false;
            _saveFault = false; _readFault = false; Status.IsOpen = false; Reload.Visibility = Visibility.Collapsed;
            SaveStatus.Text = "已自动保存"; UpdateRuntimeFeedback();
            return true;
        } catch (Exception error) { _saveFault = true; Problem("保存失败：" + error.Message); return false; }
    }

    private void Poll()
    {
        if (_recording) RefreshRecording();
        UpdateRuntimeFeedback();
        if (_closing || _choosing) return;
        if (!ConfigStore.TryLoad(out AppConfig config, out string revision)) {
            _readFault = true;
            if (!_conflict && !_saveFault) Problem("暂时无法读取配置，保留当前内容；原文件不会被覆盖。"); return;
        }
        ClearReadProblem();
        if (_dirty || _conflict) return;
        if (revision == _revision) return;
        _config = config; _revision = revision;
        _page = Math.Min(_page, _config.Pages.Count - 1);
        Render();
        SaveStatus.Text = "已同步另一处保存的配置";
    }

    private void Problem(string text)
    {
        bool changed = !Status.IsOpen || Status.Message != text;
        Status.IsOpen = true; Status.Title = "需要处理"; Status.Message = text; Status.Severity = InfoBarSeverity.Warning;
        Reload.Visibility = Visibility.Visible;
        if (changed) FrameworkElementAutomationPeer.FromElement(Status)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
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
        _save.Stop(); _config = config; _revision = revision; _dirty = false; _conflict = false; _saveFault = false; _readFault = false;
        _page = Math.Min(_page, _config.Pages.Count - 1); Render();
        Status.IsOpen = false; SaveStatus.Text = "已重新加载"; Reload.Visibility = Visibility.Collapsed;
        return true;
    }

    private void Render()
    {
        CaptureContext();
        _rendering = true;
        _generation++;
        try {
            var title = Heading(((NavigationViewItem)Navigation.MenuItems[_section]).Content.ToString());
            AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level1); Navigation.Header = title;
            PageContent.Children.Clear();
            _page = Math.Max(0, Math.Min(_page, _config.Pages.Count - 1));
            switch (_section) {
                case 0: General(); break;
                case 1: Actions(); break;
                case 2: Appearance(); break;
                case 3: Hotkeys(); break;
                case 4: Advanced(); break;
                case 5: About(); break;
            }
            RestoreContext(); UpdateRuntimeFeedback();
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
        Add(startup); Add(RuntimeLine("startup-result")); Add(Text("启动项由主程序应用；关闭设置不会退出正在运行的主程序。"));
        Add(Heading("触发模式"));
        var mode = Choice("触发模式", new[] { "点击模式", "按住并松开执行" }, _config.Mode == "Hold" ? 1 : _config.Mode == "Click" ? 0 : -1);
        Identify(mode, "mode", "触发模式");
        mode.SelectionChanged += (_, _) => {
            string value = mode.SelectedIndex == 1 ? "Hold" : "Click";
            if (_rendering || generation != _generation || _config.Mode == value) return;
            _config.Mode = value; Changed();
        };
        Add(mode); Add(Text("点击模式：按快捷键打开，点击扇区执行。按住模式：按住快捷键，移向扇区后松开执行。Esc 或回到中心可取消；数字键 1–6 执行对应扇区；滚轮或左右方向键切换页面。"));
    }

    private void Actions()
    {
        int generation = _generation;
        var pages = Choice("页面", _config.Pages.Select(page => page.Name).ToArray(), _page);
        Identify(pages, "pages", "选择页面");
        pages.SelectionChanged += (_, _) => {
            if (_rendering || generation != _generation || pages.SelectedIndex < 0 || pages.SelectedIndex == _page) return;
            SavePending();
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
                Content = "页面“" + _config.Pages[_page].Name + "”及其六个动作都会删除。", PrimaryButtonText = "删除", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
            _choosing = true;
            try {
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
                _choosing = false; RemovePageConfirmed();
            } finally { _choosing = false; }
        });
        remove.IsEnabled = _config.Pages.Count > 1;
        AutomationProperties.SetHelpText(remove, remove.IsEnabled ? "删除当前页面及六个动作，需要确认。" : "至少保留一个页面，无法删除最后一页。");
        Identify(remove, "delete-page", "删除当前页面及六个动作"); buttons.Children.Add(remove); Add(buttons);
        WheelPage page = _config.Pages[_page];
        var name = Edit("页面名称", page.Name); Identify(name, "page-name", "页面名称");
        var nameHint = Text("");
        nameHint.Visibility = Visibility.Collapsed;
        name.TextChanging += (_, _) => {
            if (_rendering || generation != _generation || page.Name == name.Text) return;
            page.Name = name.Text;
            _rendering = true; pages.ItemsSource = _config.Pages.Select(p => p.Name).ToArray(); pages.SelectedIndex = _page; _rendering = false;
            nameHint.Text = string.IsNullOrWhiteSpace(name.Text) ? "页面名称不能为空。" :
                _config.Pages.Any(p => p != page && string.Equals(p.Name?.Trim(), name.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)) ? "页面名称重复。" : "";
            nameHint.Visibility = nameHint.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            Changed();
        }; Add(name); Add(nameHint);
        for (int slot = 0; slot < 6; slot++) {
            ActionItem action = page.Actions[slot]; int index = slot;
            var expander = new Expander { IsExpanded = slot == 0, HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch };
            void Summary() { expander.Header = Positions[index] + " · 扇区 " + (index + 1) + " · " + action.Name + "（" + ActionNames.Chinese(action.Type) + "）"; }
            Summary();
            var fields = new StackPanel { Spacing = 12 };
            var label = Edit("名称", action.Name); Identify(label, "slot-" + slot + "-name", Positions[slot] + "名称");
            label.TextChanging += (_, _) => { if (_rendering || generation != _generation || action.Name == label.Text) return; action.Name = label.Text; Summary(); Changed(); }; fields.Children.Add(label);
            string[] names = ActionNames.AllChinese().Cast<string>().ToArray();
            var type = Choice("动作类型", names, Array.IndexOf(names, ActionNames.Chinese(action.Type)));
            Identify(type, "slot-" + slot + "-type", Positions[slot] + "动作类型");
            var target = Edit("目标", action.Target);
            var hint = Text("");
            Identify(target, "slot-" + slot + "-target", Positions[slot] + "目标");
            target.IsEnabled = action.Type == "App" || action.Type == "Folder" || action.Type == "Command";
            Button browse = null;
            target.TextChanging += (_, _) => { if (_rendering || generation != _generation || action.Target == target.Text) return; action.Target = target.Text; TargetHint(); Changed(); };
            browse = Button("选择应用或文件夹", async () => { await Browse(action, target, label); TargetHint(); Summary(); });
            Identify(browse, "slot-" + slot + "-browse", Positions[slot] + "选择目标");
            browse.IsEnabled = action.Type == "App" || action.Type == "Folder";
            void TargetHint() {
                target.Header = action.Type == "Folder" ? "文件夹路径" : action.Type == "Command" ? "命令" : action.Type == "App" ? "程序路径或应用标识" : "此动作不需要目标";
                target.PlaceholderText = action.Type == "Command" ? "例如：notepad.exe" : action.Type == "Folder" ? @"例如：D:\Documents" : @"例如：C:\Apps\app.exe";
                hint.Text = action.Type == "Shutdown" ? "触发后立即关机；请避免把它放在容易误选的扇区。" :
                    action.Type == "Restart" ? "触发后立即重启；请避免把它放在容易误选的扇区。" :
                    NeedsTarget(action.Type) && string.IsNullOrWhiteSpace(target.Text) ? "目标必填；填写前不会覆盖最近有效配置。" :
                    action.Type is "App" or "Folder" && !(action.Target ?? "").StartsWith("shell:", StringComparison.OrdinalIgnoreCase) && !File.Exists(action.Target) && !Directory.Exists(action.Target) ? "路径当前不可用；允许保存离线目标，请确认路径。" : "";
                AutomationProperties.SetHelpText(target, hint.Text);
                hint.Visibility = hint.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
                AutomationProperties.SetHelpText(browse, browse.IsEnabled ? "打开目标选择器；取消保留当前动作。" : "只有应用和文件夹动作可使用选择器，命令请直接输入。");
            }
            TargetHint();
            type.SelectionChanged += async (_, _) => {
                if (_rendering || generation != _generation || type.SelectedIndex < 0 || _choosing) return;
                string selected = ActionNames.Id((string)type.SelectedItem);
                if (action.Type == selected) return;
                if (selected is "App" or "Folder") {
                    var candidate = new ActionItem { Name = action.Name, Type = selected, Target = "" };
                    if (!await Browse(candidate, null, null)) {
                        _rendering = true; type.SelectedIndex = Array.IndexOf(names, ActionNames.Chinese(action.Type)); _rendering = false; return;
                    }
                    action.Type = candidate.Type; action.Target = candidate.Target; action.Name = candidate.Name;
                    _rendering = true; target.Text = action.Target; label.Text = action.Name; _rendering = false;
                } else action.Type = selected;
                target.IsEnabled = NeedsTarget(action.Type);
                browse.IsEnabled = action.Type == "App" || action.Type == "Folder";
                // Preserve the previous target so switching back is recoverable.
                TargetHint(); Summary(); Changed();
            };
            fields.Children.Add(type); fields.Children.Add(target); fields.Children.Add(hint); fields.Children.Add(browse);
            expander.Content = fields;
            Identify(expander, "slot-" + slot, Positions[slot] + "扇区 " + (slot + 1)); Add(expander);
        }
    }

    private void AddPage()
    {
        if (!SavePending()) return;
        int number = _config.Pages.Count + 1;
        while (_config.Pages.Any(p => p.Name == "页面 " + number)) number++;
        _config.Pages.Add(new WheelPage { Name = "页面 " + number,
            Actions = Enumerable.Range(0, 6).Select(_ => new ActionItem { Name = "空", Type = "None", Target = "" }).ToList() });
        _page = _config.Pages.Count - 1; Changed(); SavePending(); Render();
    }

    private void RemovePageConfirmed()
    {
        if (_config.Pages.Count <= 1) return;
        _config.Pages.RemoveAt(_page); _page = Math.Min(_page, _config.Pages.Count - 1);
        Changed(); SavePending(); Render();
    }

    private async Task<bool> Browse(ActionItem action, TextBox target, TextBox label)
    {
        if (_choosing) return false;
        _choosing = true; _save.Stop();
        try {
            string path = null;
            if (action.Type == "Folder") {
                var picker = new FolderPicker(); picker.FileTypeFilter.Add("*");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                var folder = await picker.PickSingleFolderAsync(); path = folder?.Path;
            } else if (action.Type == "App") {
                var all = new List<ApplicationChoice>();
                var list = new ListView { Height = 280, SelectionMode = ListViewSelectionMode.Single,
                    DisplayMemberPath = "DisplayLabel", ItemsSource = all };
                Identify(list, "application-results", "应用搜索结果，包含目标路径");
                var search = new AutoSuggestBox { PlaceholderText = "搜索应用" };
                Identify(search, "application-search", "搜索应用");
                var loading = Text("正在加载应用…");
                var panel = new StackPanel { Spacing = 12 }; panel.Children.Add(search); panel.Children.Add(loading); panel.Children.Add(list);
                var dialog = new ContentDialog { XamlRoot = Content.XamlRoot, Title = "选择应用", Content = panel,
                    PrimaryButtonText = "选择", IsPrimaryButtonEnabled = false, SecondaryButtonText = "浏览文件", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
                _applicationDialog = dialog;
                void Filter() {
                    var matches = all.Where(app => app.Name.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)).ToList();
                    list.ItemsSource = matches; dialog.IsPrimaryButtonEnabled = false;
                    loading.Text = matches.Count == 0 ? "没有找到应用；可以浏览程序文件。" : "找到 " + matches.Count + " 个应用";
                }
                search.TextChanged += (_, _) => Filter();
                list.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = list.SelectedItem is ApplicationChoice;
                bool active = true;
                async Task LoadCatalog() {
                    try { var loaded = await ApplicationCatalog.LoadAsync(); if (active) { all = loaded; Filter(); } }
                    catch (Exception error) { if (active) loading.Text = "加载应用失败：" + error.Message + "。可浏览文件。"; }
                }
                _ = LoadCatalog();
                var result = await dialog.ShowAsync();
                _applicationDialog = null;
                active = false;
                if (result == ContentDialogResult.Primary) {
                    if (list.SelectedItem is ApplicationChoice app) { path = app.Target; action.Name = app.Name; }
                } else if (result == ContentDialogResult.Secondary) {
                    var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".exe"); picker.FileTypeFilter.Add(".lnk");
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                    var file = await picker.PickSingleFileAsync(); path = file?.Path;
                    if (file != null) action.Name = Path.GetFileNameWithoutExtension(file.Name);
                }
            }
            if (path != null) { action.Target = path; if (target != null) { target.Text = path; label.Text = action.Name; Changed(); } return true; }
        } catch (Exception error) { Problem("选择目标失败：" + error.Message); }
        finally { _choosing = false; if (_dirty) _save.Start(); }
        return false;
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
        Add(choice); Add(Text("液态玻璃：折射背景并呈现流动高光。高斯模糊：柔化背景，减少干扰。亚克力：半透明磨砂效果。三种效果只作用于圆环。"));
    }

    private void Hotkeys()
    {
        BuildHotkeyEditor();
    }

    private static bool Down(VirtualKey key) => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key) &
        Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
    private string HotkeyText() => ((_config.Modifiers & 2) != 0 ? "Ctrl + " : "") +
        ((_config.Modifiers & 1) != 0 ? "Alt + " : "") + ((_config.Modifiers & 4) != 0 ? "Shift + " : "") +
        ((_config.Modifiers & 8) != 0 ? "Win + " : "") + (_config.KeyCode == 32 ? "空格键" : ((VirtualKey)_config.KeyCode).ToString());

    private void Advanced()
    {
        int generation = _generation;
        Add(Heading("鼠标手势"));
        var gestures = new ToggleSwitch { Header = "启用左右键组合手势", IsOn = _config.MouseGestures };
        Identify(gestures, "gestures", "启用鼠标手势");
        gestures.Toggled += (_, _) => {
            if (_rendering || generation != _generation || _config.MouseGestures == gestures.IsOn) return;
            _config.MouseGestures = gestures.IsOn; Changed();
        }; Add(gestures); Add(RuntimeLine("gestures-result"));
        Add(Text("左右键同时按下并移动，松开后执行：上滑开始菜单，下滑桌面，左右滑切换窗口。"));
        Add(Text("组合手势期间屏蔽原点击；普通单击仅在 110 毫秒识别窗口内短暂延后。"));
    }

    private void Add(UIElement element) => PageContent.Children.Add(element);
    private static TextBlock Heading(string text) {
        var heading = new TextBlock { Text = text, Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"] };
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level2); return heading;
    }
    private static TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
    private static TextBox Edit(string header, string value) => new() { Header = header, Text = value ?? "", HorizontalAlignment = HorizontalAlignment.Stretch };
    private static ComboBox Choice(string header, string[] values, int selected) => new() {
        Header = header, ItemsSource = values, SelectedIndex = selected, PlaceholderText = "当前值不受支持，请选择", HorizontalAlignment = HorizontalAlignment.Stretch };
    private static Button Button(string text, Action action)
    {
        var button = new Button { Content = text }; button.Click += (_, _) => action(); return button;
    }
    private static void Identify(DependencyObject element, string id, string name)
    {
        AutomationProperties.SetAutomationId(element, id); AutomationProperties.SetName(element, name);
    }
}
