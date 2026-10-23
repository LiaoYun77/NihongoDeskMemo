using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
                HotkeyRecorder hotkeys = (HotkeyRecorder)Field(window, "hideHotkeyBox");
                Check(hotkeys.Gesture.DisplayName == "F10", "default recorded shortcut");
                int recordingChanges = 0;
                window.RecordingChanged = delegate(bool value) { recordingChanges += value ? 1 : -1; return string.Empty; };
                hotkeys.BeginRecording();
                Check(recordingChanges == 1, "recording suspends global key");
                Check(!hotkeys.Capture(Key.LeftCtrl, ModifierKeys.Control), "modifier alone is not recorded");
                Check(hotkeys.Capture(Key.X, ModifierKeys.Control | ModifierKeys.Shift), "record arbitrary combination");
                hotkeys.Capture(Key.Escape, ModifierKeys.None);
                Check(hotkeys.Gesture.DisplayName == "F10" && recordingChanges == 0, "Esc cancels and restores registration");
                hotkeys.BeginRecording();
                hotkeys.Capture(Key.Q, ModifierKeys.Control | ModifierKeys.Alt);
                hotkeys.EndRecording();
                Check(hotkeys.Text == "Ctrl+Alt+Q" && recordingChanges == 0, "recorded chord displayed and capture ends");
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
                int renderHeight = args.Length > 1 ? int.Parse(args[1]) : 660;
                window.Height = renderHeight;
                content.Measure(new Size(560, renderHeight));
                content.Arrange(new Rect(0, 0, 560, renderHeight));
                content.UpdateLayout();
                Check(control.ActualWidth > 200 && control.ActualHeight >= 154, "selection panel has stable dimensions");
                if (args.Length > 0)
                {
                    RenderTargetBitmap bitmap = new RenderTargetBitmap(560, renderHeight, 96, 96, PixelFormats.Pbgra32);
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
                window.ApplyHideHotkey = delegate(HotkeyGesture key) { return "occupied"; };
                Check(!(bool)save.Invoke(window, saveArgs) && config.HideHotkey == 10, "failed registration does not save selected key");
                string applied = string.Empty;
                window.ApplyHideHotkey = delegate(HotkeyGesture key) { applied = key.DisplayName; return string.Empty; };
                ((ComboBox)Field(window, "studyModeBox")).SelectedIndex = 1;
                Check((bool)save.Invoke(window, saveArgs) && HotkeyGesture.FromConfig(config).DisplayName == "Ctrl+Alt+Q" && applied == "Ctrl+Alt+Q", "save applies and stores custom chord");
                Check(config.StudyMode == 1, "study mode saved independently of question direction");
                window.Close();
                TestPracticePresets(database);

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
                    appConfig.StudyMode = 0;
                    HashSet<string> seen = new HashSet<string>();
                    MethodInfo pick = typeof(MemoWindow).GetMethod("PickDueWord", BindingFlags.NonPublic | BindingFlags.Instance);
                    for (int i = 0; i < 300; i++)
                    {
                        WordItem chosen = (WordItem)pick.Invoke(main, null);
                        Check(chosen.Unit == "A" || chosen.Unit == "C", "random selection excludes unselected units");
                        seen.Add(chosen.Unit);
                    }
                    Check(seen.Count == 2, "both selected units can be drawn");
                    ReviewData.For(data.Words[0], 0).NextReviewTime = DateTime.UtcNow.AddDays(2).ToString("o");
                    ReviewData.For(data.Words[2], 0).NextReviewTime = DateTime.UtcNow.AddDays(3).ToString("o");
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

        private static void TestPracticePresets(WordDatabase database)
        {
            AppConfig config = new AppConfig { ReviewMode = 2, StudyMode = 1 };
            config.PracticePresets.Add(new PracticePreset { Name = "基础", Units = new string[] { "第01课", "第02课" } });
            PracticeScope.SetUnits(config, new string[] { "第48课" });
            WpfSettingsWindow window = new WpfSettingsWindow(config, database, null, null, null);
            UnitSelectionControl units = (UnitSelectionControl)Field(window, "unitSelection");
            MethodInfo savePreset = typeof(WpfSettingsWindow).GetMethod("TrySavePreset", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(savePreset != null, "named practice presets can be saved from settings");
            ComboBox presets = (ComboBox)Field(window, "presetBox");
            TextBox name = (TextBox)Field(window, "presetNameBox");
            presets.SelectedIndex = 1;
            Check(units.SelectedUnits().Count == 2 && units.SelectedUnits()[0] == "第01课", "applying preset loads its units");
            Check(config.ReviewMode == 2 && config.StudyMode == 1 && PracticeScope.GetUnits(config)[0] == "第48课",
                "preset application does not mutate live config or modes");
            units.SelectUnits(database, new string[] { "第03课", "第05课" });
            Check(presets.SelectedIndex == 0 && config.PracticePresets[0].Units[0] == "第01课",
                "custom edits leave the saved preset unchanged");
            name.Text = "重命名基础";
            object[] saveArgs = new object[] { null };
            Check((bool)savePreset.Invoke(window, saveArgs), "rename and update preset in draft");
            Check(config.PracticePresets[0].Name == "基础", "draft rename is isolated");
            window.Close();
            Check(config.PracticePresets.Count == 1 && config.PracticePresets[0].Name == "基础" &&
                PracticeScope.GetUnits(config)[0] == "第48课", "closing without save discards preset and scope edits");

            window = new WpfSettingsWindow(config, database, null, null, null);
            try
            {
                units = (UnitSelectionControl)Field(window, "unitSelection");
                presets = (ComboBox)Field(window, "presetBox");
                name = (TextBox)Field(window, "presetNameBox");
                units.SelectUnits(database, new string[] { "第03课", "第05课" });
                name.Text = "   ";
                Check(!(bool)savePreset.Invoke(window, saveArgs), "blank preset name rejected");
                name.Text = new string('x', 61);
                Check(!(bool)savePreset.Invoke(window, saveArgs), "oversized preset name rejected");
                name.Text = "基础";
                Check(!(bool)savePreset.Invoke(window, saveArgs), "duplicate preset name rejected");
                name.Text = "周末";
                Check((bool)savePreset.Invoke(window, saveArgs), "create multi-unit preset");
                List<PracticePreset> drafts = (List<PracticePreset>)Field(window, "presetDraft");
                Check(drafts.Count == 2, "new preset added once");
                presets.SelectedIndex = 1;
                name.Text = "周末";
                Check(!(bool)savePreset.Invoke(window, saveArgs) && drafts[0].Name == "基础", "rename collision preserves existing preset");
                name.Text = "基础复习";
                Check((bool)savePreset.Invoke(window, saveArgs) && drafts[0].Name == "基础复习", "rename preserves preset ordering");
                presets.SelectedIndex = 2;
                typeof(WpfSettingsWindow).GetMethod("DeletePreset", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                Check(drafts.Count == 1 && units.SelectedUnits().Contains("第03课"), "deleting preset keeps current units");
                units.SelectUnits(database, new string[0]);
                name.Text = "全部";
                Check((bool)savePreset.Invoke(window, saveArgs) && drafts[1].Units.Length == 0, "all-units preset is explicit");
                presets.SelectedIndex = 1;
                presets.SelectedIndex = 2;
                Check(units.IsAll, "applying all-units preset enables the all toggle");
                presets.SelectedIndex = 0;
                units.SelectUnits(database, new string[] { "已删除课程" });
                name.Text = "保留范围";
                Check((bool)savePreset.Invoke(window, saveArgs), "missing unit can remain in preset");
                Check(!units.IsAll && units.SelectedUnits()[0] == "已删除课程", "missing unit is never expanded to all");
                MethodInfo saveSettings = typeof(WpfSettingsWindow).GetMethod("TrySaveSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                Check((bool)saveSettings.Invoke(window, saveArgs), "save settings commits preset draft");
                Check(config.PracticePresets.Count == 3 && config.PracticePresets[0].Name == "基础复习", "preset changes copied to config");
                drafts[0].Units[0] = "edited after save";
                Check(config.PracticePresets[0].Units[0] == "第01课", "saved preset arrays are deep copies");
                WordItem word = new WordItem { Japanese = "word", Pinyin = "ことば", Unit = "第01课" };
                Check(!StudySession.Eligible(word, config), "missing scope does not sample unrelated units");
                CheckPresetXml(config);
            }
            finally { window.Close(); }
        }

        private static void CheckPresetXml(AppConfig config)
        {
            string directory = Path.Combine(Path.GetTempPath(), "NihongoPresetTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "config.xml");
                XmlFileStore.Save(path, config);
                AppConfig reloaded = XmlFileStore.Load<AppConfig>(path, null);
                Check(reloaded.PracticePresets.Count == 3 && reloaded.PracticePresets[2].Units[0] == "已删除课程",
                    "presets survive XML round trip");
                File.WriteAllText(path, "<AppConfig><PracticeUnit>旧课程</PracticeUnit></AppConfig>");
                AppConfig legacy = XmlFileStore.Load<AppConfig>(path, null);
                Check(legacy.PracticePresets.Count == 0 && PracticeScope.GetUnits(legacy)[0] == "旧课程",
                    "legacy XML without presets keeps its original scope");
            }
            finally { Directory.Delete(directory, true); }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
