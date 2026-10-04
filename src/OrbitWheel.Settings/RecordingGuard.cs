using System.Runtime.InteropServices;

namespace OrbitWheel.Settings;

public sealed partial class MainWindow
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WindowSubclass(IntPtr window, uint message, UIntPtr wp, IntPtr lp, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(IntPtr window, WindowSubclass callback, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr window, WindowSubclass callback, UIntPtr id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr window, uint message, UIntPtr wp, IntPtr lp);
    private WindowSubclass _recordingGuard;
    private void InstallRecordingGuard()
    {
        _recordingGuard = (window, message, wp, lp, id, data) => {
            // RegisterHotKey consumes an Alt chord before XAML sees it. Its
            // Alt release must not activate the native menu and consume Esc.
            if ((_recording || _recordingRelease.IsEnabled) && message == 0x112 && (wp.ToUInt64() & 0xFFF0) == 0xF100)
                return IntPtr.Zero;
            if (_recording && (message == 0x100 || message == 0x104)) {
                if (wp.ToUInt64() == 27) { CancelRecording(); return IntPtr.Zero; }
                if (wp.ToUInt64() == 9) CancelRecording();
            }
            if (message == 0x82) RemoveWindowSubclass(window, _recordingGuard, id);
            return DefSubclassProc(window, message, wp, lp);
        };
        if (!SetWindowSubclass(WinRT.Interop.WindowNative.GetWindowHandle(this), _recordingGuard, new UIntPtr(21), UIntPtr.Zero))
            throw new InvalidOperationException("无法初始化快捷键录制保护。");
    }
}
