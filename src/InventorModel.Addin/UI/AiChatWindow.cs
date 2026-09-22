using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace InventorModel.Addin;

internal sealed class AiChatWindow : Window
{
    private readonly global::Inventor.Application _application;
    private readonly StackPanel _conversation = new StackPanel();
    private readonly ScrollViewer _scroll = new ScrollViewer();
    private readonly TextBox _input = new TextBox();
    private readonly TextBlock _modelLabel = new TextBlock();
    private readonly TextBlock _attachmentLabel = new TextBlock();
    private readonly Button _send = new Button();
    private readonly Button _stop = new Button();

    private AiSettings _settings;
    private AiAgentSession _session;
    private CancellationTokenSource? _cancellation;
    private TextBlock? _assistantText;
    private TextBlock? _activityText;
    private string _imagePath = string.Empty;

    public AiChatWindow(global::Inventor.Application application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _settings = AiSettings.Load();
        _session = new AiAgentSession(_application, Dispatcher, _settings);

        Title = "InventorModel AI";
        Width = 560;
        Height = 760;
        MinWidth = 440;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = true;

        Content = BuildLayout();
        UpdateHeader();
        AddMessage("InventorModel", "Describe the Part you want to build, or attach an engineering image.");
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

    private UIElement BuildLayout()
    {
        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };

        var newButton = MakeToolbarButton("New");
        var imageButton = MakeToolbarButton("Image");
        var settingsButton = MakeToolbarButton("Settings");
        var historyButton = MakeToolbarButton("History");

        newButton.Click += (_, __) => NewConversation();
        imageButton.Click += (_, __) => AttachImage();
        settingsButton.Click += (_, __) => OpenSettings();
        historyButton.Click += (_, __) => OpenHistory();

        actions.Children.Add(newButton);
        actions.Children.Add(imageButton);
        actions.Children.Add(settingsButton);
        actions.Children.Add(historyButton);

        _modelLabel.VerticalAlignment = VerticalAlignment.Center;
        _modelLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _modelLabel.Opacity = 0.72;

        DockPanel.SetDock(actions, Dock.Left);
        DockPanel.SetDock(_modelLabel, Dock.Right);
        toolbar.Children.Add(actions);
        toolbar.Children.Add(_modelLabel);
        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);

        _scroll.Content = _conversation;
        _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _conversation.Margin = new Thickness(2);
        Grid.SetRow(_scroll, 1);
        root.Children.Add(_scroll);

        _attachmentLabel.Margin = new Thickness(2, 8, 2, 4);
        _attachmentLabel.Opacity = 0.72;
        Grid.SetRow(_attachmentLabel, 2);
        root.Children.Add(_attachmentLabel);

        var inputGrid = new Grid();
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _input.AcceptsReturn = true;
        _input.TextWrapping = TextWrapping.Wrap;
        _input.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _input.MinHeight = 72;
        _input.MaxHeight = 180;
        _input.Padding = new Thickness(8);
        _input.KeyDown += Input_KeyDown;

        _send.Content = "Send";
        _send.Width = 76;
        _send.Margin = new Thickness(8, 0, 0, 0);
        _send.Click += async (_, __) => await SendAsync();

        _stop.Content = "Stop";
        _stop.Width = 76;
        _stop.Margin = new Thickness(8, 0, 0, 0);
        _stop.IsEnabled = false;
        _stop.Click += (_, __) => _cancellation?.Cancel();

        Grid.SetColumn(_input, 0);
        Grid.SetColumn(_send, 1);
        Grid.SetColumn(_stop, 2);
        inputGrid.Children.Add(_input);
        inputGrid.Children.Add(_send);
        inputGrid.Children.Add(_stop);

        Grid.SetRow(inputGrid, 3);
        root.Children.Add(inputGrid);
        return root;
    }

    private static Button MakeToolbarButton(string text) =>
        new Button
        {
            Content = text,
            MinWidth = 68,
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(8, 4, 8, 4)
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
        AddMessage("You", prompt + (string.IsNullOrWhiteSpace(attached)
            ? string.Empty
            : (string.IsNullOrWhiteSpace(prompt) ? string.Empty : Environment.NewLine) +
              "[Image] " + Path.GetFileName(attached)));

        _input.Clear();
        ClearAttachment();
        TextBlock assistantText = AddMessage("InventorModel", string.Empty);
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
                () => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_assistantText != null) _assistantText.Text = string.Empty;
                })),
                delta => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_assistantText != null) _assistantText.Text += delta;
                    _scroll.ScrollToEnd();
                })),
                activity => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_activityText != null) _activityText.Text = activity;
                })),
                cancellation.Token);

            if (string.IsNullOrWhiteSpace(assistantText.Text))
                assistantText.Text = string.IsNullOrWhiteSpace(final) ? "Completed." : final;

            activityText.Text = string.Empty;
        }
        catch (OperationCanceledException)
        {
            activityText.Text = "Canceled.";
        }
        catch (Exception ex)
        {
            assistantText.Text = "Error: " + Compact(ex.Message);
            activityText.Text = string.Empty;
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

    private TextBlock AddMessage(string role, string text)
    {
        var roleText = new TextBlock
        {
            Text = role,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var body = new TextBlock
        {
            Text = text ?? string.Empty,
            TextWrapping = TextWrapping.Wrap
        };

        var panel = new StackPanel();
        panel.Children.Add(roleText);
        panel.Children.Add(body);

        var border = new Border
        {
            Child = panel,
            BorderBrush = SystemColors.ControlDarkBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 8),
            MaxWidth = 500,
            HorizontalAlignment = role == "You" ? HorizontalAlignment.Right : HorizontalAlignment.Left
        };

        _conversation.Children.Add(border);
        _scroll.ScrollToEnd();
        return body;
    }

    private TextBlock AddActivity()
    {
        var text = new TextBlock
        {
            Margin = new Thickness(8, -4, 0, 8),
            Opacity = 0.65,
            FontSize = 11
        };
        _conversation.Children.Add(text);
        return text;
    }

    private void AttachImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Attach engineering image",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        _imagePath = dialog.FileName;
        _attachmentLabel.Text = "Attached: " + Path.GetFileName(_imagePath);
    }

    private void ClearAttachment()
    {
        _imagePath = string.Empty;
        _attachmentLabel.Text = string.Empty;
    }

    private void OpenSettings()
    {
        var window = new AiSettingsWindow(_settings) { Owner = this };
        if (window.ShowDialog() != true || window.Settings == null) return;

        _settings = window.Settings;
        _session.Dispose();
        _session = new AiAgentSession(_application, Dispatcher, _settings);
        UpdateHeader();
        AddMessage("InventorModel", "AI settings updated. A new conversation context has started.");
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
        AddMessage("InventorModel", "New conversation. Describe the Part you want to build.");
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
    }

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
        try { _session?.Dispose(); } catch { }
    }
}
