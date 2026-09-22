using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MarkdownWPF;
using InventorModel.Core.Diagnostics;

namespace InventorModel.Addin;

internal sealed class MarkdownTextBlock : ContentControl
{
    private readonly MarkdownViewer _viewer;
    private readonly DispatcherTimer _renderTimer;
    private readonly TextBlock _fallback;
    private readonly TextBox _selectionBox;
    private readonly StringBuilder _markdown = new StringBuilder();

    private bool _renderFailureLogged;
    private bool _selectionMode;
    private string _uiLanguage = AiSettings.LanguageChinese;

    public MarkdownTextBlock()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Top;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        MinWidth = 0;

        try
        {
            Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/InventorModel.Addin;component/UI/Themes/Markdown.xaml",
                        UriKind.Absolute)
                });
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "UI.Markdown",
                "Markdown theme resources could not be loaded.",
                ex);
        }

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

        _selectionBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Height = double.NaN,
            Background = System.Windows.Media.Brushes.Transparent,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0),
            MinHeight = 24
        };
        _selectionBox.PreviewKeyDown += SelectionBox_PreviewKeyDown;

        ContextMenu = BuildContextMenu();
        Content = _viewer;

        _renderTimer =
            new DispatcherTimer(DispatcherPriority.Background)
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

    public bool IsSelectionMode => _selectionMode;

    public string UiLanguage
    {
        get => _uiLanguage;
        set
        {
            _uiLanguage =
                AiSettings.NormalizeUiLanguage(value);
            ContextMenu = BuildContextMenu();
        }
    }

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

    public void EnterSelectionMode()
    {
        _renderTimer.Stop();
        UpdateSelectionText();

        _selectionMode = true;
        Content = _selectionBox;
        ContextMenu = BuildContextMenu();

        _selectionBox.Focus();
        Keyboard.Focus(_selectionBox);
    }

    public void ExitSelectionMode()
    {
        _selectionMode = false;
        ContextMenu = BuildContextMenu();
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

        if (_selectionMode)
        {
            int start = _selectionBox.SelectionStart;
            int length = _selectionBox.SelectionLength;
            UpdateSelectionText();
            _selectionBox.Select(
                Math.Min(start, _selectionBox.Text.Length),
                Math.Min(
                    length,
                    Math.Max(
                        0,
                        _selectionBox.Text.Length -
                        Math.Min(start, _selectionBox.Text.Length))));
            return;
        }

        try
        {
            _viewer.Markdown = text;
            if (!ReferenceEquals(Content, _viewer))
                Content = _viewer;
        }
        catch (Exception ex)
        {
            if (!_renderFailureLogged)
            {
                _renderFailureLogged = true;
                RuntimeLog.Warning(
                    "UI.Markdown",
                    "Markdown rendering failed; plain-text fallback is active.",
                    ex);
            }

            _fallback.Text = text;
            _fallback.Visibility = Visibility.Visible;
            Content = _fallback;
        }
    }

    private void UpdateSelectionText()
    {
        _selectionBox.Text = _markdown.ToString();
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        if (_selectionMode)
        {
            var copySelection = new MenuItem
            {
                Header = UiText.Get(
                    _uiLanguage,
                    "Markdown.CopySelection")
            };
            copySelection.Click += (_, __) =>
            {
                if (!string.IsNullOrEmpty(
                        _selectionBox.SelectedText))
                {
                    Clipboard.SetText(
                        _selectionBox.SelectedText);
                }
            };
            menu.Items.Add(copySelection);

            var preview = new MenuItem
            {
                Header = UiText.Get(
                    _uiLanguage,
                    "Markdown.Preview")
            };
            preview.Click += (_, __) =>
                ExitSelectionMode();
            menu.Items.Add(preview);
        }
        else
        {
            var select = new MenuItem
            {
                Header = UiText.Get(
                    _uiLanguage,
                    "Markdown.Select")
            };
            select.Click += (_, __) =>
                EnterSelectionMode();
            menu.Items.Add(select);
        }

        menu.Items.Add(new Separator());

        var copyAll = new MenuItem
        {
            Header = UiText.Get(
                _uiLanguage,
                "Markdown.CopyAll")
        };
        copyAll.Click += (_, __) =>
        {
            if (_markdown.Length > 0)
                Clipboard.SetText(_markdown.ToString());
        };
        menu.Items.Add(copyAll);

        return menu;
    }

    private void SelectionBox_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            ExitSelectionMode();
        }
    }
}
