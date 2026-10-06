using System;
using System.Windows.Input;

namespace NihongoDeskMemoWpf
{
    internal sealed class HotkeyGesture
    {
        public int VirtualKey { get; private set; }
        public uint Modifiers { get; private set; }
        public string DisplayName { get; private set; }

        private HotkeyGesture(int key, uint modifiers)
        {
            VirtualKey = key;
            Modifiers = modifiers;
            string name = KeyInterop.KeyFromVirtualKey(key).ToString();
            if (key >= 0x30 && key <= 0x39) name = ((char)key).ToString();
            DisplayName = ((modifiers & 2) != 0 ? "Ctrl+" : "") +
                ((modifiers & 1) != 0 ? "Alt+" : "") + ((modifiers & 4) != 0 ? "Shift+" : "") + name;
        }

        public static bool TryCreate(Key key, ModifierKeys modifiers, out HotkeyGesture result, out string error)
        {
            result = null;
            error = string.Empty;
            int vk = KeyInterop.VirtualKeyFromKey(key);
            if (vk < 8 || vk > 254 || key == Key.None || key == Key.System || key == Key.ImeProcessed ||
                vk == 0x10 || vk == 0x11 || vk == 0x12 || (vk >= 0xA0 && vk <= 0xA5) || vk == 0x5B || vk == 0x5C)
                error = "请按一个主键，可同时按 Ctrl、Alt、Shift。";
            else if (((uint)modifiers & ~7u) != 0 ||
                ((modifiers & ModifierKeys.Alt) != 0 && (key == Key.Tab || key == Key.F4 || key == Key.Escape)) ||
                ((modifiers & ModifierKeys.Control) != 0 && key == Key.Escape) ||
                ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == (ModifierKeys.Control | ModifierKeys.Alt) && key == Key.Delete))
                error = "该组合为系统保留快捷键，请换一个。";
            else if (key == Key.Escape && modifiers == ModifierKeys.None)
                error = "Esc 用于取消录入，请换一个按键。";
            if (error.Length > 0) return false;
            result = new HotkeyGesture(vk, (uint)modifiers);
            return true;
        }

        public static HotkeyGesture FunctionKey(int number)
        {
            return new HotkeyGesture(0x6F + GlobalHideHotkey.NormalizeKey(number), 0);
        }

        public static HotkeyGesture FromConfig(NihongoDeskMemo.AppConfig config)
        {
            HotkeyGesture result;
            string error;
            if (config.HideHotkeyVirtualKey >= 8 && config.HideHotkeyVirtualKey <= 254 &&
                TryCreate(KeyInterop.KeyFromVirtualKey(config.HideHotkeyVirtualKey),
                    (ModifierKeys)config.HideHotkeyModifiers, out result, out error)) return result;
            return FunctionKey(config.HideHotkey);
        }

        public void SaveTo(NihongoDeskMemo.AppConfig config)
        {
            config.HideHotkeyVirtualKey = VirtualKey;
            config.HideHotkeyModifiers = (int)Modifiers;
            if (Modifiers == 0 && VirtualKey >= 0x79 && VirtualKey <= 0x7B) config.HideHotkey = VirtualKey - 0x6F;
        }

        public bool SameAs(HotkeyGesture other)
        {
            return other != null && VirtualKey == other.VirtualKey && Modifiers == other.Modifiers;
        }
    }
}
