using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace InventorModel.Addin;

internal sealed class AiSettingsWindow : Window
{
    private readonly TextBox _baseUrl = new TextBox();
    private readonly PasswordBox _apiKey = new PasswordBox();
    private readonly TextBox _model = new TextBox();
    private readonly TextBox _temperature = new TextBox();

    public AiSettingsWindow(AiSettings settings)
    {
        Title = "InventorModel AI Settings";
        Width = 520;
        Height = 300;
        MinWidth = 460;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        AiSettings source = (settings ?? new AiSettings()).Clone();
        source.Normalize();

        _baseUrl.Text = source.BaseUrl;
        _apiKey.Password = source.ApiKey;
        _model.Text = source.Model;
        _temperature.Text = source.Temperature.ToString("0.##", CultureInfo.InvariantCulture);

        var root = new Grid { Margin = new Thickness(18) };
        for (int i = 0; i < 5; i++)
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddRow(root, 0, "Base URL", _baseUrl);
        AddRow(root, 1, "API Key", _apiKey);
        AddRow(root, 2, "Model", _model);
        AddRow(root, 3, "Temperature", _temperature);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var cancel = new Button { Content = "Cancel", Width = 90, Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        var save = new Button { Content = "Save", Width = 90, Margin = new Thickness(8, 0, 0, 0), IsDefault = true };
        save.Click += Save_Click;
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        Grid.SetRow(buttons, 4);
        Grid.SetColumnSpan(buttons, 2);
        root.Children.Add(buttons);

        Content = root;
    }

    public AiSettings Settings { get; private set; }

    private static void AddRow(Grid grid, int row, string label, Control control)
    {
        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 12)
        };
        control.Margin = new Thickness(0, 0, 0, 12);
        control.MinHeight = 28;
        Grid.SetRow(text, row);
        Grid.SetColumn(text, 0);
        Grid.SetRow(control, row);
        Grid.SetColumn(control, 1);
        grid.Children.Add(text);
        grid.Children.Add(control);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string model = (_model.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            MessageBox.Show(this, "Model name is required.", "InventorModel", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show(this, "Temperature must be between 0 and 2.", "InventorModel", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = new AiSettings
        {
            BaseUrl = AiSettings.NormalizeBaseUrl(_baseUrl.Text),
            ApiKey = (_apiKey.Password ?? string.Empty).Trim(),
            Model = model,
            Temperature = AiSettings.NormalizeTemperature(temperature)
        };
        result.Save();
        Settings = result;
        DialogResult = true;
    }
}
