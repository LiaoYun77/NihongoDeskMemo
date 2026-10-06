using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace NihongoDeskMemoWpf
{
    internal sealed class GlobalHideHotkey : IDisposable
    {
        private int hotkeyId = 0x4E48;
        private bool registered;
        private F12Listener f12Listener;
        private readonly IntPtr handle;
        private readonly uint threadId;
        private readonly HwndSource source;
        private readonly List<HiddenWindow> hiddenWindows = new List<HiddenWindow>();
        private IntPtr activeWindow;
        private bool disposed;

        public string Shortcut { get; private set; }
        public HotkeyGesture SelectedGesture { get; private set; }
        private bool recording;
        private HotkeyGesture resumeGesture;
        public bool IsHidden { get; private set; }
        public event EventHandler VisibilityChanged;

        public GlobalHideHotkey(Window window) : this(window, 10) { }

        public GlobalHideHotkey(Window window, int key) : this(window, HotkeyGesture.FunctionKey(key)) { }

        public GlobalHideHotkey(Window window, HotkeyGesture key)
        {
            handle = new WindowInteropHelper(window).EnsureHandle();
            threadId = GetCurrentThreadId();
            source = HwndSource.FromHwnd(handle);
            source.AddHook(WindowMessage);
            Shortcut = string.Empty;
            string error;
            if (!TryChange(key, out error) && key.Modifiers == 0 && (key.VirtualKey == 0x79 || key.VirtualKey == 0x7A))
            {
                HotkeyGesture fallback;
                HotkeyGesture.TryCreate(System.Windows.Input.KeyInterop.KeyFromVirtualKey(key.VirtualKey),
                    System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt, out fallback, out error);
                TryChange(fallback, out error);
            }
        }

        public static int NormalizeKey(int key) { return key >= 10 && key <= 12 ? key : 10; }

        public bool TryChange(int key, out string error)
        {
            return TryChange(HotkeyGesture.FunctionKey(key), out error);
        }

        public bool TryChange(HotkeyGesture key, out string error)
        {
            error = string.Empty;
            if (disposed || recording || key == null) { error = "请先结束快捷键录入。"; return false; }
            if (key.SameAs(SelectedGesture) && Shortcut.Length > 0) return true;
            int nextId = hotkeyId == 0x4E48 ? 0x4E49 : 0x4E48;
            F12Listener nextListener = null;
            if (key.VirtualKey == 0x7B)
            {
                nextListener = new F12Listener(delegate { PostMessage(handle, 0x0312, new IntPtr(nextId), IntPtr.Zero); }, key.Modifiers);
                if (!nextListener.Available)
                {
                    nextListener.Dispose();
                    error = "F12 监听启动失败或已被本软件的另一实例使用，原快捷键未改变。";
                    return false;
                }
            }
            // Acquire the replacement first; a conflict must not remove the working key.
            else if (!RegisterHotKey(handle, nextId, 0x4000 | key.Modifiers, (uint)key.VirtualKey))
            {
                error = key.DisplayName + " 已被占用或无法注册，请选择其他按键。原快捷键未改变。";
                return false;
            }
            ReleaseKey();
            f12Listener = nextListener;
            registered = key.VirtualKey != 0x7B;
            hotkeyId = nextId;
            SelectedGesture = key;
            Shortcut = key.DisplayName;
            return true;
        }

        public string SetRecording(bool value)
        {
            if (disposed || recording == value) return string.Empty;
            recording = value;
            if (value)
            {
                resumeGesture = SelectedGesture;
                ReleaseKey();
                return string.Empty;
            }
            string error = string.Empty;
            if (resumeGesture != null) TryChange(resumeGesture, out error);
            return error;
        }

        private void ReleaseKey()
        {
            if (registered) UnregisterHotKey(handle, hotkeyId);
            if (f12Listener != null) f12Listener.Dispose();
            registered = false;
            f12Listener = null;
            Shortcut = string.Empty;
        }

        private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (!disposed && message == 0x0312 && wParam.ToInt32() == hotkeyId && Shortcut.Length > 0)
            {
                handled = true;
                Toggle();
            }
            return IntPtr.Zero;
        }

        private void Toggle()
        {
            if (IsHidden)
            {
                for (int i = hiddenWindows.Count - 1; i >= 0; i--)
                {
                    HiddenWindow window = hiddenWindows[i];
                    uint processId;
                    if (GetWindowThreadProcessId(window.Handle, out processId) == threadId)
                        ShowWindow(window.Handle, window.Minimized ? 7 : 8);
                }
                hiddenWindows.Clear();
                IsHidden = false;
                if (IsWindowVisible(activeWindow) && IsWindowEnabled(activeWindow))
                    SetForegroundWindow(activeWindow);
            }
            else
            {
                activeWindow = GetActiveWindow();
                hiddenWindows.Clear();
                // Capture before hiding an owner, since Windows also hides its owned windows.
                EnumThreadWindows(threadId, delegate(IntPtr hwnd, IntPtr state)
                {
                    if (IsWindowVisible(hwnd))
                        hiddenWindows.Add(new HiddenWindow { Handle = hwnd, Minimized = IsIconic(hwnd) });
                    return true;
                }, IntPtr.Zero);
                IsHidden = true;
                // Native hiding preserves WPF ShowDialog's modal loop and unsaved edits.
                foreach (HiddenWindow window in hiddenWindows) ShowWindow(window.Handle, 0);
            }
            if (VisibilityChanged != null) VisibilityChanged(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ReleaseKey();
            if (!source.IsDisposed) source.RemoveHook(WindowMessage);
        }

        private sealed class HiddenWindow
        {
            public IntPtr Handle;
            public bool Minimized;
        }

        private delegate bool EnumWindowCallback(IntPtr hwnd, IntPtr state);
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint threadId, EnumWindowCallback callback, IntPtr state);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
    }

    // F12 is reserved by RegisterHotKey. Keep its hook on a dedicated message loop
    // so slow UI work cannot time it out. Only the selected F12 chord is consumed.
    internal sealed class F12Listener : IDisposable
    {
        private readonly Thread thread;
        private readonly Action pressed;
        private readonly HookCallback callback;
        private readonly ManualResetEvent ready = new ManualResetEvent(false);
        private Dispatcher dispatcher;
        private IntPtr hook;
        private bool keyDown;
        private readonly uint modifiers;
        public bool Available { get; private set; }

        public F12Listener(Action action) : this(action, 0) { }

        public F12Listener(Action action, uint modifiers)
        {
            this.modifiers = modifiers;
            pressed = action;
            callback = OnKey;
            thread = new Thread(Run) { IsBackground = true, Name = "F12 hide shortcut" };
            thread.Start();
            ready.WaitOne();
            ready.Close();
        }

        private void Run()
        {
            Mutex ownership = null;
            bool owned = false;
            try
            {
                dispatcher = Dispatcher.CurrentDispatcher;
                ownership = new Mutex(false, "Local\\NihongoDeskMemo.F12GlobalHide" + (modifiers == 0 ? "" : "." + modifiers));
                try { owned = ownership.WaitOne(0); }
                catch (AbandonedMutexException) { owned = true; }
                if (owned) hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
                Available = hook != IntPtr.Zero;
            }
            catch { Available = false; }
            finally { ready.Set(); }
            try { if (Available) Dispatcher.Run(); }
            finally
            {
                if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
                if (owned) ownership.ReleaseMutex();
                if (ownership != null) ownership.Close();
            }
        }

        private IntPtr OnKey(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 && Marshal.ReadInt32(data) == 0x7B)
            {
                int kind = message.ToInt32();
                if (kind == 0x0101 || kind == 0x0105)
                {
                    if (keyDown) { keyDown = false; return new IntPtr(1); }
                }
                else if (kind == 0x0100 || kind == 0x0104)
                {
                    if (keyDown) return new IntPtr(1);
                    uint current = (Held(0x12) ? 1u : 0) | (Held(0x11) ? 2u : 0) |
                        (Held(0x10) ? 4u : 0) | ((Held(0x5B) || Held(0x5C)) ? 8u : 0);
                    if (current == modifiers)
                    {
                        keyDown = true;
                        pressed();
                        return new IntPtr(1);
                    }
                }
            }
            return CallNextHookEx(hook, code, message, data);
        }

        private static bool Held(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }

        public void Dispose()
        {
            if (!thread.IsAlive) return;
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            thread.Join();
        }

        private delegate IntPtr HookCallback(int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int type, HookCallback callback, IntPtr module, uint threadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string name);
    }
}
