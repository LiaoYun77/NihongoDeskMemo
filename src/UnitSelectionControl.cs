using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace NihongoDeskMemoWpf
{
    internal sealed class UnitSelectionControl : StackPanel
    {
        private readonly CheckBox allBox;
        private readonly UniformGrid choices;
        private readonly TextBlock countText;
        private readonly List<CheckBox> boxes = new List<CheckBox>();

        public UnitSelectionControl(NihongoDeskMemo.AppConfig config, NihongoDeskMemo.WordDatabase database)
        {
            List<string> selected = PracticeScope.GetUnits(config);
            allBox = new CheckBox { Content = "全部词库", Foreground = Brushes.White,
                IsChecked = selected.Count == 0, Margin = new Thickness(0, 3, 0, 8) };
            Children.Add(allBox);
            choices = new UniformGrid { Columns = 2 };
            Children.Add(new ScrollViewer { Content = choices, Height = 154,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            countText = new TextBlock { Foreground = Brushes.LightGray, Margin = new Thickness(0, 6, 0, 0) };
            Children.Add(countText);
            allBox.Checked += SelectionChanged;
            allBox.Unchecked += SelectionChanged;
            Populate(database, selected);
        }

        public bool IsAll { get { return allBox.IsChecked == true; } }

        public List<string> SelectedUnits()
        {
            List<string> units = new List<string>();
            foreach (CheckBox box in boxes)
                if (box.IsChecked == true) units.Add((string)box.Tag);
            return units;
        }

        public void RefreshUnits(NihongoDeskMemo.WordDatabase database)
        {
            Populate(database, SelectedUnits());
        }

        private void Populate(NihongoDeskMemo.WordDatabase database, List<string> selected)
        {
            HashSet<string> selectedSet = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
            List<string> available = NihongoDeskMemo.Storage.GetUnits(database);
            HashSet<string> present = new HashSet<string>(available, StringComparer.OrdinalIgnoreCase);
            foreach (string unit in selected) if (!present.Contains(unit)) available.Add(unit);
            boxes.Clear();
            choices.Children.Clear();
            foreach (string unit in available)
            {
                string label = present.Contains(unit) ? unit : unit + "（无词条）";
                CheckBox box = new CheckBox { Tag = unit, Foreground = Brushes.White,
                    IsChecked = selectedSet.Contains(unit), Margin = new Thickness(0, 4, 8, 4),
                    Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, ToolTip = label };
                box.Checked += SelectionChanged;
                box.Unchecked += SelectionChanged;
                boxes.Add(box);
                choices.Children.Add(box);
            }
            SelectionChanged(this, new RoutedEventArgs());
        }

        private void SelectionChanged(object sender, RoutedEventArgs e)
        {
            choices.IsEnabled = !IsAll;
            countText.Text = IsAll ? "全部词库" : "已选 " + SelectedUnits().Count + " 个单元";
        }
    }
}
