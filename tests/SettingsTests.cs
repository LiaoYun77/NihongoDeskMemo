using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal static class SettingsTests
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                AppConfig config = new AppConfig { PracticeUnit = "第01课" };
                WordDatabase database = new WordDatabase();
                for (int i = 1; i <= 48; i++) database.Words.Add(new WordItem { Unit = "第" + i.ToString("00") + "课" });
                WpfSettingsWindow window = new WpfSettingsWindow(config, database, null, null, null);
                ComboBox hotkeys = (ComboBox)Field(window, "hideHotkeyBox");
                Check(hotkeys.Items.Count == 3 && (string)hotkeys.SelectedItem == "F10", "default and available hide keys");
                hotkeys.SelectedItem = "F12";
                Check(config.HideHotkey == 10, "changing selection without saving does not modify config");
                UnitSelectionControl control = (UnitSelectionControl)Field(window, "unitSelection");
                Check(!control.IsAll && control.SelectedUnits()[0] == "第01课", "legacy selection displayed");
                List<CheckBox> boxes = (List<CheckBox>)Field(control, "boxes");
                boxes[2].IsChecked = true;
                boxes[47].IsChecked = true;
                database.Words.Add(new WordItem { Unit = "很长的自定义单元名称用于检查文字换行与边界" });
                control.RefreshUnits(database);
                Check(control.SelectedUnits().Count == 3, "unsaved selection survives library refresh");
                FrameworkElement content = (FrameworkElement)window.Content;
                content.Measure(new Size(560, 660));
                content.Arrange(new Rect(0, 0, 560, 660));
                content.UpdateLayout();
                Check(control.ActualWidth > 200 && control.ActualHeight >= 154, "selection panel has stable dimensions");
                if (args.Length > 0)
                {
                    RenderTargetBitmap bitmap = new RenderTargetBitmap(560, 660, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(content);
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (Stream stream = File.Create(args[0])) encoder.Save(stream);
                }
                ((CheckBox)Field(control, "allBox")).IsChecked = true;
                Check(control.IsAll, "all-units toggle works");
                ((CheckBox)Field(control, "allBox")).IsChecked = false;
                Check(control.SelectedUnits().Count == 3, "toggling all retains manual choices");
                object[] saveArgs = new object[] { null };
                MethodInfo save = typeof(WpfSettingsWindow).GetMethod("TrySaveSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                window.ApplyHideHotkey = delegate(int key) { return "occupied"; };
                Check(!(bool)save.Invoke(window, saveArgs) && config.HideHotkey == 10, "failed registration does not save selected key");
                int applied = 0;
                window.ApplyHideHotkey = delegate(int key) { applied = key; return string.Empty; };
                Check((bool)save.Invoke(window, saveArgs) && config.HideHotkey == 12 && applied == 12, "save applies and stores F12");
                window.Close();

                MemoWindow main = new MemoWindow { Left = 0, Top = 0 };
                try
                {
                    AppConfig appConfig = (AppConfig)Field(main, "config");
                    WordDatabase data = (WordDatabase)Field(main, "database");
                    data.Words.Clear();
                    data.Words.Add(new WordItem { Japanese = "one", Unit = "A" });
                    data.Words.Add(new WordItem { Japanese = "two", Unit = "B" });
                    data.Words.Add(new WordItem { Japanese = "three", Unit = "C" });
                    PracticeScope.SetUnits(appConfig, new string[] { "A", "C" });
                    appConfig.ReviewMode = 0;
                    HashSet<string> seen = new HashSet<string>();
                    MethodInfo pick = typeof(MemoWindow).GetMethod("PickDueWord", BindingFlags.NonPublic | BindingFlags.Instance);
                    for (int i = 0; i < 300; i++)
                    {
                        WordItem chosen = (WordItem)pick.Invoke(main, null);
                        Check(chosen.Unit == "A" || chosen.Unit == "C", "random selection excludes unselected units");
                        seen.Add(chosen.Unit);
                    }
                    Check(seen.Count == 2, "both selected units can be drawn");
                    data.Words[0].NextReviewTime = DateTime.UtcNow.AddDays(2).ToString("o");
                    data.Words[2].NextReviewTime = DateTime.UtcNow.AddDays(3).ToString("o");
                    Check(pick.Invoke(main, null) == null, "no fallback to unselected units when selected ones are not due");
                }
                finally { main.Close(); }
                Console.WriteLine("Settings and multi-unit sampling tests passed.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        }

        private static object Field(object instance, string name)
        {
            return instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
