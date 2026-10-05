using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NihongoDeskMemoWpf
{
    internal sealed class GlobalHideHotkey : IDisposable
    {
        private const int HotkeyId = 0x4E48;
        private readonly IntPtr handle;
        private readonly uint threadId;
        private readonly HwndSource source;
        private readonly List<HiddenWindow> hiddenWindows = new List<HiddenWindow>();
        private IntPtr activeWindow;
        private bool disposed;

        public string Shortcut { get; private set; }
        public bool IsHidden { get; private set; }
        public event EventHandler VisibilityChanged;

        public GlobalHideHotkey(Window window)
        {
            handle = new WindowInteropHelper(window).EnsureHandle();
            threadId = GetCurrentThreadId();
            source = HwndSource.FromHwnd(handle);
            source.AddHook(WindowMessage);
            // MOD_NOREPEAT keeps a held key from immediately showing the windows again.
            if (RegisterHotKey(handle, HotkeyId, 0x4000, 0x79)) Shortcut = "F10";
            else if (RegisterHotKey(handle, HotkeyId, 0x4003, 0x79)) Shortcut = "Ctrl+Alt+F10";
            else Shortcut = string.Empty;
        }

        private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == 0x0312 && wParam.ToInt32() == HotkeyId && Shortcut.Length > 0)
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
            if (Shortcut.Length > 0) UnregisterHotKey(handle, HotkeyId);
            if (!source.IsDisposed) source.RemoveHook(WindowMessage);
        }

        private sealed class HiddenWindow
        {
            public IntPtr Handle;
            public bool Minimized;
        }

        private delegate bool EnumWindowCallback(IntPtr hwnd, IntPtr state);
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
}
