using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InventorModel.Core.Ai;

namespace InventorModel.Addin;

internal sealed class AiSettingsWindow : Window
{
    private static readonly Brush WindowBackground = Brush(247, 248, 250);
    private static readonly Brush PanelBackground = Brush(255, 255, 255);
    private static readonly Brush BorderBrush = Brush(218, 220, 224);
    private static readonly Brush AccentBrush = Brush(25, 103, 210);
    private static readonly Brush SecondaryTextBrush = Brush(95, 99, 104);

    private readonly TextBox _baseUrl = new TextBox();
    private readonly PasswordBox _apiKey = new PasswordBox();
    private readonly TextBox _model = new TextBox();
    private readonly TextBox _temperature = new TextBox();

    public AiSettingsWindow(AiSettings settings)
    {
        Title = "InventorModel · AI配置";
        Width = 610;
        Height = 490;
        MinWidth = 540;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = WindowBackground;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        AiSettings source = (settings ?? new AiSettings()).Clone();
        source.Normalize();

        _baseUrl.Text = source.BaseUrl;
        _apiKey.Password = source.ApiKey;
        _model.Text = source.Model;
        _temperature.Text =
            source.Temperature.ToString("0.##", CultureInfo.InvariantCulture);

        Content = BuildLayout();
    }

    public AiSettings? Settings { get; private set; }

    private UIElement BuildLayout()
    {
        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var intro = new StackPanel { Margin = new Thickness(2, 0, 2, 14) };
        intro.Children.Add(new TextBlock
        {
            Text = "AI 配置",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(32, 33, 36)
        });
        intro.Children.Add(new TextBlock
        {
            Text = "配置 OpenAI Compatible 接口。建模脚本、图片、验证视图和临时文件统一进入 InventorModel AI 工作目录。",
            Margin = new Thickness(0, 5, 0, 0),
            Foreground = SecondaryTextBrush,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetRow(intro, 0);
        root.Children.Add(intro);

        var form = new Grid { Background = PanelBackground };
        form.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(118) });
        form.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < 4; i++)
            form.RowDefinitions.Add(
                new RowDefinition { Height = GridLength.Auto });

        PrepareInput(_baseUrl);
        PrepareInput(_model);
        PrepareInput(_temperature);

        _apiKey.Padding = new Thickness(9, 6, 9, 6);
        _apiKey.BorderBrush = BorderBrush;
        _apiKey.BorderThickness = new Thickness(1);

        AddRow(
            form,
            0,
            "接口地址",
            _baseUrl,
            "例如 http://127.0.0.1:11434/v1，或其它 OpenAI Compatible /v1 地址");
        AddRow(
            form,
            1,
            "API Key",
            _apiKey,
            "本地服务可留空；云端服务按提供商要求填写");
        AddRow(
            form,
            2,
            "模型",
            _model,
            "填写接口实际暴露的模型名称");
        AddRow(
            form,
            3,
            "温度",
            _temperature,
            "范围 0–2；工程建模建议保持较低温度");

        var configCard = new Border
        {
            Child = form,
            Background = PanelBackground,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(16, 14, 16, 8)
        };
        Grid.SetRow(configCard, 1);
        root.Children.Add(configCard);

        Border workspaceCard = BuildWorkspaceCard();
        workspaceCard.Margin = new Thickness(0, 12, 0, 14);
        Grid.SetRow(workspaceCard, 2);
        root.Children.Add(workspaceCard);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancel = new Button
        {
            Content = "取消",
            Width = 86,
            Height = 32,
            Margin = new Thickness(8, 0, 0, 0),
            IsCancel = true,
            Background = PanelBackground,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1)
        };

        var save = new Button
        {
            Content = "保存",
            Width = 86,
            Height = 32,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = true,
            Foreground = Brushes.White,
            Background = AccentBrush,
            BorderBrush = AccentBrush,
            BorderThickness = new Thickness(1),
            FontWeight = FontWeights.SemiBold
        };
        save.Click += Save_Click;

        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        Grid.SetRow(buttons, 3);
        root.Children.Add(buttons);

        return root;
    }

    private Border BuildWorkspaceCard()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = "AI 工作目录",
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(60, 64, 67)
        });
        text.Children.Add(new TextBlock
        {
            Text = AiWorkspace.RootDirectory,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = SecondaryTextBrush,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = AiWorkspace.RootDirectory
        });
        grid.Children.Add(text);

        var open = new Button
        {
            Content = "打开目录",
            Width = 78,
            Height = 30,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = PanelBackground,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1)
        };
        open.Click += (_, __) =>
        {
            try
            {
                Process.Start(
                    "explorer.exe",
                    "\"" + AiWorkspace.RootDirectory + "\"");
            }
            catch { }
        };
        Grid.SetColumn(open, 1);
        grid.Children.Add(open);

        return new Border
        {
            Child = grid,
            Background = PanelBackground,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(14, 11, 12, 11)
        };
    }

    private static void PrepareInput(TextBox box)
    {
        box.Padding = new Thickness(9, 6, 9, 6);
        box.BorderBrush = BorderBrush;
        box.BorderThickness = new Thickness(1);
    }

    private static void AddRow(
        Grid grid,
        int row,
        string label,
        Control control,
        string hint)
    {
        var labelText = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 8, 12, 0),
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush(60, 64, 67)
        };

        var field = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 12)
        };
        field.Children.Add(control);
        field.Children.Add(new TextBlock
        {
            Text = hint,
            Margin = new Thickness(2, 4, 0, 0),
            FontSize = 11,
            Foreground = SecondaryTextBrush,
            TextWrapping = TextWrapping.Wrap
        });

        Grid.SetRow(labelText, row);
        Grid.SetColumn(labelText, 0);
        Grid.SetRow(field, row);
        Grid.SetColumn(field, 1);
        grid.Children.Add(labelText);
        grid.Children.Add(field);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string model = (_model.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            MessageBox.Show(
                this,
                "模型名称不能为空。",
                "InventorModel",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        string baseUrl = AiSettings.NormalizeBaseUrl(_baseUrl.Text);
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(
                this,
                "接口地址必须是有效的 http/https 地址。",
                "InventorModel",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(
                (_temperature.Text ?? string.Empty).Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double temperature) ||
            temperature < 0 ||
            temperature > 2)
        {
            MessageBox.Show(
                this,
                "温度必须在 0 到 2 之间。",
                "InventorModel",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var result = new AiSettings
        {
            BaseUrl = baseUrl,
            ApiKey = (_apiKey.Password ?? string.Empty).Trim(),
            Model = model,
            Temperature = AiSettings.NormalizeTemperature(temperature)
        };

        result.Save();
        Settings = result;
        DialogResult = true;
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
