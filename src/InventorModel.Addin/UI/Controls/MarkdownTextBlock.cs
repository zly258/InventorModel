using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MarkdownWPF;

namespace InventorModel.Addin;

internal sealed class MarkdownTextBlock : ContentControl
{
    private readonly MarkdownViewer _viewer;
    private readonly DispatcherTimer _renderTimer;
    private readonly TextBlock _fallback;
    private readonly StringBuilder _markdown = new StringBuilder();

    public MarkdownTextBlock()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Top;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        MinWidth = 0;

        try
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/InventorModel.Addin;component/UI/Themes/Markdown.xaml",
                    UriKind.Absolute)
            });
        }
        catch { }

        _viewer = new MarkdownViewer
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            IsScrollViewerEnabled = false
        };

        _fallback = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };

        Content = _viewer;
        _renderTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(70)
        };
        _renderTimer.Tick += (_, __) =>
        {
            _renderTimer.Stop();
            Render();
        };
        Unloaded += (_, __) => _renderTimer.Stop();
    }

    public string Markdown => _markdown.ToString();

    public void SetMarkdown(string value)
    {
        _markdown.Clear();
        _markdown.Append(value ?? string.Empty);
        ScheduleRender();
    }

    public void Append(string value)
    {
        if (!string.IsNullOrEmpty(value))
            _markdown.Append(value);
        ScheduleRender();
    }

    public void Clear()
    {
        _markdown.Clear();
        ScheduleRender();
    }

    public void Flush()
    {
        _renderTimer.Stop();
        Render();
    }

    private void ScheduleRender()
    {
        if (!_renderTimer.IsEnabled)
            _renderTimer.Start();
    }

    private void Render()
    {
        string text = _markdown.ToString();
        try
        {
            _viewer.Markdown = text;
            if (!ReferenceEquals(Content, _viewer))
                Content = _viewer;
        }
        catch
        {
            _fallback.Text = text;
            _fallback.Visibility = Visibility.Visible;
            Content = _fallback;
        }
    }
}
