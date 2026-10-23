using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal sealed class LearningStatisticsWindow : DarkDialog
    {
        private readonly WordDatabase database;
        private readonly AppConfig config;
        private readonly ComboBox direction;
        private readonly DataGrid weakWords;
        private readonly TextBlock totals;

        public LearningStatisticsWindow(WordDatabase data, AppConfig appConfig) : base("学习统计", 900, 560)
        {
            database = data; config = appConfig;
            ResizeMode = ResizeMode.CanResize; MinWidth = 620; MinHeight = 380;
            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            Body.RowDefinitions.Add(new RowDefinition());
            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            Grid filters = new Grid();
            filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            filters.ColumnDefinitions.Add(new ColumnDefinition());
            filters.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            direction = UiComboBox();
            foreach (string name in ReviewData.DirectionNames) direction.Items.Add(name);
            direction.SelectedIndex = Math.Max(0, Math.Min(3, config.ReviewMode));
            filters.Children.Add(direction);
            TextBlock scope = UiLabel(PracticeScope.Summary(config)); scope.TextTrimming = TextTrimming.CharacterEllipsis;
            scope.Margin = new Thickness(12, 0, 8, 0); Grid.SetColumn(scope, 1); filters.Children.Add(scope);
            Button refresh = UiButton("↻"); refresh.ToolTip = "刷新统计";
            refresh.Click += delegate { Refresh(); }; Grid.SetColumn(refresh, 2); filters.Children.Add(refresh);
            Body.Children.Add(filters);
            totals = UiLabel(string.Empty); Grid.SetRow(totals, 1); Body.Children.Add(totals);
            weakWords = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
                SelectionMode = DataGridSelectionMode.Single, HeadersVisibility = DataGridHeadersVisibility.Column,
                ColumnHeaderStyle = HeaderStyle(), Background = UiBrush("#FF182331"), RowBackground = UiBrush("#FF182331"),
                AlternatingRowBackground = UiBrush("#FF223041"), Foreground = System.Windows.Media.Brushes.White };
            AddColumn("Japanese", "薄弱词", 140); AddColumn("Reading", "假名", 140); AddColumn("Chinese", "意思", 220);
            AddColumn("Unit", "单元", 90); AddColumn("Difficulty", "难度", 65); AddColumn("WrongCount", "错误", 60); AddColumn("CorrectStreak", "连续", 60);
            Grid.SetRow(weakWords, 2); Body.Children.Add(weakWords);
            StackPanel footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom };
            Button details = UiButton("例句与来源"); details.Click += delegate
            {
                WeakWordRow selected = weakWords.SelectedItem as WeakWordRow;
                if (selected != null) new WordDetailsWindow(selected.Word) { Owner = this }.ShowDialog();
            };
            Button history = UiButton("复习记录"); history.Click += delegate
            {
                WeakWordRow selected = weakWords.SelectedItem as WeakWordRow;
                if (selected != null) new ReviewHistoryWindow(database, selected.Word) { Owner = this }.ShowDialog();
            };
            details.IsEnabled = history.IsEnabled = false;
            weakWords.SelectionChanged += delegate { details.IsEnabled = history.IsEnabled = weakWords.SelectedItem != null; };
            footer.Children.Add(details); footer.Children.Add(history);
            Grid.SetRow(footer, 3); Body.Children.Add(footer);
            direction.SelectionChanged += delegate { Refresh(); };
            Refresh();
        }

        private void AddColumn(string property, string header, int width)
        {
            Binding binding = new Binding(property);
            if (property == "Difficulty") binding.StringFormat = "0.0";
            weakWords.Columns.Add(new DataGridTextColumn { Header = header, Binding = binding, Width = width });
        }

        private void Refresh()
        {
            LearningStatistics stats = LearningStatistics.Calculate(database, config, direction.SelectedIndex, DateTimeOffset.Now, TimeZoneInfo.Local);
            totals.Text = "今日评级 " + stats.TodayRatings + "   ·   到期 " + stats.DueCount + " / " + stats.TotalWords + "   ·   薄弱词 " + stats.WeakWords.Count;
            weakWords.ItemsSource = stats.WeakWords;
        }
    }
}
