using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace NihongoDeskMemoWpf
{
    internal abstract class DarkDialog : Window
    {
        protected readonly Grid Body;

        protected DarkDialog(string title, double width, double height)
        {
            Title = title;
            Width = width;
            Height = height;
            MinWidth = Math.Min(width, 420);
            MinHeight = Math.Min(height, 300);
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            Border shell = new Border();
            shell.Background = UiBrush("#F518212D");
            shell.BorderBrush = UiBrush("#FF596A80");
            shell.BorderThickness = new Thickness(1);
            shell.CornerRadius = new CornerRadius(8);
            Content = shell;

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            shell.Child = root;

            Grid header = new Grid();
            header.Background = UiBrush("#FF222E3C");
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.MouseLeftButtonDown += HeaderMouseDown;
            root.Children.Add(header);

            TextBlock titleText = new TextBlock();
            titleText.Text = title;
            titleText.Foreground = UiBrush("#FFF4F7FA");
            titleText.FontSize = 15;
            titleText.FontWeight = FontWeights.SemiBold;
            titleText.VerticalAlignment = VerticalAlignment.Center;
            titleText.Margin = new Thickness(14, 0, 0, 0);
            header.Children.Add(titleText);

            Button close = UiButton("×");
            close.Width = 40;
            close.Margin = new Thickness(0);
            close.ToolTip = "关闭";
            close.Click += delegate { Close(); };
            Grid.SetColumn(close, 1);
            header.Children.Add(close);

            Body = new Grid();
            Body.Margin = new Thickness(14);
            Grid.SetRow(Body, 1);
            root.Children.Add(Body);
        }

        protected static SolidColorBrush UiBrush(string value)
        {
            SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            brush.Freeze();
            return brush;
        }

        protected static Button UiButton(string text)
        {
            Button button = new Button();
            button.Content = text;
            button.Foreground = UiBrush("#FFF3F6F9");
            button.Background = UiBrush("#FF354354");
            button.BorderBrush = UiBrush("#FF607086");
            button.BorderThickness = new Thickness(1);
            button.Padding = new Thickness(10, 5, 10, 5);
            button.Margin = new Thickness(4, 0, 0, 0);
            button.Cursor = Cursors.Hand;
            return button;
        }

        protected static TextBlock UiLabel(string text)
        {
            TextBlock label = new TextBlock();
            label.Text = text;
            label.Foreground = UiBrush("#FFBBC6D4");
            label.VerticalAlignment = VerticalAlignment.Center;
            return label;
        }

        protected static TextBox UiTextBox()
        {
            TextBox box = new TextBox();
            box.Foreground = Brushes.White;
            box.Background = UiBrush("#FF101824");
            box.BorderBrush = UiBrush("#FF526176");
            box.BorderThickness = new Thickness(1);
            box.Padding = new Thickness(7, 5, 7, 5);
            return box;
        }

        protected static ComboBox UiComboBox()
        {
            ComboBox box = new ComboBox();
            box.Foreground = UiBrush("#FF1A2430");
            box.Background = UiBrush("#FFE8EDF3");
            box.BorderBrush = UiBrush("#FF607086");
            box.Padding = new Thickness(6, 4, 6, 4);
            return box;
        }

        private void HeaderMouseDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is Button) return;
                source = DialogParentOf(source);
            }
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch (InvalidOperationException) { }
            }
        }

        private static DependencyObject DialogParentOf(DependencyObject source)
        {
            if (source is Visual || source is System.Windows.Media.Media3D.Visual3D)
            {
                return VisualTreeHelper.GetParent(source);
            }
            FrameworkContentElement content = source as FrameworkContentElement;
            if (content != null) return content.Parent;
            return LogicalTreeHelper.GetParent(source);
        }
    }

    internal sealed class WpfSettingsWindow : DarkDialog
    {
        private readonly NihongoDeskMemo.AppConfig config;
        private readonly NihongoDeskMemo.WordDatabase database;
        private readonly Action importAction;
        private readonly Action libraryAction;
        private readonly Action resetAction;
        private readonly TextBox refreshBox;
        private readonly CheckBox topMostBox;
        private readonly CheckBox transparentBox;
        private readonly CheckBox revealBox;
        private readonly CheckBox ratingButtonsBox;
        private readonly ComboBox modeBox;
        private readonly UnitSelectionControl unitSelection;
        private readonly TextBlock countText;

        public WpfSettingsWindow(
            NihongoDeskMemo.AppConfig appConfig,
            NihongoDeskMemo.WordDatabase wordDatabase,
            Action import,
            Action library,
            Action reset)
            : base("设置 · v" + AppVersion.Number, 560, 660)
        {
            Height = Math.Min(660, SystemParameters.WorkArea.Height - 32);
            config = appConfig;
            database = wordDatabase;
            importAction = import;
            libraryAction = library;
            resetAction = reset;

            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid form = new Grid();
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int i;
            for (i = 0; i < 8; i++) form.RowDefinitions.Add(new RowDefinition { Height = i == 6 ? GridLength.Auto : new GridLength(41) });
            Body.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 0, 0, 10) });

            refreshBox = UiTextBox();
            refreshBox.Text = Math.Max(3, config.RefreshSeconds).ToString();
            AddRow(form, "刷新间隔（秒）", refreshBox, 0);

            topMostBox = Check("窗口置顶", config.AlwaysOnTop);
            AddRow(form, "固定窗口", topMostBox, 1);
            transparentBox = Check("纯文字透明背景", config.BackgroundTransparent);
            AddRow(form, "背景", transparentBox, 2);
            revealBox = Check("答案需点击或按 F8 显示", config.HideMeaningUntilClick);
            AddRow(form, "答案显示", revealBox, 3);
            ratingButtonsBox = Check("显示“不会 / 模糊 / 记得 / 熟练”按钮", config.ShowRatingButtons);
            AddRow(form, "复习评级", ratingButtonsBox, 4);

            modeBox = UiComboBox();
            modeBox.Items.Add("日语认中文");
            modeBox.Items.Add("中文回忆日语");
            modeBox.Items.Add("假名回忆汉字");
            modeBox.Items.Add("汉字回忆读音");
            modeBox.SelectedIndex = Math.Max(0, Math.Min(3, config.ReviewMode));
            AddRow(form, "抽题方向", modeBox, 5);

            unitSelection = new UnitSelectionControl(config, database);
            AddRow(form, "练习单元", unitSelection, 6);

            countText = UiLabel(string.Empty);
            UpdateCount();
            AddRow(form, "词库", countText, 7);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetRow(actions, 1);
            Body.Children.Add(actions);

            Button importButton = UiButton("导入词表");
            importButton.Click += ImportClick;
            actions.Children.Add(importButton);
            Button libraryButton = UiButton("词库管理");
            libraryButton.Click += LibraryClick;
            actions.Children.Add(libraryButton);
            Button resetButton = UiButton("重置复习");
            resetButton.Click += ResetClick;
            actions.Children.Add(resetButton);
            Button saveButton = UiButton("保存并关闭");
            saveButton.Background = UiBrush("#FF397A68");
            saveButton.Click += SaveClick;
            actions.Children.Add(saveButton);
        }

        private static CheckBox Check(string text, bool value)
        {
            CheckBox box = new CheckBox();
            box.Content = text;
            box.IsChecked = value;
            box.Foreground = Brushes.White;
            box.VerticalAlignment = VerticalAlignment.Center;
            return box;
        }

        private static void AddRow(Grid form, string label, UIElement control, int row)
        {
            TextBlock text = UiLabel(label);
            Grid.SetRow(text, row);
            form.Children.Add(text);
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            if (control is FrameworkElement) ((FrameworkElement)control).VerticalAlignment = VerticalAlignment.Center;
            form.Children.Add(control);
        }

        private void PopulateUnits()
        {
            unitSelection.RefreshUnits(database);
        }

        private void UpdateCount()
        {
            countText.Text = "词条 " + database.Words.Count.ToString() + " 条";
        }

        private void ImportClick(object sender, RoutedEventArgs e)
        {
            if (importAction != null) importAction();
            UpdateCount();
            PopulateUnits();
        }

        private void LibraryClick(object sender, RoutedEventArgs e)
        {
            if (libraryAction != null) libraryAction();
            UpdateCount();
            PopulateUnits();
        }

        private void ResetClick(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this, "确定清空所有词条的复习进度吗？", "确认", MessageBoxButton.OKCancel) == MessageBoxResult.OK && resetAction != null)
            {
                resetAction();
            }
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            List<string> selected = unitSelection.SelectedUnits();
            if (!unitSelection.IsAll && selected.Count == 0)
            {
                MessageBox.Show(this, "请至少选择一个单元，或勾选全部词库。", "未选择练习单元");
                return;
            }
            int seconds;
            if (!int.TryParse(refreshBox.Text, out seconds)) seconds = 30;
            config.RefreshSeconds = Math.Max(3, Math.Min(3600, seconds));
            config.AlwaysOnTop = topMostBox.IsChecked == true;
            config.BackgroundTransparent = transparentBox.IsChecked == true;
            config.HideMeaningUntilClick = revealBox.IsChecked == true;
            config.ShowRatingButtons = ratingButtonsBox.IsChecked == true;
            config.ReviewMode = Math.Max(0, modeBox.SelectedIndex);
            PracticeScope.SetUnits(config, unitSelection.IsAll ? new List<string>() : selected);
            DialogResult = true;
            Close();
        }
    }

    internal sealed class WpfLibraryWindow : DarkDialog
    {
        private readonly NihongoDeskMemo.WordDatabase database;
        private readonly DataGrid grid;
        private readonly TextBox searchBox;
        private readonly ComboBox unitBox;
        private readonly TextBlock countText;

        public WpfLibraryWindow(NihongoDeskMemo.WordDatabase wordDatabase)
            : base("词库管理", 1040, 610)
        {
            database = wordDatabase;
            ResizeMode = ResizeMode.CanResize;
            MinWidth = 760;
            MinHeight = 440;

            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });

            Grid search = new Grid();
            search.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            search.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            search.Children.Add(UiLabel("搜索"));
            searchBox = UiTextBox();
            searchBox.TextChanged += SearchChanged;
            Grid.SetColumn(searchBox, 1);
            search.Children.Add(searchBox);
            Body.Children.Add(search);

            grid = new DataGrid();
            grid.AutoGenerateColumns = false;
            grid.CanUserAddRows = false;
            grid.CanUserDeleteRows = false;
            grid.SelectionMode = DataGridSelectionMode.Extended;
            grid.SelectionUnit = DataGridSelectionUnit.FullRow;
            grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
            grid.HorizontalGridLinesBrush = UiBrush("#FF344254");
            grid.Background = UiBrush("#FF111923");
            grid.Foreground = Brushes.White;
            grid.RowBackground = UiBrush("#FF182331");
            grid.AlternatingRowBackground = UiBrush("#FF1D2A39");
            grid.BorderBrush = UiBrush("#FF526176");
            grid.HeadersVisibility = DataGridHeadersVisibility.Column;
            grid.ColumnHeaderStyle = HeaderStyle();
            AddTextColumn("Japanese", "单词", 130, false);
            AddTextColumn("Pinyin", "假名/读音", 130, false);
            AddTextColumn("Chinese", "意思", 220, false);
            AddTextColumn("Unit", "单元", 100, false);
            AddTextColumn("Weight", "权重", 58, false);
            DataGridCheckBoxColumn marked = new DataGridCheckBoxColumn();
            marked.Header = "标记";
            marked.Binding = new Binding("Marked");
            marked.Width = 52;
            grid.Columns.Add(marked);
            AddTextColumn("ReviewCount", "复习", 55, true);
            AddTextColumn("WrongCount", "错误", 55, true);
            AddTextColumn("CorrectStreak", "连续", 55, true);
            AddTextColumn("IntervalDays", "间隔天", 65, true);
            Grid.SetRow(grid, 1);
            Body.Children.Add(grid);

            countText = UiLabel(string.Empty);
            Grid.SetRow(countText, 2);
            Body.Children.Add(countText);

            Grid footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(footer, 3);
            Body.Children.Add(footer);

            StackPanel unitActions = new StackPanel { Orientation = Orientation.Horizontal };
            unitBox = UiComboBox();
            unitBox.IsEditable = true;
            unitBox.Width = 150;
            unitActions.Children.Add(unitBox);
            Button assign = UiButton("划分单元");
            assign.Click += AssignClick;
            unitActions.Children.Add(assign);
            footer.Children.Add(unitActions);

            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetColumn(actions, 1);
            footer.Children.Add(actions);
            Button add = UiButton("新增");
            add.Click += AddClick;
            actions.Children.Add(add);
            Button delete = UiButton("删除");
            delete.Click += DeleteClick;
            actions.Children.Add(delete);
            Button save = UiButton("保存");
            save.Background = UiBrush("#FF397A68");
            save.Click += SaveClick;
            actions.Children.Add(save);

            Closing += WindowClosing;
            PopulateUnitBox();
            RefreshGrid();
        }

        private static Style HeaderStyle()
        {
            Style style = new Style(typeof(DataGridColumnHeader));
            style.Setters.Add(new Setter(Control.BackgroundProperty, UiBrush("#FF293748")));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, UiBrush("#FF526176")));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(7, 6, 7, 6)));
            return style;
        }

        private void AddTextColumn(string property, string header, double width, bool readOnly)
        {
            DataGridTextColumn column = new DataGridTextColumn();
            column.Header = header;
            column.Binding = new Binding(property) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus };
            column.Width = width;
            column.IsReadOnly = readOnly;
            grid.Columns.Add(column);
        }

        private void PopulateUnitBox()
        {
            string current = unitBox.Text;
            unitBox.Items.Clear();
            List<string> units = NihongoDeskMemo.Storage.GetUnits(database);
            int i;
            for (i = 0; i < units.Count; i++) unitBox.Items.Add(units[i]);
            unitBox.Text = current;
        }

        private void RefreshGrid()
        {
            string query = NihongoDeskMemo.Storage.SafeTrim(searchBox.Text).ToLowerInvariant();
            List<NihongoDeskMemo.WordItem> shown = new List<NihongoDeskMemo.WordItem>();
            int i;
            for (i = 0; i < database.Words.Count; i++)
            {
                NihongoDeskMemo.WordItem item = database.Words[i];
                if (query.Length == 0 || Contains(item.Japanese, query) || Contains(item.Pinyin, query) ||
                    Contains(item.Chinese, query) || Contains(item.Unit, query)) shown.Add(item);
            }
            grid.ItemsSource = shown;
            countText.Text = "显示 " + shown.Count.ToString() + " 条 / 共 " + database.Words.Count.ToString() + " 条";
        }

        private static bool Contains(string value, string query)
        {
            return value != null && value.ToLowerInvariant().Contains(query);
        }

        private void SearchChanged(object sender, TextChangedEventArgs e)
        {
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            RefreshGrid();
        }

        private void AssignClick(object sender, RoutedEventArgs e)
        {
            string unit = NihongoDeskMemo.Storage.SafeTrim(unitBox.Text);
            foreach (object selected in grid.SelectedItems)
            {
                NihongoDeskMemo.WordItem item = selected as NihongoDeskMemo.WordItem;
                if (item != null) item.Unit = unit;
            }
            NihongoDeskMemo.Storage.SaveDatabase(database);
            PopulateUnitBox();
            RefreshGrid();
        }

        private void AddClick(object sender, RoutedEventArgs e)
        {
            NihongoDeskMemo.WordItem item = new NihongoDeskMemo.WordItem();
            database.Words.Add(item);
            RefreshGrid();
            grid.SelectedItem = item;
            grid.ScrollIntoView(item);
        }

        private void DeleteClick(object sender, RoutedEventArgs e)
        {
            if (grid.SelectedItems.Count == 0) return;
            if (MessageBox.Show(this, "确定删除选中的词条吗？", "确认", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            List<NihongoDeskMemo.WordItem> selected = new List<NihongoDeskMemo.WordItem>();
            foreach (object value in grid.SelectedItems)
            {
                NihongoDeskMemo.WordItem item = value as NihongoDeskMemo.WordItem;
                if (item != null) selected.Add(item);
            }
            int i;
            for (i = 0; i < selected.Count; i++) database.Words.Remove(selected[i]);
            SaveDatabase();
            RefreshGrid();
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            SaveDatabase();
            PopulateUnitBox();
            RefreshGrid();
        }

        private void SaveDatabase()
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            int i;
            for (i = database.Words.Count - 1; i >= 0; i--)
            {
                NihongoDeskMemo.WordItem item = database.Words[i];
                if (item == null || NihongoDeskMemo.Storage.SafeTrim(item.Japanese).Length == 0 ||
                    NihongoDeskMemo.Storage.SafeTrim(item.Chinese).Length == 0) database.Words.RemoveAt(i);
            }
            NihongoDeskMemo.Storage.SaveDatabase(database);
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveDatabase();
        }
    }
}
