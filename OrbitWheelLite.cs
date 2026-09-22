using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Automation;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("OrbitWheel")]
[assembly: AssemblyDescription("OrbitWheel 1.1.2 - 径向快捷操作中心")]
[assembly: AssemblyCompany("OrbitWheel")]
[assembly: AssemblyProduct("OrbitWheel")]
[assembly: AssemblyVersion("1.1.2.0")]
[assembly: AssemblyFileVersion("1.1.2.0")]

namespace OrbitWheelLite
{
    public class ActionItem
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Target { get; set; }
    }

    public class WheelPage
    {
        public string Name { get; set; }
        public List<ActionItem> Actions { get; set; }
    }

    public class AppConfig
    {
        public int Modifiers { get; set; }
        public int KeyCode { get; set; }
        public string Mode { get; set; }
        public string Style { get; set; }
        public bool StartWithWindows { get; set; }
        public bool MouseGestures { get; set; }
        public List<WheelPage> Pages { get; set; }

        public static AppConfig Default()
        {
            return new AppConfig {
                Modifiers = 2,
                KeyCode = (int)Keys.Space,
                Mode = "Hold",
                Style = "液态玻璃",
                StartWithWindows = false,
                MouseGestures = false,
                Pages = new List<WheelPage> {
                    new WheelPage {
                        Name = "常用",
                        Actions = new List<ActionItem> {
                            A("资源管理器", "Explorer", ""),
                            A("设置", "Settings", ""),
                            A("锁定", "Lock", ""),
                            A("音量 +", "VolumeUp", ""),
                            A("音量 -", "VolumeDown", ""),
                            A("睡眠", "Sleep", "")
                        }
                    }
                }
            };
        }

        private static ActionItem A(string name, string type, string target)
        {
            return new ActionItem { Name = name, Type = type, Target = target };
        }
    }

    static class ConfigStore
    {
        public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OrbitWheel");
        public static readonly string FilePath = Path.Combine(Folder, "config.json");

        public static AppConfig Load()
        {
            try {
                if (File.Exists(FilePath)) {
                    AppConfig c = new JavaScriptSerializer().Deserialize<AppConfig>(File.ReadAllText(FilePath));
                    Normalize(c);
                    return c;
                }
            } catch { }
            AppConfig d = AppConfig.Default();
            Save(d);
            return d;
        }

        public static void Save(AppConfig config)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(config));
        }

        private static void Normalize(AppConfig c)
        {
            if (c.Pages == null || c.Pages.Count == 0) c.Pages = AppConfig.Default().Pages;
            foreach (WheelPage p in c.Pages) {
                if (p.Actions == null) p.Actions = new List<ActionItem>();
                while (p.Actions.Count < 6) p.Actions.Add(new ActionItem { Name = "空", Type = "None", Target = "" });
                if (p.Actions.Count > 6) p.Actions.RemoveRange(6, p.Actions.Count - 6);
            }
            if (String.IsNullOrEmpty(c.Mode)) c.Mode = "Hold";
            if (String.IsNullOrEmpty(c.Style)) c.Style = "液态玻璃";
        }
    }

    static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const int WH_KEYBOARD_LL = 13;
        public const int WH_MOUSE_LL = 14;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYUP = 0x0105;
        public const int WM_TIMER = 0x0113;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int WM_GESTURE_STOP = 0x8001;
        public const uint LLMHF_INJECTED = 0x00000001;
        public const uint INPUT_MOUSE = 0;
        public const uint INPUT_KEYBOARD = 1;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const int MOD_ALT = 1;
        public const int MOD_CONTROL = 2;
        public const int MOD_SHIFT = 4;
        public const int MOD_WIN = 8;

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int idHook, KeyboardProc callback, IntPtr module, uint threadId);
        [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW")] public static extern IntPtr SetMouseHook(int idHook, MouseProc callback, IntPtr module, uint threadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wp, IntPtr lp);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] public static extern bool LockWorkStation();
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr hwnd, int command);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr hwnd, bool altTab);
        [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr data);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder text, int count);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out WindowRect rect);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out NativePoint point);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool ClipCursor(ref WindowRect rect);
        [DllImport("user32.dll")] public static extern bool ClipCursor(IntPtr rect);
        [DllImport("user32.dll")] public static extern uint GetDoubleClickTime();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern int GetApplicationUserModelId(IntPtr process, ref uint length, System.Text.StringBuilder appId);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SHGetFileInfo(string path, uint attributes, ref ShellFileInfo info, uint size, uint flags);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern int SHParseDisplayName(string name, IntPtr bindingContext, out IntPtr pidl, uint attributesIn, out uint attributesOut);
        [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW", CharSet = CharSet.Unicode)] public static extern IntPtr SHGetFileInfoPidl(IntPtr pidl, uint attributes, ref ShellFileInfo info, uint size, uint flags);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
        [DllImport("ole32.dll")] public static extern void CoTaskMemFree(IntPtr pointer);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern int GetMessage(out NativeMessage message, IntPtr window, uint min, uint max);
        [DllImport("user32.dll")] public static extern bool TranslateMessage(ref NativeMessage message);
        [DllImport("user32.dll")] public static extern IntPtr DispatchMessage(ref NativeMessage message);
        [DllImport("user32.dll")] public static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint min, uint max, uint remove);
        [DllImport("user32.dll")] public static extern UIntPtr SetTimer(IntPtr window, UIntPtr id, uint interval, IntPtr callback);
        [DllImport("user32.dll")] public static extern bool KillTimer(IntPtr window, UIntPtr id);
        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint count, Input[] inputs, int size);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ShellFileInfo { public IntPtr Icon; public int IconIndex; public uint Attributes; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName; }
        [StructLayout(LayoutKind.Sequential)]
        public struct WindowRect { public int Left; public int Top; public int Right; public int Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        public struct NativePoint { public int X; public int Y; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MouseHookData { public NativePoint Point; public uint MouseData; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct NativeMessage { public IntPtr Window; public uint Message; public UIntPtr WParam; public IntPtr LParam; public uint Time; public NativePoint Point; }
        [StructLayout(LayoutKind.Sequential)]
        public struct Input { public uint Type; public InputUnion Data; }
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion {
            [FieldOffset(0)] public MouseInput Mouse;
            [FieldOffset(0)] public KeyboardInput Keyboard;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct MouseInput { public int Dx; public int Dy; public uint MouseData; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct KeyboardInput { public ushort VirtualKey; public ushort Scan; public uint Flags; public uint Time; public UIntPtr ExtraInfo; }
        public delegate IntPtr KeyboardProc(int code, IntPtr wp, IntPtr lp);
        public delegate IntPtr MouseProc(int code, IntPtr wp, IntPtr lp);
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr data);
    }

    class HotkeyWindow : NativeWindow, IDisposable
    {
        public event EventHandler Triggered;
        public HotkeyWindow() { CreateHandle(new CreateParams()); }
        public bool Set(int modifiers, int key)
        {
            Native.UnregisterHotKey(Handle, 77);
            return Native.RegisterHotKey(Handle, 77, (uint)modifiers, (uint)key);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && Triggered != null) Triggered(this, EventArgs.Empty);
            base.WndProc(ref m);
        }
        public void Dispose() { Native.UnregisterHotKey(Handle, 77); DestroyHandle(); }
    }

    class KeyboardWatcher : IDisposable
    {
        private Native.KeyboardProc callback;
        private IntPtr hook;
        private int triggerKey;
        public event EventHandler TriggerReleased;
        public KeyboardWatcher(int key)
        {
            triggerKey = key;
            callback = Proc;
            hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, callback, Native.GetModuleHandle(null), 0);
        }
        private IntPtr Proc(int code, IntPtr wp, IntPtr lp)
        {
            if (code >= 0 && (wp.ToInt32() == Native.WM_KEYUP || wp.ToInt32() == Native.WM_SYSKEYUP)) {
                int vk = Marshal.ReadInt32(lp);
                if (vk == triggerKey && TriggerReleased != null) TriggerReleased(this, EventArgs.Empty);
            }
            return Native.CallNextHookEx(hook, code, wp, lp);
        }
        public void Dispose() { if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook); }
    }

    class MouseGestureService : IDisposable
    {
        private enum MouseButton { None, Left, Right }

        private const int ChordWindow = 110;
        private const int HorizontalActivation = 120;
        private const int HorizontalStep = 100;
        private readonly System.Threading.ManualResetEvent ready = new System.Threading.ManualResetEvent(false);
        private readonly System.Threading.Thread hookThread;
        private Native.MouseProc callback;
        private IntPtr hook;
        private UIntPtr timer;
        private uint hookThreadId;
        private int disposeRequested;
        private MouseButton pendingButton;
        private bool pendingForwarded;
        private bool pendingReleased;
        private int pendingSince;
        private Point pendingPoint;
        private bool leftDown;
        private bool rightDown;
        private bool gestureActive;
        private bool gestureCompleted;
        private bool gestureActionExecuted;
        private bool gestureStartLogged;
        private Point gestureOrigin;
        private Point gestureEndPoint;
        private static readonly object traceSync = new object();

        public bool IsRunning { get { return hook != IntPtr.Zero; } }

        public MouseGestureService()
        {
            hookThread = new System.Threading.Thread(HookThreadMain) { IsBackground = true, Name = "OrbitWheel.MouseGestures" };
            hookThread.SetApartmentState(System.Threading.ApartmentState.MTA);
            hookThread.Start();
            ready.WaitOne(2000);
        }

        private void HookThreadMain()
        {
            hookThreadId = Native.GetCurrentThreadId();
            Native.NativeMessage message;
            Native.PeekMessage(out message, IntPtr.Zero, 0, 0, 0);
            callback = HookProc;
            hook = Native.SetMouseHook(Native.WH_MOUSE_LL, callback, Native.GetModuleHandle(null), 0);
            if (hook != IntPtr.Zero) timer = Native.SetTimer(IntPtr.Zero, UIntPtr.Zero, 8, IntPtr.Zero);
            ready.Set();
            if (hook == IntPtr.Zero) return;

            try {
                while (Native.GetMessage(out message, IntPtr.Zero, 0, 0) > 0) {
                    if (message.Message == Native.WM_GESTURE_STOP) break;
                    if (message.Message == Native.WM_TIMER) Tick();
                    else {
                        Native.TranslateMessage(ref message);
                        Native.DispatchMessage(ref message);
                    }
                }
            } finally {
                FlushBeforeStop();
                if (timer != UIntPtr.Zero) Native.KillTimer(IntPtr.Zero, timer);
                IntPtr current = hook;
                hook = IntPtr.Zero;
                if (current != IntPtr.Zero) Native.UnhookWindowsHookEx(current);
            }
        }

        private IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0 || System.Threading.Volatile.Read(ref disposeRequested) != 0)
                return Native.CallNextHookEx(hook, code, wParam, lParam);

            int message = wParam.ToInt32();
            if (message != Native.WM_LBUTTONDOWN && message != Native.WM_LBUTTONUP &&
                message != Native.WM_RBUTTONDOWN && message != Native.WM_RBUTTONUP)
                return Native.CallNextHookEx(hook, code, wParam, lParam);

            Native.MouseHookData data = (Native.MouseHookData)Marshal.PtrToStructure(lParam, typeof(Native.MouseHookData));
            if ((data.Flags & Native.LLMHF_INJECTED) != 0)
                return Native.CallNextHookEx(hook, code, wParam, lParam);

            MouseButton button = (message == Native.WM_LBUTTONDOWN || message == Native.WM_LBUTTONUP) ? MouseButton.Left : MouseButton.Right;
            bool isDown = message == Native.WM_LBUTTONDOWN || message == Native.WM_RBUTTONDOWN;
            if (button == MouseButton.Left) leftDown = isDown;
            else rightDown = isDown;

            if (gestureActive) {
                if (!isDown) {
                    gestureEndPoint = new Point(data.Point.X, data.Point.Y);
                    if (!gestureCompleted) gestureCompleted = true;
                }
                return new IntPtr(1);
            }

            if (isDown) {
                if (pendingButton == MouseButton.None) {
                    pendingButton = button;
                    pendingForwarded = false;
                    pendingReleased = false;
                    pendingSince = Environment.TickCount;
                    pendingPoint = new Point(data.Point.X, data.Point.Y);
                    return new IntPtr(1);
                }
                if (!pendingForwarded && !pendingReleased && pendingButton != button && Elapsed(pendingSince) <= ChordWindow) {
                    gestureActive = true;
                    gestureCompleted = false;
                    gestureActionExecuted = false;
                    gestureStartLogged = false;
                    gestureOrigin = pendingPoint;
                    gestureEndPoint = pendingPoint;
                    pendingButton = MouseButton.None;
                    pendingReleased = false;
                    return new IntPtr(1);
                }
                return Native.CallNextHookEx(hook, code, wParam, lParam);
            }

            if (pendingButton == button && !pendingForwarded) {
                pendingReleased = true;
                return new IntPtr(1);
            }
            if (pendingButton == button && pendingForwarded) pendingButton = MouseButton.None;
            return Native.CallNextHookEx(hook, code, wParam, lParam);
        }

        private void Tick()
        {
            if (pendingButton != MouseButton.None && !pendingForwarded) {
                if (pendingReleased) {
                    SendMouse(pendingButton, true);
                    SendMouse(pendingButton, false);
                    ClearPending();
                } else if (Elapsed(pendingSince) >= ChordWindow) {
                    SendMouse(pendingButton, true);
                    pendingForwarded = true;
                }
            }

            if (!gestureActive) return;
            if (!gestureStartLogged) {
                Trace("start origin=" + gestureOrigin.X + "," + gestureOrigin.Y);
                gestureStartLogged = true;
            }
            if (gestureCompleted) {
                // Execute only after both physical buttons are up. Windows can ignore Win+D
                // while the second mouse button is still held during a chord release.
                if (leftDown || rightDown) return;
                if (!gestureActionExecuted) {
                    CompleteGesture();
                    gestureActionExecuted = true;
                }
                ResetGesture();
                return;
            }
        }

        private void CompleteGesture()
        {
            // Both points come from MSLLHOOKSTRUCT, so they remain in the same physical-pixel
            // coordinate space even when Windows display scaling is above 100%.
            int dx = gestureEndPoint.X - gestureOrigin.X;
            int dy = gestureEndPoint.Y - gestureOrigin.Y;
            int ax = Math.Abs(dx);
            int ay = Math.Abs(dy);

            // Decide once, after both buttons are released. Vertical deliberately wins over
            // diagonals; horizontal switching requires a long and clearly horizontal stroke.
            int verticalThreshold = dy > 0 ? 38 : 48;
            if (ay >= verticalThreshold && ay >= ax * 0.50f) {
                Trace("complete dx=" + dx + " dy=" + dy + " action=" + (dy < 0 ? "start" : "desktop"));
                QueueVerticalShortcut(dy < 0);
                return;
            }
            if (ax >= HorizontalActivation && ax >= ay * 2.0f) {
                int steps = Math.Min(6, 1 + Math.Max(0, ax - HorizontalActivation) / HorizontalStep);
                Trace("complete dx=" + dx + " dy=" + dy + " action=" + (dx < 0 ? "switch-left" : "switch-right") + " steps=" + steps);
                QueueHorizontalShortcut(dx < 0, steps);
                return;
            }
            Trace("complete dx=" + dx + " dy=" + dy + " action=none");
        }

        private void QueueVerticalShortcut(bool up)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate {
                // Let Windows finish processing both swallowed mouse-button releases first.
                System.Threading.Thread.Sleep(80);
                if (System.Threading.Volatile.Read(ref disposeRequested) != 0) return;
                ReleaseSyntheticModifiers();
                bool sent = SendShortcut(0x5B, up ? 0 : 0x44);
                Trace("execute action=" + (up ? "start" : "desktop") + " sendInput=" + sent);
            });
        }

        private void QueueHorizontalShortcut(bool reverse, int steps)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate {
                System.Threading.Thread.Sleep(80);
                if (System.Threading.Volatile.Read(ref disposeRequested) != 0) return;
                ReleaseSyntheticModifiers();
                SendWindowSwitch(reverse, steps);
                Trace("execute action=" + (reverse ? "switch-left" : "switch-right") + " steps=" + steps);
            });
        }

        private void ResetGesture()
        {
            gestureActive = false;
            gestureCompleted = false;
            gestureActionExecuted = false;
            gestureStartLogged = false;
        }

        private void FlushBeforeStop()
        {
            if (pendingButton != MouseButton.None && !pendingForwarded) {
                SendMouse(pendingButton, true);
                if (pendingReleased) SendMouse(pendingButton, false);
            }
            ClearPending();
            ReleaseSyntheticModifiers();
        }

        private void ClearPending()
        {
            pendingButton = MouseButton.None;
            pendingForwarded = false;
            pendingReleased = false;
        }

        private static void SendWindowSwitch(bool reverse, int steps)
        {
            SendKey(0x12, true);
            for (int i = 0; i < steps; i++) {
                if (reverse) SendKey(0x10, true);
                SendKey(0x09, true);
                SendKey(0x09, false);
                if (reverse) SendKey(0x10, false);
            }
            SendKey(0x12, false);
        }

        private static void ReleaseSyntheticModifiers()
        {
            SendKey(0x12, false);
            SendKey(0x10, false);
            SendKey(0x5B, false);
            SendKey(0x5C, false);
        }

        private static bool SendShortcut(int modifier, int key)
        {
            int count = key == 0 ? 2 : 4;
            Native.Input[] inputs = new Native.Input[count];
            inputs[0] = KeyboardInput(modifier, false);
            if (key == 0) {
                inputs[1] = KeyboardInput(modifier, true);
            } else {
                inputs[1] = KeyboardInput(key, false);
                inputs[2] = KeyboardInput(key, true);
                inputs[3] = KeyboardInput(modifier, true);
            }
            uint sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Native.Input)));
            if (sent == (uint)inputs.Length) return true;

            // Fallback for systems that reject a batched SendInput request.
            Native.keybd_event((byte)modifier, 0, 0, UIntPtr.Zero);
            if (key != 0) Native.keybd_event((byte)key, 0, 0, UIntPtr.Zero);
            if (key != 0) Native.keybd_event((byte)key, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
            Native.keybd_event((byte)modifier, 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
            return false;
        }

        private static void Trace(string message)
        {
            try {
                lock (traceSync) {
                    Directory.CreateDirectory(ConfigStore.Folder);
                    File.AppendAllText(Path.Combine(ConfigStore.Folder, "gesture.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine);
                }
            } catch { }
        }

        private static Native.Input KeyboardInput(int key, bool keyUp)
        {
            Native.Input input = new Native.Input { Type = Native.INPUT_KEYBOARD };
            input.Data.Keyboard.VirtualKey = (ushort)key;
            input.Data.Keyboard.Flags = keyUp ? Native.KEYEVENTF_KEYUP : 0u;
            return input;
        }

        private static void SendMouse(MouseButton button, bool down)
        {
            uint flags = button == MouseButton.Left
                ? (down ? Native.MOUSEEVENTF_LEFTDOWN : Native.MOUSEEVENTF_LEFTUP)
                : (down ? Native.MOUSEEVENTF_RIGHTDOWN : Native.MOUSEEVENTF_RIGHTUP);
            Native.Input input = new Native.Input { Type = Native.INPUT_MOUSE };
            input.Data.Mouse.Flags = flags;
            Native.SendInput(1, new Native.Input[] { input }, Marshal.SizeOf(typeof(Native.Input)));
        }

        private static void SendKey(int key, bool down)
        {
            Native.Input input = new Native.Input { Type = Native.INPUT_KEYBOARD };
            input.Data.Keyboard.VirtualKey = (ushort)key;
            input.Data.Keyboard.Flags = down ? 0u : Native.KEYEVENTF_KEYUP;
            Native.SendInput(1, new Native.Input[] { input }, Marshal.SizeOf(typeof(Native.Input)));
        }

        private static int Elapsed(int start)
        {
            return unchecked(Environment.TickCount - start);
        }

        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref disposeRequested, 1) != 0) return;
            uint threadId = hookThreadId;
            if (threadId != 0) Native.PostThreadMessage(threadId, Native.WM_GESTURE_STOP, IntPtr.Zero, IntPtr.Zero);
            if (!hookThread.Join(1200)) {
                IntPtr current = hook;
                hook = IntPtr.Zero;
                if (current != IntPtr.Zero) Native.UnhookWindowsHookEx(current);
            }
            ready.Dispose();
        }
    }

    static class IconFactory
    {
        public static Icon AppIcon()
        {
            Bitmap b = new Bitmap(64, 64);
            using (Graphics g = Graphics.FromImage(b)) {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(0,0,64,64), Color.FromArgb(64,215,255), Color.FromArgb(123,76,255), 45))
                    g.FillEllipse(bg, 3, 3, 58, 58);
                using (Pen p = new Pen(Color.FromArgb(225,255,255,255), 5)) {
                    g.DrawArc(p, 15, 15, 34, 34, 20, 275);
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLine(p, 34, 32, 46, 20);
                }
                g.FillEllipse(Brushes.White, 29, 27, 9, 9);
            }
            return Icon.FromHandle(b.GetHicon());
        }
    }

    class WheelForm : Form
    {
        private AppConfig config;
        private int pageIndex;
        private int selected = -1;
        private Point center;
        private bool closing;
        private Bitmap backdrop;
        private Timer animationTimer;
        private float animationPhase;
        private Point liquidFocus;
        private const int Outer = 235;
        private const int Inner = 67;
        public event Action<ActionItem> ExecuteRequested;
        public event EventHandler CloseRequested;

        public WheelForm(AppConfig c)
        {
            config = c;
            Text = "OrbitWheel Menu";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            DoubleBuffered = true;
            Cursor = Cursors.Cross;
            Size = new Size(510, 510);
            Point mouse = ClampCenterToVisibleScreen(Cursor.Position);
            if (mouse != Cursor.Position) Cursor.Position = mouse;
            Location = new Point(mouse.X - Width / 2, mouse.Y - Height / 2);
            center = new Point(Width / 2, Height / 2);
            liquidFocus = center;
            BackColor = Color.Black;
            SetCircularRegion();
            backdrop = CaptureAndBlur(Location, Size, config.Style);
            animationTimer = new Timer { Interval = 40 };
            animationTimer.Tick += delegate { animationPhase += 1.35f; if (animationPhase >= 360) animationPhase -= 360; Invalidate(); };
            animationTimer.Start();
            MouseMove += OnMove;
            MouseDown += OnDown;
            MouseWheel += OnWheel;
            KeyDown += OnKey;
            Deactivate += delegate { if (config.Mode == "Click" && Visible) RequestClose(); };
        }

        private void SetCircularRegion()
        {
            using (GraphicsPath path = new GraphicsPath()) {
                path.AddEllipse(center.X - Outer - 1, center.Y - Outer - 1, (Outer + 1) * 2, (Outer + 1) * 2);
                Region = new Region(path);
            }
        }

        private Point ClampCenterToVisibleScreen(Point requested)
        {
            Rectangle bounds = Screen.FromPoint(requested).Bounds;
            int halfWidth = Width / 2;
            int halfHeight = Height / 2;
            int x = Math.Max(bounds.Left + halfWidth, Math.Min(requested.X, bounds.Right - halfWidth));
            int y = Math.Max(bounds.Top + halfHeight, Math.Min(requested.Y, bounds.Bottom - halfHeight));
            return new Point(x, y);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            Focus();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (animationTimer != null) { animationTimer.Stop(); animationTimer.Dispose(); }
            if (backdrop != null) backdrop.Dispose();
            base.OnFormClosed(e);
        }

        private Bitmap CaptureAndBlur(Point location, Size size, string style)
        {
            Bitmap source = new Bitmap(size.Width, size.Height);
            using (Graphics g = Graphics.FromImage(source)) {
                g.Clear(Color.FromArgb(18, 23, 34));
                Rectangle requested = new Rectangle(location, size);
                Rectangle visible = Rectangle.Intersect(requested, SystemInformation.VirtualScreen);
                if (visible.Width > 0 && visible.Height > 0)
                    g.CopyFromScreen(visible.Location, new Point(visible.X - location.X, visible.Y - location.Y), visible.Size);
            }
            int divisor = style == "高斯模糊" ? 18 : style == "亚克力" ? 7 : 8;
            Bitmap small = new Bitmap(Math.Max(1, size.Width / divisor), Math.Max(1, size.Height / divisor), PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(small)) {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(Point.Empty, small.Size));
            }
            Bitmap result = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result)) {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(small, new Rectangle(Point.Empty, result.Size));
                if (style == "液态玻璃") {
                    using (Bitmap refracted = CreateLiquidRefraction(source))
                        g.DrawImageUnscaled(refracted, Point.Empty);
                    using (LinearGradientBrush tint = new LinearGradientBrush(new Rectangle(Point.Empty, result.Size), Color.FromArgb(42, 36, 88, 154), Color.FromArgb(88, 7, 16, 38), 118f))
                        g.FillRectangle(tint, 0, 0, result.Width, result.Height);
                    using (GraphicsPath lightPath = new GraphicsPath()) {
                        lightPath.AddEllipse(32, 18, 390, 300);
                        using (PathGradientBrush light = new PathGradientBrush(lightPath)) {
                            light.CenterPoint = new PointF(148, 98);
                            light.CenterColor = Color.FromArgb(32, 210, 240, 255);
                            light.SurroundColors = new Color[] { Color.FromArgb(0, 75, 140, 220) };
                            g.FillPath(light, lightPath);
                        }
                    }
                } else {
                    int materialAlpha = style == "亚克力" ? 188 : 58;
                    Color tint = style == "亚克力" ? Color.FromArgb(materialAlpha, 23, 29, 43) : Color.FromArgb(materialAlpha, 20, 25, 38);
                    using (SolidBrush b = new SolidBrush(tint)) g.FillRectangle(b, 0, 0, result.Width, result.Height);
                }
                if (style == "亚克力") {
                    Random random = new Random(8);
                    using (SolidBrush grain = new SolidBrush(Color.FromArgb(11, 255, 255, 255)))
                        for (int i = 0; i < 1500; i++) g.FillRectangle(grain, random.Next(result.Width), random.Next(result.Height), 1, 1);
                }
            }
            small.Dispose();
            source.Dispose();
            return result;
        }

        private Bitmap CreateLiquidRefraction(Bitmap source)
        {
            Bitmap normalized = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(normalized)) g.DrawImageUnscaled(source, Point.Empty);
            Bitmap result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            Rectangle bounds = new Rectangle(Point.Empty, source.Size);
            BitmapData sourceData = normalized.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData resultData = result.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int sourceBytes = Math.Abs(sourceData.Stride) * source.Height;
            int resultBytes = Math.Abs(resultData.Stride) * result.Height;
            byte[] input = new byte[sourceBytes];
            byte[] output = new byte[resultBytes];
            Marshal.Copy(sourceData.Scan0, input, 0, input.Length);

            double inner = Inner + 12;
            double outer = Outer - 5;
            for (int y = 0; y < source.Height; y++) {
                for (int x = 0; x < source.Width; x++) {
                    double dx = x - center.X;
                    double dy = y - center.Y;
                    double radius = Math.Sqrt(dx * dx + dy * dy);
                    if (radius < inner || radius > outer) continue;
                    double outerRim = Math.Max(0, 1.0 - (outer - radius) / 25.0);
                    double innerRim = Math.Max(0, 1.0 - (radius - inner) / 20.0);
                    double angle = Math.Atan2(dy, dx);
                    double wave = Math.Sin(angle * 6.0 + radius * 0.035) * (0.8 + outerRim * 1.8);
                    double displacement = outerRim * 12.5 - innerRim * 7.5 + wave;
                    double sampleRadius = radius - displacement;
                    double ux = dx / radius;
                    double uy = dy / radius;
                    double chroma = (outerRim + innerRim) * 1.6;
                    int blueIndex = PixelIndex(center.X + ux * (sampleRadius - chroma), center.Y + uy * (sampleRadius - chroma), source.Width, source.Height, sourceData.Stride);
                    int greenIndex = PixelIndex(center.X + ux * sampleRadius, center.Y + uy * sampleRadius, source.Width, source.Height, sourceData.Stride);
                    int redIndex = PixelIndex(center.X + ux * (sampleRadius + chroma), center.Y + uy * (sampleRadius + chroma), source.Width, source.Height, sourceData.Stride);
                    int destination = y * resultData.Stride + x * 4;
                    output[destination] = input[blueIndex];
                    output[destination + 1] = input[greenIndex + 1];
                    output[destination + 2] = input[redIndex + 2];
                    output[destination + 3] = (byte)Math.Min(220, 60 + (outerRim + innerRim) * 120);
                }
            }

            Marshal.Copy(output, 0, resultData.Scan0, output.Length);
            normalized.UnlockBits(sourceData);
            result.UnlockBits(resultData);
            normalized.Dispose();
            return result;
        }

        private static int PixelIndex(double x, double y, int width, int height, int stride)
        {
            int px = Math.Max(0, Math.Min(width - 1, (int)Math.Round(x)));
            int py = Math.Max(0, Math.Min(height - 1, (int)Math.Round(y)));
            return py * stride + px * 4;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Color.FromArgb(18, 23, 34));
            if (backdrop != null) {
                GraphicsState state = e.Graphics.Save();
                using (GraphicsPath clip = new GraphicsPath()) {
                    clip.AddEllipse(center.X - Outer + 2, center.Y - Outer + 2, (Outer - 2) * 2, (Outer - 2) * 2);
                    e.Graphics.SetClip(clip);
                    e.Graphics.DrawImageUnscaled(backdrop, Point.Empty);
                }
                e.Graphics.Restore(state);
            }
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) {
                RequestClose();
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) {
                ChangePage(e.KeyCode == Keys.Right ? 1 : -1);
                e.SuppressKeyPress = true;
                return;
            }
            int number = e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D6 ? (int)e.KeyCode - (int)Keys.D0 :
                         e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad6 ? (int)e.KeyCode - (int)Keys.NumPad0 : 0;
            if (number > 0) {
                selected = number == 1 ? 5 : number - 2;
                Invalidate();
                RequestExecute();
                e.SuppressKeyPress = true;
            }
        }

        private void OnWheel(object sender, MouseEventArgs e)
        {
            ChangePage(e.Delta < 0 ? 1 : -1);
        }

        private void ChangePage(int direction)
        {
            if (config.Pages.Count < 2) return;
            pageIndex = (pageIndex + direction + config.Pages.Count) % config.Pages.Count;
            selected = -1;
            Invalidate();
        }

        private void OnMove(object sender, MouseEventArgs e)
        {
            liquidFocus = e.Location;
            double dx = e.X - center.X, dy = e.Y - center.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            int old = selected;
            if (dist < Inner || dist > Outer + 35) selected = -1;
            else {
                double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
                if (angle < 0) angle += 360;
                selected = ((int)Math.Floor((angle + 30) / 60)) % 6;
            }
            if (old != selected) Invalidate();
        }

        private void OnDown(object sender, MouseEventArgs e)
        {
            double dx = e.X - center.X, dy = e.Y - center.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist <= Inner) { RequestClose(); return; }
            if (selected >= 0 && config.Mode == "Click") RequestExecute();
        }

        public void ExecuteHoldSelection()
        {
            if (selected >= 0) RequestExecute(); else RequestClose();
        }

        private void RequestExecute()
        {
            ActionItem a = config.Pages[pageIndex].Actions[selected];
            closing = true;
            Hide();
            if (ExecuteRequested != null) ExecuteRequested(a);
            Close();
        }
        private void RequestClose()
        {
            if (closing) return;
            closing = true;
            if (CloseRequested != null) CloseRequested(this, EventArgs.Empty);
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            bool acrylic = config.Style == "亚克力";
            bool blur = config.Style == "高斯模糊";
            bool liquid = config.Style == "液态玻璃";
            int fillAlpha = acrylic ? 70 : blur ? 18 : 38;
            Color baseColor = acrylic ? Color.FromArgb(fillAlpha, 35, 42, 58) : blur ? Color.FromArgb(fillAlpha, 28, 33, 48) : Color.FromArgb(fillAlpha, 22, 36, 66);
            Color accent = acrylic ? Color.FromArgb(150, 92, 130, 185) : blur ? Color.FromArgb(90, 160, 200, 245) : Color.FromArgb(125, 87, 190, 255);

            if (liquid) DrawLiquidFoundation(g);
            for (int i = 0; i < 6; i++) {
                using (GraphicsPath path = SegmentPath(center, Inner + 14, Outer - 3, i * 60 - 28, 56)) {
                    if (liquid) DrawLiquidSegment(g, path, i == selected, i);
                    else {
                        using (SolidBrush fill = new SolidBrush(i == selected ? accent : baseColor)) g.FillPath(fill, path);
                        using (Pen border = new Pen(Color.FromArgb(i == selected ? 220 : 46, 220, 240, 255), i == selected ? 2f : 1f)) g.DrawPath(border, path);
                    }
                }
            }

            DrawRealtimeGlass(g);

            for (int i = 0; i < 6; i++) {
                DrawAction(g, i);
                DrawSectorNumber(g, i);
            }

            if (liquid) DrawLiquidRims(g);
            else using (Pen cleanEdge = new Pen(Color.FromArgb(245, 112, 164, 224), 4f))
                    g.DrawEllipse(cleanEdge, center.X - Outer + 4, center.Y - Outer + 4, (Outer - 4) * 2, (Outer - 4) * 2);

            Rectangle closeRect = new Rectangle(center.X - Inner, center.Y - Inner, Inner * 2, Inner * 2);
            if (liquid) {
                using (Pen shadow = new Pen(Color.FromArgb(82, 1, 7, 18), 6f)) g.DrawEllipse(shadow, closeRect);
                using (LinearGradientBrush cb = new LinearGradientBrush(closeRect, Color.FromArgb(235, 35, 60, 102), Color.FromArgb(238, 8, 17, 39), 125)) g.FillEllipse(cb, closeRect);
                using (Pen glow = new Pen(Color.FromArgb(70, 88, 198, 255), 5f)) g.DrawArc(glow, closeRect, 205, 145);
            } else {
                using (LinearGradientBrush cb = new LinearGradientBrush(closeRect, Color.FromArgb(220,38,45,65), Color.FromArgb(210,24,30,46), 45)) g.FillEllipse(cb, closeRect);
            }
            using (Pen ring = new Pen(Color.FromArgb(liquid ? 185 : 130, 210, 235, 255), liquid ? 1.8f : 1.5f)) g.DrawEllipse(ring, closeRect);
            using (Pen x = new Pen(Color.FromArgb(235, 245, 250, 255), 3)) {
                x.StartCap = x.EndCap = LineCap.Round;
                g.DrawLine(x, center.X - 12, center.Y - 12, center.X + 12, center.Y + 12);
                g.DrawLine(x, center.X + 12, center.Y - 12, center.X - 12, center.Y + 12);
            }

            string page = (pageIndex + 1) + "/" + config.Pages.Count;
            using (Font f = new Font("Microsoft YaHei UI", 8f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(205, 225, 235, 250)))
                g.DrawString(page, f, b, center.X - 12, center.Y + 39);
        }

        private void DrawLiquidFoundation(Graphics g)
        {
            Rectangle outer = new Rectangle(center.X - Outer + 7, center.Y - Outer + 7, (Outer - 7) * 2, (Outer - 7) * 2);
            Rectangle inner = new Rectangle(center.X - Inner - 13, center.Y - Inner - 13, (Inner + 13) * 2, (Inner + 13) * 2);
            using (Pen depth = new Pen(Color.FromArgb(62, 0, 6, 18), 10f)) g.DrawEllipse(depth, outer);
            using (Pen innerDepth = new Pen(Color.FromArgb(78, 0, 5, 16), 8f)) g.DrawEllipse(innerDepth, inner);
            using (LinearGradientBrush wash = new LinearGradientBrush(outer, Color.FromArgb(30, 90, 190, 255), Color.FromArgb(12, 4, 16, 44), 120f))
            using (Pen depthLight = new Pen(wash, 8f)) g.DrawEllipse(depthLight, outer);
        }

        private void DrawLiquidSegment(Graphics g, GraphicsPath path, bool isSelected, int index)
        {
            RectangleF bounds = path.GetBounds();
            Color top = isSelected ? Color.FromArgb(126, 65, 185, 255) : Color.FromArgb(38, 155, 218, 255);
            Color bottom = isSelected ? Color.FromArgb(92, 10, 70, 145) : Color.FromArgb(48, 4, 18, 52);
            using (LinearGradientBrush fill = new LinearGradientBrush(bounds, top, bottom, 110f + index * 7f)) {
                ColorBlend blend = new ColorBlend(4) {
                    Colors = new Color[] { top, Color.FromArgb(isSelected ? 104 : 44, 100, 198, 255), Color.FromArgb(isSelected ? 76 : 32, 20, 65, 120), bottom },
                    Positions = new float[] { 0f, 0.24f, 0.68f, 1f }
                };
                fill.InterpolationColors = blend;
                g.FillPath(fill, path);
            }
            if (isSelected) {
                using (Pen halo = new Pen(Color.FromArgb(76, 62, 182, 255), 7f)) g.DrawPath(halo, path);
            }
            using (Pen darkEdge = new Pen(Color.FromArgb(66, 0, 10, 28), 1.8f)) g.DrawPath(darkEdge, path);
            using (Pen glassEdge = new Pen(Color.FromArgb(isSelected ? 235 : 92, 214, 242, 255), isSelected ? 2.2f : 1.15f)) g.DrawPath(glassEdge, path);
        }

        private void DrawRealtimeGlass(Graphics g)
        {
            Rectangle ring = new Rectangle(center.X - Outer + 5, center.Y - Outer + 5, (Outer - 5) * 2, (Outer - 5) * 2);
            if (config.Style != "液态玻璃") {
                using (Pen glow = new Pen(Color.FromArgb(55, 185, 225, 255), 3f)) {
                    glow.StartCap = glow.EndCap = LineCap.Round;
                    g.DrawArc(glow, ring, animationPhase, 38);
                    g.DrawArc(glow, ring, animationPhase + 180, 24);
                }
            } else {
                GraphicsState state = g.Save();
                using (GraphicsPath clip = new GraphicsPath(FillMode.Alternate)) {
                    clip.AddEllipse(center.X - Outer + 7, center.Y - Outer + 7, (Outer - 7) * 2, (Outer - 7) * 2);
                    clip.AddEllipse(center.X - Inner - 13, center.Y - Inner - 13, (Inner + 13) * 2, (Inner + 13) * 2);
                    g.SetClip(clip);

                    Rectangle pointerLens = new Rectangle(liquidFocus.X - 145, liquidFocus.Y - 145, 290, 290);
                    using (GraphicsPath lensPath = new GraphicsPath()) {
                        lensPath.AddEllipse(pointerLens);
                        using (PathGradientBrush lens = new PathGradientBrush(lensPath)) {
                            lens.CenterColor = Color.FromArgb(58, 224, 247, 255);
                            lens.SurroundColors = new Color[] { Color.FromArgb(0, 28, 112, 220) };
                            g.FillPath(lens, lensPath);
                        }
                    }

                    double a = animationPhase * Math.PI / 180.0;
                    DrawRotatedCaustic(g, center.X + (int)(Math.Cos(a) * 126), center.Y + (int)(Math.Sin(a) * 126), 168, 46, animationPhase + 28, 62);
                    DrawRotatedCaustic(g, center.X + (int)(Math.Cos(a * -0.72 + 2.25) * 172), center.Y + (int)(Math.Sin(a * -0.72 + 2.25) * 172), 112, 30, -animationPhase + 82, 40);
                    DrawRotatedCaustic(g, center.X + (int)(Math.Cos(a * 0.45 + 4.4) * 104), center.Y + (int)(Math.Sin(a * 0.45 + 4.4) * 104), 92, 22, animationPhase * 0.4f, 28);
                    using (Pen refraction = new Pen(Color.FromArgb(42, 184, 231, 255), 11f)) {
                        refraction.StartCap = refraction.EndCap = LineCap.Round;
                        g.DrawArc(refraction, ring, animationPhase + 78, 92);
                    }
                }
                g.Restore(state);

                using (Pen movingGlow = new Pen(Color.FromArgb(82, 207, 241, 255), 9f)) {
                    movingGlow.StartCap = movingGlow.EndCap = LineCap.Round;
                    g.DrawArc(movingGlow, ring, animationPhase + 196, 48);
                }
            }
        }

        private static void DrawRotatedCaustic(Graphics g, int x, int y, int width, int height, float angle, int alpha)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(angle);
            Rectangle sheen = new Rectangle(-width / 2, -height / 2, width, height);
            using (GraphicsPath path = new GraphicsPath()) {
                path.AddEllipse(sheen);
                using (PathGradientBrush brush = new PathGradientBrush(path)) {
                    brush.CenterColor = Color.FromArgb(alpha, 232, 249, 255);
                    brush.SurroundColors = new Color[] { Color.FromArgb(0, 120, 205, 255) };
                    g.FillPath(brush, path);
                }
            }
            g.Restore(state);
        }

        private void DrawLiquidRims(Graphics g)
        {
            Rectangle outer = new Rectangle(center.X - Outer + 4, center.Y - Outer + 4, (Outer - 4) * 2, (Outer - 4) * 2);
            Rectangle inner = new Rectangle(center.X - Inner - 13, center.Y - Inner - 13, (Inner + 13) * 2, (Inner + 13) * 2);
            using (Pen shadow = new Pen(Color.FromArgb(88, 0, 5, 16), 4f)) g.DrawEllipse(shadow, outer);
            using (LinearGradientBrush rimBrush = new LinearGradientBrush(outer, Color.FromArgb(245, 225, 248, 255), Color.FromArgb(225, 48, 135, 225), 132f))
            using (Pen rim = new Pen(rimBrush, 3.4f)) g.DrawEllipse(rim, outer);
            using (Pen rimLight = new Pen(Color.FromArgb(155, 232, 250, 255), 1f)) g.DrawArc(rimLight, outer, 198, 144);
            using (Pen innerShadow = new Pen(Color.FromArgb(96, 0, 6, 20), 4f)) g.DrawEllipse(innerShadow, inner);
            using (Pen innerRim = new Pen(Color.FromArgb(170, 147, 220, 255), 2.1f)) g.DrawEllipse(innerRim, inner);
            using (Pen innerLight = new Pen(Color.FromArgb(150, 238, 250, 255), 1.2f)) g.DrawArc(innerLight, inner, 205, 128);
        }

        private GraphicsPath SegmentPath(Point c, int inner, int outer, float start, float sweep)
        {
            GraphicsPath p = new GraphicsPath();
            Rectangle ro = new Rectangle(c.X - outer, c.Y - outer, outer * 2, outer * 2);
            Rectangle ri = new Rectangle(c.X - inner, c.Y - inner, inner * 2, inner * 2);
            p.AddArc(ro, start, sweep);
            p.AddArc(ri, start + sweep, -sweep);
            p.CloseFigure();
            return p;
        }

        private void DrawAction(Graphics g, int i)
        {
            ActionItem a = config.Pages[pageIndex].Actions[i];
            double angle = i * Math.PI / 3;
            int radius = 151;
            Point p = new Point(center.X + (int)(Math.Cos(angle) * radius), center.Y + (int)(Math.Sin(angle) * radius));
            Rectangle iconRect = new Rectangle(p.X - 28, p.Y - 28, 56, 56);
            ActionIcons.Draw(g, a, iconRect, i == selected);
        }

        private void DrawSectorNumber(Graphics g, int i)
        {
            int number = ((i + 1) % 6) + 1;
            double angle = i * Math.PI / 3;
            int radius = 101;
            Point p = new Point(center.X + (int)(Math.Cos(angle) * radius), center.Y + (int)(Math.Sin(angle) * radius));
            using (Font font = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (SolidBrush background = new SolidBrush(Color.FromArgb(i == selected ? 150 : 92, 8, 18, 34)))
            using (SolidBrush text = new SolidBrush(Color.FromArgb(230, 225, 242, 255))) {
                Rectangle badge = new Rectangle(p.X - 11, p.Y - 11, 22, 22);
                g.FillEllipse(background, badge);
                StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(number.ToString(), font, text, badge, format);
                format.Dispose();
            }
        }
    }

    static class ActionRunner
    {
        public static void Run(ActionItem a, Action showSettings)
        {
            try {
                switch (a.Type) {
                    case "App": ActivateOrStart(a.Target, a.Name); break;
                    case "Folder": OpenFolder(a.Target); break;
                    case "Explorer": Start("explorer.exe", "shell:ThisPCFolder"); break;
                    case "Settings": showSettings(); break;
                    case "Lock": Native.LockWorkStation(); break;
                    case "Sleep": Application.SetSuspendState(PowerState.Suspend, true, false); break;
                    case "Shutdown": Start("shutdown.exe", "/s /t 0"); break;
                    case "Restart": Start("shutdown.exe", "/r /t 0"); break;
                    case "VolumeUp": MediaKey(0xAF); break;
                    case "VolumeDown": MediaKey(0xAE); break;
                    case "Mute": MediaKey(0xAD); break;
                    case "Command": Start("cmd.exe", "/c " + a.Target); break;
                }
            } catch (Exception ex) { MessageBox.Show("无法执行“" + a.Name + "”\n" + ex.Message, "OrbitWheel"); }
        }

        private static void MediaKey(byte key)
        {
            Native.keybd_event(key, 0, 0, UIntPtr.Zero);
            Native.keybd_event(key, 0, 2, UIntPtr.Zero);
        }

        private static void Start(string file, string args)
        {
            ProcessStartInfo info = new ProcessStartInfo(file, args);
            info.UseShellExecute = true;
            Process.Start(info);
        }

        private static void OpenFolder(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return;
            string expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            if (!Directory.Exists(expanded)) throw new DirectoryNotFoundException(expanded);
            Start("explorer.exe", "\"" + expanded + "\"");
        }

        private static void ActivateOrStart(string target, string displayName)
        {
            string executable = ShortcutResolver.ResolveTarget(target);
            string appId = target.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase) ? target.Substring("shell:AppsFolder\\".Length) : "";
            string processName = ResolveProcessName(executable, appId, displayName);
            IntPtr processMainWindow = FindVisibleProcessMainWindow(processName);
            if (processMainWindow != IntPtr.Zero) {
                ActivateApplicationWindow(processMainWindow);
                return;
            }
            IntPtr visibleWindow = FindExistingWindow(executable, appId, displayName, true);
            if (visibleWindow != IntPtr.Zero) {
                ActivateApplicationWindow(visibleWindow);
                return;
            }
            bool running = IsApplicationRunning(processName, executable, appId);
            if (running && TryActivateFromWindowsTray(BuildTrayAliases(displayName, processName, executable), processName, executable, appId, displayName)) return;
            IntPtr existing = FindExistingWindow(executable, appId, displayName, false);
            if (existing != IntPtr.Zero) {
                ActivateApplicationWindow(existing);
                return;
            }
            if (target.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
                Start("explorer.exe", target);
            else
                Start(target, "");
        }

        private static string ResolveProcessName(string executable, string appId, string displayName)
        {
            if (File.Exists(executable)) return Path.GetFileNameWithoutExtension(executable);
            List<string> candidates = new List<string>();
            if (!String.IsNullOrWhiteSpace(appId)) {
                int bang = appId.LastIndexOf('!');
                if (bang >= 0 && bang + 1 < appId.Length) AddProcessCandidate(candidates, appId.Substring(bang + 1));
                string[] parts = appId.Split(new char[] { '.', '_', '!' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++) {
                    if (String.Equals(parts[i], "EXE", StringComparison.OrdinalIgnoreCase) && i > 0) AddProcessCandidate(candidates, parts[i - 1]);
                }
                AddProcessCandidate(candidates, appId);
            }
            AddProcessCandidate(candidates, displayName);
            foreach (string candidate in candidates) {
                try { if (Process.GetProcessesByName(candidate).Length > 0) return candidate; } catch { }
            }
            return candidates.Count > 0 ? candidates[0] : "";
        }

        private static void AddProcessCandidate(List<string> candidates, string candidate)
        {
            if (String.IsNullOrWhiteSpace(candidate)) return;
            candidate = candidate.Trim();
            if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) candidate = candidate.Substring(0, candidate.Length - 4);
            if (String.Equals(candidate, "App", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(candidate, "Application", StringComparison.OrdinalIgnoreCase)) return;
            if (candidate.IndexOf('\\') >= 0 || candidate.IndexOf('/') >= 0 || candidate.Length > 80) return;
            if (!candidates.Exists(delegate(string value) { return String.Equals(value, candidate, StringComparison.OrdinalIgnoreCase); })) candidates.Add(candidate);
        }

        private static IntPtr FindVisibleProcessMainWindow(string processName)
        {
            if (String.IsNullOrWhiteSpace(processName)) return IntPtr.Zero;
            try {
                foreach (Process process in Process.GetProcessesByName(processName)) {
                    using (process) {
                        try {
                            IntPtr hwnd = process.MainWindowHandle;
                            if (hwnd != IntPtr.Zero && Native.IsWindowVisible(hwnd)) return hwnd;
                        } catch { }
                    }
                }
            } catch { }
            return IntPtr.Zero;
        }

        private static bool IsApplicationRunning(string processName, string executable, string appId)
        {
            if (!String.IsNullOrWhiteSpace(processName)) {
                try { if (Process.GetProcessesByName(processName).Length > 0) return true; } catch { }
            }
            string expectedPath = File.Exists(executable) ? Path.GetFullPath(executable) : "";
            foreach (Process process in Process.GetProcesses()) {
                using (process) {
                    try {
                        if (expectedPath.Length > 0 && String.Equals(Path.GetFullPath(process.MainModule.FileName), expectedPath, StringComparison.OrdinalIgnoreCase)) return true;
                        if (appId.Length > 0) {
                            string runningAppId = GetAppUserModelId(process);
                            if (runningAppId.Length > 0 && (String.Equals(runningAppId, appId, StringComparison.OrdinalIgnoreCase) || runningAppId.StartsWith(appId + "!", StringComparison.OrdinalIgnoreCase))) return true;
                        }
                    } catch { }
                }
            }
            return false;
        }

        private static List<string> BuildTrayAliases(string displayName, string processName, string executable)
        {
            List<string> aliases = new List<string>();
            AddTrayAlias(aliases, processName);
            if (String.Equals(processName, "Weixin", StringComparison.OrdinalIgnoreCase)) AddTrayAlias(aliases, "微信");
            if (String.Equals(processName, "QQ", StringComparison.OrdinalIgnoreCase)) AddTrayAlias(aliases, "QQ");
            if (File.Exists(executable)) {
                try {
                    FileVersionInfo version = FileVersionInfo.GetVersionInfo(executable);
                    AddTrayAlias(aliases, version.FileDescription);
                    AddTrayAlias(aliases, version.ProductName);
                    AddTrayAlias(aliases, version.InternalName);
                } catch { }
            }
            AddTrayAlias(aliases, displayName);
            return aliases;
        }

        private static void AddTrayAlias(List<string> aliases, string alias)
        {
            if (String.IsNullOrWhiteSpace(alias)) return;
            alias = alias.Trim();
            if (alias.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) alias = alias.Substring(0, alias.Length - 4);
            if (alias.Length < 2 || aliases.Exists(delegate(string value) { return String.Equals(value, alias, StringComparison.CurrentCultureIgnoreCase); })) return;
            aliases.Add(alias);
        }

        private static bool TryActivateFromWindowsTray(List<string> aliases, string processName, string executable, string appId, string displayName)
        {
            if (aliases == null || aliases.Count == 0) return false;
            IntPtr oldDpiContext = Native.SetThreadDpiAwarenessContext(new IntPtr(-4));
            Native.NativePoint oldPosition;
            Native.GetCursorPos(out oldPosition);
            bool clicked = false;
            try {
                IntPtr taskbar = Native.FindWindow("Shell_TrayWnd", null);
                Native.WindowRect taskbarRect;
                if (taskbar != IntPtr.Zero && Native.GetWindowRect(taskbar, out taskbarRect)) {
                    Native.NativePoint revealPoint = GetTaskbarRevealPoint(taskbarRect);
                    Native.WindowRect cursorLock = new Native.WindowRect {
                        Left = revealPoint.X,
                        Top = revealPoint.Y,
                        Right = revealPoint.X + 1,
                        Bottom = revealPoint.Y + 1
                    };
                    Native.SetCursorPos(revealPoint.X, revealPoint.Y);
                    Native.ClipCursor(ref cursorLock);
                    System.Threading.Thread.Sleep(700);
                }
                AutomationElement taskbarElement = taskbar != IntPtr.Zero ? AutomationElement.FromHandle(taskbar) : null;
                AutomationElement overflowRoot = FindTrayOverflowRoot();
                if (overflowRoot != null) clicked = TryClickTrayButton(overflowRoot, aliases);
                if (!clicked && taskbarElement != null) clicked = TryClickTrayButton(taskbarElement, aliases);

                AutomationElement overflow = !clicked && taskbarElement != null ? FindTrayOverflowButton(taskbarElement) : null;
                if (!clicked && overflow != null && TryInvokeAutomationElement(overflow)) {
                    overflowRoot = null;
                    for (int i = 0; i < 12 && overflowRoot == null; i++) {
                        System.Threading.Thread.Sleep(50);
                        overflowRoot = FindTrayOverflowRoot();
                    }
                    if (overflowRoot != null) clicked = TryClickTrayButton(overflowRoot, aliases);
                    if (!clicked) {
                        Native.keybd_event(0x1B, 0, 0, UIntPtr.Zero);
                        Native.keybd_event(0x1B, 0, 2, UIntPtr.Zero);
                    }
                }
                if (clicked) WaitForApplicationWindow(processName, executable, appId, displayName, 8000);
            } catch { }
            finally {
                Native.ClipCursor(IntPtr.Zero);
                Native.SetCursorPos(oldPosition.X, oldPosition.Y);
                Native.SetThreadDpiAwarenessContext(oldDpiContext);
            }
            return clicked;
        }

        private static IntPtr WaitForApplicationWindow(string processName, string executable, string appId, string displayName, int timeoutMilliseconds)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < timeoutMilliseconds) {
                IntPtr hwnd = FindVisibleProcessMainWindow(processName);
                if (hwnd == IntPtr.Zero) hwnd = FindExistingWindow(executable, appId, displayName, true);
                if (hwnd != IntPtr.Zero) return hwnd;
                System.Threading.Thread.Sleep(50);
            }
            return IntPtr.Zero;
        }

        private static Native.NativePoint GetTaskbarRevealPoint(Native.WindowRect rect)
        {
            bool horizontal = (rect.Right - rect.Left) >= (rect.Bottom - rect.Top);
            if (horizontal) {
                return new Native.NativePoint {
                    X = (rect.Left + rect.Right) / 2,
                    Y = rect.Top <= 1 ? rect.Bottom - 1 : rect.Top + 1
                };
            }
            return new Native.NativePoint {
                X = rect.Left <= 1 ? rect.Right - 1 : rect.Left + 1,
                Y = (rect.Top + rect.Bottom) / 2
            };
        }

        private static bool TryClickTrayButton(AutomationElement root, List<string> aliases)
        {
            System.Windows.Rect rootBounds;
            try { rootBounds = root.Current.BoundingRectangle; }
            catch { return false; }
            AutomationElementCollection buttons = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            AutomationElement bestButton = null;
            int bestScore = 0;
            for (int i = 0; i < buttons.Count; i++) {
                AutomationElement button = buttons[i];
                try {
                    if (!(button.Current.ClassName ?? "").StartsWith("SystemTray.", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!String.Equals(button.Current.AutomationId, "NotifyItemIcon", StringComparison.OrdinalIgnoreCase)) continue;
                    System.Windows.Rect bounds = button.Current.BoundingRectangle;
                    double centerX = bounds.Left + bounds.Width / 2;
                    double centerY = bounds.Top + bounds.Height / 2;
                    if (!rootBounds.Contains(centerX, centerY)) continue;
                    string name = (button.Current.Name ?? "").Trim();
                    int score = GetTrayNameMatchScore(name, aliases);
                    if (score > bestScore) {
                        bestScore = score;
                        bestButton = button;
                    }
                } catch { continue; }
            }
            return bestButton != null && TryClickAutomationElement(bestButton);
        }

        private static int GetTrayNameMatchScore(string name, List<string> aliases)
        {
            if (String.IsNullOrWhiteSpace(name)) return 0;
            int best = 0;
            for (int i = 0; i < aliases.Count; i++) {
                string alias = aliases[i];
                int priority = (aliases.Count - i) * 10;
                if (String.Equals(name, alias, StringComparison.CurrentCultureIgnoreCase)) best = Math.Max(best, 1000 + alias.Length + priority);
                else if (name.StartsWith(alias + ":", StringComparison.CurrentCultureIgnoreCase) ||
                         name.StartsWith(alias + " ", StringComparison.CurrentCultureIgnoreCase) ||
                         name.StartsWith(alias + "\r", StringComparison.CurrentCultureIgnoreCase) ||
                         name.StartsWith(alias + "\n", StringComparison.CurrentCultureIgnoreCase)) best = Math.Max(best, 800 + alias.Length + priority);
                else if (name.IndexOf(alias, StringComparison.CurrentCultureIgnoreCase) >= 0) best = Math.Max(best, 100 + alias.Length + priority);
            }
            return best;
        }

        private static AutomationElement FindTrayOverflowButton(AutomationElement root)
        {
            string[] names = new string[] { "显示隐藏的图标", "Show hidden icons" };
            for (int i = 0; i < names.Length; i++) {
                AutomationElement exact = root.FindFirst(
                    TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                        new PropertyCondition(AutomationElement.NameProperty, names[i], PropertyConditionFlags.IgnoreCase)));
                if (exact != null) return exact;
            }
            AutomationElementCollection buttons = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            for (int i = 0; i < buttons.Count; i++) {
                try {
                    AutomationElement button = buttons[i];
                    string name = button.Current.Name ?? "";
                    if (!(button.Current.ClassName ?? "").StartsWith("SystemTray.", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!String.Equals(button.Current.AutomationId, "SystemTrayIcon", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.IndexOf("隐藏", StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                        name.IndexOf("hidden", StringComparison.OrdinalIgnoreCase) >= 0) return button;
                } catch { }
            }
            return null;
        }

        private static bool TryInvokeAutomationElement(AutomationElement element)
        {
            try {
                object pattern;
                if (!element.TryGetCurrentPattern(InvokePattern.Pattern, out pattern)) return false;
                ((InvokePattern)pattern).Invoke();
                return true;
            } catch { return false; }
        }

        private static AutomationElement FindTrayOverflowRoot()
        {
            IntPtr hwnd = Native.FindWindow("TopLevelWindowForOverflowXamlIsland", null);
            if (hwnd == IntPtr.Zero || !Native.IsWindowVisible(hwnd)) return null;
            try { return AutomationElement.FromHandle(hwnd); }
            catch { return null; }
        }

        private static bool TryClickAutomationElement(AutomationElement element)
        {
            try {
                System.Windows.Rect bounds = element.Current.BoundingRectangle;
                if (bounds.IsEmpty || bounds.Width < 2 || bounds.Height < 2) return false;
                int x = (int)(bounds.Left + bounds.Width / 2);
                int y = (int)(bounds.Top + bounds.Height / 2);
                Native.WindowRect cursorLock = new Native.WindowRect { Left = x, Top = y, Right = x + 1, Bottom = y + 1 };
                Native.ClipCursor(ref cursorLock);
                Native.SetCursorPos(x, y);
                int interval = Math.Max(40, Math.Min(160, (int)Native.GetDoubleClickTime() / 3));
                for (int i = 0; i < 2; i++) {
                    Native.mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
                    System.Threading.Thread.Sleep(20);
                    Native.mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
                    if (i == 0) System.Threading.Thread.Sleep(interval);
                }
                return true;
            } catch { return false; }
        }

        private static void ActivateApplicationWindow(IntPtr hwnd)
        {
            const uint WM_SYSCOMMAND = 0x0112;
            const int SC_RESTORE = 0xF120;
            Native.PostMessage(hwnd, WM_SYSCOMMAND, new IntPtr(SC_RESTORE), IntPtr.Zero);
            Native.SwitchToThisWindow(hwnd, true);
            Native.BringWindowToTop(hwnd);
            Native.SetForegroundWindow(hwnd);
        }

        private static IntPtr FindExistingWindow(string executable, string appId, string displayName, bool visibleOnly)
        {
            string expectedPath = File.Exists(executable) ? Path.GetFullPath(executable) : "";
            string expectedProcess = expectedPath.Length > 0 ? Path.GetFileNameWithoutExtension(expectedPath) : "";
            IntPtr found = IntPtr.Zero;
            int bestScore = Int32.MinValue;
            Native.EnumWindows(delegate(IntPtr hwnd, IntPtr data) {
                if (visibleOnly && !Native.IsWindowVisible(hwnd)) return true;
                uint processId;
                Native.GetWindowThreadProcessId(hwnd, out processId);
                if (processId == 0) return true;
                try {
                    using (Process process = Process.GetProcessById((int)processId)) {
                        bool matches = false;
                        if (expectedPath.Length > 0) {
                            try { matches = String.Equals(Path.GetFullPath(process.MainModule.FileName), expectedPath, StringComparison.OrdinalIgnoreCase); } catch { }
                            if (!matches && expectedProcess.Length > 0) matches = String.Equals(process.ProcessName, expectedProcess, StringComparison.OrdinalIgnoreCase);
                        }
                        if (!matches && appId.Length > 0) {
                            string runningAppId = GetAppUserModelId(process);
                            matches = runningAppId.Length > 0 && (String.Equals(runningAppId, appId, StringComparison.OrdinalIgnoreCase) || runningAppId.StartsWith(appId + "!", StringComparison.OrdinalIgnoreCase));
                        }
                        if (!matches && appId.Length > 0 && !String.IsNullOrWhiteSpace(displayName)) {
                            System.Text.StringBuilder title = new System.Text.StringBuilder(512);
                            Native.GetWindowText(hwnd, title, title.Capacity);
                            matches = title.Length > 0 && title.ToString().IndexOf(displayName, StringComparison.CurrentCultureIgnoreCase) >= 0;
                        }
                        if (matches) {
                            int score = ScoreApplicationWindow(hwnd, displayName);
                            if (score > bestScore) { bestScore = score; found = hwnd; }
                        }
                    }
                } catch { }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static int ScoreApplicationWindow(IntPtr hwnd, string displayName)
        {
            System.Text.StringBuilder title = new System.Text.StringBuilder(512);
            System.Text.StringBuilder className = new System.Text.StringBuilder(256);
            Native.GetWindowText(hwnd, title, title.Capacity);
            Native.GetClassName(hwnd, className, className.Capacity);
            string windowTitle = title.ToString();
            string windowClass = className.ToString();
            if (windowClass.IndexOf("TrayIcon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                windowClass.IndexOf("MessageWindow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                windowClass.IndexOf("SystemMessage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                windowClass.IndexOf("IME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                windowClass.IndexOf("SoPY", StringComparison.OrdinalIgnoreCase) >= 0) return Int32.MinValue;
            Native.WindowRect rect;
            Native.GetWindowRect(hwnd, out rect);
            int width = Math.Max(0, rect.Right - rect.Left);
            int height = Math.Max(0, rect.Bottom - rect.Top);
            if (width < 200 || height < 150) return Int32.MinValue;
            int score = Math.Min(10000, (width * height) / 100);
            if (Native.IsWindowVisible(hwnd)) score += 300;
            if (!String.IsNullOrWhiteSpace(windowTitle)) score += 1200;
            if (!String.IsNullOrWhiteSpace(displayName) && String.Equals(windowTitle, displayName, StringComparison.CurrentCultureIgnoreCase)) score += 6000;
            else if (!String.IsNullOrWhiteSpace(displayName) && windowTitle.IndexOf(displayName, StringComparison.CurrentCultureIgnoreCase) >= 0) score += 3000;
            if (windowClass.IndexOf("QWindow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                windowClass.IndexOf("Chrome_WidgetWin_1", StringComparison.OrdinalIgnoreCase) >= 0) score += 2500;
            return score;
        }

        private static string GetAppUserModelId(Process process)
        {
            try {
                uint length = 0;
                Native.GetApplicationUserModelId(process.Handle, ref length, null);
                if (length == 0) return "";
                System.Text.StringBuilder id = new System.Text.StringBuilder((int)length);
                return Native.GetApplicationUserModelId(process.Handle, ref length, id) == 0 ? id.ToString() : "";
            } catch { return ""; }
        }
    }

    static class ShortcutResolver
    {
        public static string ResolveTarget(string path)
        {
            if (String.IsNullOrEmpty(path) || !path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return path;
            try {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
                return Convert.ToString(shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null));
            } catch { return path; }
        }
    }

    static class ActionIcons
    {
        private static readonly Dictionary<string, Icon> Cache = new Dictionary<string, Icon>(StringComparer.OrdinalIgnoreCase);
        private static Bitmap systemIconSheet;
        private static readonly Dictionary<string, Point> SystemIconCells = new Dictionary<string, Point> {
            {"Explorer", new Point(0, 0)}, {"Settings", new Point(1, 0)}, {"Lock", new Point(2, 0)}, {"Sleep", new Point(3, 0)},
            {"Shutdown", new Point(0, 1)}, {"Restart", new Point(1, 1)}, {"VolumeUp", new Point(2, 1)}, {"VolumeDown", new Point(3, 1)},
            {"Mute", new Point(0, 2)}, {"Command", new Point(1, 2)}, {"None", new Point(2, 2)}
        };

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }

        public static void Draw(Graphics g, ActionItem a, Rectangle r, bool selected)
        {
            if (a.Type == "App" || a.Type == "Folder") {
                using (Icon appIcon = GetApplicationIcon(a.Target)) if (appIcon != null) { g.DrawIcon(appIcon, r); return; }
            }
            if (DrawGeneratedSystemIcon(g, a.Type, r)) return;
            Color fg = Color.FromArgb(245, 245, 250, 255);
            Rectangle tile = new Rectangle(r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6);
            using (GraphicsPath tilePath = Rounded(tile, 13)) {
                using (LinearGradientBrush b = new LinearGradientBrush(tile, selected ? Color.FromArgb(190, 86, 176, 255) : Color.FromArgb(120, 78, 101, 137), selected ? Color.FromArgb(145, 65, 112, 235) : Color.FromArgb(75, 39, 53, 78), 45)) g.FillPath(b, tilePath);
                using (Pen outline = new Pen(Color.FromArgb(selected ? 210 : 85, 225, 242, 255), selected ? 1.7f : 1f)) g.DrawPath(outline, tilePath);
            }
            using (Pen p = new Pen(fg, 3f)) {
                p.StartCap = p.EndCap = LineCap.Round;
                int x = r.X, y = r.Y, w = r.Width, h = r.Height, cx = x + w / 2, cy = y + h / 2;
                switch (a.Type) {
                    case "App":
                        using (Font appFont = new Font("Segoe UI", 16f, FontStyle.Bold))
                        using (SolidBrush appText = new SolidBrush(fg)) {
                            string initial = String.IsNullOrWhiteSpace(a.Name) ? "A" : a.Name.Substring(0, 1).ToUpper();
                            SizeF size = g.MeasureString(initial, appFont);
                            g.DrawString(initial, appFont, appText, cx - size.Width / 2, cy - size.Height / 2);
                        }
                        break;
                    case "Explorer":
                    case "Folder":
                        g.DrawRectangle(p, x + 9, y + 16, w - 18, h - 24);
                        g.DrawLine(p, x + 10, y + 16, x + 20, y + 10);
                        g.DrawLine(p, x + 20, y + 10, x + 29, y + 16);
                        break;
                    case "Settings":
                        g.DrawEllipse(p, x + 12, y + 12, w - 24, h - 24);
                        g.DrawEllipse(p, x + 19, y + 19, w - 38, h - 38);
                        for (int i = 0; i < 8; i++) {
                            double a0 = i * Math.PI / 4;
                            g.DrawLine(p, cx + (int)(Math.Cos(a0) * 12), cy + (int)(Math.Sin(a0) * 12), cx + (int)(Math.Cos(a0) * 17), cy + (int)(Math.Sin(a0) * 17));
                        }
                        break;
                    case "Lock":
                        g.DrawRectangle(p, x + 12, y + 20, w - 24, h - 27);
                        g.DrawArc(p, x + 15, y + 7, w - 30, 25, 180, -180);
                        break;
                    case "VolumeUp":
                    case "VolumeDown":
                    case "Mute":
                        Point[] speaker = { new Point(x+9,cy-6), new Point(x+17,cy-6), new Point(x+27,cy-15), new Point(x+27,cy+15), new Point(x+17,cy+6), new Point(x+9,cy+6) };
                        g.DrawPolygon(p, speaker);
                        if (a.Type == "Mute") { g.DrawLine(p,x+31,y+15,x+39,y+29); g.DrawLine(p,x+39,y+15,x+31,y+29); }
                        else { g.DrawArc(p,x+25,y+12,13,20,-55,110); if(a.Type=="VolumeUp"){g.DrawLine(p,x+37,cy,x+43,cy);g.DrawLine(p,x+40,cy-3,x+40,cy+3);} }
                        break;
                    case "Sleep":
                        g.DrawArc(p, x+10,y+8,w-20,h-16,70,235);
                        g.DrawArc(p, x+18,y+5,w-20,h-16,105,210);
                        break;
                    case "Shutdown":
                    case "Restart":
                        g.DrawArc(p,x+9,y+9,w-18,h-18,-55,290); g.DrawLine(p,cx,y+7,cx,cy+8);
                        if(a.Type=="Restart") g.DrawLine(p,x+10,y+14,x+10,y+24);
                        break;
                    case "Command":
                        g.DrawRectangle(p,x+8,y+10,w-16,h-20); g.DrawLine(p,x+13,y+17,x+20,y+22); g.DrawLine(p,x+20,y+22,x+13,y+27); g.DrawLine(p,x+23,y+28,x+31,y+28);
                        break;
                    case "None":
                        g.DrawLine(p,x+14,cy,x+w-14,cy);
                        break;
                    default:
                        g.DrawEllipse(p,x+12,y+12,w-24,h-24); g.DrawLine(p,cx,y+14,cx,y+h-14); g.DrawLine(p,x+14,cy,x+w-14,cy);
                        break;
                }
            }
        }

        private static bool DrawGeneratedSystemIcon(Graphics g, string type, Rectangle destination)
        {
            Point cell;
            if (!SystemIconCells.TryGetValue(type, out cell)) return false;
            try {
                if (systemIconSheet == null) {
                    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OrbitWheel.SystemIcons"))
                        if (stream != null) systemIconSheet = new Bitmap(stream);
                }
                if (systemIconSheet == null) return false;
                int cellWidth = systemIconSheet.Width / 4;
                int cellHeight = systemIconSheet.Height / 3;
                int insetX = 58, insetY = 54;
                Rectangle source = type == "Sleep"
                    ? new Rectangle(cell.X * cellWidth + 22, cell.Y * cellHeight + 46, cellWidth - 24, cellHeight - 92)
                    : new Rectangle(cell.X * cellWidth + insetX, cell.Y * cellHeight + insetY, cellWidth - insetX * 2, cellHeight - insetY * 2);
                InterpolationMode previous = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(systemIconSheet, destination, source, GraphicsUnit.Pixel);
                g.InterpolationMode = previous;
                return true;
            } catch { return false; }
        }

        public static Icon GetApplicationIcon(string target)
        {
            if (String.IsNullOrWhiteSpace(target)) return null;
            lock (Cache) {
                Icon cached;
                if (Cache.TryGetValue(target, out cached)) return cached == null ? null : (Icon)cached.Clone();
            }
            Icon loaded = LoadApplicationIcon(target);
            lock (Cache) Cache[target] = loaded == null ? null : (Icon)loaded.Clone();
            return loaded;
        }

        private static Icon LoadApplicationIcon(string target)
        {
            try {
                string iconTarget = ShortcutResolver.ResolveTarget(target);
                if (File.Exists(iconTarget)) { using (Icon icon = Icon.ExtractAssociatedIcon(iconTarget)) return (Icon)icon.Clone(); }
                IntPtr pidl;
                uint attributes;
                if (Native.SHParseDisplayName(target, IntPtr.Zero, out pidl, 0, out attributes) == 0 && pidl != IntPtr.Zero) {
                    try {
                        Native.ShellFileInfo pidlInfo = new Native.ShellFileInfo();
                        IntPtr parsed = Native.SHGetFileInfoPidl(pidl, 0, ref pidlInfo, (uint)Marshal.SizeOf(pidlInfo), 0x100 | 0x008);
                        if (parsed != IntPtr.Zero && pidlInfo.Icon != IntPtr.Zero) {
                            try { using (Icon icon = Icon.FromHandle(pidlInfo.Icon)) return (Icon)icon.Clone(); }
                            finally { Native.DestroyIcon(pidlInfo.Icon); }
                        }
                    } finally { Native.CoTaskMemFree(pidl); }
                }
                Native.ShellFileInfo info = new Native.ShellFileInfo();
                IntPtr result = Native.SHGetFileInfo(target, 0, ref info, (uint)Marshal.SizeOf(info), 0x100);
                if (result != IntPtr.Zero && info.Icon != IntPtr.Zero) {
                    try { using (Icon icon = Icon.FromHandle(info.Icon)) return (Icon)icon.Clone(); }
                    finally { Native.DestroyIcon(info.Icon); }
                }
            } catch { }
            return null;
        }
    }

    static class ActionNames
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string> {
            {"None","无操作"}, {"App","打开程序"}, {"Folder","打开文件夹"}, {"Command","执行命令"},
            {"Explorer","打开资源管理器"}, {"Settings","打开 OrbitWheel 设置"},
            {"Lock","锁定电脑"}, {"Sleep","进入睡眠"}, {"Shutdown","关闭电脑"},
            {"Restart","重新启动"}, {"VolumeUp","增大音量"}, {"VolumeDown","减小音量"},
            {"Mute","静音 / 取消静音"}
        };
        public static string Chinese(string id) { return Names.ContainsKey(id) ? Names[id] : "无操作"; }
        public static string Id(string chinese)
        {
            foreach (KeyValuePair<string,string> pair in Names) if (pair.Value == chinese) return pair.Key;
            return "None";
        }
        public static object[] AllChinese()
        {
            List<object> result = new List<object>();
            foreach (string id in new string[] { "None","App","Folder","Command","Explorer","Settings","Lock","Sleep","Shutdown","Restart","VolumeUp","VolumeDown","Mute" })
                result.Add(Chinese(id));
            return result.ToArray();
        }
    }

    class ApplicationChoice
    {
        public string Name { get; set; }
        public string Target { get; set; }
        public override string ToString() { return Name; }
    }

    static class ApplicationCatalog
    {
        private static string ResolveAppsFolderPath(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return path;
            Dictionary<string, string> roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                {"{6D809377-6AF0-444B-8957-A3773F02200E}", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)},
                {"{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)},
                {"{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}", Environment.GetFolderPath(Environment.SpecialFolder.System)},
                {"{D65231B0-B2F1-4857-A4CE-A8E7C6EA7D27}", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64")},
                {"{F38BF404-1D43-42F2-9305-67DE0B28FC23}", Environment.GetFolderPath(Environment.SpecialFolder.Windows)}
            };
            foreach (KeyValuePair<string,string> root in roots) {
                if (path.StartsWith(root.Key + "\\", StringComparison.OrdinalIgnoreCase)) {
                    string candidate = Path.Combine(root.Value, path.Substring(root.Key.Length + 1));
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return path;
        }

        public static List<ApplicationChoice> Load()
        {
            List<ApplicationChoice> result = new List<ApplicationChoice>();
            try {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                object shell = Activator.CreateInstance(shellType);
                object folder = shellType.InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, shell, new object[] { "shell:AppsFolder" });
                object items = folder.GetType().InvokeMember("Items", BindingFlags.InvokeMethod, null, folder, null);
                int count = Convert.ToInt32(items.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, items, null));
                for (int i = 0; i < count; i++) {
                    object item = items.GetType().InvokeMember("Item", BindingFlags.InvokeMethod, null, items, new object[] { i });
                    string name = Convert.ToString(item.GetType().InvokeMember("Name", BindingFlags.GetProperty, null, item, null));
                    string path = Convert.ToString(item.GetType().InvokeMember("Path", BindingFlags.GetProperty, null, item, null));
                    path = ResolveAppsFolderPath(path);
                    if (!String.IsNullOrWhiteSpace(name) && !String.IsNullOrWhiteSpace(path))
                        result.Add(new ApplicationChoice { Name = name, Target = File.Exists(path) ? path : path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) ? path : "shell:AppsFolder\\" + path });
                }
            } catch { }
            result.Sort(delegate(ApplicationChoice a, ApplicationChoice b) { return String.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase); });
            return result;
        }
    }

    class ApplicationPicker : Form
    {
        private TextBox search;
        private ListBox list;
        private List<ApplicationChoice> all;
        public ApplicationChoice SelectedApplication { get; private set; }

        public ApplicationPicker()
        {
            Text = "选择应用 - Applications";
            Icon = IconFactory.AppIcon();
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(680, 720);
            BackColor = Color.FromArgb(7, 15, 30);
            ForeColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 10f);
            GlassPanel shell = new GlassPanel { Left = 18, Top = 18, Width = 644, Height = 684, Radius = 22, BorderColor = Color.FromArgb(82, 112, 160, 215) };
            Controls.Add(shell);
            Label title = new Label { Text = "选择应用", Left = 28, Top = 22, Width = 400, Height = 38, Font = new Font("Microsoft YaHei UI", 20f, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent };
            Label hint = new Label { Text = "Applications 中的所有应用，也可切换为普通文件选择", Left = 30, Top = 64, Width = 540, Height = 24, ForeColor = Color.FromArgb(150, 180, 215), BackColor = Color.Transparent };
            search = new TextBox { Left = 28, Top = 104, Width = 588, Height = 36, BackColor = Color.FromArgb(18, 31, 51), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 11f) };
            list = new ListBox { Left = 28, Top = 158, Width = 588, Height = 430, BackColor = Color.FromArgb(18, 29, 47), ForeColor = Color.White, BorderStyle = BorderStyle.None, ItemHeight = 58, Font = new Font("Microsoft YaHei UI", 11f), DrawMode = DrawMode.OwnerDrawFixed };
            Button browse = new Button { Text = "浏览文件…", Left = 28, Top = 610, Width = 160, Height = 44, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(36, 55, 82), ForeColor = Color.White };
            Button choose = new Button { Text = "选择应用", Left = 456, Top = 610, Width = 160, Height = 44, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(20, 105, 224), ForeColor = Color.White };
            browse.FlatAppearance.BorderSize = 0;
            choose.FlatAppearance.BorderSize = 0;
            shell.Controls.AddRange(new Control[] { title, hint, search, list, browse, choose });
            search.TextChanged += delegate { Filter(); };
            list.DoubleClick += delegate { Accept(); };
            list.DrawItem += DrawApplication;
            browse.Click += delegate { BrowseFile(); };
            choose.Click += delegate { Accept(); };
            all = ApplicationCatalog.Load();
            Filter();
        }

        private void Filter()
        {
            string query = search.Text.Trim();
            list.BeginUpdate(); list.Items.Clear();
            foreach (ApplicationChoice app in all)
                if (query.Length == 0 || app.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0) list.Items.Add(app);
            list.EndUpdate();
        }

        private void Accept()
        {
            SelectedApplication = list.SelectedItem as ApplicationChoice;
            if (SelectedApplication == null) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void BrowseFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog { Title = "选择应用程序或快捷方式", Filter = "应用与快捷方式 (*.exe;*.lnk)|*.exe;*.lnk|所有文件 (*.*)|*.*" }) {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                SelectedApplication = new ApplicationChoice { Name = Path.GetFileNameWithoutExtension(dialog.FileName), Target = dialog.FileName };
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void DrawApplication(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            ApplicationChoice app = (ApplicationChoice)list.Items[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (SolidBrush background = new SolidBrush(selected ? Color.FromArgb(35, 100, 185) : Color.FromArgb(18, 29, 47))) e.Graphics.FillRectangle(background, e.Bounds);
            using (Icon icon = ActionIcons.GetApplicationIcon(app.Target)) {
                if (icon != null) e.Graphics.DrawIcon(icon, new Rectangle(e.Bounds.X + 14, e.Bounds.Y + 9, 40, 40));
                else {
                    Rectangle fallback = new Rectangle(e.Bounds.X + 14, e.Bounds.Y + 9, 40, 40);
                    using (SolidBrush fb = new SolidBrush(Color.FromArgb(70, 122, 220))) e.Graphics.FillEllipse(fb, fallback);
                    using (Font ff = new Font("Segoe UI", 12f, FontStyle.Bold))
                    using (SolidBrush ft = new SolidBrush(Color.White)) {
                        string initial = String.IsNullOrWhiteSpace(app.Name) ? "A" : app.Name.Substring(0, 1).ToUpper();
                        SizeF size = e.Graphics.MeasureString(initial, ff);
                        e.Graphics.DrawString(initial, ff, ft, fallback.X + (fallback.Width - size.Width) / 2, fallback.Y + (fallback.Height - size.Height) / 2);
                    }
                }
            }
            using (SolidBrush text = new SolidBrush(Color.White)) e.Graphics.DrawString(app.Name, Font, text, e.Bounds.X + 70, e.Bounds.Y + 18);
            using (Pen line = new Pen(Color.FromArgb(38, 58, 83))) e.Graphics.DrawLine(line, e.Bounds.X + 70, e.Bounds.Bottom - 1, e.Bounds.Right - 12, e.Bounds.Bottom - 1);
        }
    }

    class GlassPanel : Panel
    {
        public int Radius = 18;
        public Color BorderColor = Color.FromArgb(55, 105, 160, 220);

        public GlassPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(26, 38, 58);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (GraphicsPath path = RoundedPath(ClientRectangle, Radius)) Region = new Region(path);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedPath(r, Radius))
            using (LinearGradientBrush fill = new LinearGradientBrush(r, Color.FromArgb(36, 49, 72), Color.FromArgb(22, 31, 48), 120f)) {
                e.Graphics.FillPath(fill, path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = RoundedPath(r, Radius))
            using (Pen border = new Pen(BorderColor, 1f)) {
                e.Graphics.DrawPath(border, path);
            }
            base.OnPaint(e);
        }

        private static GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = Math.Max(2, radius * 2);
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    class SettingsForm : Form
    {
        private AppConfig config;
        private ListBox pages;
        private DataGridView grid;
        private TextBox pageName;
        private ComboBox mode, style;
        private TextBox hotkeyRecorder;
        private int recordedModifiers;
        private int recordedKey;
        private CheckBox startup;
        private CheckBox mouseGestures;
        private Label effectDescription;
        private Panel contentHost;
        private readonly List<Button> navigation = new List<Button>();
        private readonly List<Panel> sections = new List<Panel>();
        private bool loadingGrid;
        private bool choosingApp;
        private bool showingPage;
        private bool initializing;
        public event EventHandler ConfigSaved;

        public SettingsForm(AppConfig c)
        {
            config = c;
            Text = "OrbitWheel 设置";
            Icon = IconFactory.AppIcon();
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1180, 760);
            MinimumSize = new Size(1080, 700);
            BackColor = Color.FromArgb(7, 15, 30);
            ForeColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9.5f);
            initializing = true;
            Build();
            LoadPageList();
            initializing = false;
            FormClosing += delegate { if (grid != null) { grid.EndEdit(); AutoSave(); } };
        }

        private void Build()
        {
            GlassPanel shell = new GlassPanel { Left = 18, Top = 18, Width = 1144, Height = 724, Radius = 22, BorderColor = Color.FromArgb(85, 112, 161, 220) };
            Controls.Add(shell);
            Label appMark = L("◉", 26, 20, 44, 44, 24, false); appMark.ForeColor = Color.FromArgb(68, 178, 255); shell.Controls.Add(appMark);
            Label title = L("设置", 78, 24, 260, 38, 20, true); shell.Controls.Add(title);
            Panel rule = new Panel { Left = 24, Top = 76, Width = 1096, Height = 1, BackColor = Color.FromArgb(55, 112, 145, 185) }; shell.Controls.Add(rule);

            GlassPanel sidebar = new GlassPanel { Left = 22, Top = 96, Width = 220, Height = 604, Radius = 18, BorderColor = Color.FromArgb(42, 93, 130, 180) };
            shell.Controls.Add(sidebar);
            string[] navText = { "⌂   常规", "▦   页面与动作", "◉   外观效果", "⌨   快捷键", "⚙   高级", "●   关于" };
            for (int i = 0; i < navText.Length; i++) {
                Button nav = B(navText[i], 14, 18 + i * 58, 192, 46);
                nav.TextAlign = ContentAlignment.MiddleLeft;
                nav.Padding = new Padding(18, 0, 0, 0);
                int index = i;
                nav.Click += delegate { ShowSection(index); };
                sidebar.Controls.Add(nav);
                navigation.Add(nav);
            }
            Label auto = L("所有更改都会自动保存", 24, 552, 180, 24, 8, false); auto.ForeColor = Color.FromArgb(130, 165, 205); sidebar.Controls.Add(auto);

            contentHost = new Panel { Left = 262, Top = 96, Width = 858, Height = 604, BackColor = Color.Transparent };
            shell.Controls.Add(contentHost);

            Panel general = Section();
            GlassPanel startupCard = Card("启动与托盘", 0, 0, 858, 132);
            startup = new CheckBox { Left = 28, Top = 57, Width = 250, Height = 30, Text = "随 Windows 自动启动", ForeColor = Color.White, BackColor = Color.Transparent, Font = new Font("Microsoft YaHei UI", 10.5f) };
            startupCard.Controls.Add(startup);
            Label trayHint = L("关闭设置窗口后，OrbitWheel 仍会留在系统托盘", 28, 91, 520, 22, 8.5f, false); trayHint.ForeColor = Color.FromArgb(145, 174, 210); startupCard.Controls.Add(trayHint);
            general.Controls.Add(startupCard);
            GlassPanel triggerCard = Card("触发模式", 0, 148, 858, 154);
            mode = C(28, 60, 390); mode.Items.AddRange(new object[] { "点击模式", "按住并松开执行" }); triggerCard.Controls.Add(mode);
            Label triggerHint = L("默认按住快捷键，移动到目标后松开执行", 28, 102, 530, 22, 8.5f, false); triggerHint.ForeColor = Color.FromArgb(145, 174, 210); triggerCard.Controls.Add(triggerHint);
            general.Controls.Add(triggerCard);
            GlassPanel pageHintCard = Card("页面切换", 0, 318, 858, 132);
            pageHintCard.Controls.Add(L("滚动鼠标滚轮切换页面", 28, 60, 330, 26, 10.5f, false));
            Label dots = L("●  ●  ●", 690, 61, 120, 24, 11, false); dots.ForeColor = Color.FromArgb(38, 157, 255); pageHintCard.Controls.Add(dots);
            general.Controls.Add(pageHintCard);
            sections.Add(general);

            Panel pageSection = Section();
            GlassPanel pageBox = Card("页面与六个扇区", 0, 0, 858, 586);
            pages = new ListBox { Left = 22, Top = 58, Width = 176, Height = 438, BackColor = Color.FromArgb(20,31,49), ForeColor = Color.White, BorderStyle = BorderStyle.None, ItemHeight = 38, Font = new Font("Microsoft YaHei UI", 10f) };
            pages.SelectedIndexChanged += delegate { ShowPage(); };
            pageBox.Controls.Add(pages);
            Button add = B("＋  添加页面", 22, 512, 112, 42); add.Click += delegate { AddPage(); }; pageBox.Controls.Add(add);
            Button del = B("−", 144, 512, 54, 42); del.BackColor = Color.FromArgb(67, 42, 58); del.Click += delegate { DeletePage(); }; pageBox.Controls.Add(del);
            ToolTip pageTips = new ToolTip();
            pageTips.SetToolTip(add, "添加新页面");
            pageTips.SetToolTip(del, "删除当前页面");

            pageName = new TextBox { Left = 220, Top = 58, Width = 608, Height = 32, BackColor = Color.FromArgb(22,37,59), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Microsoft YaHei UI", 10.5f) };
            pageName.TextChanged += delegate {
                if (!showingPage && pages.SelectedIndex >= 0) {
                    config.Pages[pages.SelectedIndex].Name = pageName.Text;
                    if (Convert.ToString(pages.Items[pages.SelectedIndex]) != pageName.Text)
                        pages.Items[pages.SelectedIndex] = pageName.Text;
                    AutoSave();
                }
            };
            pageBox.Controls.Add(pageName);

            grid = new DataGridView { Left = 220, Top = 106, Width = 608, Height = 390, BackgroundColor = Color.FromArgb(20,31,49), ForeColor = Color.White, GridColor = Color.FromArgb(42,65,94), BorderStyle = BorderStyle.None, RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ColumnHeadersHeight = 42, RowTemplate = { Height = 48 } };
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35,47,68);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.BackColor = Color.FromArgb(24,32,48);
            grid.DefaultCellStyle.ForeColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(50,91,151);
            grid.Columns.Add("slot", "位置");
            grid.Columns.Add("name", "名称");
            DataGridViewComboBoxColumn typeCol = new DataGridViewComboBoxColumn { Name = "type", HeaderText = "动作类型" };
            typeCol.FlatStyle = FlatStyle.Flat;
            typeCol.Items.AddRange(ActionNames.AllChinese());
            grid.Columns.Add(typeCol);
            grid.Columns.Add("target", "程序路径 / 文件夹 / 命令");
            grid.Columns[0].ReadOnly = true;
            grid.Columns[0].FillWeight = 45; grid.Columns[1].FillWeight = 80; grid.Columns[2].FillWeight = 90; grid.Columns[3].FillWeight = 170;
            grid.CurrentCellDirtyStateChanged += delegate { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.CellValueChanged += delegate { HandleCellChange(); };
            grid.SelectionChanged += delegate { UpdateActionEditor(); };
            pageBox.Controls.Add(grid);
            Label appHint = L("选择“打开程序”会打开应用选择器；选择“打开文件夹”会打开文件夹选择器。", 220, 520, 590, 25, 8, false); appHint.ForeColor = Color.FromArgb(125, 166, 210); pageBox.Controls.Add(appHint);
            pageSection.Controls.Add(pageBox);
            sections.Add(pageSection);

            Panel appearance = Section();
            GlassPanel styleCard = Card("视觉效果", 0, 0, 858, 190);
            styleCard.Controls.Add(L("效果样式", 28, 62, 110, 24, 10, false));
            style = C(142, 58, 310); style.Items.AddRange(new object[] { "液态玻璃", "高斯模糊", "亚克力" }); styleCard.Controls.Add(style);
            effectDescription = L("", 28, 112, 700, 38, 9, false); effectDescription.ForeColor = Color.FromArgb(150, 188, 225); styleCard.Controls.Add(effectDescription);
            appearance.Controls.Add(styleCard);
            GlassPanel visualInfo = Card("圆环视觉说明", 0, 206, 858, 174);
            visualInfo.Controls.Add(L("视觉效果仅应用于圆环本身，不影响整个屏幕。", 28, 62, 650, 26, 10, false));
            Label material = L("液态玻璃强调折射高光；高斯模糊强调背景虚化；亚克力带有磨砂颗粒。", 28, 100, 740, 42, 9, false); material.ForeColor = Color.FromArgb(145, 174, 210); visualInfo.Controls.Add(material);
            appearance.Controls.Add(visualInfo);
            sections.Add(appearance);

            Panel hotkeySection = Section();
            GlassPanel hotkeyCard = Card("快捷键", 0, 0, 858, 190);
            hotkeyRecorder = new TextBox { Left = 28, Top = 64, Width = 520, Height = 38, ReadOnly = true, BackColor = Color.FromArgb(15, 29, 48), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center, Font = new Font("Segoe UI", 12f) };
            hotkeyRecorder.KeyDown += RecordHotkey;
            hotkeyRecorder.Enter += delegate { hotkeyRecorder.Text = "请按下新的快捷键…"; };
            hotkeyCard.Controls.Add(hotkeyRecorder);
            Button record = B("录制快捷键", 570, 62, 180, 42); record.Click += delegate { hotkeyRecorder.Focus(); hotkeyRecorder.Text = "请按下新的快捷键…"; }; hotkeyCard.Controls.Add(record);
            Label hotkeyHint = L("点击“录制快捷键”并按下任意组合键。", 28, 122, 620, 24, 9, false); hotkeyHint.ForeColor = Color.FromArgb(145, 174, 210); hotkeyCard.Controls.Add(hotkeyHint);
            hotkeySection.Controls.Add(hotkeyCard);
            sections.Add(hotkeySection);

            Panel advanced = Section();
            GlassPanel advancedCard = Card("鼠标手势", 0, 0, 858, 248);
            mouseGestures = new CheckBox { Left = 28, Top = 58, Width = 360, Height = 30, Text = "启用左右键组合手势", ForeColor = Color.White, BackColor = Color.Transparent, Font = new Font("Microsoft YaHei UI", 10.5f) };
            advancedCard.Controls.Add(mouseGestures);
            Label gestureHint = L("同时按下鼠标左右键并移动，全部松开后执行：上滑开始菜单，下滑桌面，左右滑切换窗口。", 28, 96, 790, 28, 9, false); gestureHint.ForeColor = Color.FromArgb(145,174,210); advancedCard.Controls.Add(gestureHint);
            Label safetyHint = L("组合手势期间会屏蔽原点击；普通单击仅在 110 毫秒识别窗口内短暂延后。", 28, 130, 770, 28, 9, false); safetyHint.ForeColor = Color.FromArgb(145,174,210); advancedCard.Controls.Add(safetyHint);
            Label advancedHint = L("圆环中心取打开瞬间的鼠标位置；点击模式下重复快捷键无效，按 Esc 关闭。", 28, 176, 770, 28, 9, false); advancedHint.ForeColor = Color.FromArgb(145,174,210); advancedCard.Controls.Add(advancedHint);
            advanced.Controls.Add(advancedCard);
            sections.Add(advanced);

            Panel about = Section();
            GlassPanel aboutCard = Card("关于 OrbitWheel", 0, 0, 858, 220);
            aboutCard.Controls.Add(L("OrbitWheel", 28, 62, 400, 40, 22, true));
            Label aboutHint = L("鼠标中心的六等分径向快捷操作工具", 30, 108, 620, 28, 10, false); aboutHint.ForeColor = Color.FromArgb(145, 180, 220); aboutCard.Controls.Add(aboutHint);
            aboutCard.Controls.Add(L("OrbitWheel 1.1.2 · 文件夹目标与边缘自适应", 30, 153, 500, 24, 9, false));
            about.Controls.Add(aboutCard);
            sections.Add(about);

            foreach (Panel section in sections) contentHost.Controls.Add(section);

            style.SelectedIndexChanged += delegate { UpdateEffectDescription(); };
            mode.SelectedIndexChanged += delegate { AutoSave(); };
            style.SelectedIndexChanged += delegate { AutoSave(); };
            startup.CheckedChanged += delegate { AutoSave(); };
            mouseGestures.CheckedChanged += delegate { AutoSave(); };

            recordedModifiers = config.Modifiers;
            recordedKey = config.KeyCode;
            hotkeyRecorder.Text = HotkeyText(recordedModifiers, recordedKey);
            mode.SelectedIndex = config.Mode == "Hold" ? 1 : 0;
            style.SelectedItem = config.Style;
            UpdateEffectDescription();
            startup.Checked = config.StartWithWindows;
            mouseGestures.Checked = config.MouseGestures;
            ShowSection(0);
        }

        private void LoadPageList()
        {
            pages.Items.Clear();
            foreach (WheelPage p in config.Pages) pages.Items.Add(p.Name);
            if (pages.Items.Count > 0) pages.SelectedIndex = 0;
        }

        private void ShowPage()
        {
            if (showingPage) return;
            showingPage = true;
            loadingGrid = true;
            grid.Rows.Clear();
            if (pages.SelectedIndex < 0) { loadingGrid = false; showingPage = false; return; }
            string[] slots = { "右", "右下", "左下", "左", "左上", "右上" };
            WheelPage p = config.Pages[pages.SelectedIndex];
            pageName.Text = p.Name;
            for (int i = 0; i < 6; i++) grid.Rows.Add(slots[i], p.Actions[i].Name, ActionNames.Chinese(p.Actions[i].Type), p.Actions[i].Target);
            loadingGrid = false;
            showingPage = false;
            UpdateActionEditor();
        }

        private void HandleCellChange()
        {
            if (loadingGrid || choosingApp) return;
            CommitPage();
            UpdateActionEditor();
            if (grid.CurrentRow != null && grid.CurrentCell != null && grid.CurrentCell.ColumnIndex == 2) {
                string type = ActionNames.Id(Convert.ToString(grid.CurrentRow.Cells[2].Value));
                if (type == "App") BrowseApp();
                if (type == "Folder") BrowseFolder();
            }
            AutoSave();
        }

        private void UpdateActionEditor()
        {
            if (grid == null || grid.CurrentRow == null) return;
            string type = ActionNames.Id(Convert.ToString(grid.CurrentRow.Cells[2].Value));
            grid.CurrentRow.Cells[3].ReadOnly = type != "App" && type != "Folder" && type != "Command";
            if (type != "App" && type != "Folder" && type != "Command") grid.CurrentRow.Cells[3].Value = "";
        }

        private void UpdateEffectDescription()
        {
            if (effectDescription == null || style == null) return;
            string value = Convert.ToString(style.SelectedItem);
            effectDescription.Text = value == "高斯模糊" ? "强背景虚化，颜色保持自然" : value == "亚克力" ? "高遮罩、磨砂颗粒、低透视" : "圆形局部背景模糊、实时焦散与折射高光";
        }

        private void CommitPage()
        {
            if (pages.SelectedIndex < 0 || grid.Rows.Count != 6) return;
            WheelPage p = config.Pages[pages.SelectedIndex];
            for (int i = 0; i < 6; i++) {
                p.Actions[i].Name = Convert.ToString(grid.Rows[i].Cells[1].Value);
                p.Actions[i].Type = ActionNames.Id(Convert.ToString(grid.Rows[i].Cells[2].Value));
                p.Actions[i].Target = Convert.ToString(grid.Rows[i].Cells[3].Value);
            }
        }

        private void AddPage()
        {
            CommitPage();
            WheelPage p = new WheelPage { Name = "页面 " + (config.Pages.Count + 1), Actions = new List<ActionItem>() };
            for (int i = 0; i < 6; i++) p.Actions.Add(new ActionItem { Name = "空", Type = "None", Target = "" });
            config.Pages.Add(p);
            LoadPageList();
            pages.SelectedIndex = config.Pages.Count - 1;
            AutoSave();
        }

        private void DeletePage()
        {
            if (config.Pages.Count <= 1) { MessageBox.Show("至少保留一个页面。"); return; }
            int i = pages.SelectedIndex;
            config.Pages.RemoveAt(i);
            LoadPageList();
            pages.SelectedIndex = Math.Min(i, config.Pages.Count - 1);
            AutoSave();
        }

        private void BrowseApp()
        {
            if (grid.CurrentRow == null || choosingApp) return;
            choosingApp = true;
            using (ApplicationPicker d = new ApplicationPicker()) {
                if (d.ShowDialog(this) == DialogResult.OK && d.SelectedApplication != null) {
                    grid.CurrentRow.Cells[1].Value = d.SelectedApplication.Name;
                    grid.CurrentRow.Cells[2].Value = ActionNames.Chinese("App");
                    grid.CurrentRow.Cells[3].Value = d.SelectedApplication.Target;
                }
            }
            choosingApp = false;
            CommitPage();
            AutoSave();
        }

        private void BrowseFolder()
        {
            if (grid.CurrentRow == null || choosingApp) return;
            choosingApp = true;
            using (FolderBrowserDialog d = new FolderBrowserDialog()) {
                d.Description = "选择要打开的文件夹";
                d.ShowNewFolderButton = true;
                if (d.ShowDialog(this) == DialogResult.OK && Directory.Exists(d.SelectedPath)) {
                    grid.CurrentRow.Cells[1].Value = Path.GetFileName(d.SelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (String.IsNullOrWhiteSpace(Convert.ToString(grid.CurrentRow.Cells[1].Value)))
                        grid.CurrentRow.Cells[1].Value = d.SelectedPath;
                    grid.CurrentRow.Cells[2].Value = ActionNames.Chinese("Folder");
                    grid.CurrentRow.Cells[3].Value = d.SelectedPath;
                }
            }
            choosingApp = false;
            CommitPage();
            AutoSave();
        }

        private void AutoSave()
        {
            if (initializing || loadingGrid || showingPage || choosingApp) return;
            CommitPage();
            config.Modifiers = recordedModifiers;
            config.KeyCode = recordedKey;
            config.Mode = mode.SelectedIndex == 1 ? "Hold" : "Click";
            config.Style = Convert.ToString(style.SelectedItem);
            config.StartWithWindows = startup.Checked;
            config.MouseGestures = mouseGestures.Checked;
            Startup.Set(config.StartWithWindows);
            ConfigStore.Save(config);
            if (ConfigSaved != null) ConfigSaved(this, EventArgs.Empty);
        }

        private void RecordHotkey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin) return;
            bool win = (Native.GetAsyncKeyState((int)Keys.LWin) & 0x8000) != 0 || (Native.GetAsyncKeyState((int)Keys.RWin) & 0x8000) != 0;
            recordedModifiers = (e.Control ? Native.MOD_CONTROL : 0) | (e.Alt ? Native.MOD_ALT : 0) | (e.Shift ? Native.MOD_SHIFT : 0) | (win ? Native.MOD_WIN : 0);
            recordedKey = (int)e.KeyCode;
            hotkeyRecorder.Text = HotkeyText(recordedModifiers, recordedKey);
            e.SuppressKeyPress = true;
            AutoSave();
        }

        private string HotkeyText(int modifiers, int keyCode)
        {
            List<string> parts = new List<string>();
            if ((modifiers & Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifiers & Native.MOD_ALT) != 0) parts.Add("Alt");
            if ((modifiers & Native.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((modifiers & Native.MOD_WIN) != 0) parts.Add("Win");
            parts.Add(((Keys)keyCode).ToString());
            return String.Join(" + ", parts.ToArray());
        }

        private Panel Section()
        {
            Panel section = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Visible = false };
            return section;
        }

        private GlassPanel Card(string text, int x, int y, int w, int h)
        {
            GlassPanel card = new GlassPanel { Left = x, Top = y, Width = w, Height = h, Radius = 17, BorderColor = Color.FromArgb(54, 94, 132, 184) };
            Label heading = L(text, 28, 20, w - 56, 30, 12, true);
            heading.ForeColor = Color.FromArgb(232, 242, 255);
            card.Controls.Add(heading);
            return card;
        }

        private void ShowSection(int index)
        {
            if (index < 0 || index >= sections.Count) return;
            for (int i = 0; i < sections.Count; i++) {
                sections[i].Visible = i == index;
                navigation[i].BackColor = i == index ? Color.FromArgb(20, 105, 224) : Color.FromArgb(31, 45, 66);
                navigation[i].ForeColor = i == index ? Color.White : Color.FromArgb(210, 225, 245);
            }
            sections[index].BringToFront();
        }

        private Panel Box(string text, int x, int y, int w, int h)
        {
            Panel box = new Panel { Left = x, Top = y, Width = w, Height = h, BackColor = Color.FromArgb(27,33,47), Padding = new Padding(12) };
            Label heading = L(text, 16, 12, w - 32, 25, 11, true);
            heading.ForeColor = Color.FromArgb(220, 235, 255);
            box.Controls.Add(heading);
            return box;
        }
        private Label L(string text, int x, int y, int w, int h, float size, bool bold)
        {
            return new Label { Text = text, Left = x, Top = y, Width = w, Height = h, ForeColor = Color.White, BackColor = Color.Transparent, Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular) };
        }
        private Button B(string text, int x, int y, int w, int h)
        {
            Button b = new Button { Text = text, Left = x, Top = y, Width = w, Height = h, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(31,45,66), Cursor = Cursors.Hand, Font = new Font("Microsoft YaHei UI", 9.5f) };
            b.FlatAppearance.BorderColor = Color.FromArgb(68, 105, 150);
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, 82, 145);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(20, 105, 224);
            return b;
        }
        private ComboBox C(int x, int y, int w)
        {
            return new ComboBox { Left = x, Top = y, Width = w, Height = 36, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(20,34,55), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Microsoft YaHei UI", 10.5f) };
        }
    }

    static class Startup
    {
        public static void Set(bool enabled)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
                if (enabled) k.SetValue("OrbitWheel", "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue("OrbitWheel", false);
            }
        }
    }

    class OrbitContext : ApplicationContext
    {
        private AppConfig config;
        private NotifyIcon tray;
        private HotkeyWindow hotkey;
        private KeyboardWatcher watcher;
        private MouseGestureService mouseGestures;
        private WheelForm wheel;
        private SettingsForm settings;

        public OrbitContext()
        {
            config = ConfigStore.Load();
            tray = new NotifyIcon { Icon = IconFactory.AppIcon(), Text = "OrbitWheel", Visible = true };
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("打开径向菜单", null, delegate { ShowWheel(); });
            menu.Items.Add("设置", null, delegate { ShowSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Exit(); });
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowSettings(); };
            hotkey = new HotkeyWindow();
            hotkey.Triggered += delegate {
                if (wheel != null && !wheel.IsDisposed) return;
                ShowWheel();
            };
            ApplyHotkey();
            ApplyMouseGestures();
        }

        private void ApplyHotkey()
        {
            if (!hotkey.Set(config.Modifiers, config.KeyCode))
                tray.ShowBalloonTip(3000, "OrbitWheel", "快捷键已被其他程序占用，请在设置中更换。", ToolTipIcon.Warning);
        }

        private void ApplyMouseGestures()
        {
            if (!config.MouseGestures) {
                if (mouseGestures != null) { mouseGestures.Dispose(); mouseGestures = null; }
                return;
            }
            if (mouseGestures != null && mouseGestures.IsRunning) return;
            if (mouseGestures != null) mouseGestures.Dispose();
            mouseGestures = new MouseGestureService();
            if (!mouseGestures.IsRunning) {
                mouseGestures.Dispose();
                mouseGestures = null;
                config.MouseGestures = false;
                ConfigStore.Save(config);
                tray.ShowBalloonTip(3000, "OrbitWheel", "鼠标手势启动失败，已自动关闭。", ToolTipIcon.Warning);
            }
        }

        private void ShowWheel()
        {
            if (wheel != null && !wheel.IsDisposed) {
                return;
            }
            wheel = new WheelForm(config);
            wheel.ExecuteRequested += delegate(ActionItem a) { ActionRunner.Run(a, ShowSettings); };
            wheel.FormClosed += delegate { wheel = null; if (watcher != null) { watcher.Dispose(); watcher = null; } };
            if (config.Mode == "Hold") {
                watcher = new KeyboardWatcher(config.KeyCode);
                watcher.TriggerReleased += delegate {
                    if (wheel != null && !wheel.IsDisposed) wheel.BeginInvoke(new Action(delegate { wheel.ExecuteHoldSelection(); }));
                };
            }
            wheel.Show();
        }

        private void ShowSettings()
        {
            try {
                if (settings != null && !settings.IsDisposed) { settings.Show(); settings.Activate(); settings.BringToFront(); return; }
                settings = new SettingsForm(config);
                settings.ConfigSaved += delegate { ApplyHotkey(); ApplyMouseGestures(); };
                settings.FormClosed += delegate { settings = null; };
                settings.Show();
                settings.Activate();
                settings.BringToFront();
            } catch (Exception ex) {
                Directory.CreateDirectory(ConfigStore.Folder);
                File.AppendAllText(Path.Combine(ConfigStore.Folder, "error.log"), DateTime.Now + " Settings: " + ex + Environment.NewLine);
                MessageBox.Show("设置页面打开失败：\n" + ex.Message, "OrbitWheel");
            }
        }

        public void OpenSettingsOnStart()
        {
            Timer t = new Timer { Interval = 250 };
            t.Tick += delegate { t.Stop(); t.Dispose(); ShowSettings(); };
            t.Start();
        }

        public void OpenWheelOnStart()
        {
            Timer t = new Timer { Interval = 250 };
            t.Tick += delegate { t.Stop(); t.Dispose(); ShowWheel(); };
            t.Start();
        }

        private void Exit()
        {
            tray.Visible = false;
            if (mouseGestures != null) { mouseGestures.Dispose(); mouseGestures = null; }
            hotkey.Dispose();
            if (watcher != null) watcher.Dispose();
            Application.Exit();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "OrbitWheel.SingleInstance", out created)) {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                OrbitContext context = new OrbitContext();
                if (Environment.CommandLine.IndexOf("/settings", StringComparison.OrdinalIgnoreCase) >= 0) context.OpenSettingsOnStart();
                if (Environment.CommandLine.IndexOf("/wheel", StringComparison.OrdinalIgnoreCase) >= 0) context.OpenWheelOnStart();
                Application.Run(context);
            }
        }
    }
}
