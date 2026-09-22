using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace InventorModel.Addin;

internal sealed class AiChatWindow : Window
{
    private static readonly Brush WindowBackground = Brush(247, 248, 250);
    private static readonly Brush PanelBackground = Brush(255, 255, 255);
    private static readonly Brush BorderBrush = Brush(218, 220, 224);
    private static readonly Brush AccentBrush = Brush(25, 103, 210);
    private static readonly Brush AccentSoftBrush = Brush(232, 240, 254);
    private static readonly Brush SecondaryTextBrush = Brush(95, 99, 104);
    private static readonly Brush UserBubbleBrush = Brush(232, 240, 254);

    private readonly global::Inventor.Application _application;
    private readonly StackPanel _conversation = new StackPanel();
    private readonly ScrollViewer _scroll = new ScrollViewer();
    private readonly TextBox _input = new TextBox();
    private readonly TextBlock _modelLabel = new TextBlock();
    private readonly TextBlock _statusLabel = new TextBlock();
    private readonly TextBlock _attachmentLabel = new TextBlock();
    private readonly Button _send = new Button();
    private readonly Button _stop = new Button();
    private readonly Button _removeAttachment = new Button();

    private AiSettings _settings;
    private AiAgentSession _session;
    private CancellationTokenSource? _cancellation;
    private MarkdownTextBlock? _assistantText;
    private TextBlock? _activityText;
    private string _imagePath = string.Empty;

    public AiChatWindow(global::Inventor.Application application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _settings = AiSettings.Load();
        _session = new AiAgentSession(_application, Dispatcher, _settings);

        Title = "InventorModel · AI建模";
        Width = 720;
        Height = 820;
        MinWidth = 540;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = true;
        Background = WindowBackground;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        Content = BuildLayout();
        UpdateHeader();
        AddNotice("描述你要创建的零件，也可以附加工程图或参考图片。AI 会先生成并验证 .imodel，再调用 Inventor 完成建模。");
        Closed += (_, __) => Shutdown();
    }

    public void AttachOwner(IntPtr owner)
    {
        if (owner == IntPtr.Zero) return;
        try { new System.Windows.Interop.WindowInteropHelper(this).Owner = owner; }
        catch { }
    }

    public void FocusInput()
    {
        _input.Focus();
        Keyboard.Focus(_input);
    }

    public void ReloadSettings()
    {
        _settings = AiSettings.Load();
        _session.Dispose();
        _session = new AiAgentSession(_application, Dispatcher, _settings);
        UpdateHeader();
        AddNotice("AI 配置已更新，新的配置会用于后续对话。");
    }

    private UIElement BuildLayout()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Border header = BuildHeader();
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _scroll.Content = _conversation;
        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _scroll.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _scroll.Padding = new Thickness(16, 14, 16, 8);
        _conversation.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetRow(_scroll, 1);
        root.Children.Add(_scroll);

        Border composer = BuildComposer();
        Grid.SetRow(composer, 2);
        root.Children.Add(composer);

