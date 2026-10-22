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
            return config.StudyMode == 0 && config.ShowRatingButtons && answerVisible;
        }

        public static bool CanRateWithKeyboard(NihongoDeskMemo.AppConfig config, bool answerVisible)
        {
            return config.StudyMode == 0 && config.ShowRatingButtons && answerVisible;
        }
    }

    internal static class WpfProgram
    {
        [STAThread]
        private static void Main()
        {
            string instanceName;
            using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256.Create())
                instanceName = "Local\\NihongoDeskMemo." + BitConverter.ToString(hash.ComputeHash(
                    System.Text.Encoding.UTF8.GetBytes(NihongoDeskMemo.Storage.AppDirectory.ToUpperInvariant()))).Replace("-", "");
            using (System.Threading.Mutex instance = new System.Threading.Mutex(false, instanceName))
            {
                bool acquired;
                try { acquired = instance.WaitOne(0); }
                catch (System.Threading.AbandonedMutexException) { acquired = true; }
                if (!acquired) { MessageBox.Show("软件已在运行，请从托盘打开。", "Nihongo Desk Memo"); return; }
                System.Windows.Application app = new System.Windows.Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                try { app.Run(new MemoWindow()); }
                catch (Exception ex) { MessageBox.Show("启动失败：" + ex.Message, "Nihongo Desk Memo"); }
                finally { instance.ReleaseMutex(); }
            }
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
        private bool userPaused;
        private int modalDepth;
        private bool menuOpen;
        private bool closing;
        private readonly StudySession study;
        private readonly Stack<NihongoDeskMemo.WordItem> previousWords = new Stack<NihongoDeskMemo.WordItem>();
        private ReviewSnapshot lastReview;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private Button pauseButton, previousButton, undoButton;
        private MenuItem pauseMenu, browseMenu, reviewMenu, undoMenu;
        private Button hideButton;

        private readonly Border surface;
        private readonly Border questionPanel;
        private Grid headerPanel;
        private UniformGrid navigationPanel;
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
        private bool viewingHistory;
        private bool pseudoHidden;
        private bool chromePinned;
        private Rect normalBounds;

        public MemoWindow()
        {
            database = NihongoDeskMemo.Storage.LoadDatabase();
            config = NihongoDeskMemo.Storage.LoadConfig();
            study = new StudySession(random);
            if (config.StudyMode != 1) config.StudyMode = 0;

            Title = "Nihongo Desk Memo v" + AppVersion.Number;
            Width = Math.Max(300, config.WindowWidth);
            Height = Math.Max(280, config.WindowHeight);
            MinWidth = 300;
            MinHeight = 280;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = true;
            Topmost = config.AlwaysOnTop;
            KeyDown += WindowKeyDown;
            Loaded += WindowLoaded;
            Closed += WindowClosed;
            IsKeyboardFocusWithinChanged += delegate { UpdateRefreshState(); };

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
            ContextMenu = BuildAccessMenu();
            root.Children.Add(surface);

            normalContent = new Grid();
            normalContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            normalContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            normalContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(94) });
            normalContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            normalContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
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
            questionPanel.Child = new ScrollViewer { Content = questionText, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

            answerPanel = new Border();
            answerPanel.Background = Brush("#C924313F");
            answerPanel.CornerRadius = new CornerRadius(6);
            answerPanel.Padding = new Thickness(10, 6, 10, 7);
            answerPanel.Margin = new Thickness(0, 0, 0, 7);
            answerPanel.MouseLeftButtonDown += RevealMouseDown;
            answerPanel.MouseRightButtonDown += ContentRightButtonDown;
            Grid.SetRow(answerPanel, 2);
            normalContent.Children.Add(answerPanel);

            Grid answerStack = new Grid();
            answerStack.RowDefinitions.Add(new RowDefinition());
            answerStack.RowDefinitions.Add(new RowDefinition());
            answerPanel.Child = answerStack;
            answerLine1 = AnswerText(15, "#FFE2E8F0");
            answerLine2 = AnswerText(16, "#FFFFFFFF");
            answerLine1.Effect = TextShadow(3, 0.9);
            answerLine2.Effect = TextShadow(3, 0.9);
            ScrollViewer readingScroll = new ScrollViewer { Content = answerLine1, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            ScrollViewer meaningScroll = new ScrollViewer { Content = answerLine2, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetRow(meaningScroll, 1);
            answerStack.Children.Add(readingScroll);
            answerStack.Children.Add(meaningScroll);

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

            UniformGrid navigation = new UniformGrid { Columns = 4, Rows = 1 };
            navigationPanel = navigation;
            previousButton = ToolButton("←");
            previousButton.ToolTip = "上一词";
            previousButton.Click += PreviousClick;
            navigation.Children.Add(previousButton);
            Button next = ToolButton("→");
            next.ToolTip = "下一词（不计评级）";
            next.Click += delegate { ShowNextWord(); };
            navigation.Children.Add(next);
            undoButton = ToolButton("↶");
            undoButton.ToolTip = "撤销上次评级";
            undoButton.Click += UndoClick;
            navigation.Children.Add(undoButton);
            pauseButton = ToolButton("暂停");
            pauseButton.Click += delegate { TogglePause(); };
            navigation.Children.Add(pauseButton);
            Grid.SetRow(navigation, 4);
            normalContent.Children.Add(navigation);

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
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            FrameworkElementFactory content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            template.VisualTree = border;
            Trigger disabled = new Trigger { Property = IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(OpacityProperty, 0.35));
            template.Triggers.Add(disabled);
            button.Template = template;
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
            CreateTrayIcon();
            HotkeyGesture shortcut = HotkeyGesture.FromConfig(config);
            globalHideHotkey = new GlobalHideHotkey(this, shortcut);
            globalHideHotkey.VisibilityChanged += GlobalVisibilityChanged;
            UpdateHideShortcutTip();
            if (globalHideHotkey.Shortcut.Length > 0 && globalHideHotkey.Shortcut != shortcut.DisplayName)
                MessageBox.Show(this, shortcut.DisplayName + " 已被占用，本次全局隐藏 / 恢复改为 " + globalHideHotkey.Shortcut + "。", "快捷键提示");
            else if (globalHideHotkey.Shortcut.Length == 0)
                MessageBox.Show(this, shortcut.DisplayName + " 全局隐藏快捷键启用失败。\n请在设置中选择其他按键，或关闭占用快捷键的程序。", "快捷键不可用");
            if (NihongoDeskMemo.XmlFileStore.RecoveryMessage.Length > 0)
                MessageBox.Show(this, NihongoDeskMemo.XmlFileStore.RecoveryMessage, "数据已恢复");
        }

        private void UpdateHideShortcutTip()
        {
            hideButton.ToolTip = globalHideHotkey.Shortcut.Length > 0
                ? "点击后保留显示按钮；" + globalHideHotkey.Shortcut + " 全局隐藏 / 恢复全部窗口"
                : "点击后保留显示按钮；全局快捷键注册失败";
        }

        private void GlobalVisibilityChanged(object sender, EventArgs e)
        {
            if (globalHideHotkey.IsHidden) chromeHideTimer.Stop();
            else if (config.BackgroundTransparent && !pseudoHidden) ShowChromeTemporarily();
            UpdateRefreshState();
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
            UpdateRefreshState();
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
                SetChromeOpacity(1);
            }
        }

        private void SurfaceMouseEnter(object sender, MouseEventArgs e)
        {
            UpdateRefreshState();
            if (config.BackgroundTransparent && !pseudoHidden) ShowChromeTemporarily();
        }

        private void SurfaceMouseLeave(object sender, MouseEventArgs e)
        {
            UpdateRefreshState();
            if (config.BackgroundTransparent && !pseudoHidden && !chromePinned)
            {
                chromeHideTimer.Stop();
                chromeHideTimer.Start();
            }
        }

        private void ShowChromeTemporarily()
        {
            SetChromeOpacity(1);
            chromeHideTimer.Stop();
            if (!chromePinned) chromeHideTimer.Start();
        }

        private void ChromeHideTimerTick(object sender, EventArgs e)
        {
            chromeHideTimer.Stop();
            if (config.BackgroundTransparent && !chromePinned && !IsMouseOver && !menuOpen)
            {
                SetChromeOpacity(0);
            }
        }

        private void SetChromeOpacity(double value)
        {
            headerPanel.Opacity = value;
            headerPanel.IsHitTestVisible = value > 0;
            if (navigationPanel != null)
            {
                navigationPanel.Opacity = value;
                navigationPanel.IsHitTestVisible = value > 0;
            }
        }

        private void ShowNextWord()
        {
            if (currentWord != null)
            {
                if (previousWords.Count >= 100)
                {
                    NihongoDeskMemo.WordItem[] history = previousWords.ToArray();
                    previousWords.Clear();
                    for (int i = 98; i >= 0; i--) previousWords.Push(history[i]);
                }
                previousWords.Push(currentWord);
            }
            currentWord = PickDueWord();
            viewingHistory = false;
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

            DisplayCurrentWord(false);
        }

        private void DisplayCurrentWord(bool revealed)
        {
            if (currentWord == null) return;
            answerVisible = revealed || !config.HideMeaningUntilClick;
            ApplyReviewDisplay();
            statusText.Text = (config.StudyMode == 1 ? "浏览" : "复习") + " · " + ModeName() + "  ·  " +
                PracticeScope.Summary(config) +
                "  ·  复习 " + currentWord.ReviewCount.ToString();
            UpdateAnswerState();
            timer.Stop();
            UpdateRefreshState();
        }

        private void ShowEmpty(string title, string message)
        {
            currentWord = null;
            answerVisible = false;
            questionText.Text = title;
            answerLine1.Text = message;
            answerLine2.Text = string.Empty;
            answerPanel.Visibility = Visibility.Visible;
            answerLine1.Visibility = Visibility.Visible;
            answerLine2.Visibility = Visibility.Hidden;
            ratingGrid.Visibility = Visibility.Hidden;
            statusText.Text = PracticeScope.Summary(config);
            UpdateNavigation();
            UpdateRefreshState();
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
            answerPanel.Visibility = Visibility.Visible;
            answerLine1.Visibility = answerVisible || readingAlwaysVisible ? Visibility.Visible : Visibility.Hidden;
            answerLine2.Visibility = answerVisible ? Visibility.Visible : Visibility.Hidden;
            ratingGrid.Visibility = !viewingHistory && ReviewPresentation.ShouldShowRatingButtons(config, answerVisible)
                ? Visibility.Visible
                : Visibility.Hidden;
            questionText.ToolTip = questionText.Text;
            answerLine1.ToolTip = answerLine1.Text;
            answerLine2.ToolTip = answerLine2.Text;
            UpdateNavigation();
            UpdateRefreshState();
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
            return study.Next(database, config);
        }

        private bool InPracticeUnit(NihongoDeskMemo.WordItem item)
        {
            return PracticeScope.Contains(config, item.Unit);
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
            if (currentWord == null || viewingHistory || !ReviewPresentation.CanRateWithKeyboard(config, answerVisible)) return;
            ReviewSnapshot before = new ReviewSnapshot(currentWord);
            ReviewActions.Rate(currentWord, rating, DateTime.UtcNow);
            try { NihongoDeskMemo.Storage.SaveDatabase(database); }
            catch (Exception ex) { before.Restore(); MessageBox.Show(this, "评级未保存：" + ex.Message, "保存失败"); return; }
            lastReview = before;
            ShowNextWord();
        }

        private void PreviousClick(object sender, RoutedEventArgs e)
        {
            while (previousWords.Count > 0)
            {
                NihongoDeskMemo.WordItem previous = previousWords.Pop();
                if (!database.Words.Contains(previous) || !InPracticeUnit(previous) || !EligibleForMode(previous)) continue;
                currentWord = previous;
                viewingHistory = true;
                DisplayCurrentWord(true);
                return;
            }
            UpdateNavigation();
        }

        private void UndoClick(object sender, RoutedEventArgs e)
        {
            if (lastReview == null || !database.Words.Contains(lastReview.Word)) return;
            ReviewSnapshot after = new ReviewSnapshot(lastReview.Word);
            lastReview.Restore();
            try { NihongoDeskMemo.Storage.SaveDatabase(database); }
            catch (Exception ex) { after.Restore(); MessageBox.Show(this, "撤销未保存：" + ex.Message, "保存失败"); return; }
            currentWord = lastReview.Word;
            lastReview = null;
            viewingHistory = false;
            if (previousWords.Count > 0 && object.ReferenceEquals(previousWords.Peek(), currentWord)) previousWords.Pop();
            study.Reset();
            DisplayCurrentWord(true);
        }

        private void UpdateNavigation()
        {
            previousButton.IsEnabled = previousWords.Count > 0;
            undoButton.IsEnabled = lastReview != null && database.Words.Contains(lastReview.Word);
            if (undoMenu != null) undoMenu.IsEnabled = undoButton.IsEnabled;
            pauseButton.Content = userPaused ? "继续" : "暂停";
            if (pauseMenu != null) pauseMenu.IsChecked = userPaused;
            if (browseMenu != null) browseMenu.IsChecked = config.StudyMode == 1;
            if (reviewMenu != null) reviewMenu.IsChecked = config.StudyMode == 0;
        }

        private void TogglePause() { userPaused = !userPaused; UpdateNavigation(); UpdateRefreshState(); }

        private bool ShouldPauseRefresh()
        {
            return closing || userPaused || pseudoHidden || menuOpen || modalDepth > 0 || IsMouseOver || IsMouseCaptureWithin ||
                (globalHideHotkey != null && globalHideHotkey.IsHidden) ||
                (currentWord != null && !viewingHistory && ReviewPresentation.ShouldShowRatingButtons(config, answerVisible));
        }

        private void UpdateRefreshState()
        {
            if (ShouldPauseRefresh()) timer.Stop();
            else if (!timer.IsEnabled) timer.Start();
        }

        private void WithDialog(Action action)
        {
            modalDepth++;
            UpdateRefreshState();
            try { action(); }
            finally { modalDepth--; UpdateRefreshState(); }
        }

        private void SetStudyMode(int value)
        {
            if (ActivateExistingDialog()) return;
            if (config.StudyMode == value) return;
            int old = config.StudyMode;
            config.StudyMode = value;
            try { NihongoDeskMemo.Storage.SaveConfig(config); }
            catch (Exception ex) { config.StudyMode = old; MessageBox.Show(this, ex.Message, "设置未保存"); return; }
            ResetSession();
            ShowNextWord();
        }

        private void ResetSession()
        {
            study.Reset(); previousWords.Clear(); lastReview = null; currentWord = null;
        }

        private ContextMenu BuildAccessMenu()
        {
            ContextMenu menu = new ContextMenu();
            menu.Opened += delegate { menuOpen = true; UpdateNavigation(); UpdateRefreshState(); };
            menu.Closed += delegate { menuOpen = false; UpdateRefreshState(); };
            AddMenu(menu, "显示窗口", ShowMainWindow);
            pauseMenu = AddMenu(menu, "暂停自动换词", TogglePause);
            pauseMenu.IsCheckable = true;
            menu.Items.Add(new Separator());
            browseMenu = AddMenu(menu, "浏览模式", delegate { SetStudyMode(1); });
            reviewMenu = AddMenu(menu, "到期复习", delegate { SetStudyMode(0); });
            AddMenu(menu, "上一词", delegate { PreviousClick(this, new RoutedEventArgs()); });
            AddMenu(menu, "下一词", ShowNextWord);
            undoMenu = AddMenu(menu, "撤销上次评级", delegate { UndoClick(this, new RoutedEventArgs()); });
            menu.Items.Add(new Separator());
            AddMenu(menu, "词库管理", delegate { LibraryClick(this, new RoutedEventArgs()); });
            AddMenu(menu, "设置", delegate { SettingsClick(this, new RoutedEventArgs()); });
            AddMenu(menu, "备份词库", BackupWords);
            AddMenu(menu, "从备份恢复", RestoreWords);
            menu.Items.Add(new Separator());
            AddMenu(menu, "退出", Close);
            return menu;
        }

        private static MenuItem AddMenu(ContextMenu menu, string text, Action action)
        {
            MenuItem item = new MenuItem { Header = text };
            item.Click += delegate { action(); };
            menu.Items.Add(item);
            return item;
        }

        private void ShowMainWindow()
        {
            if (globalHideHotkey != null && globalHideHotkey.IsHidden) globalHideHotkey.Toggle();
            if (pseudoHidden) ShowClick(this, new RoutedEventArgs());
            Show();
            WindowState = WindowState.Normal;
            Activate();
            chromePinned = true;
            ShowChromeTemporarily();
        }

        private void CreateTrayIcon()
        {
            if (trayIcon != null) return;
            trayIcon = new System.Windows.Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Information,
                Text = "Nihongo Desk Memo", Visible = true };
            System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Opening += delegate { menuOpen = true; UpdateRefreshState(); };
            menu.Closed += delegate { menuOpen = false; UpdateRefreshState(); };
            AddTrayMenu(menu, "显示窗口", ShowMainWindow);
            AddTrayMenu(menu, "暂停 / 继续自动换词", TogglePause);
            AddTrayMenu(menu, "浏览模式", delegate { SetStudyMode(1); });
            AddTrayMenu(menu, "到期复习", delegate { SetStudyMode(0); });
            AddTrayMenu(menu, "设置", delegate { ShowMainWindow(); SettingsClick(this, new RoutedEventArgs()); });
            AddTrayMenu(menu, "词库管理", delegate { ShowMainWindow(); LibraryClick(this, new RoutedEventArgs()); });
            AddTrayMenu(menu, "备份词库", BackupWords);
            AddTrayMenu(menu, "从备份恢复", RestoreWords);
            AddTrayMenu(menu, "退出", Close);
            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += delegate { Dispatcher.BeginInvoke(new Action(ShowMainWindow)); };
        }

        private void AddTrayMenu(System.Windows.Forms.ContextMenuStrip menu, string text, Action action)
        {
            menu.Items.Add(text, null, delegate { Dispatcher.BeginInvoke(new Action(action)); });
        }

        private void BackupWords()
        {
            ShowMainWindow();
            if (ActivateExistingDialog()) return;
            WithDialog(delegate
            {
                try { NihongoDeskMemo.Storage.SaveDatabase(database); MessageBox.Show(this, NihongoDeskMemo.Storage.BackupDatabase(), "词库备份已创建"); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "备份失败"); }
            });
        }

        private void RestoreWords()
        {
            ShowMainWindow();
            if (ActivateExistingDialog()) return;
            WithDialog(delegate
            {
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog { Title = "恢复词库备份", Filter = "词库备份|*.xml;*.bak" };
                if (dialog.ShowDialog(this) != true) return;
                try
                {
                    NihongoDeskMemo.WordDatabase restored = NihongoDeskMemo.XmlFileStore.Load<NihongoDeskMemo.WordDatabase>(dialog.FileName, null);
                    if (restored == null || restored.Words == null) throw new InvalidOperationException("不是有效的词库备份。");
                    if (MessageBox.Show(this, "恢复 " + restored.Words.Count + " 条词条及其复习进度？当前词库会先备份。", "确认恢复", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
                    NihongoDeskMemo.Storage.BackupDatabase();
                    List<NihongoDeskMemo.WordItem> original = database.Words;
                    database.Words = restored.Words;
                    try { NihongoDeskMemo.Storage.SaveDatabase(database); }
                    catch { database.Words = original; throw; }
                    ResetSession(); ShowNextWord();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "恢复未完成"); }
            });
        }

        private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.F9)
            {
                chromePinned = !chromePinned;
                SetChromeOpacity(chromePinned ? 1 : 0);
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
                SetChromeOpacity(1);
                chromeHideTimer.Stop();
                e.Handled = false;
            }
        }

        private void TimerTick(object sender, EventArgs e)
        {
            if (ShouldPauseRefresh()) { UpdateRefreshState(); return; }
            ShowNextWord();
        }

        private void LibraryClick(object sender, RoutedEventArgs e)
        {
            if (ActivateExistingDialog()) return;
            WithDialog(delegate
            {
                try
                {
                    WpfLibraryWindow form = new WpfLibraryWindow(database);
                    form.Owner = this;
                    form.ShowDialog();
                    NihongoDeskMemo.Storage.SaveDatabase(database);
                    ResetSession(); ShowNextWord();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "词库未保存"); }
            });
        }

        private void SettingsClick(object sender, RoutedEventArgs e)
        {
            if (ActivateExistingDialog()) return;
            WithDialog(delegate
            {
                try
                {
                    WpfSettingsWindow form = new WpfSettingsWindow(
                        config, database, ImportWords, OpenLibraryFromSettings, ResetReviewProgress);
                    form.Owner = this;
                    form.RecordingChanged = globalHideHotkey.SetRecording;
                    form.ApplyHideHotkey = delegate(HotkeyGesture key)
                    {
                        string error;
                        if (!globalHideHotkey.TryChange(key, out error)) return error;
                        UpdateHideShortcutTip();
                        return string.Empty;
                    };
                    if (form.ShowDialog() == true)
                    {
                        NihongoDeskMemo.Storage.SaveConfig(config);
                        ResetSession();
                    }
                    UpdateHideShortcutTip();
                    if (globalHideHotkey.Shortcut.Length == 0)
                        MessageBox.Show(this, "原快捷键恢复失败，请重新打开设置选择其他按键。", "快捷键不可用");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(this, ex.ToString(), "设置窗口错误");
                }
                ApplyConfig();
                if (currentWord == null) ShowNextWord();
                else DisplayCurrentWord(answerVisible);
            });
        }

        private void ImportWords()
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = "导入词表";
            dialog.Filter = "支持的词表 (*.xml;*.txt;*.csv)|*.xml;*.txt;*.csv|完整 XML 词库 (*.xml)|*.xml|文本词表 (*.txt;*.csv)|*.txt;*.csv";
            if (dialog.ShowDialog() != true) return;
            try
            {
                string backup = NihongoDeskMemo.Storage.BackupDatabase();
                NihongoDeskMemo.ImportResult result = NihongoDeskMemo.WordImporter.ImportFile(dialog.FileName, database);
                NihongoDeskMemo.Storage.SaveDatabase(database);
                ResetSession(); ShowNextWord();
                System.Windows.MessageBox.Show(
                    "新增 " + result.Added + " 条，更新 " + result.Updated + " 条，已存在 " + result.Unchanged +
                    " 条，跳过 " + result.Skipped + " 行。" +
                    (backup.Length > 0 ? "\n导入前词库备份：\n" + backup : string.Empty), "导入完成");
            }
            catch (Exception ex)
            {
                Exception reason = ex.InnerException ?? ex;
                System.Windows.MessageBox.Show("导入未完成：" + reason.Message, "导入失败");
            }
        }

        private bool ActivateExistingDialog()
        {
            if (modalDepth == 0) return false;
            foreach (Window owned in OwnedWindows) if (owned.IsVisible) { owned.Activate(); break; }
            return true;
        }

        private void OpenLibraryFromSettings()
        {
            WpfLibraryWindow form = new WpfLibraryWindow(database);
            form.Owner = this;
            form.ShowDialog();
            NihongoDeskMemo.Storage.SaveDatabase(database);
            ResetSession(); ShowNextWord();
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
            ResetSession(); ShowNextWord();
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
            UpdateRefreshState();
        }

        private void ShowClick(object sender, RoutedEventArgs e)
        {
            pseudoHidden = false;
            normalContent.Visibility = Visibility.Visible;
            showButton.Visibility = Visibility.Collapsed;
            MinWidth = 300;
            MinHeight = 280;
            surface.Padding = new Thickness(12, 10, 12, 10);
            if (normalBounds.Width > 0)
            {
                Left = normalBounds.Left;
                Top = normalBounds.Top;
                Width = normalBounds.Width;
                Height = normalBounds.Height;
            }
            UpdateRefreshState();
        }

        private void WindowClosed(object sender, EventArgs e)
        {
            closing = true;
            if (globalHideHotkey != null) globalHideHotkey.Dispose();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                if (trayIcon.ContextMenuStrip != null) trayIcon.ContextMenuStrip.Dispose();
                trayIcon.Dispose();
            }
            timer.Stop();
            chromeHideTimer.Stop();
            if (!pseudoHidden)
            {
                config.WindowX = double.IsNaN(Left) ? -1 : Convert.ToInt32(Left);
                config.WindowY = double.IsNaN(Top) ? -1 : Convert.ToInt32(Top);
                config.WindowWidth = Convert.ToInt32(Width);
                config.WindowHeight = Convert.ToInt32(Height);
            }
            try
            {
                NihongoDeskMemo.Storage.SaveConfig(config);
                NihongoDeskMemo.Storage.SaveDatabase(database);
            }
            catch (Exception ex) { MessageBox.Show("退出前保存失败：" + ex.Message, "Nihongo Desk Memo"); }
        }
    }
}
