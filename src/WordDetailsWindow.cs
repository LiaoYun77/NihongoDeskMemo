using System.Windows;
using System.Windows.Controls;
using NihongoDeskMemo;

namespace NihongoDeskMemoWpf
{
    internal sealed class WordDetailsWindow : DarkDialog
    {
        public WordDetailsWindow(WordItem word) : base("例句与来源 · " + word.Japanese, 660, 520)
        {
            ResizeMode = ResizeMode.CanResize; MinWidth = 420; MinHeight = 330;
            StackPanel content = new StackPanel();
            Add(content, "单词", word.Japanese + "   " + word.Pinyin);
            Add(content, "中文意思", word.Chinese);
            Add(content, "日语例句", word.ExampleJapanese);
            Add(content, "例句翻译", word.ExampleChinese);
            Add(content, "来源", word.SourceLabel);
            Add(content, "原始记录", word.SourceText);
            Body.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        }

        private static void Add(StackPanel panel, string title, string value)
        {
            TextBlock label = UiLabel(title); label.Margin = new Thickness(0, 8, 0, 4); panel.Children.Add(label);
            TextBox text = UiTextBox(); text.IsReadOnly = true; text.TextWrapping = TextWrapping.Wrap;
            text.Text = string.IsNullOrEmpty(value) ? "（未填写）" : value;
            text.BorderThickness = new Thickness(0); text.Background = System.Windows.Media.Brushes.Transparent;
            panel.Children.Add(text);
        }
    }
}
