using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal static class WorkflowTests
    {
        [STAThread]
        private static void Main(string[] args)
        {
            MemoWindow window = null;
            try
            {
                window = new MemoWindow { Left = 0, Top = 0, Width = 360, Height = 330 };
                AppConfig config = (AppConfig)Field(window, "config");
                config.ReviewMode = 0; config.StudyMode = 0; config.ShowRatingButtons = true;
                config.HideMeaningUntilClick = true; config.BackgroundTransparent = false;
                PracticeScope.SetUnits(config, new string[0]);
                WordDatabase data = (WordDatabase)Field(window, "database");
                data.Words.Clear();
                data.Words.Add(new WordItem { Japanese = "会社員", Pinyin = "かいしゃいん", Chinese = "公司职员", Unit = "第01课" });
                data.Words.Add(new WordItem { Japanese = "会議", Pinyin = "かいぎ", Chinese = "会议", Unit = "第02课" });
                Call(window, "ApplyConfig"); Call(window, "ShowNextWord");
                CheckRevealClicks(window, config);
                NihongoDeskMemo.WordItem first = (WordItem)Field(window, "currentWord");
                FrameworkElement root = (FrameworkElement)window.Content;
                Layout(root, 360, 330);
                TextBlock question = (TextBlock)Field(window, "questionText");
                TextBlock meaning = (TextBlock)Field(window, "answerLine2");
                Point before = question.TranslatePoint(new Point(), root);
                Check(meaning.Visibility == Visibility.Hidden, "meaning reserves space before reveal");
                if (args.Length > 0) Screenshot(root, Path.Combine(args[0], "main-before-answer.png"), 360, 330);
                Call(window, "RevealAnswer");
                Layout(root, 360, 330);
                Point after = question.TranslatePoint(new Point(), root);
                Check(before == after && window.Height == 330, "revealing does not move word or resize window");
                Check(meaning.Visibility == Visibility.Visible && meaning.Text == first.Chinese, "meaning and kana coexist");
                DispatcherTimer timer = (DispatcherTimer)Field(window, "timer");
                Check(!timer.IsEnabled, "review waits for rating after answer");
                if (args.Length > 0) Screenshot(root, Path.Combine(args[0], "main-after-answer.png"), 360, 330);
                int previousCount = first.ReviewCount;
                Call(window, "Rate", 1);
                Check(first.ReviewCount == previousCount + 1 && first.WrongCount == 1, "rating updates current word");
                Call(window, "UndoClick", window, new RoutedEventArgs());
                Check(first.ReviewCount == previousCount && first.WrongCount == 0 && first.NextReviewTime.Length == 0,
                    "undo restores persisted scheduling state");
                Check(Storage.LoadDatabase().Words.Find(w => w.Japanese == first.Japanese).ReviewCount == previousCount,
                    "undo survives reload");
                Check(object.ReferenceEquals(first, Field(window, "currentWord")), "undo returns to rated word");
                Call(window, "ShowNextWord");
                Call(window, "PreviousClick", window, new RoutedEventArgs());
                Check(object.ReferenceEquals(first, Field(window, "currentWord")), "previous word navigation");
                Call(window, "Rate", 4);
                Check(first.ReviewCount == previousCount, "history navigation cannot double grade");
                config.StudyMode = 1;
                foreach (WordItem word in data.Words) word.NextReviewTime = DateTime.UtcNow.AddDays(3).ToString("o");
                Call(window, "ResetSession"); Call(window, "ShowNextWord"); Call(window, "RevealAnswer");
                Check(Field(window, "currentWord") != null && timer.IsEnabled, "browse uses future cards and keeps automatic refresh");
                Call(window, "Rate", 1);
                Check(first.ReviewCount == previousCount, "browse never writes ratings");
                Call(window, "TogglePause");
                Check(!timer.IsEnabled, "manual pause stops refresh");
                Call(window, "HideClick", window, new RoutedEventArgs());
                Call(window, "ShowClick", window, new RoutedEventArgs());
                Check(!timer.IsEnabled, "restoring window preserves manual pause");
                Call(window, "TogglePause");
                Check(timer.IsEnabled, "resume restarts refresh");
                typeof(MemoWindow).GetField("modalDepth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, 1);
                Call(window, "SetStudyMode", 0);
                Check(config.StudyMode == 1, "tray commands do not change scope behind an open dialog");
                Call(window, "SettingsClick", window, new RoutedEventArgs());
                typeof(MemoWindow).GetField("modalDepth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, 0);
                Check(window.ContextMenu.Items.Count >= 12, "settings and recovery accessible from context menu");
                config.BackgroundTransparent = true;
                Call(window, "ApplyAppearance");
                Check(((SolidColorBrush)((Border)Field(window, "surface")).Background).Color.A == 0, "pure text mode stays transparent");
                Call(window, "ChromeHideTimerTick", window, EventArgs.Empty);
                Check(((FrameworkElement)Field(window, "headerPanel")).Opacity == 0 &&
                    ((FrameworkElement)Field(window, "navigationPanel")).Opacity == 0, "transparent chrome hides together after leaving");
                Call(window, "ShowChromeTemporarily");
                Check(((FrameworkElement)Field(window, "navigationPanel")).IsHitTestVisible, "revealed navigation is clickable");
                Layout(root, 300, 280);
                Check(((FrameworkElement)Field(window, "answerPanel")).ActualHeight > 60, "minimum window keeps separate answer region");
                Console.WriteLine("Desktop workflow and fixed-layout tests passed.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { if (window != null) window.Close(); }
        }

        private static void Layout(FrameworkElement root, int width, int height)
        { root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout(); }

        private static void CheckRevealClicks(MemoWindow window, AppConfig config)
        {
            TextBlock question = (TextBlock)Field(window, "questionText");
            TextBlock reading = (TextBlock)Field(window, "answerLine1");
            for (int studyMode = 0; studyMode < 2; studyMode++)
                for (int mode = 0; mode < 4; mode++)
                    for (int transparent = 0; transparent < 2; transparent++)
                    {
                        config.StudyMode = studyMode; config.ReviewMode = mode;
                        config.BackgroundTransparent = transparent == 1;
                        Call(window, "ApplyAppearance");
                        Call(window, "DisplayCurrentWord", false);
                        RouteClick(question, true);
                        Check((bool)Field(window, "answerVisible"), "handled text click reveals answer in every mode");
                        if (mode == 0)
                        {
                            Call(window, "DisplayCurrentWord", false);
                            RouteClick(reading, true);
                            Check((bool)Field(window, "answerVisible"), "handled kana click reveals meaning");
                        }
                    }
            config.StudyMode = 0; config.ReviewMode = 0; config.BackgroundTransparent = false;
            Call(window, "ApplyAppearance"); Call(window, "DisplayCurrentWord", false);
            Border panel = (Border)Field(window, "questionPanel");
            ScrollViewer scroll = (ScrollViewer)panel.Child;
            ScrollBar bar = new ScrollBar();
            panel.Child = bar;
            RouteClick(bar, true);
            Check(!(bool)Field(window, "answerVisible"), "scrollbar click does not reveal answer");
            panel.Child = scroll;
            Call(window, "HideClick", window, new RoutedEventArgs());
            RouteClick(question, true);
            Check(!(bool)Field(window, "answerVisible"), "pseudo-hidden content does not reveal answer");
            Call(window, "ShowClick", window, new RoutedEventArgs());
        }

        private static void RouteClick(UIElement target, bool handled)
        {
            target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.MouseDownEvent, Handled = handled });
        }

        private static void Screenshot(FrameworkElement root, string path, int width, int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            RenderTargetBitmap image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            image.Render(root);
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (Stream stream = File.Create(path)) encoder.Save(stream);
        }
        private static object Field(object value, string name)
        { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }
        private static object Call(object value, string name, params object[] args)
        { return value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args); }
        private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    }
}
