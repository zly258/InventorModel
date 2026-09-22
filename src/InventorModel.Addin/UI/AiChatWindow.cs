using System;
using System.Collections.Generic;
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
using InventorModel.Core.Diagnostics;

namespace InventorModel.Addin;

internal sealed class AiChatWindow : Window
{
    private static readonly Brush WindowBackground = Brush(233, 237, 242);
    private static readonly Brush PanelBackground = Brush(250, 251, 252);
    private static readonly Brush UiBorderBrush = Brush(208, 213, 219);
    private static readonly Brush AccentBrush = Brush(47, 111, 159);
    private static readonly Brush AccentSoftBrush = Brush(237, 244, 248);
    private static readonly Brush SecondaryTextBrush = Brush(75, 85, 99);
    private static readonly Brush UserBubbleBrush = Brush(228, 238, 245);

    private readonly global::Inventor.Application _application;
    private readonly StackPanel _conversation = new StackPanel();
    private readonly ScrollViewer _scroll = new ScrollViewer();
    private readonly TextBox _input = new TextBox();
    private readonly TextBlock _modelLabel = new TextBlock();
    private readonly TextBlock _statusLabel = new TextBlock();
    private readonly TextBlock _contextLabel = new TextBlock();
    private readonly TextBlock _attachmentLabel = new TextBlock();
    private readonly Image _attachmentPreview = new Image();
    private readonly Border _attachmentPanel = new Border();
    private readonly TextBlock _headerTitle = new TextBlock();
    private readonly TextBlock _attachmentTitle = new TextBlock();
    private readonly TextBlock _shortcutHint = new TextBlock();
    private readonly Button _newButton = new Button();
    private readonly Button _imageButton = new Button();
    private readonly Button _workspaceButton = new Button();
    private readonly Button _historyButton = new Button();
    private readonly Button _settingsButton = new Button();
    private readonly Button _send = new Button();
    private readonly Button _stop = new Button();
    private readonly Button _removeAttachment = new Button();
    private readonly Dictionary<string, ToolTraceView> _toolTraceViews =
        new Dictionary<string, ToolTraceView>();

    private AiSettings _settings;
    private AiAgentSession _session;
    private CancellationTokenSource? _cancellation;
    private MarkdownTextBlock? _assistantText;
    private TextBlock? _activityText;
    private int _assistantRound;
    private bool _pendingSessionReload;
    private string _imagePath = string.Empty;

    public AiChatWindow(global::Inventor.Application application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _settings = AiSettings.Load();
        _session = new AiAgentSession(_application, Dispatcher, _settings);

        Title = T("Chat.Title");
        Width = 720;
        Height = 820;
        MinWidth = 540;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = true;
        UiTheme.Apply(this);
        Background = WindowBackground;

        Content = BuildLayout();
        ApplyLocalization();
        UpdateHeader();
        AddNotice(T("Chat.Notice.Initial"));
        Closed += (_, __) => Shutdown();
    }

