using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    private readonly Image _attachmentPreview = new Image();
    private readonly Border _attachmentPanel = new Border();
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
        AddNotice(
            "描述要创建的零件，也可以选择、拖入或直接 Ctrl+V 粘贴工程图。AI 会在当前工作目录内保存脚本、附件和验证视图。");
        Closed += (_, __) => Shutdown();
    }

    public void AttachOwner(IntPtr owner)
    {
        if (owner == IntPtr.Zero)
            return;

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
        ClearAttachment();
        UpdateHeader();
        AddNotice("AI 配置已更新，已为后续对话创建新的 AI 工作目录。");
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
        Button workspaceButton = ToolbarButton("目录");
        Button historyButton = ToolbarButton("历史");
        Button settingsButton = ToolbarButton("设置");

        newButton.ToolTip = "开始新对话并创建新的 AI 工作目录";
        imageButton.ToolTip = "选择工程图或参考图片，也可在输入框直接 Ctrl+V 粘贴";
        workspaceButton.ToolTip = "打开当前 AI 工作目录";
        historyButton.ToolTip = "打开当前对话记录";
        settingsButton.ToolTip = "AI 服务配置";

        newButton.Click += (_, __) => NewConversation();
        imageButton.Click += (_, __) => AttachImage();
        workspaceButton.Click += (_, __) => OpenWorkspace();
        historyButton.Click += (_, __) => OpenHistory();
        settingsButton.Click += (_, __) => OpenSettings();

        actions.Children.Add(newButton);
        actions.Children.Add(imageButton);
        actions.Children.Add(workspaceButton);
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

        var attachment = new Grid();
        attachment.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        attachment.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        attachment.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _attachmentPreview.Width = 76;
        _attachmentPreview.Height = 54;
        _attachmentPreview.Stretch = Stretch.Uniform;
        _attachmentPreview.VerticalAlignment = VerticalAlignment.Center;
        attachment.Children.Add(_attachmentPreview);

        var attachmentText = new StackPanel
        {
            Margin = new Thickness(10, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        attachmentText.Children.Add(new TextBlock
        {
            Text = "图片附件",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(60, 64, 67)
        });

        _attachmentLabel.Foreground = SecondaryTextBrush;
        _attachmentLabel.FontSize = 11;
        _attachmentLabel.Margin = new Thickness(0, 3, 0, 0);
        _attachmentLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        attachmentText.Children.Add(_attachmentLabel);
        Grid.SetColumn(attachmentText, 1);
        attachment.Children.Add(attachmentText);

        _removeAttachment.Content = "移除";
        _removeAttachment.Width = 58;
        _removeAttachment.Height = 28;
        _removeAttachment.Background = Brushes.Transparent;
        _removeAttachment.BorderBrush = BorderBrush;
        _removeAttachment.BorderThickness = new Thickness(1);
        _removeAttachment.Click += (_, __) => ClearAttachment();
        Grid.SetColumn(_removeAttachment, 2);
        attachment.Children.Add(_removeAttachment);

        _attachmentPanel.Child = attachment;
        _attachmentPanel.Padding = new Thickness(8);
        _attachmentPanel.Margin = new Thickness(0, 0, 0, 8);
        _attachmentPanel.Background = Brush(249, 250, 252);
        _attachmentPanel.BorderBrush = BorderBrush;
        _attachmentPanel.BorderThickness = new Thickness(1);
        _attachmentPanel.CornerRadius = new CornerRadius(5);
        _attachmentPanel.Visibility = Visibility.Collapsed;
        Grid.SetRow(_attachmentPanel, 0);
        layout.Children.Add(_attachmentPanel);

        _input.AcceptsReturn = true;
        _input.TextWrapping = TextWrapping.Wrap;
        _input.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _input.MinHeight = 84;
        _input.MaxHeight = 190;
        _input.Padding = new Thickness(11, 9, 11, 9);
        _input.Background = PanelBackground;
        _input.BorderBrush = BorderBrush;
        _input.BorderThickness = new Thickness(1);
        _input.PreviewKeyDown += Input_PreviewKeyDown;
        _input.AllowDrop = true;
        _input.Drop += Input_Drop;
        _input.ToolTip = "输入建模要求。Ctrl+V 可粘贴图片，Ctrl+Enter 发送。";
        Grid.SetRow(_input, 1);
        layout.Children.Add(_input);

        var bottom = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        bottom.Children.Add(new TextBlock
        {
            Text = "Ctrl+V 粘贴图片 · Ctrl+Enter 发送",
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
            MinWidth = 50,
            Height = 28,
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(8, 2, 8, 2),
            Background = Brushes.Transparent,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            Foreground = Brush(60, 64, 67)
        };

    private async void Input_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V &&
            (Keyboard.Modifiers & ModifierKeys.Control) != 0 &&
            TryAttachClipboardImage())
        {
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter ||
            (Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;

        e.Handled = true;
        await SendAsync();
    }

    private void Input_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[] ??
                             Array.Empty<string>();
            string? image = files.FirstOrDefault(IsSupportedImage);
            if (image == null)
                return;

            AttachWorkspaceCopy(image);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            ShowAttachmentError(ex);
        }
    }

    private bool TryAttachClipboardImage()
    {
        try
        {
            if (Clipboard.ContainsImage())
            {
                BitmapSource image = Clipboard.GetImage();
                string path = _session.Workspace.CreateAttachmentPath(".png");

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using (FileStream stream = File.Create(path))
                    encoder.Save(stream);

                SetAttachment(path);
                return true;
            }

            if (Clipboard.ContainsFileDropList())
            {
                StringCollection files = Clipboard.GetFileDropList();
                string? imagePath = files.Cast<string>().FirstOrDefault(IsSupportedImage);
                if (imagePath != null)
                {
                    AttachWorkspaceCopy(imagePath);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            ShowAttachmentError(ex);
            return true;
        }

        return false;
    }

    private async Task SendAsync()
    {
        if (_cancellation != null)
            return;

        string prompt = (_input.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prompt) &&
            string.IsNullOrWhiteSpace(_imagePath))
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
                () => Dispatcher.BeginInvoke(new Action(
                    () => _assistantText?.Clear())),
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
                assistantText.SetMarkdown(
                    string.IsNullOrWhiteSpace(final)
                        ? "已完成。"
                        : final);

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
            assistantText.SetMarkdown(
                "**错误**\n\n" + EscapeMarkdown(Compact(ex.Message)));
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
            Margin = new Thickness(
                user ? 86 : 0,
                0,
                user ? 0 : 40,
                10),
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

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            AttachWorkspaceCopy(dialog.FileName);
        }
        catch (Exception ex)
        {
            ShowAttachmentError(ex);
        }
    }

    private void AttachWorkspaceCopy(string sourcePath)
    {
        string path = _session.Workspace.ImportAttachment(sourcePath);
        SetAttachment(path);
    }

    private void SetAttachment(string path)
    {
        _imagePath = path;
        _attachmentLabel.Text = Path.GetFileName(path);
        _attachmentPreview.Source = TryLoadImage(path);
        _attachmentPanel.Visibility = Visibility.Visible;
    }

    private void ClearAttachment()
    {
        _imagePath = string.Empty;
        _attachmentLabel.Text = string.Empty;
        _attachmentPreview.Source = null;
        _attachmentPanel.Visibility = Visibility.Collapsed;
    }

    private static BitmapSource? TryLoadImage(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsSupportedImage(string path)
    {
        string extension = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
        return extension == ".png" ||
               extension == ".jpg" ||
               extension == ".jpeg" ||
               extension == ".webp";
    }

    private void ShowAttachmentError(Exception ex)
    {
        MessageBox.Show(
            this,
            "无法添加图片：" + Compact(ex.Message),
            "InventorModel",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OpenSettings()
    {
        var window = new AiSettingsWindow(_settings) { Owner = this };
        if (window.ShowDialog() != true || window.Settings == null)
            return;

        _settings = window.Settings;
        _session.Dispose();
        _session = new AiAgentSession(_application, Dispatcher, _settings);
        ClearAttachment();
        UpdateHeader();
        AddNotice("AI 配置已更新，已为后续对话创建新的 AI 工作目录。");
    }

    private void OpenWorkspace()
    {
        try
        {
            Process.Start(
                "explorer.exe",
                "\"" + _session.Workspace.SessionDirectory + "\"");
        }
        catch { }
    }

    private void OpenHistory()
    {
        try
        {
            string path = _session.HistoryPath;
            if (File.Exists(path))
            {
                Process.Start("explorer.exe", "/select,\"" + path + "\"");
                return;
            }

            OpenWorkspace();
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
        UpdateHeader();
        AddNotice(
            "新对话已开始。可以输入建模要求，也可以 Ctrl+V 粘贴工程图；所有 AI 文件只会写入当前工作目录。");
        _statusLabel.Text = "就绪";
    }

    private void UpdateHeader()
    {
        _modelLabel.Text = _settings.Model + "  ·  " + _settings.BaseUrl;
        _modelLabel.ToolTip = "AI 工作目录：" + _session.Workspace.SessionDirectory;
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
        (value ?? string.Empty)
        .Replace("\\", "\\\\")
        .Replace("*", "\\*")
        .Replace("_", "\\_");

    private static string Compact(string value)
    {
        string text = (value ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        return text.Length <= 500
            ? text
            : text.Substring(0, 500) + "…";
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
