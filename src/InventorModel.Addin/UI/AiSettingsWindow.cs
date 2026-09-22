using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using InventorModel.Core.Ai;

namespace InventorModel.Addin;

internal sealed class AiSettingsWindow : Window
{
    private readonly TextBox _baseUrl = new TextBox();
    private readonly PasswordBox _apiKey = new PasswordBox();
    private readonly TextBox _model = new TextBox();
    private readonly TextBox _temperature = new TextBox();

    public AiSettingsWindow(AiSettings settings)
    {
        Title = "InventorModel · AI配置";
        Width = 610;
        Height = 500;
        MinWidth = 540;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        UiTheme.Apply(this);

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
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Border intro = SurfaceCard(new Thickness(14, 10, 14, 10));
        intro.Margin = new Thickness(0, 0, 0, 10);
        var introText = new StackPanel();
        introText.Children.Add(new TextBlock
        {
            Text = "AI 配置",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold
        });
        var introHint = new TextBlock
        {
            Text = "配置 OpenAI Compatible 接口。模型、附件、脚本、验证图和临时文件统一由 InventorModel AI 工作目录管理。",
            Margin = new Thickness(0, 3, 0, 0),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        introHint.SetResourceReference(TextBlock.ForegroundProperty, "AppMutedTextBrush");
        introText.Children.Add(introHint);
        intro.Child = introText;
        Grid.SetRow(intro, 0);
        root.Children.Add(intro);

        var form = new Grid();
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 4; i++)
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        AddRow(form, 0, "接口地址", _baseUrl, "例如 http://127.0.0.1:11434/v1");
        AddRow(form, 1, "API Key", _apiKey, "本地服务可留空；云端服务按提供商要求填写");
        AddRow(form, 2, "模型", _model, "填写接口实际暴露的模型名称");
        AddRow(form, 3, "温度", _temperature, "范围 0–2；工程建模建议使用 0.0–0.3");

        Border configCard = SurfaceCard(new Thickness(14, 12, 14, 4));
        configCard.Child = form;
        configCard.Margin = new Thickness(0, 0, 0, 10);
        Grid.SetRow(configCard, 1);
        root.Children.Add(configCard);

        Border workspaceCard = BuildWorkspaceCard();
        workspaceCard.Margin = new Thickness(0, 0, 0, 10);
        Grid.SetRow(workspaceCard, 2);
        root.Children.Add(workspaceCard);

        var footer = new Border
        {
            Padding = new Thickness(12, 8, 12, 8),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2)
        };
        footer.SetResourceReference(Border.BackgroundProperty, "AppSurfaceBrush");
        footer.SetResourceReference(Border.BorderBrushProperty, "AppBorderBrush");

        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var pathText = new TextBlock
        {
            Text = AiSettings.SettingsPath,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = AiSettings.SettingsPath
        };
        pathText.SetResourceReference(TextBlock.ForegroundProperty, "AppMutedTextBrush");
        footerGrid.Children.Add(pathText);

        var cancel = new Button
        {
            Content = "取消",
            Width = 86,
            Margin = new Thickness(10, 0, 0, 0),
            IsCancel = true
        };
        Grid.SetColumn(cancel, 1);
        footerGrid.Children.Add(cancel);

        var save = new Button
        {
            Content = "保存并应用",
            Width = 110,
            Margin = new Thickness(10, 0, 0, 0),
            IsDefault = true,
            Tag = "Primary",
            FontWeight = FontWeights.SemiBold
        };
        save.Click += Save_Click;
        Grid.SetColumn(save, 2);
        footerGrid.Children.Add(save);

        footer.Child = footerGrid;
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);

        return root;
    }

    private Border BuildWorkspaceCard()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = "AI 工作目录",
            FontWeight = FontWeights.SemiBold
        });

        var path = new TextBlock
        {
            Text = AiWorkspace.RootDirectory,
            Margin = new Thickness(0, 3, 0, 0),
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = AiWorkspace.RootDirectory
        };
        path.SetResourceReference(TextBlock.ForegroundProperty, "AppMutedTextBrush");
        text.Children.Add(path);
        grid.Children.Add(text);

        var open = new Button
        {
            Content = "打开目录",
            Width = 90,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
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

        Border card = SurfaceCard(new Thickness(14, 10, 14, 10));
        card.Child = grid;
        return card;
    }

    private static Border SurfaceCard(Thickness padding)
    {
        var card = new Border
        {
            Padding = padding,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2)
        };
        card.SetResourceReference(Border.BackgroundProperty, "AppSurfaceBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "AppBorderBrush");
        return card;
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
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 8)
        };
        labelText.SetResourceReference(TextBlock.ForegroundProperty, "AppSecondaryTextBrush");

        var field = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        field.Children.Add(control);

        var hintText = new TextBlock
        {
            Text = hint,
            Margin = new Thickness(2, 3, 0, 0),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };
        hintText.SetResourceReference(TextBlock.ForegroundProperty, "AppMutedTextBrush");
        field.Children.Add(hintText);

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
            Warn("模型名称不能为空。");
            return;
        }

        string baseUrl = AiSettings.NormalizeBaseUrl(_baseUrl.Text);
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            Warn("接口地址必须是有效的 http/https 地址。");
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
            Warn("温度必须在 0 到 2 之间。");
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

    private void Warn(string message)
    {
        MessageBox.Show(
            this,
            message,
            "InventorModel",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
