using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
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
            if (args.Length > 0 && args[0] == "--registration") { TestRegistration(); return; }
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

                string error;
                Check(hotkey.TryChange(11, out error) && hotkey.Shortcut == "F11", "switch to F11 immediately");
                Check(RegisterHotKey(handle, 9003, 0x4000, 0x79), "old F10 released after switch");
                try { Press("F10"); Check(!hotkey.IsHidden, "old key no longer hides window"); }
                finally { UnregisterHotKey(handle, 9003); }
                Press("F11");
                Check(hotkey.IsHidden, "F11 hides from background");
                Press("F11");
                Check(!hotkey.IsHidden, "F11 restores");
                Check(hotkey.TryChange(12, out error) && hotkey.Shortcut == "F12", "switch to F12 listener");
                using (F12Listener duplicate = new F12Listener(delegate { }))
                    Check(!duplicate.Available, "second F12 listener cannot steal this app's shortcut");
                Check(RegisterHotKey(handle, 9003, 0x4000, 0x7A), "old F11 released after switch");
                try
                {
                    Check(!hotkey.TryChange(11, out error) && error.Length > 0 && hotkey.Shortcut == "F12",
                        "occupied new key preserves F12");
                    Press("F11");
                    Check(!hotkey.IsHidden, "failed switch does not activate new key");
                }
                finally { UnregisterHotKey(handle, 9003); }
                SendKey(0x7B, 0); Pump();
                Check(hotkey.IsHidden, "F12 hides from background");
                SendKey(0x7B, 0); Pump();
                Check(hotkey.IsHidden, "holding F12 does not restore repeatedly");
                SendKey(0x7B, 2); Pump();
                Press("F12");
                Check(!hotkey.IsHidden, "F12 restores after release");

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
                Check(hotkey.TryChange(10, out error), "switch away from F12 to F10");
                using (F12Listener released = new F12Listener(delegate { }))
                    Check(released.Available, "F12 listener released when switching away");
                Press("F10");
                Check(hotkey.IsHidden, "F10 hides window again");
                Press("F10");
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
            string[] parts = shortcut.Split('+');
            ushort key = (ushort)KeyInterop.VirtualKeyFromKey((Key)Enum.Parse(typeof(Key), parts[parts.Length - 1]));
            bool ctrl = shortcut.Contains("Ctrl+");
            bool alt = shortcut.Contains("Alt+");
            bool shift = shortcut.Contains("Shift+");
            try
            {
                if (ctrl) SendKey(0x11, 0);
                if (alt) SendKey(0x12, 0);
                if (shift) SendKey(0x10, 0);
                SendKey(key, 0);
                Pump();
            }
            finally
            {
                SendKey(key, 2);
                if (shift) SendKey(0x10, 2);
                if (alt) SendKey(0x12, 2);
                if (ctrl) SendKey(0x11, 2);
            }
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
                NihongoDeskMemo.AppConfig config = (NihongoDeskMemo.AppConfig)Field(main, "config");
                HotkeyGesture.FunctionKey(12).SaveTo(config);
                main.ShowActivated = false;
                main.Show();
                Pump();
                GlobalHideHotkey hotkey = (GlobalHideHotkey)Field(main, "globalHideHotkey");
                Check(hotkey.Shortcut == "F12", "app startup honors configured F12");
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
                ExerciseSettings(main, "F11", false);
                Check(config.HideHotkey == 12 && hotkey.Shortcut == "F12", "closing settings without save preserves key");
                ExerciseSettings(main, "F11", true);
                Check(config.HideHotkey == 11 && hotkey.Shortcut == "F11", "settings save applies new key immediately");
                Check(NihongoDeskMemo.Storage.LoadConfig().HideHotkey == 11, "settings save persists selected key to XML");
                Press("F11");
                Check(hotkey.IsHidden, "newly saved F11 hides app");
                Press("F11");
                Check(!hotkey.IsHidden, "newly saved F11 restores app");
                ExerciseSettings(main, "Ctrl+Shift+Q", true);
                Check(HotkeyGesture.FromConfig(NihongoDeskMemo.Storage.LoadConfig()).DisplayName == "Ctrl+Shift+Q", "custom chord persists to XML");
                Press("Ctrl+Shift+Q");
                Check(hotkey.IsHidden, "saved custom chord hides app");
                Press("Ctrl+Shift+Q");
                Check(!hotkey.IsHidden, "saved custom chord restores app");
                Console.WriteLine("App integration tests passed.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { main.Close(); }
        }

        private static void ExerciseSettings(MemoWindow main, string key, bool save)
        {
            Exception failure = null;
            main.Dispatcher.BeginInvoke(new Action(delegate
            {
                WpfSettingsWindow settings = null;
                try
                {
                    foreach (Window owned in main.OwnedWindows)
                        if (owned is WpfSettingsWindow) settings = (WpfSettingsWindow)owned;
                    Check(settings != null, "real settings dialog opened");
                    HotkeyRecorder choices = (HotkeyRecorder)typeof(WpfSettingsWindow).GetField("hideHotkeyBox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(settings);
                    settings.Activate();
                    choices.Focus();
                    Check(choices.IsKeyboardFocused && choices.IsRecording, "focusing recorder starts capture");
                    Press(key);
                    Check(choices.Gesture.DisplayName == key, "real keyboard input recorded correctly");
                    if (save) typeof(WpfSettingsWindow).GetMethod("SaveClick", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(settings, new object[] { settings, new RoutedEventArgs() });
                    else settings.Close();
                }
                catch (Exception ex) { failure = ex; if (settings != null) settings.Close(); }
            }));
            typeof(MemoWindow).GetMethod("SettingsClick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(main, new object[] { main, new RoutedEventArgs() });
            if (failure != null) throw failure;
        }

        private static object Field(MemoWindow window, string name)
        {
            return typeof(MemoWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        }

        private static void TestRegistration()
        {
            Window window = new Window();
            GlobalHideHotkey hotkey = null;
            try
            {
                HotkeyGesture first, second, f12;
                string error;
                ModifierKeys mods = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift;
                HotkeyGesture.TryCreate(Key.Q, mods, out first, out error);
                HotkeyGesture.TryCreate(Key.K, mods, out second, out error);
                HotkeyGesture.TryCreate(Key.F12, ModifierKeys.Control, out f12, out error);
                hotkey = new GlobalHideHotkey(window, first);
                IntPtr handle = new WindowInteropHelper(window).Handle;
                Check(hotkey.Shortcut == "Ctrl+Alt+Shift+Q", "native arbitrary chord registration");
                Check(hotkey.SetRecording(true).Length == 0 && hotkey.Shortcut.Length == 0, "recording suspends shortcut");
                Check(RegisterHotKey(handle, 9100, 0x4000 | first.Modifiers, (uint)first.VirtualKey), "recording releases native key for capture");
                try { Check(hotkey.SetRecording(false).Length > 0, "resume conflict is reported"); }
                finally { UnregisterHotKey(handle, 9100); }
                Check(hotkey.TryChange(first, out error), "restore after conflict removed");
                Check(RegisterHotKey(handle, 9100, 0x4000 | second.Modifiers, (uint)second.VirtualKey), "reserve conflicting chord");
                try { Check(!hotkey.TryChange(second, out error) && hotkey.Shortcut == first.DisplayName, "failed chord replacement retains old key"); }
                finally { UnregisterHotKey(handle, 9100); }
                Check(hotkey.TryChange(second, out error), "change arbitrary chord");
                Check(hotkey.TryChange(f12, out error) && hotkey.Shortcut == "Ctrl+F12", "F12 modifier combination supported");
                Check(hotkey.SetRecording(true).Length == 0, "F12 recording suspends hook");
                using (F12Listener released = new F12Listener(delegate { }, 2))
                    Check(released.Available, "F12 hook released during recording");
                Check(hotkey.SetRecording(false).Length == 0 && hotkey.Shortcut == "Ctrl+F12", "F12 hook resumes after recording");
                Check(hotkey.TryChange(first, out error), "switch F12 back to ordinary chord");
                Console.WriteLine("Registration tests passed (no simulated keyboard input).");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { if (hotkey != null) hotkey.Dispose(); window.Close(); }
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
