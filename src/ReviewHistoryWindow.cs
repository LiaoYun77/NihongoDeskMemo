using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal sealed class ReviewHistoryWindow : DarkDialog
    {
        private readonly WordDatabase database;
        private readonly WordItem word;
        private readonly ComboBox direction;
        private readonly DataGrid events;
        private readonly TextBlock count;

        public ReviewHistoryWindow(WordDatabase source, WordItem selected) : base("复习记录 · " + selected.Japanese, 940, 610)
        {
            database = source; word = selected;
            ResizeMode = ResizeMode.CanResize; MinWidth = 720; MinHeight = 440;
            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(180) });
            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            Body.RowDefinitions.Add(new RowDefinition());
            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            DataGrid states = CreateGrid();
            Add(states, "Direction", "方向", 145); Add(states, "Reviews", "复习", 60); Add(states, "Wrong", "错误", 60);
            Add(states, "Streak", "连续", 60); Add(states, "Difficulty", "难度", 65); Add(states, "Interval", "间隔天", 65);
            Add(states, "Next", "下次复习", 185);
            List<object> rows = new List<object>();
            for (int i = 0; i < 4; i++)
            {
                DirectionProgress state = ReviewData.For(word, i);
                rows.Add(new { Direction = ReviewData.DirectionNames[i], Reviews = state.ReviewCount, Wrong = state.WrongCount,
                    Streak = state.CorrectStreak, Difficulty = state.Difficulty.ToString("0.##"), Interval = state.IntervalDays,
                    Next = LocalTime(state.NextReviewTime) });
            }
            states.ItemsSource = rows; Body.Children.Add(states);
            direction = UiComboBox(); direction.Width = 200; direction.HorizontalAlignment = HorizontalAlignment.Left;
            direction.VerticalAlignment = VerticalAlignment.Center;
            direction.Items.Add("全部方向"); foreach (string name in ReviewData.DirectionNames) direction.Items.Add(name);
            Grid.SetRow(direction, 1); Body.Children.Add(direction);
            events = CreateGrid();
            Add(events, "Time", "时间", 170); Add(events, "Direction", "方向", 145); Add(events, "Rating", "评级", 65);
            Add(events, "Next", "当次计划复习时间", 175); Add(events, "Status", "状态", 80);
            Grid.SetRow(events, 2); Body.Children.Add(events);
            count = UiLabel(string.Empty); Grid.SetRow(count, 3); Body.Children.Add(count);
            direction.SelectionChanged += delegate { RefreshEvents(); }; direction.SelectedIndex = 0;
        }

        private void RefreshEvents()
        {
            List<object> rows = new List<object>();
            for (int i = database.ReviewEvents.Count - 1; i >= 0; i--)
            {
                ReviewEvent entry = database.ReviewEvents[i];
                if (entry.WordId != word.WordId || (direction.SelectedIndex > 0 && entry.Direction != direction.SelectedIndex - 1)) continue;
                rows.Add(new { Time = LocalTime(entry.ReviewedAt), Direction = ReviewData.DirectionNames[entry.Direction],
                    Rating = ReviewData.RatingNames[entry.Rating], Next = LocalTime(entry.NextReviewTime), Status = entry.Undone ? "已撤销" : "有效" });
            }
            events.ItemsSource = rows;
            count.Text = "记录 " + rows.Count + " 条 · 词条编号 " + word.WordId;
        }

        private static string LocalTime(string value)
        {
            DateTime time;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out time)
                ? time.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "待复习";
        }

        private static DataGrid CreateGrid()
        {
            return new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column, ColumnHeaderStyle = HeaderStyle(),
                RowHeight = 30, Background = UiBrush("#FF182331"), RowBackground = UiBrush("#FF182331"),
                AlternatingRowBackground = UiBrush("#FF223041"), Foreground = Brushes.White };
        }

        private static void Add(DataGrid grid, string property, string label, int width)
        { grid.Columns.Add(new DataGridTextColumn { Header = label, Binding = new Binding(property), Width = width }); }
    }
}