        return root;
    }

    private Border BuildHeader()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titlePanel.Children.Add(new TextBlock
        {
            Text = "AI 建模",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(32, 33, 36)
        });

        _modelLabel.Margin = new Thickness(0, 3, 0, 0);
        _modelLabel.Foreground = SecondaryTextBrush;
        _modelLabel.FontSize = 11;
        _modelLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        titlePanel.Children.Add(_modelLabel);
        Grid.SetColumn(titlePanel, 0);
        grid.Children.Add(titlePanel);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        _statusLabel.Foreground = AccentBrush;
        _statusLabel.FontSize = 11;
        _statusLabel.Text = "就绪";
        _statusLabel.VerticalAlignment = VerticalAlignment.Center;
        actions.Children.Add(new Border
        {
            Child = _statusLabel,
            Background = AccentSoftBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 8, 0)
        });

        Button newButton = ToolbarButton("新对话");
        Button imageButton = ToolbarButton("图片");
        Button historyButton = ToolbarButton("历史");
        Button settingsButton = ToolbarButton("设置");

        newButton.Click += (_, __) => NewConversation();
        imageButton.Click += (_, __) => AttachImage();
        historyButton.Click += (_, __) => OpenHistory();
        settingsButton.Click += (_, __) => OpenSettings();

        actions.Children.Add(newButton);
        actions.Children.Add(imageButton);
        actions.Children.Add(historyButton);
        actions.Children.Add(settingsButton);

        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        return new Border
        {
            Child = grid,
            Padding = new Thickness(16, 11, 14, 11),
            Background = PanelBackground,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
    }

    private Border BuildComposer()
    {
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var attachment = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        attachment.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        attachment.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _attachmentLabel.Foreground = SecondaryTextBrush;
        _attachmentLabel.FontSize = 11;
        _attachmentLabel.VerticalAlignment = VerticalAlignment.Center;
        _attachmentLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        attachment.Children.Add(_attachmentLabel);

        _removeAttachment.Content = "移除";
        _removeAttachment.Visibility = Visibility.Collapsed;
        _removeAttachment.Margin = new Thickness(8, 0, 0, 0);
        _removeAttachment.Padding = new Thickness(8, 3, 8, 3);
        _removeAttachment.Background = Brushes.Transparent;
        _removeAttachment.BorderBrush = BorderBrush;
        _removeAttachment.BorderThickness = new Thickness(1);
        _removeAttachment.Click += (_, __) => ClearAttachment();
        Grid.SetColumn(_removeAttachment, 1);
        attachment.Children.Add(_removeAttachment);

        Grid.SetRow(attachment, 0);
        layout.Children.Add(attachment);

        _input.AcceptsReturn = true;
        _input.TextWrapping = TextWrapping.Wrap;
        _input.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _input.MinHeight = 84;
        _input.MaxHeight = 190;
        _input.Padding = new Thickness(11, 9, 11, 9);
        _input.Background = PanelBackground;
        _input.BorderBrush = BorderBrush;
        _input.BorderThickness = new Thickness(1);
        _input.KeyDown += Input_KeyDown;
        _input.ToolTip = "输入建模要求。Ctrl+Enter 发送。";
        Grid.SetRow(_input, 1);
        layout.Children.Add(_input);

        var bottom = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        bottom.Children.Add(new TextBlock
        {
            Text = "Ctrl+Enter 发送",
            Foreground = SecondaryTextBrush,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        });

        _stop.Content = "停止";
        _stop.Width = 68;
        _stop.Height = 32;
        _stop.Margin = new Thickness(0, 0, 8, 0);
        _stop.IsEnabled = false;
        _stop.Background = PanelBackground;
        _stop.BorderBrush = BorderBrush;
        _stop.BorderThickness = new Thickness(1);
        _stop.Click += (_, __) => _cancellation?.Cancel();
        Grid.SetColumn(_stop, 1);
        bottom.Children.Add(_stop);

        _send.Content = "发送";
        _send.Width = 72;
        _send.Height = 32;
        _send.Foreground = Brushes.White;
        _send.Background = AccentBrush;
        _send.BorderBrush = AccentBrush;
        _send.BorderThickness = new Thickness(1);
        _send.FontWeight = FontWeights.SemiBold;
        _send.Click += async (_, __) => await SendAsync();
        Grid.SetColumn(_send, 2);
        bottom.Children.Add(_send);

        Grid.SetRow(bottom, 2);
        layout.Children.Add(bottom);

        return new Border
        {
            Child = layout,
            Padding = new Thickness(16, 10, 16, 14),
            Background = PanelBackground,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
    }

    private static Button ToolbarButton(string text) =>
        new Button
        {
            Content = text,
            MinWidth = 54,
            Height = 28,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(8, 2, 8, 2),
            Background = Brushes.Transparent,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            Foreground = Brush(60, 64, 67)
        };

    private async void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        e.Handled = true;
        await SendAsync();
    }

    private async Task SendAsync()
    {
        if (_cancellation != null) return;

        string prompt = (_input.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prompt) && string.IsNullOrWhiteSpace(_imagePath))
            return;

        string attached = _imagePath;
        string userText = prompt;
        if (!string.IsNullOrWhiteSpace(attached))
        {
            string imageNote = "附件：" + Path.GetFileName(attached);
            userText = string.IsNullOrWhiteSpace(userText)
                ? imageNote
                : userText + Environment.NewLine + imageNote;
        }

        AddUserMessage(userText);
        _input.Clear();
        ClearAttachment();

        MarkdownTextBlock assistantText = AddAssistantMessage();
        TextBlock activityText = AddActivity();
        _assistantText = assistantText;
        _activityText = activityText;

        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        SetBusy(true);

        try
        {
            string final = await _session.SendAsync(
                prompt,
                attached,
                () => Dispatcher.BeginInvoke(new Action(() => _assistantText?.Clear())),
                delta => Dispatcher.BeginInvoke(new Action(() =>
                {
                    _assistantText?.Append(delta);
                    _scroll.ScrollToEnd();
                })),
                activity => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_activityText != null)
                        _activityText.Text = activity;
                    _scroll.ScrollToEnd();
                })),
                cancellation.Token);

            if (string.IsNullOrWhiteSpace(assistantText.Markdown))
                assistantText.SetMarkdown(string.IsNullOrWhiteSpace(final) ? "已完成。" : final);

            assistantText.Flush();
            activityText.Text = string.Empty;
            _statusLabel.Text = "完成";
        }
        catch (OperationCanceledException)
        {
            activityText.Text = "已停止。";
            _statusLabel.Text = "已停止";
        }
        catch (Exception ex)
        {
            assistantText.SetMarkdown("**错误**\n\n" + EscapeMarkdown(Compact(ex.Message)));
            assistantText.Flush();
            activityText.Text = string.Empty;
            _statusLabel.Text = "失败";
        }
        finally
        {
            cancellation.Dispose();
            if (ReferenceEquals(_cancellation, cancellation))
                _cancellation = null;
            SetBusy(false);
            FocusInput();
        }
    }

    private void AddUserMessage(string text)
    {
        var body = new TextBlock
        {
            Text = text ?? string.Empty,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush(32, 33, 36),
            LineHeight = 20
        };

        Border card = MessageCard("你", body, true);
        _conversation.Children.Add(card);
        _scroll.ScrollToEnd();
    }

    private MarkdownTextBlock AddAssistantMessage()
    {
        var markdown = new MarkdownTextBlock();
        Border card = MessageCard("InventorModel", markdown, false);
        _conversation.Children.Add(card);
        _scroll.ScrollToEnd();
        return markdown;
    }

    private Border MessageCard(string role, UIElement content, bool user)
    {
        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        panel.Children.Add(new TextBlock
        {
            Text = role,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = user ? AccentBrush : SecondaryTextBrush,
            Margin = new Thickness(0, 0, 0, 5)
        });
        panel.Children.Add(content);

        return new Border
        {
            Child = panel,
            Background = user ? UserBubbleBrush : PanelBackground,
            BorderBrush = user ? Brush(210, 224, 250) : BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(user ? 86 : 0, 0, user ? 0 : 40, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    private void AddNotice(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = SecondaryTextBrush,
            FontSize = 12,
            LineHeight = 18
        };

        _conversation.Children.Add(new Border
        {
            Child = block,
            Background = Brush(243, 246, 250),
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(20, 0, 20, 10)
        });
        _scroll.ScrollToEnd();
    }

    private TextBlock AddActivity()
    {
        var text = new TextBlock
        {
            Margin = new Thickness(12, -4, 0, 10),
            Foreground = SecondaryTextBrush,
            FontSize = 11
        };
        _conversation.Children.Add(text);
        return text;
    }

    private void AttachImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "附加工程图或参考图片",
            Filter = "图片 (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;

        _imagePath = dialog.FileName;
        _attachmentLabel.Text = "已附加：" + Path.GetFileName(_imagePath);
        _removeAttachment.Visibility = Visibility.Visible;
    }

    private void ClearAttachment()
    {
        _imagePath = string.Empty;
        _attachmentLabel.Text = string.Empty;
        _removeAttachment.Visibility = Visibility.Collapsed;
    }

    private void OpenSettings()
    {
        var window = new AiSettingsWindow(_settings) { Owner = this };
        if (window.ShowDialog() != true || window.Settings == null) return;

        _settings = window.Settings;
        _session.Dispose();
        _session = new AiAgentSession(_application, Dispatcher, _settings);
        UpdateHeader();
        AddNotice("AI 配置已更新，新的配置会用于后续对话。");
    }

    private void OpenHistory()
    {
        try
        {
            string path = _session.HistoryPath;
            if (!File.Exists(path)) return;
            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }
        catch { }
    }

    private void NewConversation()
    {
        _cancellation?.Cancel();
        _session.Dispose();
        _session = new AiAgentSession(_application, Dispatcher, _settings);
        _conversation.Children.Clear();
        ClearAttachment();
        AddNotice("新对话已开始。描述零件、尺寸和关键特征即可。");
        _statusLabel.Text = "就绪";
    }

    private void UpdateHeader()
    {
        _modelLabel.Text = _settings.Model + "  ·  " + _settings.BaseUrl;
    }

    private void SetBusy(bool busy)
    {
        _send.IsEnabled = !busy;
        _stop.IsEnabled = busy;
        _input.IsEnabled = !busy;
        if (busy)
            _statusLabel.Text = "处理中";
        else if (_statusLabel.Text == "处理中")
            _statusLabel.Text = "就绪";
    }

    private static string EscapeMarkdown(string value) =>
        (value ?? string.Empty).Replace("\\", "\\\\").Replace("*", "\\*").Replace("_", "\\_");

    private static string Compact(string value)
    {
        string text = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length <= 500 ? text : text.Substring(0, 500) + "…";
    }

    private void Shutdown()
    {
        try { _cancellation?.Cancel(); } catch { }
        try { _cancellation?.Dispose(); } catch { }
        _cancellation = null;
        try { _session.Dispose(); } catch { }
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
