using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using OrbitWheelLite;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace OrbitWheel.Settings;

public sealed partial class MainWindow
{
    [StructLayout(LayoutKind.Sequential)] private struct KeyInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Explicit)] private struct InputData { [FieldOffset(0)] public KeyInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputData Data; }
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint from, uint to, bool attach);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rectangle);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public uint Size; public int Width, Height; public ushort Planes, Bits; public uint Compression, ImageSize; public int Xppm, Yppm; public uint Used, Important; }
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint mode);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] pixels, ref BitmapInfo info, uint usage);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);

    private async Task ForegroundForTest()
    {
        if (_previousForeground == IntPtr.Zero) _previousForeground = GetForegroundWindow();
        Activate(); var handle = WinRT.Interop.WindowNative.GetWindowHandle(this); ShowWindow(handle, 9); SetForegroundWindow(handle);
        if (GetForegroundWindow() != handle) {
            uint other = GetWindowThreadProcessId(GetForegroundWindow(), out _), current = GetCurrentThreadId();
            bool attached = other != 0 && other != current && AttachThreadInput(current, other, true);
            try { if (attached) SetForegroundWindow(handle); }
            finally { if (attached) AttachThreadInput(current, other, false); }
        }
        await Task.Delay(100);
        if (GetForegroundWindow() != handle) throw new Exception("Desktop test requires foreground settings window; another app owns focus. Expected=" + handle + "; actual=" + GetForegroundWindow());
    }
    private IntPtr _previousForeground;
    private async Task EndDesktopSmoke()
    {
        if (_previousForeground == IntPtr.Zero) return;
        if (GetForegroundWindow() == WinRT.Interop.WindowNative.GetWindowHandle(this)) {
            await SendKeys((17, true), (16, true), (18, true));
            SetForegroundWindow(_previousForeground);
        }
    }
    private async Task SendKeys(params (int Key, bool Up)[] keys)
    {
        if (GetForegroundWindow() != WinRT.Interop.WindowNative.GetWindowHandle(this))
            throw new Exception("Desktop keyboard test lost foreground; no input was sent to another app.");
        var inputs = keys.Select(key => new Input { Type = 1, Data = new InputData {
            Keyboard = new KeyInput { Key = (ushort)key.Key, Flags = key.Up ? 2u : 0u }
        } }).ToArray();
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length) throw new Exception("Native keyboard input was rejected.");
        await Task.Delay(120);
    }
    private void InvokeButton(string id) => ((IInvokeProvider)new ButtonAutomationPeer((Button)Find(id)).GetPattern(PatternInterface.Invoke)).Invoke();
    private async Task DesktopKeyboardSmoke(List<string> steps)
    {
        await ForegroundForTest(); _hotkey.Focus(FocusState.Programmatic);
        string original = HotkeyText();
        await SendKeys((17, false), (16, false), (133, false), (133, true), (16, true), (17, true));
        if (HotkeyText() != original || _dirty || _recording) throw new Exception("Read-only hotkey display rebound key on real input.");
        InvokeButton("change-hotkey"); await Task.Delay(100);
        if (!_recording || RuntimeState.Read("recording") == null) throw new Exception("Recording lease missing.");
        if (RuntimeState.Read("runtime") is { Passive: false }) {
            await SendKeys((17, false), (18, false), (16, false), (133, false), (133, true), (16, true), (18, true), (17, true));
            await Task.Delay(450);
            if (File.ReadAllText(Path.Combine(ConfigStore.Folder, "wheel-count.txt")) != "0") throw new Exception("Recording unexpectedly opened the live host wheel.");
            if (!_recording) { InvokeButton("change-hotkey"); await Task.Delay(100); }
            original = HotkeyText();
            steps.Add("live production host registered test-only hotkey; recording current global key did not open a wheel (startup untouched, actions inert)");
        }
        await SendKeys((17, false), (17, true));
        if (!_recording || HotkeyText() != original) throw new Exception("Modifier alone completed recording.");
        await SendKeys((27, false), (27, true));
        if (_recording || HotkeyText() != original) throw new Exception("Esc did not cancel recording. Recording=" + _recording + "; expected=" + original + "; actual=" + HotkeyText() + "; foreground=" + GetForegroundWindow() + "; focus=" + Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(Content.XamlRoot)?.GetType().Name);
        InvokeButton("change-hotkey"); await Task.Delay(100);
        await SendKeys((9, false), (9, true));
        if (_recording || HotkeyText() != original) throw new Exception("Tab did not leave/cancel recording.");
        InvokeButton("change-hotkey"); await Task.Delay(100);
        await SendKeys((17, false), (13, false), (13, true), (17, true));
        if (!_recording || HotkeyText() != original) throw new Exception("Invalid key committed.");
        await SendKeys((17, false), (16, false), (133, false), (133, true), (16, true), (17, true));
        if (_recording || _config.Modifiers != 6 || _config.KeyCode != 133) throw new Exception("Native valid candidate did not commit once.");
        if (!SavePending()) throw new Exception("Recorded shortcut failed to save.");
        await Wait(() => Peer().KeyCode == 133, "recorded shortcut sync");
        await SendKeys((117, false), (117, true)); // F6 content -> navigation.
        if (Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(Content.XamlRoot) is not Microsoft.UI.Xaml.Controls.NavigationViewItem)
            throw new Exception("F6 failed to reach navigation.");
        await SendKeys((117, false), (117, true));
        steps.Add("native SendInput routed idle/modifier/Esc/Tab/invalid/valid hotkey input through WinUI; F6 crosses regions (automated desktop, not Narrator)");
    }
    private async Task DesktopSnapshot(string name)
    {
        await ForegroundForTest();
        IntPtr handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (!GetWindowRect(handle, out var rect)) throw new Exception("Window bounds unavailable.");
        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        var pixels = new byte[width * height * 4];
        IntPtr screen = GetDC(IntPtr.Zero), dc = CreateCompatibleDC(screen), bitmap = CreateCompatibleBitmap(screen, width, height);
        IntPtr previous = SelectObject(dc, bitmap);
        try {
            if (!BitBlt(dc, 0, 0, width, height, screen, rect.Left, rect.Top, 0x00CC0020)) throw new Exception("Desktop capture failed.");
            SelectObject(dc, previous);
            var info = new BitmapInfo { Size = (uint)Marshal.SizeOf<BitmapInfo>(), Width = width, Height = -height, Planes = 1, Bits = 32 };
            if (GetDIBits(dc, bitmap, 0, (uint)height, pixels, ref info, 0) != height) throw new Exception("Desktop pixels unavailable.");
        } finally { SelectObject(dc, previous); DeleteObject(bitmap); DeleteDC(dc); ReleaseDC(IntPtr.Zero, screen); }
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)width, (uint)height, 96, 96, pixels);
        await encoder.FlushAsync(); stream.Seek(0);
        using var reader = new DataReader(stream.GetInputStreamAt(0)); await reader.LoadAsync((uint)stream.Size);
        byte[] png = new byte[(int)stream.Size]; reader.ReadBytes(png);
        await File.WriteAllBytesAsync(Path.Combine(ConfigStore.Folder, "desktop-" + name + ".png"), png);
    }
}
