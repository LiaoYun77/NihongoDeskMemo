using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NihongoDeskMemoWpf
{
    internal sealed class HotkeyRecorder : TextBox
    {
        private HotkeyGesture beforeRecording;
        public HotkeyGesture Gesture { get; private set; }
        public bool IsRecording { get; private set; }
        public Func<bool, string> RecordingChanged { get; set; }

        public HotkeyRecorder(HotkeyGesture gesture)
        {
            Gesture = gesture;
            Text = gesture.DisplayName;
            IsReadOnly = true;
            IsUndoEnabled = false;
            ContextMenu = new ContextMenu();
            Foreground = Brushes.White;
            Background = new SolidColorBrush(Color.FromRgb(16, 24, 36));
            BorderBrush = new SolidColorBrush(Color.FromRgb(82, 97, 118));
            Padding = new Thickness(7, 5, 7, 5);
            ToolTip = "点击后按下快捷键；Esc 取消本次录入";
            InputMethod.SetIsInputMethodEnabled(this, false);
            GotKeyboardFocus += delegate { BeginRecording(); };
            LostKeyboardFocus += delegate { EndRecording(); };
            PreviewMouseLeftButtonDown += delegate
            {
                if (IsKeyboardFocused) BeginRecording();
            };
            PreviewKeyDown += KeyPressed;
        }

        public void BeginRecording()
        {
            if (IsRecording) return;
            beforeRecording = Gesture;
            IsRecording = true;
            if (RecordingChanged != null) RecordingChanged(true);
            Text = "…";
        }

        public void EndRecording()
        {
            if (!IsRecording) return;
            IsRecording = false;
            string error = RecordingChanged == null ? null : RecordingChanged(false);
            Text = Gesture.DisplayName;
            ToolTip = string.IsNullOrEmpty(error) ? "点击后按下快捷键；Esc 取消本次录入" : error;
        }

        public bool Capture(Key key, ModifierKeys modifiers)
        {
            if (!IsRecording) return false;
            if (key == Key.Escape && modifiers == ModifierKeys.None)
            {
                Gesture = beforeRecording;
                EndRecording();
                return false;
            }
            HotkeyGesture result;
            string error;
            if (!HotkeyGesture.TryCreate(key, modifiers, out result, out error))
            {
                ToolTip = error;
                Text = error;
                return false;
            }
            Gesture = result;
            Text = result.DisplayName;
            ToolTip = "保存后此快捷键将用于全局隐藏 / 恢复，可能覆盖其他程序的同名快捷键。";
            return true;
        }

        private void KeyPressed(object sender, KeyEventArgs e)
        {
            if (!IsRecording) BeginRecording();
            e.Handled = true;
            if (e.IsRepeat) return;
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.ImeProcessed) key = e.ImeProcessedKey;
            Capture(key, Keyboard.Modifiers);
        }
    }
}
