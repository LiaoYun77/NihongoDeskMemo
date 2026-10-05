using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace NihongoDeskMemoWpf
{
    internal static class ReviewPresentation
    {
        public static bool ShouldShowRatingButtons(NihongoDeskMemo.AppConfig config, bool answerVisible)
        {
            return config.ShowRatingButtons && answerVisible;
        }

        public static bool CanRateWithKeyboard(NihongoDeskMemo.AppConfig config, bool answerVisible)
        {
            return config.ShowRatingButtons && answerVisible;
        }
    }

    internal static class WpfProgram
    {
        [STAThread]
        private static void Main()
        {
            System.Windows.Application app = new System.Windows.Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(new MemoWindow());
        }
    }

    internal sealed class MemoWindow : Window
    {
        private readonly NihongoDeskMemo.WordDatabase database;
        private readonly NihongoDeskMemo.AppConfig config;
        private readonly Random random = new Random();
        private readonly DispatcherTimer timer = new DispatcherTimer();
        private readonly DispatcherTimer chromeHideTimer = new DispatcherTimer();
        private GlobalHideHotkey globalHideHotkey;
        private bool resumeTimerAfterHide;
        private Button hideButton;

        private readonly Border surface;
        private readonly Border questionPanel;
        private Grid headerPanel;
        private TextBlock statusText;
        private readonly TextBlock questionText;
        private readonly TextBlock answerLine1;
        private readonly TextBlock answerLine2;
        private readonly Border answerPanel;
        private readonly UniformGrid ratingGrid;
        private readonly Grid normalContent;
        private readonly Button showButton;

        private NihongoDeskMemo.WordItem currentWord;
        private bool answerVisible;
        private bool pseudoHidden;
        private bool chromePinned;
        private Rect normalBounds;

        public MemoWindow()
        {
            database = NihongoDeskMemo.Storage.LoadDatabase();
            config = NihongoDeskMemo.Storage.LoadConfig();

            Title = "Nihongo Desk Memo";
            Width = Math.Max(300, config.WindowWidth);
            Height = Math.Max(250, config.WindowHeight);
            MinWidth = 280;
            MinHeight = 230;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = true;
            Topmost = config.AlwaysOnTop;
            KeyDown += WindowKeyDown;
            Loaded += WindowLoaded;
            Closed += WindowClosed;

            Grid root = new Grid();
            Content = root;

            surface = new Border();
            surface.CornerRadius = new CornerRadius(8);
            surface.BorderThickness = new Thickness(1);
            surface.BorderBrush = Brush("#667C8DA3");
            surface.Padding = new Thickness(12, 10, 12, 10);
            surface.MouseLeftButtonDown += SurfaceMouseLeftButtonDown;
            surface.MouseEnter += SurfaceMouseEnter;
            surface.MouseLeave += SurfaceMouseLeave;
            root.Children.Add(surface);

            normalContent = new Grid();
            normalContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            normalContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            normalContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            normalContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            surface.Child = normalContent;

            Grid header = BuildHeader();
            Grid.SetRow(header, 0);
            normalContent.Children.Add(header);

            questionPanel = new Border();
            questionPanel.Background = Brush("#5A111827");
            questionPanel.CornerRadius = new CornerRadius(6);
            questionPanel.Padding = new Thickness(12, 8, 12, 8);
            questionPanel.Margin = new Thickness(0, 7, 0, 5);
            questionPanel.MouseLeftButtonDown += RevealMouseDown;
            questionPanel.MouseRightButtonDown += ContentRightButtonDown;
            Grid.SetRow(questionPanel, 1);
            normalContent.Children.Add(questionPanel);

            questionText = new TextBlock();
            questionText.Foreground = Brush("#FFF8FAFC");
            questionText.FontFamily = new FontFamily("Yu Gothic UI");
            questionText.FontWeight = FontWeights.SemiBold;
            questionText.FontSize = 30;
            questionText.TextAlignment = TextAlignment.Center;
            questionText.TextWrapping = TextWrapping.Wrap;
            questionText.VerticalAlignment = VerticalAlignment.Center;
            questionText.Effect = TextShadow(4, 0.95);
            questionPanel.Child = questionText;

            answerPanel = new Border();
            answerPanel.Background = Brush("#C924313F");
            answerPanel.CornerRadius = new CornerRadius(6);
            answerPanel.Padding = new Thickness(10, 6, 10, 7);
            answerPanel.Margin = new Thickness(0, 0, 0, 7);
            answerPanel.MouseLeftButtonDown += RevealMouseDown;
            answerPanel.MouseRightButtonDown += ContentRightButtonDown;
            Grid.SetRow(answerPanel, 2);
            normalContent.Children.Add(answerPanel);

            StackPanel answerStack = new StackPanel();
            answerPanel.Child = answerStack;
            answerLine1 = AnswerText(15, "#FFE2E8F0");
            answerLine2 = AnswerText(16, "#FFFFFFFF");
            answerLine1.Effect = TextShadow(3, 0.9);
            answerLine2.Effect = TextShadow(3, 0.9);
            answerStack.Children.Add(answerLine1);
            answerStack.Children.Add(answerLine2);

            ratingGrid = new UniformGrid();
            ratingGrid.Rows = 1;
            ratingGrid.Columns = 4;
            ratingGrid.Margin = new Thickness(0, 0, 0, 1);
            ratingGrid.Children.Add(RatingButton("不会\n1", "#FFB94A57", 1));
            ratingGrid.Children.Add(RatingButton("模糊\n2", "#FFC58A39", 2));
            ratingGrid.Children.Add(RatingButton("记得\n3", "#FF3C8B72", 3));
            ratingGrid.Children.Add(RatingButton("熟练\n4", "#FF3976A8", 4));
            Grid.SetRow(ratingGrid, 3);
            normalContent.Children.Add(ratingGrid);

            showButton = ToolButton("显示");
            showButton.Visibility = Visibility.Collapsed;
            showButton.Click += ShowClick;
            root.Children.Add(showButton);

            Thumb resizeThumb = new Thumb();
            resizeThumb.Width = 16;
            resizeThumb.Height = 16;
            resizeThumb.HorizontalAlignment = HorizontalAlignment.Right;
            resizeThumb.VerticalAlignment = VerticalAlignment.Bottom;
            resizeThumb.Cursor = Cursors.SizeNWSE;
            resizeThumb.Background = Brushes.Transparent;
            resizeThumb.Opacity = 0;
            resizeThumb.DragDelta += ResizeDragDelta;
            root.Children.Add(resizeThumb);

            timer.Tick += TimerTick;
            chromeHideTimer.Interval = TimeSpan.FromSeconds(4);
            chromeHideTimer.Tick += ChromeHideTimerTick;
            ApplyAppearance();
        }

        private Grid BuildHeader()
        {
            Grid header = new Grid();
            headerPanel = header;
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            statusText = new TextBlock();
            statusText.Foreground = Brush("#FFB8C4D4");
            statusText.FontSize = 11;
            statusText.VerticalAlignment = VerticalAlignment.Center;
            statusText.TextTrimming = TextTrimming.CharacterEllipsis;
            header.Children.Add(statusText);

            StackPanel tools = new StackPanel();
            tools.Orientation = Orientation.Horizontal;
            Grid.SetColumn(tools, 1);
            header.Children.Add(tools);

            Button library = ToolButton("词库");
            library.Click += LibraryClick;
            tools.Children.Add(library);
            Button settings = ToolButton("设置");
            settings.Click += SettingsClick;
            tools.Children.Add(settings);
            Button hide = ToolButton("隐藏");
            hideButton = hide;
            hide.Click += HideClick;
            tools.Children.Add(hide);
            Button close = ToolButton("×");
            close.ToolTip = "关闭";
            close.Click += delegate { Close(); };
            tools.Children.Add(close);
            return header;
        }

        private static TextBlock AnswerText(double size, string color)
        {
            TextBlock text = new TextBlock();
            text.Foreground = Brush(color);
            text.FontFamily = new FontFamily("Microsoft YaHei UI");
            text.FontSize = size;
            text.TextAlignment = TextAlignment.Center;
            text.TextWrapping = TextWrapping.Wrap;
            text.Margin = new Thickness(0, 1, 0, 1);
            return text;
        }

        private static Button ToolButton(string text)
        {
            Button button = new Button();
            button.Content = text;
            button.Foreground = Brush("#FFE8EDF4");
            button.Background = Brush("#E0354354");
            button.BorderBrush = Brush("#805F7188");
            button.BorderThickness = new Thickness(1);
            button.Padding = new Thickness(8, 3, 8, 3);
            button.Margin = new Thickness(3, 0, 0, 0);
            button.Cursor = Cursors.Hand;
            return button;
        }

        private Button RatingButton(string text, string color, int rating)
        {
            Button button = new Button();
            button.Content = text;
            button.Foreground = Brushes.White;
            button.Background = Brush(color);
            button.BorderThickness = new Thickness(0);
            button.Padding = new Thickness(4, 4, 4, 4);
            button.Margin = new Thickness(2, 0, 2, 0);
            button.FontSize = 12;
            button.Cursor = Cursors.Hand;
            button.Tag = rating;
            button.Click += RatingClick;
            return button;
        }

        private static SolidColorBrush Brush(string color)
        {
            SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }

        private static DropShadowEffect TextShadow(double blurRadius, double opacity)
        {
            DropShadowEffect effect = new DropShadowEffect();
            effect.Color = Colors.Black;
            effect.BlurRadius = blurRadius;
            effect.ShadowDepth = 1;
            effect.Direction = 270;
            effect.Opacity = opacity;
            return effect;
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            RestoreLocation();
            ApplyConfig();
            ShowNextWord();
            globalHideHotkey = new GlobalHideHotkey(this);
            globalHideHotkey.VisibilityChanged += GlobalVisibilityChanged;
            hideButton.ToolTip = globalHideHotkey.Shortcut.Length > 0
                ? "点击后保留显示按钮；" + globalHideHotkey.Shortcut + " 全局隐藏 / 恢复全部窗口"
                : "点击后保留显示按钮；全局快捷键注册失败";
            if (globalHideHotkey.Shortcut == "Ctrl+Alt+F10")
                MessageBox.Show(this, "F10 已被占用，全局隐藏 / 恢复改为 Ctrl+Alt+F10。", "快捷键提示");
            else if (globalHideHotkey.Shortcut.Length == 0)
                MessageBox.Show(this, "F10 和 Ctrl+Alt+F10 均注册失败，本次全局隐藏不可用。\n请关闭重复运行的本软件或占用快捷键的程序，然后重新启动。", "快捷键不可用");
        }

        private void GlobalVisibilityChanged(object sender, EventArgs e)
        {
            if (globalHideHotkey.IsHidden)
            {
                resumeTimerAfterHide = timer.IsEnabled;
                timer.Stop();
                chromeHideTimer.Stop();
            }
            else
            {
                if (resumeTimerAfterHide) timer.Start();
                if (config.BackgroundTransparent && !pseudoHidden) ShowChromeTemporarily();
            }
        }

        private void RestoreLocation()
        {
            if (config.WindowX >= 0 && config.WindowY >= 0)
            {
                Left = config.WindowX;
                Top = config.WindowY;
                return;
            }
            Rect area = SystemParameters.WorkArea;
            Left = area.Right - Width - 24;
            Top = area.Bottom - Height - 24;
        }

        private void ApplyConfig()
        {
            Topmost = config.AlwaysOnTop;
            timer.Interval = TimeSpan.FromSeconds(Math.Max(3, config.RefreshSeconds));
            timer.Start();
            ApplyAppearance();
        }

        private void ApplyAppearance()
        {
            if (config.BackgroundTransparent)
            {
                surface.Background = Brushes.Transparent;
                surface.BorderBrush = Brushes.Transparent;
                questionPanel.Background = Brushes.Transparent;
                answerPanel.Background = Brushes.Transparent;
                ShowChromeTemporarily();
            }
            else
            {
                surface.Background = Brush("#FF18212D");
                surface.BorderBrush = Brush("#667C8DA3");
                questionPanel.Background = Brush("#5A111827");
                answerPanel.Background = Brush("#C924313F");
                headerPanel.Opacity = 1.0;
            }
        }

        private void SurfaceMouseEnter(object sender, MouseEventArgs e)
        {
            if (config.BackgroundTransparent && !pseudoHidden) ShowChromeTemporarily();
        }

        private void SurfaceMouseLeave(object sender, MouseEventArgs e)
        {
            if (config.BackgroundTransparent && !pseudoHidden && !chromePinned)
            {
                chromeHideTimer.Stop();
                chromeHideTimer.Start();
            }
        }

        private void ShowChromeTemporarily()
        {
            headerPanel.Opacity = 1.0;
            chromeHideTimer.Stop();
            if (!chromePinned) chromeHideTimer.Start();
        }

        private void ChromeHideTimerTick(object sender, EventArgs e)
        {
            chromeHideTimer.Stop();
            if (config.BackgroundTransparent && !chromePinned && !headerPanel.IsMouseOver)
            {
                headerPanel.Opacity = 0.0;
            }
        }

        private void ShowNextWord()
        {
            currentWord = PickDueWord();
            if (database.Words.Count == 0)
            {
                ShowEmpty("暂无词条", "请在设置中导入词表");
                return;
            }
            if (currentWord == null)
            {
                DateTime next;
                string message = TryGetNextDueTime(out next)
                    ? "下次复习：" + next.ToLocalTime().ToString("MM-dd HH:mm")
                    : "当前练习单元没有可用词条";
                ShowEmpty("暂无到期复习", message);
                return;
            }

            currentWord.SeenCount++;
            currentWord.LastSeenUtc = DateTime.UtcNow.ToString("o");
            answerVisible = !config.HideMeaningUntilClick;
            ApplyReviewDisplay();
            statusText.Text = ModeName() + "  ·  " +
                (config.PracticeUnit.Length == 0 ? "全部词库" : config.PracticeUnit) +
                "  ·  复习 " + currentWord.ReviewCount.ToString();
            UpdateAnswerState();
        }

        private void ShowEmpty(string title, string message)
        {
            currentWord = null;
            answerVisible = false;
            questionText.Text = title;
            answerLine1.Text = message;
            answerLine2.Text = string.Empty;
            answerPanel.Visibility = Visibility.Visible;
            ratingGrid.Visibility = Visibility.Collapsed;
            statusText.Text = config.PracticeUnit.Length == 0 ? "全部词库" : config.PracticeUnit;
        }

        private void ApplyReviewDisplay()
        {
            if (config.ReviewMode == 1)
            {
                questionText.Text = currentWord.Chinese;
                answerLine1.Text = currentWord.Japanese;
                answerLine2.Text = currentWord.Pinyin;
            }
            else if (config.ReviewMode == 2)
            {
                questionText.Text = currentWord.Pinyin;
                answerLine1.Text = currentWord.Japanese;
                answerLine2.Text = currentWord.Chinese;
            }
            else if (config.ReviewMode == 3)
            {
                questionText.Text = currentWord.Japanese;
                answerLine1.Text = currentWord.Pinyin;
                answerLine2.Text = currentWord.Chinese;
            }
            else
            {
                questionText.Text = currentWord.Japanese;
                answerLine1.Text = currentWord.Pinyin;
                answerLine2.Text = currentWord.Chinese;
            }
        }

        private void UpdateAnswerState()
        {
            bool readingAlwaysVisible = config.ReviewMode == 0;
            answerPanel.Visibility = answerVisible || readingAlwaysVisible ? Visibility.Visible : Visibility.Collapsed;
            answerLine1.Visibility = answerVisible || readingAlwaysVisible ? Visibility.Visible : Visibility.Collapsed;
            answerLine2.Visibility = answerVisible ? Visibility.Visible : Visibility.Collapsed;
            ratingGrid.Visibility = ReviewPresentation.ShouldShowRatingButtons(config, answerVisible)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void RevealAnswer()
        {
            if (currentWord == null || answerVisible)
            {
                return;
            }
            answerVisible = true;
            UpdateAnswerState();
        }

        private NihongoDeskMemo.WordItem PickDueWord()
        {
            List<NihongoDeskMemo.WordItem> candidates = new List<NihongoDeskMemo.WordItem>();
            int total = 0;
            int i;
            for (i = 0; i < database.Words.Count; i++)
            {
                NihongoDeskMemo.WordItem item = database.Words[i];
                if (!InPracticeUnit(item) || !EligibleForMode(item) || !IsDue(item)) continue;
                candidates.Add(item);
                total += Math.Max(1, Convert.ToInt32(Math.Round(item.Difficulty * 2.0)));
            }
            if (candidates.Count == 0) return null;
            int roll = random.Next(total);
            for (i = 0; i < candidates.Count; i++)
            {
                roll -= Math.Max(1, Convert.ToInt32(Math.Round(candidates[i].Difficulty * 2.0)));
                if (roll < 0) return candidates[i];
            }
            return candidates[0];
        }

        private bool InPracticeUnit(NihongoDeskMemo.WordItem item)
        {
            return config.PracticeUnit.Length == 0 ||
                string.Equals(NihongoDeskMemo.Storage.SafeTrim(item.Unit), config.PracticeUnit, StringComparison.OrdinalIgnoreCase);
        }

        private bool EligibleForMode(NihongoDeskMemo.WordItem item)
        {
            return (config.ReviewMode != 2 && config.ReviewMode != 3) ||
                NihongoDeskMemo.Storage.SafeTrim(item.Pinyin).Length > 0;
        }

        private static bool IsDue(NihongoDeskMemo.WordItem item)
        {
            DateTime next;
            return !TryParseUtc(item.NextReviewTime, out next) || next <= DateTime.UtcNow;
        }

        private bool TryGetNextDueTime(out DateTime nextDue)
        {
            nextDue = DateTime.MaxValue;
            bool found = false;
            int i;
            for (i = 0; i < database.Words.Count; i++)
            {
                NihongoDeskMemo.WordItem item = database.Words[i];
                if (!InPracticeUnit(item) || !EligibleForMode(item)) continue;
                DateTime next;
                if (TryParseUtc(item.NextReviewTime, out next) && next < nextDue)
                {
                    nextDue = next;
                    found = true;
                }
            }
            return found;
        }

        private static bool TryParseUtc(string value, out DateTime result)
        {
            return DateTime.TryParse(
                NihongoDeskMemo.Storage.SafeTrim(value),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out result);
        }

        private string ModeName()
        {
            string[] names = new string[] { "日语认中文", "中文回忆日语", "假名回忆汉字", "汉字回忆读音" };
            return names[Math.Max(0, Math.Min(3, config.ReviewMode))];
        }

        private void Rate(int rating)
        {
            if (currentWord == null || !ReviewPresentation.CanRateWithKeyboard(config, answerVisible)) return;
            DateTime now = DateTime.UtcNow;
            currentWord.LastReviewTime = now.ToString("o");
            currentWord.ReviewCount++;
            if (rating == 1)
            {
                currentWord.WrongCount++;
                currentWord.CorrectStreak = 0;
                currentWord.Difficulty = Math.Min(10.0, currentWord.Difficulty + 1.0);
                currentWord.IntervalDays = 0;
                currentWord.NextReviewTime = now.AddMinutes(5).ToString("o");
                currentWord.Marked = true;
            }
            else if (rating == 2)
            {
                currentWord.CorrectStreak = 0;
                currentWord.Difficulty = Math.Min(10.0, currentWord.Difficulty + 0.3);
                currentWord.IntervalDays = 1;
                currentWord.NextReviewTime = now.AddDays(1).ToString("o");
            }
            else
            {
                currentWord.CorrectStreak++;
                currentWord.Difficulty = Math.Max(1.0, currentWord.Difficulty - (rating == 4 ? 0.5 : 0.25));
                currentWord.IntervalDays = NextCorrectInterval(currentWord.IntervalDays, rating == 4 ? 7 : 3);
                currentWord.NextReviewTime = now.AddDays(currentWord.IntervalDays).ToString("o");
            }
            NihongoDeskMemo.Storage.SaveDatabase(database);
            ShowNextWord();
        }

        private static int NextCorrectInterval(int currentDays, int minimumDays)
        {
            int[] intervals = new int[] { 3, 7, 15, 30, 60 };
            int i;
            for (i = 0; i < intervals.Length; i++)
            {
                if (intervals[i] >= minimumDays && intervals[i] > currentDays) return intervals[i];
            }
            return 60;
        }

        private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F9)
            {
                chromePinned = !chromePinned;
                headerPanel.Opacity = chromePinned ? 1.0 : 0.0;
                chromeHideTimer.Stop();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SettingsClick(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
            {
                LibraryClick(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F8)
            {
                RevealAnswer();
                e.Handled = true;
                return;
            }
            int rating = 0;
            if (e.Key == Key.D1 || e.Key == Key.NumPad1) rating = 1;
            if (e.Key == Key.D2 || e.Key == Key.NumPad2) rating = 2;
            if (e.Key == Key.D3 || e.Key == Key.NumPad3) rating = 3;
            if (e.Key == Key.D4 || e.Key == Key.NumPad4) rating = 4;
            if (rating > 0 && ReviewPresentation.CanRateWithKeyboard(config, answerVisible))
            {
                Rate(rating);
                e.Handled = true;
            }
        }

        private void RatingClick(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null) Rate(Convert.ToInt32(button.Tag));
        }

        private void RevealMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!pseudoHidden)
            {
                ShowChromeTemporarily();
                RevealAnswer();
                e.Handled = true;
            }
        }

        private void ContentRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!pseudoHidden)
            {
                chromePinned = true;
                headerPanel.Opacity = 1.0;
                chromeHideTimer.Stop();
                e.Handled = true;
            }
        }

        private void TimerTick(object sender, EventArgs e)
        {
            if (globalHideHotkey != null && globalHideHotkey.IsHidden) return;
            ShowNextWord();
        }

        private void LibraryClick(object sender, RoutedEventArgs e)
        {
            WpfLibraryWindow form = new WpfLibraryWindow(database);
            form.Owner = this;
            form.ShowDialog();
            NihongoDeskMemo.Storage.SaveDatabase(database);
            ShowNextWord();
        }

        private void SettingsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                WpfSettingsWindow form = new WpfSettingsWindow(
                    config,
                    database,
                    ImportWords,
                    OpenLibraryFromSettings,
                    ResetReviewProgress);
                form.Owner = this;
                if (form.ShowDialog() == true) NihongoDeskMemo.Storage.SaveConfig(config);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(this, ex.ToString(), "设置窗口错误");
            }
            ApplyConfig();
            ShowNextWord();
        }

        private void ImportWords()
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = "导入词表";
            dialog.Filter = "文本文件 (*.txt;*.csv)|*.txt;*.csv|所有文件 (*.*)|*.*";
            if (dialog.ShowDialog() != true) return;
            NihongoDeskMemo.ImportResult result = NihongoDeskMemo.WordImporter.ImportFile(dialog.FileName, database);
            NihongoDeskMemo.Storage.SaveDatabase(database);
            System.Windows.MessageBox.Show(
                "新增 " + result.Added.ToString() + " 条，更新 " + result.Updated.ToString() + " 条，跳过 " + result.Skipped.ToString() + " 行。",
                "导入完成");
        }

        private void OpenLibraryFromSettings()
        {
            WpfLibraryWindow form = new WpfLibraryWindow(database);
            form.Owner = this;
            form.ShowDialog();
        }

        private void ResetReviewProgress()
        {
            int i;
            for (i = 0; i < database.Words.Count; i++)
            {
                NihongoDeskMemo.WordItem item = database.Words[i];
                item.Weight = 1;
                item.Marked = false;
                item.LastReviewTime = string.Empty;
                item.NextReviewTime = string.Empty;
                item.ReviewCount = 0;
                item.WrongCount = 0;
                item.CorrectStreak = 0;
                item.Difficulty = 5.0;
                item.IntervalDays = 0;
            }
            NihongoDeskMemo.Storage.SaveDatabase(database);
        }

        private void SurfaceMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is Button || source is Thumb) return;
                source = ParentOf(source);
            }
            if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch (InvalidOperationException) { }
            }
        }

        private static DependencyObject ParentOf(DependencyObject source)
        {
            if (source is Visual || source is System.Windows.Media.Media3D.Visual3D)
            {
                return VisualTreeHelper.GetParent(source);
            }
            FrameworkContentElement content = source as FrameworkContentElement;
            if (content != null)
            {
                return content.Parent;
            }
            return LogicalTreeHelper.GetParent(source);
        }

        private void ResizeDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (pseudoHidden) return;
            Width = Math.Max(MinWidth, Width + e.HorizontalChange);
            Height = Math.Max(MinHeight, Height + e.VerticalChange);
        }

        private void HideClick(object sender, RoutedEventArgs e)
        {
            if (pseudoHidden) return;
            normalBounds = new Rect(Left, Top, Width, Height);
            pseudoHidden = true;
            normalContent.Visibility = Visibility.Collapsed;
            showButton.Visibility = Visibility.Visible;
            MinWidth = 72;
            MinHeight = 32;
            Width = 76;
            Height = 34;
            surface.Padding = new Thickness(3);
        }

        private void ShowClick(object sender, RoutedEventArgs e)
        {
            pseudoHidden = false;
            normalContent.Visibility = Visibility.Visible;
            showButton.Visibility = Visibility.Collapsed;
            MinWidth = 280;
            MinHeight = 230;
            surface.Padding = new Thickness(12, 10, 12, 10);
            if (normalBounds.Width > 0)
            {
                Left = normalBounds.Left;
                Top = normalBounds.Top;
                Width = normalBounds.Width;
                Height = normalBounds.Height;
            }
        }

        private void WindowClosed(object sender, EventArgs e)
        {
            if (globalHideHotkey != null) globalHideHotkey.Dispose();
            timer.Stop();
            chromeHideTimer.Stop();
            if (!pseudoHidden)
            {
                config.WindowX = Convert.ToInt32(Left);
                config.WindowY = Convert.ToInt32(Top);
                config.WindowWidth = Convert.ToInt32(Width);
                config.WindowHeight = Convert.ToInt32(Height);
            }
            NihongoDeskMemo.Storage.SaveConfig(config);
            NihongoDeskMemo.Storage.SaveDatabase(database);
        }
    }
}
