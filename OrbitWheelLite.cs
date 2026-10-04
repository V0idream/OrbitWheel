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
[assembly: AssemblyDescription("OrbitWheel 2.0 - WinUI 设置与径向快捷操作中心")]
[assembly: AssemblyCompany("OrbitWheel")]
[assembly: AssemblyProduct("OrbitWheel")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace OrbitWheelLite
{
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
        [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr data);
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
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool QueryFullProcessImageName(IntPtr process, uint flags, System.Text.StringBuilder path, ref uint length);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
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
            MouseLeave += delegate { selected = -1; Invalidate(); };
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
            int old = selected;
            selected = HitTestSector(e.Location);
            if (old != selected) Invalidate();
        }

        private int HitTestSector(Point point)
        {
            double dx = point.X - center.X, dy = point.Y - center.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist <= Inner || dist > Outer) return -1;
            double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
            if (angle < 0) angle += 360;
            return ((int)Math.Floor((angle + 30) / 60)) % 6;
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
            if (closing || IsDisposed) return;
            // MouseMove stops at the circular window boundary; sample the release position.
            selected = HitTestSector(PointToClient(Cursor.Position));
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
            target = Environment.ExpandEnvironmentVariables((target ?? "").Trim().Trim('"'));
            string executable = ShortcutResolver.ResolveTarget(target);
            string appId = executable.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase) ? executable.Substring("shell:AppsFolder\\".Length) : "";
            if (appId.Length == 0 && target.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase)) appId = target.Substring("shell:AppsFolder\\".Length);
            string processName = ResolveProcessName(executable, appId, displayName);
            IntPtr processMainWindow = FindVisibleProcessMainWindow(processName, executable, appId);
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
            string expectedPath = NormalizeExecutablePath(executable);
            if (expectedPath.Length > 0) return Path.GetFileNameWithoutExtension(expectedPath);
            List<string> candidates = new List<string>();
            if (appId.Length == 0 && executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) AddProcessCandidate(candidates, Path.GetFileNameWithoutExtension(executable));
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
                try {
                    Process[] processes = Process.GetProcessesByName(candidate);
                    bool running = processes.Length > 0;
                    foreach (Process process in processes) process.Dispose();
                    if (running) return candidate;
                } catch { }
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

        private static string NormalizeExecutablePath(string executable)
        {
            if (String.IsNullOrWhiteSpace(executable)) return "";
            string path = Environment.ExpandEnvironmentVariables(executable.Trim().Trim('"'));
            if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) return "";
            try {
                // Preserve a known path even if the target has been moved or deleted.
                if (Path.IsPathRooted(path) || path.IndexOf('\\') >= 0 || path.IndexOf('/') >= 0 || File.Exists(path)) return Path.GetFullPath(path);
            } catch { }
            return "";
        }

        private static string GetProcessExecutablePath(Process process)
        {
            IntPtr handle = Native.OpenProcess(0x1000, false, process.Id); // PROCESS_QUERY_LIMITED_INFORMATION
            if (handle != IntPtr.Zero) {
                try {
                    uint length = 32768;
                    System.Text.StringBuilder path = new System.Text.StringBuilder((int)length);
                    if (Native.QueryFullProcessImageName(handle, 0, path, ref length)) return NormalizeExecutablePath(path.ToString());
                } finally { Native.CloseHandle(handle); }
            }
            try { return NormalizeExecutablePath(process.MainModule.FileName); } catch { return ""; }
        }

        private static bool MatchesApplicationIdentity(string actualPath, string actualAppId, string actualProcess, string expectedPath, string appId, string processName)
        {
            // An observed mismatch must never become a match just because names agree.
            if (expectedPath.Length > 0 && actualPath.Length > 0) return String.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase);
            if (appId.Length > 0 && actualAppId.Length > 0) return String.Equals(actualAppId, appId, StringComparison.OrdinalIgnoreCase) || actualAppId.StartsWith(appId + "!", StringComparison.OrdinalIgnoreCase);
            return !String.IsNullOrWhiteSpace(processName) && String.Equals(actualProcess, processName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ProcessMatchesApplication(Process process, string processName, string expectedPath, string appId)
        {
            string actualPath = expectedPath.Length > 0 ? GetProcessExecutablePath(process) : "";
            string actualAppId = appId.Length > 0 ? GetAppUserModelId(process) : "";
            return MatchesApplicationIdentity(actualPath, actualAppId, process.ProcessName, expectedPath, appId, processName);
        }

        private static IntPtr FindVisibleProcessMainWindow(string processName, string executable, string appId)
        {
            if (String.IsNullOrWhiteSpace(processName)) return IntPtr.Zero;
            string expectedPath = NormalizeExecutablePath(executable);
            try {
                foreach (Process process in Process.GetProcessesByName(processName)) {
                    using (process) {
                        try {
                            if (!ProcessMatchesApplication(process, processName, expectedPath, appId)) continue;
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
            string expectedPath = NormalizeExecutablePath(executable);
            foreach (Process process in Process.GetProcesses()) {
                using (process) {
                    try {
                        if (ProcessMatchesApplication(process, processName, expectedPath, appId)) return true;
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
            // Windows tray buttons expose shell ownership and text, not the app's EXE path.
            // Avoid clicking a same-name app when its identity cannot be distinguished.
            if (HasConflictingProcessIdentity(processName, executable, appId)) return false;
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
                    if (overflowRoot != null) {
                        // The popup handle is visible before its icons finish laying out.
                        System.Threading.Thread.Sleep(200);
                        clicked = TryClickTrayButton(overflowRoot, aliases);
                    }
                    if (!clicked) {
                        Native.keybd_event(0x1B, 0, 0, UIntPtr.Zero);
                        Native.keybd_event(0x1B, 0, 2, UIntPtr.Zero);
                    }
                }
                if (clicked) clicked = WaitForApplicationWindow(processName, executable, appId, displayName, 8000) != IntPtr.Zero;
            } catch { }
            finally {
                Native.ClipCursor(IntPtr.Zero);
                Native.SetCursorPos(oldPosition.X, oldPosition.Y);
                Native.SetThreadDpiAwarenessContext(oldDpiContext);
            }
            return clicked;
        }

        private static bool HasConflictingProcessIdentity(string processName, string executable, string appId)
        {
            if (String.IsNullOrWhiteSpace(processName)) return false;
            string expectedPath = NormalizeExecutablePath(executable);
            foreach (Process process in Process.GetProcessesByName(processName)) {
                using (process) {
                    try { if (!ProcessMatchesApplication(process, processName, expectedPath, appId)) return true; }
                    catch { return true; }
                }
            }
            return false;
        }

        private static IntPtr WaitForApplicationWindow(string processName, string executable, string appId, string displayName, int timeoutMilliseconds)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < timeoutMilliseconds) {
                IntPtr hwnd = FindVisibleProcessMainWindow(processName, executable, appId);
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
            bool ambiguous = false;
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
                        ambiguous = false;
                    }
                    else if (score > 0 && score == bestScore) ambiguous = true;
                } catch { continue; }
            }
            return bestButton != null && !ambiguous && TryClickAutomationElement(bestButton);
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
                // Let the shell process the pointer move before the first button press.
                System.Threading.Thread.Sleep(100);
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
            string expectedPath = NormalizeExecutablePath(executable);
            string processName = ResolveProcessName(executable, appId, displayName);
            IntPtr found = IntPtr.Zero;
            int bestScore = Int32.MinValue;
            Native.EnumWindows(delegate(IntPtr hwnd, IntPtr data) {
                if (visibleOnly && !Native.IsWindowVisible(hwnd)) return true;
                uint processId;
                Native.GetWindowThreadProcessId(hwnd, out processId);
                if (processId == 0) return true;
                try {
                    using (Process process = Process.GetProcessById((int)processId)) {
                        bool matches = ProcessMatchesApplication(process, processName, expectedPath, appId);
                        if (!matches && appId.Length > 0 && String.Equals(process.ProcessName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase)) {
                            // UWP frame windows can be owned by a host; identify the child app.
                            Native.EnumChildWindows(hwnd, delegate(IntPtr child, IntPtr childData) {
                                uint childId;
                                Native.GetWindowThreadProcessId(child, out childId);
                                if (childId == 0 || childId == processId) return true;
                                try {
                                    using (Process childProcess = Process.GetProcessById((int)childId))
                                        matches = ProcessMatchesApplication(childProcess, processName, expectedPath, appId);
                                } catch { }
                                return !matches;
                            }, IntPtr.Zero);
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
        private Process settingsProcess;
        private Timer configTimer;
        private string configRevision;
        private readonly bool passive;
        internal AppConfig CurrentConfig { get { return config; } }
        internal string ConfigRevision { get { return configRevision; } }

        public OrbitContext(bool passive = false)
        {
            this.passive = passive;
            config = ConfigStore.Load();
            AppConfig initial;
            if (ConfigStore.TryLoad(out initial, out configRevision)) config = initial;
            tray = new NotifyIcon { Icon = IconFactory.AppIcon(), Text = "OrbitWheel", Visible = !passive };
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
            if (!passive) {
                try { Startup.Set(config.StartWithWindows); }
                catch (Exception error) { LogSettingsError(error); }
            }
            configTimer = new Timer { Interval = 300 };
            configTimer.Tick += delegate { try { ReloadConfiguration(); } catch (Exception error) { LogSettingsError(error); } };
            configTimer.Start();
        }

        private void ApplyHotkey()
        {
            if (passive) return;
            if (!hotkey.Set(config.Modifiers, config.KeyCode))
                tray.ShowBalloonTip(3000, "OrbitWheel", "快捷键已被其他程序占用，请在设置中更换。", ToolTipIcon.Warning);
        }

        private void ApplyMouseGestures()
        {
            if (passive) return;
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
                string revision;
                if (ConfigStore.TrySave(config, configRevision, out revision)) configRevision = revision;
                tray.ShowBalloonTip(3000, "OrbitWheel", "鼠标手势启动失败，已自动关闭。", ToolTipIcon.Warning);
            }
        }

        internal void ReloadConfiguration()
        {
            AppConfig updated; string revision;
            if (!ConfigStore.TryLoad(out updated, out revision) || revision == configRevision) return;
            AppConfig previous = config;
            // Do not execute a stale cached sector after another process edits it.
            if (wheel != null && !wheel.IsDisposed) wheel.Close();
            config = updated;
            configRevision = revision;
            if (previous.Modifiers != config.Modifiers || previous.KeyCode != config.KeyCode) ApplyHotkey();
            if (previous.MouseGestures != config.MouseGestures) ApplyMouseGestures();
            if (!passive && previous.StartWithWindows != config.StartWithWindows) {
                try { Startup.Set(config.StartWithWindows); }
                catch (Exception error) { LogSettingsError(error); }
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
            if (config.Mode == "Hold" && !passive) {
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
                if (settingsProcess != null && !settingsProcess.HasExited) {
                    settingsProcess.Refresh();
                    Native.ShowWindow(settingsProcess.MainWindowHandle, 9);
                    Native.SetForegroundWindow(settingsProcess.MainWindowHandle);
                    return;
                }
                string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings", "OrbitWheel.Settings.exe");
                if (!File.Exists(executable)) throw new FileNotFoundException("未找到 WinUI 设置程序，请解压完整发布包。", executable);
                if (settingsProcess != null) settingsProcess.Dispose();
                settingsProcess = Process.Start(new ProcessStartInfo(executable) {
                    WorkingDirectory = Path.GetDirectoryName(executable), UseShellExecute = true });
            } catch (Exception ex) {
                LogSettingsError(ex);
                MessageBox.Show("设置页面打开失败：\n" + ex.Message, "OrbitWheel");
            }
        }

        private static void LogSettingsError(Exception error)
        {
            try {
                Directory.CreateDirectory(ConfigStore.Folder);
                File.AppendAllText(Path.Combine(ConfigStore.Folder, "error.log"), DateTime.Now + " Settings: " + error + Environment.NewLine);
            } catch (IOException) { }
              catch (UnauthorizedAccessException) { }
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
            configTimer.Stop(); configTimer.Dispose();
            tray.Visible = false;
            if (mouseGestures != null) { mouseGestures.Dispose(); mouseGestures = null; }
            hotkey.Dispose();
            if (watcher != null) watcher.Dispose();
            if (settingsProcess != null) settingsProcess.Dispose();
            tray.Dispose();
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
                OrbitContext context;
                try { context = new OrbitContext(); }
                catch (Exception error) {
                    MessageBox.Show("配置加载失败，原文件已保留。\n" + error.Message, "OrbitWheel", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (Environment.CommandLine.IndexOf("/settings", StringComparison.OrdinalIgnoreCase) >= 0) context.OpenSettingsOnStart();
                if (Environment.CommandLine.IndexOf("/wheel", StringComparison.OrdinalIgnoreCase) >= 0) context.OpenWheelOnStart();
                Application.Run(context);
            }
        }
    }
}
