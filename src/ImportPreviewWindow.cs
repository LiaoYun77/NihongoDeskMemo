using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal sealed class ImportPreviewWindow : DarkDialog
    {
        private readonly string path;
        private readonly WordDatabase database;
        private readonly TextBlock summary;
        private readonly DataGrid grid;
        private readonly CheckBox updateBox;
        private readonly Button confirm;
        public ImportPreview Plan { get; private set; }

        public ImportPreviewWindow(string file, WordDatabase words) : base("导入预览 · " + Path.GetFileName(file), 960, 580)
        {
            path = file; database = words;
            ResizeMode = ResizeMode.CanResize; MinWidth = 680; MinHeight = 380;
            Body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            Body.RowDefinitions.Add(new RowDefinition());
            Body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });
            summary = UiLabel(string.Empty); summary.TextWrapping = TextWrapping.Wrap;
            Body.Children.Add(summary);
            updateBox = new CheckBox { Content = "更新已有单词的读音和意思", Foreground = System.Windows.Media.Brushes.White,
                VerticalAlignment = VerticalAlignment.Center, IsEnabled = !string.Equals(Path.GetExtension(file), ".xml", StringComparison.OrdinalIgnoreCase) };
            Grid.SetRow(updateBox, 1); Body.Children.Add(updateBox);
            grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
                RowHeight = 30, Background = UiBrush("#FF182331"), RowBackground = UiBrush("#FF182331"),
                AlternatingRowBackground = UiBrush("#FF223041"), Foreground = System.Windows.Media.Brushes.White };
            AddColumn("Line", "行", 45); AddColumn("Status", "处理", 205); AddColumn("Japanese", "单词", 130);
            AddColumn("Reading", "假名", 130); AddColumn("Chinese", "意思", 200); AddColumn("Unit", "单元", 90);
            AddColumn("Original", "原文", 300);
            Grid.SetRow(grid, 2); Body.Children.Add(grid);
            StackPanel footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom };
            Button cancel = UiButton("取消"); cancel.Click += delegate { Close(); }; footer.Children.Add(cancel);
            confirm = UiButton("确认导入"); confirm.Background = UiBrush("#FF397A68");
            confirm.Click += delegate { DialogResult = true; }; footer.Children.Add(confirm);
            Grid.SetRow(footer, 3); Body.Children.Add(footer);
            updateBox.Checked += delegate { RefreshPlan(); }; updateBox.Unchecked += delegate { RefreshPlan(); };
            RefreshPlan();
        }

        private void AddColumn(string property, string title, int width)
        { grid.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(property), Width = width }); }

        private void RefreshPlan()
        {
            try
            {
                Plan = WordImporter.PreviewFile(path, database, updateBox.IsChecked == true);
                grid.ItemsSource = Plan.Rows;
                summary.Text = "新增 " + Plan.Result.Added + " · 更新 " + Plan.Result.Updated + " · 已有 " + Plan.Result.Unchanged +
                    " · 重复跳过 " + Plan.Result.Skipped + " · 错误 " + Plan.Errors;
                confirm.IsEnabled = Plan.CanApply;
            }
            catch (Exception ex)
            {
                Plan = null; confirm.IsEnabled = false; grid.ItemsSource = null;
                summary.Text = "预览失败：" + (ex.InnerException ?? ex).Message;
            }
        }
    }
}
