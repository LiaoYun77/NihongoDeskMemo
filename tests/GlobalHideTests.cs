using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace NihongoDeskMemoWpf
{
    internal static class GlobalHideTests
    {
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint Type; public INPUTDATA Data; }
        [StructLayout(LayoutKind.Explicit)] private struct INPUTDATA
        {
            [FieldOffset(0)] public KEYBDINPUT Keyboard;
            [FieldOffset(0)] public MOUSEINPUT Mouse;
        }
        [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT
        { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT
        { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--app") { TestApp(); return; }
            Window main = new Window { Title = "Global hide test", Width = 180, Height = 100,
                ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
            GlobalHideHotkey hotkey = null;
            try
            {
                main.Show();
                IntPtr handle = new WindowInteropHelper(main).Handle;
                hotkey = new GlobalHideHotkey(main);
                Check(hotkey.Shortcut.Length > 0, "hotkey registered");
                Check(GetForegroundWindow() != handle, "test window is in background");
                Press(hotkey.Shortcut);
                Check(hotkey.IsHidden && !IsWindowVisible(handle), "global key hides background window");
                Press(hotkey.Shortcut);
                Check(!hotkey.IsHidden && IsWindowVisible(handle), "global key restores window");

                bool returned = false;
                TextBox edit = new TextBox { Text = "unsaved kana" };
                Window dialog = new Window { Owner = main, Content = edit, Width = 180, Height = 100,
                    Left = -10000, Top = -10000 };
                Exception modalFailure = null;
                dialog.Loaded += delegate
                {
                    dialog.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        try
                        {
                            IntPtr dialogHandle = new WindowInteropHelper(dialog).Handle;
                            Press(hotkey.Shortcut);
                            Check(!returned, "hiding does not finish ShowDialog");
                            Check(!IsWindowVisible(handle) && !IsWindowVisible(dialogHandle), "all windows hidden");
                            Press(hotkey.Shortcut);
                            Check(IsWindowVisible(handle) && IsWindowVisible(dialogHandle), "all windows restored");
                            Check(!IsWindowEnabled(handle), "owner stays disabled while modal dialog is open");
                            Check(edit.Text == "unsaved kana", "unsaved edit preserved");
                        }
                        catch (Exception ex) { modalFailure = ex; }
                        finally { dialog.Close(); }
                    }));
                };
                dialog.ShowDialog();
                returned = true;
                if (modalFailure != null) throw modalFailure;
                hotkey.Dispose();
                hotkey = null;

                // Reserve both choices to exercise fallback and complete registration failure.
                Check(RegisterHotKey(handle, 9001, 0x4000, 0x79), "released F10 can be reserved");
                try
                {
                    using (GlobalHideHotkey fallback = new GlobalHideHotkey(main))
                        Check(fallback.Shortcut == "Ctrl+Alt+F10", "occupied F10 uses fallback");
                    Check(RegisterHotKey(handle, 9002, 0x4003, 0x79), "released fallback can be reserved");
                    try
                    {
                        using (GlobalHideHotkey unavailable = new GlobalHideHotkey(main))
                            Check(unavailable.Shortcut == string.Empty, "conflicts are reported, not silently ignored");
                    }
                    finally { UnregisterHotKey(handle, 9002); }
                }
                finally { UnregisterHotKey(handle, 9001); }
                Console.WriteLine("Global hide tests passed.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { if (hotkey != null) hotkey.Dispose(); main.Close(); }
        }

        private static void Press(string shortcut)
        {
            bool modified = shortcut != "F10";
            if (modified) { SendKey(0x11, 0); SendKey(0x12, 0); }
            SendKey(0x79, 0);
            SendKey(0x79, 2);
            if (modified) { SendKey(0x12, 2); SendKey(0x11, 2); }
            Pump();
        }

        private static void Pump()
        {
            DispatcherFrame frame = new DispatcherFrame();
            DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += delegate { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        private static void TestApp()
        {
            MemoWindow main = new MemoWindow();
            try
            {
                main.ShowActivated = false;
                main.Show();
                Pump();
                GlobalHideHotkey hotkey = (GlobalHideHotkey)Field(main, "globalHideHotkey");
                DispatcherTimer timer = (DispatcherTimer)Field(main, "timer");
                IntPtr handle = new WindowInteropHelper(main).Handle;
                object word = Field(main, "currentWord");
                Check(timer.IsEnabled, "app refresh initially running");
                Press(hotkey.Shortcut);
                Check(hotkey.IsHidden && !IsWindowVisible(handle), "real app hidden");
                Check(!timer.IsEnabled, "refresh paused while hidden");
                Press(hotkey.Shortcut);
                Check(timer.IsEnabled && IsWindowVisible(handle), "app restored and refresh resumed");
                Check(object.ReferenceEquals(word, Field(main, "currentWord")), "same word preserved");
                typeof(MemoWindow).GetMethod("HideClick", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(main, new object[] { main, new RoutedEventArgs() });
                double width = main.Width;
                Press(hotkey.Shortcut);
                Check(!IsWindowVisible(handle), "pseudo-hidden show button also disappears");
                Press(hotkey.Shortcut);
                Check(main.Width == width && (bool)Field(main, "pseudoHidden"), "pseudo-hidden state preserved");
                Console.WriteLine("App integration tests passed.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { main.Close(); }
        }

        private static object Field(MemoWindow window, string name)
        {
            return typeof(MemoWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        }

        private static void SendKey(ushort key, uint flags)
        {
            INPUT input = new INPUT { Type = 1, Data = new INPUTDATA { Keyboard = new KEYBDINPUT { Key = key, Flags = flags } } };
            if (SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT))) != 1)
                throw new InvalidOperationException("SendInput failed: " + Marshal.GetLastWin32Error());
        }

        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS: " + message);
        }
    }
}