    public void AttachOwner(IntPtr owner)
    {
        if (owner == IntPtr.Zero)
            return;

        try
        {
            new System.Windows.Interop.WindowInteropHelper(this).Owner = owner;
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "UI.Chat",
                "Failed to attach the Inventor window as AI Chat owner.",
                ex);
        }
    }

    public void FocusInput()
    {
        _input.Focus();
        Keyboard.Focus(_input);
    }

    public void ReloadSettings()
    {
        AiSettings updated = AiSettings.Load();

        if (_cancellation != null)
        {
            _settings = updated;
            _pendingSessionReload = true;
            ApplyLocalization();
            UpdateHeader();
            AddNotice(
                Ui(
                    "AI 配置已保存；当前请求继续使用原配置，新配置从下一次请求生效。",
                    "AI settings were saved. The current request keeps its original settings; the new settings apply from the next request."));
            return;
        }

        _settings = updated;
        _session.Dispose();
        _session = new AiAgentSession(
            _application,
            Dispatcher,
            _settings);
        ClearAttachment();
        ApplyLocalization();
        UpdateHeader();
        AddNotice(T("Chat.Notice.Reload"));
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
        _headerTitle.FontSize = 16;
        _headerTitle.FontWeight = FontWeights.SemiBold;
        _headerTitle.Foreground = Brush(32, 33, 36);
        titlePanel.Children.Add(_headerTitle);

        _modelLabel.Margin = new Thickness(0, 3, 0, 0);
        _modelLabel.Foreground = SecondaryTextBrush;
        _modelLabel.FontSize = 11;
        _modelLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        titlePanel.Children.Add(_modelLabel);

        _contextLabel.Margin = new Thickness(0, 2, 0, 0);
        _contextLabel.Foreground = SecondaryTextBrush;
        _contextLabel.FontSize = 11;
        _contextLabel.Text = T("Chat.ContextAuto");
        titlePanel.Children.Add(_contextLabel);

        Grid.SetColumn(titlePanel, 0);
        grid.Children.Add(titlePanel);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        _statusLabel.Foreground = AccentBrush;
        _statusLabel.FontSize = 11;
        _statusLabel.Text = T("Chat.Ready");
        _statusLabel.VerticalAlignment = VerticalAlignment.Center;
        actions.Children.Add(new Border
        {
            Child = _statusLabel,
            Background = AccentSoftBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 8, 0)
        });

        ConfigureToolbarButton(_newButton);
        ConfigureToolbarButton(_imageButton);
        ConfigureToolbarButton(_workspaceButton);
        ConfigureToolbarButton(_historyButton);
        ConfigureToolbarButton(_settingsButton);

        _newButton.Click += (_, __) => NewConversation();
        _imageButton.Click += (_, __) => AttachImage();
        _workspaceButton.Click += (_, __) => OpenWorkspace();
        _historyButton.Click += (_, __) => OpenHistory();
        _settingsButton.Click += (_, __) => OpenSettings();

        actions.Children.Add(_newButton);
        actions.Children.Add(_imageButton);
        actions.Children.Add(_workspaceButton);
        actions.Children.Add(_historyButton);
        actions.Children.Add(_settingsButton);

        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        return new Border
        {
            Child = grid,
            Padding = new Thickness(16, 11, 14, 11),
            Background = PanelBackground,
            BorderBrush = UiBorderBrush,
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
        _attachmentTitle.FontWeight = FontWeights.SemiBold;
        _attachmentTitle.Foreground = Brush(60, 64, 67);
        attachmentText.Children.Add(_attachmentTitle);

        _attachmentLabel.Foreground = SecondaryTextBrush;
        _attachmentLabel.FontSize = 11;
        _attachmentLabel.Margin = new Thickness(0, 3, 0, 0);
        _attachmentLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        attachmentText.Children.Add(_attachmentLabel);
        Grid.SetColumn(attachmentText, 1);
        attachment.Children.Add(attachmentText);

        _removeAttachment.Content = T("Chat.Remove");
        _removeAttachment.Width = 64;
        _removeAttachment.Click += (_, __) => ClearAttachment();
        Grid.SetColumn(_removeAttachment, 2);
        attachment.Children.Add(_removeAttachment);

        _attachmentPanel.Child = attachment;
        _attachmentPanel.Padding = new Thickness(8);
        _attachmentPanel.Margin = new Thickness(0, 0, 0, 8);
        _attachmentPanel.Background = Brush(249, 250, 252);
        _attachmentPanel.BorderBrush = UiBorderBrush;
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
        _input.BorderBrush = UiBorderBrush;
        _input.BorderThickness = new Thickness(1);
        _input.PreviewKeyDown += Input_PreviewKeyDown;
        _input.AllowDrop = true;
        _input.Drop += Input_Drop;
        _input.ToolTip = T("Chat.InputTip");
        Grid.SetRow(_input, 1);
        layout.Children.Add(_input);

        var bottom = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _shortcutHint.Foreground = SecondaryTextBrush;
        _shortcutHint.FontSize = 11;
        _shortcutHint.VerticalAlignment = VerticalAlignment.Center;
        bottom.Children.Add(_shortcutHint);

        _stop.Content = T("Chat.Stop");
        _stop.Width = 72;
        _stop.Margin = new Thickness(0, 0, 8, 0);
        _stop.IsEnabled = false;
        _stop.Click += (_, __) => _cancellation?.Cancel();
        Grid.SetColumn(_stop, 1);
        bottom.Children.Add(_stop);

        _send.Content = T("Chat.Send");
        _send.Width = 76;
        _send.Tag = "Primary";
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
            BorderBrush = UiBorderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
    }

    private static void ConfigureToolbarButton(Button button)
    {
        button.MinWidth = 58;
        button.Margin = new Thickness(6, 0, 0, 0);
    }

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
            string imageNote = T("Chat.AttachmentPrefix") + Path.GetFileName(attached);
            userText = string.IsNullOrWhiteSpace(userText)
                ? imageNote
                : userText + Environment.NewLine + imageNote;
        }

        AddUserMessage(userText);
        _input.Clear();
        ClearAttachment();

        MarkdownTextBlock assistantText = AddAssistantMessage();
        _assistantRound = 0;
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
                newRound => Dispatcher.BeginInvoke(new Action(
                    () => BeginAssistantRound(newRound))),
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
                trace => Dispatcher.BeginInvoke(new Action(
                    () => UpdateToolTrace(trace))),
                context => Dispatcher.BeginInvoke(new Action(
                    () => UpdateContextStatus(context))),
                cancellation.Token);

            MarkdownTextBlock finalAssistant =
                _assistantText ?? assistantText;

            if (string.IsNullOrWhiteSpace(finalAssistant.Markdown))
                finalAssistant.SetMarkdown(
                    string.IsNullOrWhiteSpace(final)
                        ? (UiText.IsEnglish(_settings.UiLanguage) ? "Done." : "已完成。")
                        : final);

            finalAssistant.Flush();
            activityText.Text = string.Empty;
            _statusLabel.Text = T("Chat.Completed");
        }
        catch (OperationCanceledException)
        {
            activityText.Text = T("Chat.StopText");
            _statusLabel.Text = T("Chat.Stopped");
        }
        catch (Exception ex)
        {
            assistantText.SetMarkdown(
                "**" + T("Chat.Error") + "**\n\n" +
                EscapeMarkdown(Compact(ex.Message)));
            assistantText.Flush();
            activityText.Text = string.Empty;
            _statusLabel.Text = T("Chat.Failed");
        }
        finally
        {
            cancellation.Dispose();
            if (ReferenceEquals(_cancellation, cancellation))
                _cancellation = null;

            if (_pendingSessionReload)
            {
                _pendingSessionReload = false;
                _session.Dispose();
                _session = new AiAgentSession(
                    _application,
                    Dispatcher,
                    _settings);
                ClearAttachment();
                UpdateHeader();
            }

            SetBusy(false);
            FocusInput();
        }
    }

    private void AddUserMessage(string text)
    {
        var body = new TextBox
        {
            Text = text ?? string.Empty,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush(32, 33, 36),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Height = double.NaN,
            MinHeight = 0,
            Padding = new Thickness(0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        Border card = MessageCard(T("Chat.You"), body, true);
        _conversation.Children.Add(card);
        _scroll.ScrollToEnd();
    }

    private MarkdownTextBlock AddAssistantMessage()
    {
        var markdown = new MarkdownTextBlock
        {
            UiLanguage = _settings.UiLanguage
        };
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
            Tag = user ? "Role.User" : "Role.Assistant",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = user ? AccentBrush : SecondaryTextBrush,
            Margin = new Thickness(0, 0, 0, 5)
        });
        panel.Children.Add(content);

        if (!user &&
            content is MarkdownTextBlock markdown)
        {
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 6, 0, 0)
            };

            var selectText = new Button
            {
                Content = T("Markdown.Select"),
                ToolTip = T("Markdown.SelectionHint"),
                Tag = "Markdown.SelectAction",
                MinWidth = 96,
                Height = 28,
                Padding = new Thickness(10, 2, 10, 2)
            };
            selectText.Click += (_, __) =>
            {
                if (markdown.IsSelectionMode)
                {
                    markdown.ExitSelectionMode();
                    selectText.Content = T("Markdown.Select");
                    selectText.ToolTip = T("Markdown.SelectionHint");
                }
                else
                {
                    markdown.EnterSelectionMode();
                    selectText.Content = T("Markdown.Preview");
                    selectText.ToolTip = T("Markdown.SelectionHint");
                }
            };

            actions.Children.Add(selectText);
            panel.Children.Add(actions);
        }

        return new Border
        {
            Child = panel,
            Background = user ? UserBubbleBrush : PanelBackground,
            BorderBrush = user ? Brush(210, 224, 250) : UiBorderBrush,
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
            BorderBrush = UiBorderBrush,
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

    private void BeginAssistantRound(bool newRound)
    {
        if (!newRound)
        {
            _assistantText?.Clear();
            return;
        }

        _assistantRound++;

        if (_assistantRound <= 1)
        {
            _assistantText?.Clear();
            return;
        }

        _assistantText?.Flush();
        _assistantText = AddAssistantMessage();
    }

    private void UpdateContextStatus(ContextPreparation context)
    {
        if (context == null)
            return;

        _contextLabel.Text =
            (UiText.IsEnglish(_settings.UiLanguage) ? "Context " : "上下文 ") +
            FormatTokens(context.EstimatedTokens) +
            " / " +
            (context.BudgetTokens > 0
                ? FormatTokens(context.BudgetTokens)
                : "Auto") +
            (context.Compressed
                ? (UiText.IsEnglish(_settings.UiLanguage)
                    ? " · compacted"
                    : " · 已自动压缩")
                : string.Empty);

        _contextLabel.ToolTip =
            context.Compressed
                ? Ui(
                    "上下文确实不足后才执行压缩；最近对话、当前模型源码和最近工具链继续保留。",
                    "Compaction ran only after context was insufficient; recent turns, current model source, and the recent tool chain were retained.")
                : (_settings.ContextWindowTokens > 0
                    ? Ui(
                        "按已配置的真实上下文窗口计算可用输入预算；只有预计放不下时才压缩。",
                        "The configured real context window is used to calculate input budget; compaction runs only when the next request would not fit.")
                    : Ui(
                        "Auto 模式不会提前压缩；只有服务端明确返回上下文超限时才压缩并重试。",
                        "Auto mode never compacts proactively; it compacts and retries only after the provider reports a context-limit error."));
    }

    private void UpdateToolTrace(AgentToolTrace trace)
    {
        if (trace == null || string.IsNullOrWhiteSpace(trace.Id))
            return;

        if (!_toolTraceViews.TryGetValue(trace.Id, out ToolTraceView view))
        {
            view = CreateToolTraceView(trace);
            _toolTraceViews[trace.Id] = view;
            _conversation.Children.Add(view.Container);
        }

        view.Arguments.Text = string.IsNullOrWhiteSpace(trace.Arguments)
            ? "{}"
            : trace.Arguments;

        if (trace.Completed)
        {
            view.Result.Text = string.IsNullOrWhiteSpace(trace.Result)
                ? T("Chat.NoToolResult")
                : trace.Result;
            view.Status.Text = trace.Succeeded
                ? T("Chat.ToolDone")
                : T("Chat.ToolFailed");
            view.Status.Foreground = trace.Succeeded
                ? Brush(53, 101, 72)
                : Brush(169, 68, 66);
            view.Expander.IsExpanded = !trace.Succeeded;
        }
        else
        {
            view.Status.Text = T("Chat.ToolRunning");
            view.Status.Foreground = AccentBrush;
        }

        _scroll.ScrollToEnd();
    }

    private ToolTraceView CreateToolTraceView(AgentToolTrace trace)
    {
        var status = new TextBlock
        {
            Text = T("Chat.ToolRunning"),
            FontSize = 11,
            Foreground = AccentBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        var header = new Grid();
        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
        header.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = ToolDisplayName(trace.Name) + " · " + trace.Name,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(title);
        Grid.SetColumn(status, 1);
        header.Children.Add(status);

        var body = new StackPanel
        {
            Margin = new Thickness(0, 8, 0, 0)
        };

        body.Children.Add(CreateToolSectionTitle(T("Chat.ToolArgs")));
        TextBox arguments = CreateJsonBox(
            string.IsNullOrWhiteSpace(trace.Arguments)
                ? "{}"
                : trace.Arguments,
            104);
        body.Children.Add(arguments);

        body.Children.Add(CreateToolSectionTitle(T("Chat.ToolResult")));
        TextBox result = CreateJsonBox(
            trace.Completed && !string.IsNullOrWhiteSpace(trace.Result)
                ? trace.Result
                : T("Chat.ToolWaiting"),
            128);
        body.Children.Add(result);

        var expander = new Expander
        {
            Header = header,
            Content = body,
            IsExpanded = false,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };

        var container = new Border
        {
            Child = expander,
            Background = PanelBackground,
            BorderBrush = UiBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(18, 0, 18, 10)
        };

        return new ToolTraceView
        {
            Container = container,
            Expander = expander,
            Status = status,
            Arguments = arguments,
            Result = result
        };
    }

    private string ToolDisplayName(string name)
    {
        switch ((name ?? string.Empty).ToLowerInvariant())
        {
            case "validate": return T("Chat.Tool.Validate");
            case "skill_reference": return T("Chat.Tool.Skill");
            case "status": return T("Chat.Tool.Status");
            case "build": return T("Chat.Tool.Build");
            case "modify": return T("Chat.Tool.Modify");
            case "inspect": return T("Chat.Tool.Inspect");
            case "render": return T("Chat.Tool.Render");
            case "save": return T("Chat.Tool.Save");
            default: return T("Chat.Tool.Generic");
        }
    }

    private static TextBlock CreateToolSectionTitle(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = SecondaryTextBrush,
            Margin = new Thickness(0, 4, 0, 4)
        };
    }

    private static TextBox CreateJsonBox(string text, double height)
    {
        return new TextBox
        {
            Text = text ?? string.Empty,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11.5,
            Height = height,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private void AttachImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = Ui(
                "附加工程图或参考图片",
                "Attach engineering drawing or reference image"),
            Filter = Ui(
                "图片 (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp",
                "Images (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp"),
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
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "UI.Chat",
                "Attachment preview could not be loaded.",
                ex);
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
            Ui(
                "无法添加图片：",
                "Could not add image: ") +
            Compact(ex.Message),
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
        ApplyLocalization();
        AddNotice(T("Chat.Notice.Reload"));
    }

    private void OpenWorkspace()
    {
        try
        {
            Process.Start(
                "explorer.exe",
                "\"" + _session.Workspace.SessionDirectory + "\"");
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "UI.Chat",
                "Current AI workspace could not be opened.",
                ex);
        }
    }

    private void OpenHistory()
    {
        try
        {
            var window = new AiHistoryWindow(_session.Workspace.SessionDirectory)
            {
                Owner = this
            };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                Ui(
                    "无法打开历史对话：",
                    "Could not open history: ") +
                Compact(ex.Message),
                "InventorModel",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void NewConversation()
    {
        _cancellation?.Cancel();
        _session.Dispose();
        _session = new AiAgentSession(_application, Dispatcher, _settings);
        _conversation.Children.Clear();
        _toolTraceViews.Clear();
        _contextLabel.Text = T("Chat.ContextAuto");
        ClearAttachment();
        UpdateHeader();
        AddNotice(T("Chat.Notice.New"));
        _statusLabel.Text = T("Chat.Ready");
    }

    private void UpdateHeader()
    {
        string responseLanguage =
            string.Equals(
                _settings.EffectiveResponseLanguage,
                AiSettings.LanguageEnglish,
                StringComparison.OrdinalIgnoreCase)
                ? "English"
                : "中文";

        _modelLabel.Text =
            _settings.Model +
            "  ·  " +
            responseLanguage +
            "  ·  " +
            Ui(
                _settings.ReasoningEnabled ? "推理开" : "推理关",
                _settings.ReasoningEnabled ? "Reasoning on" : "Reasoning off") +
            "  ·  Tool " +
            _settings.MaxToolCalls;

        _modelLabel.ToolTip =
            Ui("接口：", "Endpoint: ") +
            _settings.BaseUrl +
            Environment.NewLine +
            Ui("AI 工作目录：", "AI workspace: ") +
            _session.Workspace.SessionDirectory +
            Environment.NewLine +
            Ui("AI 回复语言：", "AI response language: ") +
            responseLanguage +
            Environment.NewLine +
            Ui("上下文窗口：", "Context window: ") +
            (_settings.ContextWindowTokens > 0
                ? _settings.ContextWindowTokens.ToString()
                : "Auto") +
            Environment.NewLine +
            Ui("最大输出 Token：", "Max output tokens: ") +
            (_settings.MaxOutputTokens > 0
                ? _settings.MaxOutputTokens.ToString()
                : Ui("服务端默认", "provider default")) +
            Environment.NewLine +
            Ui("最大 Tool Call：", "Max Tool Calls: ") +
            _settings.MaxToolCalls;
    }

    private void ApplyLocalization()
    {
        Title = T("Chat.Title");
        _headerTitle.Text = T("Chat.Header");

        _newButton.Content = T("Chat.New");
        _imageButton.Content = T("Chat.Image");
        _workspaceButton.Content = T("Chat.Workspace");
        _historyButton.Content = T("Chat.History");
        _settingsButton.Content = T("Chat.Settings");

        _newButton.ToolTip = T("Chat.NewTip");
        _imageButton.ToolTip = T("Chat.ImageTip");
        _workspaceButton.ToolTip = T("Chat.WorkspaceTip");
        _historyButton.ToolTip = T("Chat.HistoryTip");
        _settingsButton.ToolTip = T("Chat.SettingsTip");

        _attachmentTitle.Text = T("Chat.Attachment");
        _removeAttachment.Content = T("Chat.Remove");
        _input.ToolTip = T("Chat.InputTip");
        _shortcutHint.Text = T("Chat.Shortcuts");
        _stop.Content = T("Chat.Stop");
        _send.Content = T("Chat.Send");

        if (string.IsNullOrWhiteSpace(_statusLabel.Text) ||
            _statusLabel.Text == "就绪" ||
            _statusLabel.Text == "Ready")
        {
            _statusLabel.Text = T("Chat.Ready");
        }

        if (string.IsNullOrWhiteSpace(_contextLabel.Text) ||
            _contextLabel.Text == "上下文 自动管理" ||
            _contextLabel.Text == "Context Auto")
        {
            _contextLabel.Text = T("Chat.ContextAuto");
        }

        ApplyMarkdownLanguage(_conversation);
    }

    private void ApplyMarkdownLanguage(DependencyObject root)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is MarkdownTextBlock markdown)
            {
                markdown.UiLanguage =
                    _settings.UiLanguage;
                continue;
            }

            if (child is Button button &&
                string.Equals(
                    Convert.ToString(button.Tag),
                    "Markdown.SelectAction",
                    StringComparison.Ordinal))
            {
                button.Content = T("Markdown.Select");
                button.ToolTip = T("Markdown.SelectionHint");
            }
            else if (child is TextBlock role &&
                     string.Equals(
                         Convert.ToString(role.Tag),
                         "Role.User",
                         StringComparison.Ordinal))
            {
                role.Text = T("Chat.You");
            }

            if (child is DependencyObject dependency)
                ApplyMarkdownLanguage(dependency);
        }
    }

    private string T(string key) =>
        UiText.Get(_settings.UiLanguage, key);

    private string Ui(string chinese, string english) =>
        UiText.IsEnglish(_settings.UiLanguage)
            ? english
            : chinese;

    private static string FormatTokens(int value) =>
        value >= 1000
            ? (value / 1000.0).ToString("0.#") + "k"
            : value.ToString();

    private void SetBusy(bool busy)
    {
        _send.IsEnabled = !busy;
        _stop.IsEnabled = busy;
        _input.IsEnabled = !busy;
        _newButton.IsEnabled = !busy;
        _imageButton.IsEnabled = !busy;
        _settingsButton.IsEnabled = !busy;

        if (busy)
            _statusLabel.Text = T("Chat.Processing");
        else if (_statusLabel.Text == T("Chat.Processing"))
            _statusLabel.Text = T("Chat.Ready");
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
        try
        {
            _cancellation?.Cancel();
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "UI.Chat",
                "Cancellation during chat shutdown failed.",
                ex);
        }

        try
        {
            _cancellation?.Dispose();
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "UI.Chat",
                "Cancellation token disposal failed.",
                ex);
        }

        _cancellation = null;

        try
        {
            _session.Dispose();
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "UI.Chat",
                "AI session disposal failed during chat shutdown.",
                ex);
        }
    }

    private sealed class ToolTraceView
    {
        public Border Container { get; set; } = null!;
        public Expander Expander { get; set; } = null!;
        public TextBlock Status { get; set; } = null!;
        public TextBox Arguments { get; set; } = null!;
        public TextBox Result { get; set; } = null!;
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
