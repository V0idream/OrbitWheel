using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using OrbitWheelLite;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace OrbitWheel.Settings;

public sealed partial class MainWindow
{
    private bool _readFault, _saveFault, _recording, _closeDialog;
    private readonly DispatcherTimer _recordingRelease = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private TextBlock _recordStatus;
    private ContentDialog _applicationDialog, _recoveryDialog;
    private int _renderedSection = -1, _renderedPage;
    private readonly Dictionary<string, (double Scroll, HashSet<int> Expanded, string Focus)> _contexts = new();
    private readonly Dictionary<string, TextBlock> _runtimeLines = new();
    private string ContextKey(int section, int page) => section + ":" + (section == 1 ? page : 0);

    private void ClearReadProblem()
    {
        bool recovered = _readFault;
        _readFault = false;
        if (recovered && !_conflict && !_saveFault && (!_dirty || ValidateInputs(false))) { Status.IsOpen = false; Reload.Visibility = Visibility.Collapsed; }
    }
    private void RetryClick(object sender, RoutedEventArgs e)
    {
        if (_conflict) { Problem("磁盘已有其他修改。请先复制未保存内容，再重新加载；重试不会覆盖外部配置。"); return; }
        SavePending();
    }
    private void CopyPendingClick(object sender, RoutedEventArgs e)
    {
        try {
            var data = new DataPackage(); data.SetText(System.Text.Json.JsonSerializer.Serialize(_config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Clipboard.SetContent(data); SaveStatus.Text = "已复制窗口中的配置；可粘贴到文件保留";
        } catch (Exception error) { Problem("复制失败：" + error.Message); }
    }
    private async Task ResolveCloseAsync()
    {
        if (_choosing || _closeDialog) return;
        _closeDialog = true;
        try {
            var dialog = new ContentDialog { XamlRoot = Content.XamlRoot, Title = "修改尚未保存",
                Content = "取消后可重试保存、复制未保存内容或重新加载。放弃并关闭不会改动磁盘配置。",
                PrimaryButtonText = "放弃并关闭", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
            _recoveryDialog = dialog;
            if (await dialog.ShowAsync() == ContentDialogResult.Primary) { _closing = true; _dirty = false; Close(); }
        } finally { _closeDialog = false; _recoveryDialog = null; }
    }
    private bool ValidateInputs(bool show = true)
    {
        string error = null;
        if (_config.Mode != "Click" && _config.Mode != "Hold") error = "触发模式不受支持，请在常规页重新选择。";
        if (!new[] { "液态玻璃", "高斯模糊", "亚克力" }.Contains(_config.Style)) error = "效果样式不受支持，请在外观页重新选择。";
        foreach (var page in _config.Pages) {
            if (string.IsNullOrWhiteSpace(page.Name)) error = "页面名称不能为空。";
            if (_config.Pages.Count(p => string.Equals(p.Name?.Trim(), page.Name?.Trim(), StringComparison.CurrentCultureIgnoreCase)) > 1)
                error = "页面名称重复，请使用不同名称。";
            for (int i = 0; i < page.Actions.Count; i++) {
                var action = page.Actions[i];
                if (NeedsTarget(action.Type) && string.IsNullOrWhiteSpace(action.Target)) error = page.Name + " · 扇区 " + (i + 1) + "需要目标；最近有效配置尚未被覆盖。";
            }
        }
        if (error != null && show) Problem(error);
        return error == null;
    }
    private static bool NeedsTarget(string type) => type == "App" || type == "Folder" || type == "Command";
    private TextBlock RuntimeLine(string id)
    {
        var line = Text(""); Identify(line, id, "实际生效状态");
        AutomationProperties.SetLiveSetting(line, AutomationLiveSetting.Polite);
        _runtimeLines[id] = line; return line;
    }
    private void UpdateRuntimeFeedback()
    {
        var report = RuntimeState.Read("runtime");
        string common = report == null ? "主程序未运行：配置保存后等待主程序应用。" :
            report.Passive ? "隔离测试对端：未验证真实系统生效。" :
            report.Revision != _revision || _dirty ? "配置等待应用。" : null;
        foreach (var pair in _runtimeLines) {
            string text = common ?? (pair.Key switch {
            "hotkey-result" => report.Hotkey,
            "startup-result" => report.Startup,
            "gestures-result" => report.Gestures,
            _ => "主程序正在运行；关闭设置后继续在托盘运行。"
            });
            if (pair.Value.Text == text) continue;
            pair.Value.Text = text;
            if (text?.StartsWith("应用失败") == true) FrameworkElementAutomationPeer.FromElement(pair.Value)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
    private void About()
    {
        Add(Heading("OrbitWheel " + typeof(MainWindow).Assembly.GetName().Version?.ToString(2)));
        Add(RuntimeLine("host-result"));
        foreach (var entry in new[] { ("项目主页", ""), ("反馈问题", "/issues"), ("MIT 许可证", "/blob/OrbitWheel/LICENSE") })
            Add(new HyperlinkButton { Content = entry.Item1, NavigateUri = new Uri("https://github.com/V0idream/OrbitWheel" + entry.Item2) });
        var path = Edit("配置路径", ConfigStore.FilePath); path.IsReadOnly = true; Identify(path, "config-path", "配置路径，可复制"); Add(path);
        Add(Button("复制配置路径", () => { var data = new DataPackage(); data.SetText(ConfigStore.FilePath); Clipboard.SetContent(data); }));
        Add(Button("打开配置文件夹", async () => { try { Directory.CreateDirectory(ConfigStore.Folder); await Launcher.LaunchFolderPathAsync(ConfigStore.Folder); } catch (Exception error) { Problem(error.Message); } }));
        Add(Text("设置采用 WinUI 3；托盘、热键与圆环由主程序提供。"));
    }
    private void BuildHotkeyEditor()
    {
        Add(Heading("快捷键"));
        _hotkey = Edit("当前快捷键", HotkeyText()); _hotkey.IsReadOnly = true;
        Identify(_hotkey, "hotkey", "当前快捷键");
        _recordStatus = Text("选择更改快捷键后开始录制；Esc 取消，Tab 离开并取消。");
        AutomationProperties.SetLiveSetting(_recordStatus, AutomationLiveSetting.Polite);
        _hotkey.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RecordKey), true);
        _hotkey.LostFocus += (_, _) => CancelRecording();
        Add(_hotkey);
        var change = Button("更改快捷键", () => {
            if (!SavePending()) return;
            _recording = true;
            if (!RefreshRecording()) return;
            _recordStatus.Text = "正在录制：请按 Ctrl / Alt / Shift 与字母、数字或功能键。Esc 取消。";
            _hotkey.Focus(FocusState.Programmatic);
            FrameworkElementAutomationPeer.FromElement(_recordStatus)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }); Identify(change, "change-hotkey", "更改快捷键，开始录制"); Add(change);
        var cancel = Button("取消录制", CancelRecording); Identify(cancel, "cancel-recording", "取消录制并保留当前快捷键");
        Add(cancel); Add(_recordStatus); Add(RuntimeLine("hotkey-result"));
    }
    private bool RefreshRecording()
    {
        _recordingRelease.Stop();
        try { RuntimeState.Write("recording", RuntimeState.Identity()); return true; }
        catch (Exception error) { _recording = false; Problem("无法安全开始录制：" + error.Message); return false; }
    }
    private void CancelRecording()
    {
        if (!_recording) return;
        _recording = false;
        // Keep suppression briefly after the final key event so a WM_HOTKEY
        // already queued in the host cannot race the recording completion.
        _recordingRelease.Stop(); _recordingRelease.Tick -= ReleaseRecording;
        _recordingRelease.Tick += ReleaseRecording; _recordingRelease.Start();
        if (_recordStatus != null) {
            _recordStatus.Text = "录制已结束；当前快捷键：" + HotkeyText();
            FrameworkElementAutomationPeer.FromElement(_recordStatus)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }
    private void ReleaseRecording(object sender, object args)
    {
        _recordingRelease.Stop(); if (!_recording) RuntimeState.EndRecording();
    }
    private void RecordKey(object sender, KeyRoutedEventArgs args)
    {
        if (!_recording) return;
        if (args.Key == VirtualKey.Tab) { CancelRecording(); return; }
        if (args.Key == VirtualKey.Escape) { CancelRecording(); args.Handled = true; return; }
        if (args.Key is VirtualKey.Control or VirtualKey.Menu or VirtualKey.Shift or VirtualKey.LeftWindows or VirtualKey.RightWindows) return;
        args.Handled = true;
        int modifiers = (Down(VirtualKey.Menu) ? 1 : 0) | (Down(VirtualKey.Control) ? 2 : 0) | (Down(VirtualKey.Shift) ? 4 : 0);
        int key = (int)args.Key;
        bool keyAllowed = key == 32 || key >= 48 && key <= 57 || key >= 65 && key <= 90 || key >= 112 && key <= 135;
        if (modifiers == 0 || !keyAllowed || Down(VirtualKey.LeftWindows) || Down(VirtualKey.RightWindows) ||
            modifiers == 3 && key == 46 || modifiers == 1 && key == 115) {
            _recordStatus.Text = "该组合键不支持或为系统保留。请使用 Ctrl / Alt / Shift 与字母、数字或功能键。"; return;
        }
        _config.Modifiers = modifiers; _config.KeyCode = key; _hotkey.Text = HotkeyText(); Changed(); CancelRecording();
    }
    private void CaptureContext()
    {
        if (_renderedSection < 0) return;
        var expanded = new HashSet<int>();
        foreach (var expander in PageContent.Children.OfType<Expander>())
            if (expander.IsExpanded) expanded.Add(int.Parse(AutomationProperties.GetAutomationId(expander).Split('-')[1]));
        string focus = Content.XamlRoot != null && FocusManager.GetFocusedElement(Content.XamlRoot) is DependencyObject current ? AutomationProperties.GetAutomationId(current) : "";
        _contexts[ContextKey(_renderedSection, _renderedPage)] = (PageScroll.VerticalOffset, expanded, focus);
        _runtimeLines.Clear();
    }
    private void RegionNavigation(object sender, KeyRoutedEventArgs args)
    {
        if (_recording && args.Key == VirtualKey.Escape) { CancelRecording(); args.Handled = true; return; }
        if (_recording && args.Key == VirtualKey.Tab) CancelRecording();
        if (args.Key != VirtualKey.F6 || Down(VirtualKey.Control) || Down(VirtualKey.Menu) || Down(VirtualKey.Shift) || _choosing || _closeDialog) return;
        CancelRecording();
        var focused = FocusManager.GetFocusedElement(Content.XamlRoot) as DependencyObject;
        bool inContent = false;
        for (var parent = focused; parent != null; parent = VisualTreeHelper.GetParent(parent))
            if (parent == PageScroll) { inContent = true; break; }
        if (inContent) ((NavigationViewItem)Navigation.MenuItems[_section]).Focus(FocusState.Keyboard);
        else {
            Control FirstControl(DependencyObject node) {
                if (node is Control control && control.IsEnabled && control.IsTabStop) return control;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) {
                    var found = FirstControl(VisualTreeHelper.GetChild(node, i)); if (found != null) return found;
                }
                return null;
            }
            FirstControl(PageContent)?.Focus(FocusState.Keyboard);
        }
        args.Handled = true;
    }
    private void RestoreContext()
    {
        _renderedSection = _section; _renderedPage = _page;
        var key = ContextKey(_section, _page);
        if (_contexts.TryGetValue(key, out var context)) {
            foreach (var expander in PageContent.Children.OfType<Expander>())
                expander.IsExpanded = context.Expanded.Contains(int.Parse(AutomationProperties.GetAutomationId(expander).Split('-')[1]));
            int generation = _generation;
            DispatcherQueue.TryEnqueue(() => {
                if (generation != _generation) return;
                PageScroll.ChangeView(null, context.Scroll, null, true);
                if (!string.IsNullOrEmpty(context.Focus)) {
                    try { if (Find(context.Focus) is Control control) control.Focus(FocusState.Programmatic); } catch { }
                }
            });
        } else PageScroll.ChangeView(null, 0, null, true);
    }
}
