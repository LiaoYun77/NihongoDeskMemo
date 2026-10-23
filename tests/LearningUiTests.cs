using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal static class LearningUiTests
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string file = Path.Combine(Path.GetTempPath(), "NihongoPreview-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                WordDatabase data = new WordDatabase();
                WordItem word = new WordItem { Japanese = "会社員", Pinyin = "かいしゃいん", Chinese = "公司职员", Unit = "第01课" };
                data.Words.Add(word);
                File.WriteAllText(file, "会社員,かいしゃいん,公司职员\n会議,かいぎ,会议\n会議,かいぎ,重复\n,かな,缺少单词");
                ImportPreviewWindow preview = new ImportPreviewWindow(file, data);
                Check(!((Button)Field(preview, "confirm")).IsEnabled && preview.Plan.Errors == 1, "invalid preview cannot confirm");
                Render(preview, 960, 580, args, "import-preview-errors.png");
                preview.Close();
                Check(data.Words.Count == 1 && word.Chinese == "公司职员", "closing preview never applies candidate");
                File.WriteAllText(file, "会社員,かいしゃいん,公司职员\n会議,かいぎ,会议");
                preview = new ImportPreviewWindow(file, data);
                Check(((Button)Field(preview, "confirm")).IsEnabled, "valid preview enables confirmation");
                Render(preview, 680, 380, args, "import-preview-compact.png"); preview.Close();
                ReviewData.Normalize(data);
                ReviewActions.Rate(data, word, 0, 3, DateTime.UtcNow);
                ReviewSnapshot undo = new ReviewSnapshot(data, word);
                ReviewActions.Rate(data, word, 1, 1, DateTime.UtcNow);
                undo.Undo();
                ReviewHistoryWindow history = new ReviewHistoryWindow(data, word);
                DataGrid events = (DataGrid)Field(history, "events");
                Check(events.Items.Count == 2, "history shows valid and undone events");
                ((ComboBox)Field(history, "direction")).SelectedIndex = 2;
                Check(events.Items.Count == 1, "history filters the selected direction");
                ((ComboBox)Field(history, "direction")).SelectedIndex = 0;
                Render(history, 940, 610, args, "review-history.png");
                Render(history, 720, 440, args, "review-history-compact.png"); history.Close();
                WpfLibraryWindow library = new WpfLibraryWindow(data);
                Render(library, 760, 440, args, "library-compact.png"); library.Close();
                word.ExampleJapanese = "私は会社員です。\n日本語を勉強しています。";
                word.ExampleChinese = "我是公司职员。\n我正在学习日语。";
                word.SourceLabel = "自编例句";
                word.SourceText = new string('原', 600);
                WordDetailsWindow details = new WordDetailsWindow(word);
                Render(details, 660, 520, args, "word-details.png");
                Render(details, 420, 330, args, "word-details-compact.png"); details.Close();
                LearningStatisticsWindow stats = new LearningStatisticsWindow(data, new AppConfig());
                Check(((DataGrid)Field(stats, "weakWords")).Items.Count == 0, "undone error does not create a weak word");
                stats.Close(); word.Marked = true;
                stats = new LearningStatisticsWindow(data, new AppConfig());
                Check(((DataGrid)Field(stats, "weakWords")).Items.Count > 0, "weak list contains marked word");
                ((ComboBox)Field(stats, "direction")).SelectedIndex = 3;
                Render(stats, 900, 560, args, "statistics.png");
                Render(stats, 620, 380, args, "statistics-compact.png"); stats.Close();
                AppConfig config = new AppConfig { SpeechRate = 2 };
                WpfSettingsWindow settings = new WpfSettingsWindow(config, data, null, null, null);
                ((Slider)Field(settings, "speechRateSlider")).Value = -3;
                Render(settings, 560, 660, args, "settings-speech.png");
                ScrollViewer scroll = FindScroll((DependencyObject)settings.Content);
                scroll.ScrollToEnd();
                Render(settings, 560, 500, args, "settings-speech-compact.png");
                settings.Close();
                Check(config.SpeechRate == 2, "closing speech settings discards draft");
                settings = new WpfSettingsWindow(config, data, null, null, null);
                ((Slider)Field(settings, "speechRateSlider")).Value = -3;
                string error = null;
                object[] saveArgs = { error };
                bool saved = (bool)typeof(WpfSettingsWindow).GetMethod("TrySaveSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(settings, saveArgs);
                Check(saved && config.SpeechRate == -3, "saving commits speech rate"); settings.Close();
                Console.WriteLine("Import preview and review history UI tests passed.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { if (File.Exists(file)) File.Delete(file); }
        }

        private static void Render(Window window, int width, int height, string[] args, string name)
        {
            window.Width = width; window.Height = height;
            FrameworkElement root = (FrameworkElement)window.Content;
            root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
            if (args.Length == 0) return;
            Directory.CreateDirectory(args[0]);
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (Stream stream = File.Create(Path.Combine(args[0], name))) encoder.Save(stream);
        }

        private static object Field(object value, string name)
        { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }
        private static ScrollViewer FindScroll(DependencyObject parent)
        {
            ScrollViewer found = parent as ScrollViewer;
            if (found != null) return found;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            { found = FindScroll(VisualTreeHelper.GetChild(parent, i)); if (found != null) return found; }
            return null;
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
