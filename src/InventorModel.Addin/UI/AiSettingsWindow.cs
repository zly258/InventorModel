using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using InventorModel.Core.Ai;
using InventorModel.Core.Diagnostics;

namespace InventorModel.Addin;

internal sealed class AiSettingsWindow : Window
{
    private readonly string _uiLanguage;

    private readonly TextBox _baseUrl = new TextBox();
    private readonly PasswordBox _apiKey = new PasswordBox();
    private readonly TextBox _model = new TextBox();
    private readonly TextBox _timeoutSeconds = new TextBox();
    private readonly TextBox _retryCount = new TextBox();

    private readonly ComboBox _uiLanguageBox = new ComboBox();
    private readonly ComboBox _responseLanguageBox = new ComboBox();

    private readonly TextBox _temperature = new TextBox();
    private readonly CheckBox _reasoning = new CheckBox();
    private readonly TextBox _maxOutputTokens = new TextBox();

    private readonly TextBox _maxToolCalls = new TextBox();
    private readonly TextBox _contextWindowTokens = new TextBox();

    private readonly TextBox _additionalParameters = new TextBox
    {
        AcceptsReturn = true,
        AcceptsTab = true,
        TextWrapping = TextWrapping.NoWrap,
        Height = 110,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontFamily = new System.Windows.Media.FontFamily("Consolas")
    };

    public AiSettingsWindow(AiSettings settings)
    {
        AiSettings source = (settings ?? new AiSettings()).Clone();
        source.Normalize();
        _uiLanguage = source.UiLanguage;

        Title = T("Settings.Title");
        Width = 700;
        Height = 780;
        MinWidth = 620;
        MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;

        UiTheme.Apply(this);

        _baseUrl.Text = source.BaseUrl;
        _apiKey.Password = source.ApiKey;
        _model.Text = source.Model;
        _timeoutSeconds.Text =
            source.RequestTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        _retryCount.Text =
            source.RetryCount.ToString(CultureInfo.InvariantCulture);

        ConfigureUiLanguage(source.UiLanguage);
        ConfigureResponseLanguage(source.ResponseLanguage);

        _temperature.Text =
            source.Temperature.ToString("0.##", CultureInfo.InvariantCulture);
        _reasoning.Content = T("Settings.ReasoningToggle");
        _reasoning.IsChecked = source.ReasoningEnabled;
        _maxOutputTokens.Text =
            source.MaxOutputTokens.ToString(CultureInfo.InvariantCulture);

        _maxToolCalls.Text =
            source.MaxToolCalls.ToString(CultureInfo.InvariantCulture);
        _contextWindowTokens.Text =
            source.ContextWindowTokens.ToString(CultureInfo.InvariantCulture);

        _additionalParameters.Text = source.AdditionalParametersJson;

        Content = BuildLayout();
    }

    public AiSettings? Settings { get; private set; }

    private UIElement BuildLayout()
    {
        var root = new Grid();
        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });
        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        var content = new StackPanel
        {
            Margin = new Thickness(16, 16, 16, 10)
        };

        content.Children.Add(
            SectionCard(
                T("Settings.Header"),
                T("Settings.HeaderHint"),
                null));

        content.Children.Add(BuildLanguageCard());
        content.Children.Add(BuildConnectionCard());
        content.Children.Add(BuildGenerationCard());
        content.Children.Add(BuildAgentCard());
        content.Children.Add(BuildAdvancedCard());
        content.Children.Add(BuildDiagnosticsCard());

        var scroll = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetRow(scroll, 0);
        root.Children.Add(scroll);

        Border footer = BuildFooter();
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);

        return root;
    }

    private Border BuildLanguageCard()
    {
        Grid form = CreateForm(2);

        AddRow(
            form,
            0,
            T("Settings.UiLanguage"),
            _uiLanguageBox,
            Ui(
                "保存后 AI Chat、历史和设置界面会使用新语言；Ribbon 会自动刷新。",
                "After saving, AI Chat, History, and Settings use the new language; the Ribbon refreshes automatically."));

        AddRow(
            form,
            1,
            T("Settings.ResponseLanguage"),
            _responseLanguageBox,
            Ui(
                "“跟随界面”最稳定；也可以让英文 UI 固定中文回答，或中文 UI 固定英文回答。",
                "Follow UI is the simplest option, but UI and response language can also be configured independently."));

        return SectionCard(
            T("Settings.LanguageSection"),
            T("Settings.LanguageHint"),
            form);
    }

    private Border BuildConnectionCard()
    {
        Grid form = CreateForm(5);

        AddRow(
            form,
            0,
            T("Settings.BaseUrl"),
            _baseUrl,
            Ui(
                "例如 http://127.0.0.1:11434/v1",
                "Example: http://127.0.0.1:11434/v1"));
        AddRow(
            form,
            1,
            T("Settings.ApiKey"),
            _apiKey,
            Ui(
                "本地服务可留空；云端服务按提供商要求填写",
                "May be empty for local services; cloud providers may require a key"));
        AddRow(
            form,
            2,
            T("Settings.Model"),
            _model,
            Ui(
                "填写接口实际暴露的模型名称",
                "Use the model name exposed by the endpoint"));
        AddRow(
            form,
            3,
            T("Settings.Timeout"),
            _timeoutSeconds,
            Ui(
                "单位秒，范围 10–3600；本地大模型可保留较长超时",
                "Seconds, 10–3600; local models may need a longer timeout"));
        AddRow(
            form,
            4,
            T("Settings.Retry"),
            _retryCount,
            Ui(
                "网络错误、429、5xx 的自动重试次数，范围 0–5",
                "Automatic retries for network errors, 429, and 5xx; range 0–5"));

        return SectionCard(
            T("Settings.Connection"),
            T("Settings.ConnectionHint"),
            form);
    }

    private Border BuildGenerationCard()
    {
        Grid form = CreateForm(3);

        AddRow(
            form,
            0,
            T("Settings.Temperature"),
            _temperature,
            Ui(
                "范围 0–2；工程建模建议 0.0–0.3",
                "Range 0–2; 0.0–0.3 is recommended for engineering modeling"));
        AddRow(
            form,
            1,
            T("Settings.Reasoning"),
            _reasoning,
            Ui(
                "关闭时发送 reasoning_effort=none；支持的模型通常响应更快",
                "When disabled, reasoning_effort=none is sent; supported models usually respond faster"));
        AddRow(
            form,
            2,
            T("Settings.MaxOutput"),
            _maxOutputTokens,
            Ui(
                "0 表示由服务端决定；否则范围 256–262144",
                "0 uses the provider default; otherwise 256–262144"));

        return SectionCard(
            T("Settings.Generation"),
            T("Settings.GenerationHint"),
            form);
    }

    private Border BuildAgentCard()
    {
        Grid form = CreateForm(2);

        AddRow(
            form,
            0,
            T("Settings.MaxToolCalls"),
            _maxToolCalls,
            Ui(
                "单次用户请求允许的 Tool Call 总数，范围 1–128；默认 64",
                "Total Tool Calls allowed per user request, 1–128; default 64"));
        AddRow(
            form,
            1,
            T("Settings.ContextWindow"),
            _contextWindowTokens,
            Ui(
                "0 = Auto：不提前压缩，只有服务端明确返回上下文超限才压缩；也可填写真实窗口，如 65536、131072",
                "0 = Auto: no proactive compaction; compact only after the provider reports a context limit. Or enter the real window, e.g. 65536 or 131072"));

        return SectionCard(
            T("Settings.Agent"),
            T("Settings.AgentHint"),
            form);
    }

    private Border BuildAdvancedCard()
    {
        var panel = new StackPanel();

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var format = new Button
        {
            Content = T("Settings.FormatJson"),
            MinWidth = 100,
            Margin = new Thickness(0, 0, 8, 0)
        };
        format.Click += (_, __) => FormatAdvancedJson();

        var reset = new Button
        {
            Content = T("Settings.ResetJson"),
            MinWidth = 72
        };
        reset.Click += (_, __) =>
            _additionalParameters.Text = "{}";

        actions.Children.Add(format);
        actions.Children.Add(reset);
        panel.Children.Add(actions);
        panel.Children.Add(_additionalParameters);

        var example = new TextBlock
        {
            Text = Ui(
                "示例：{\"top_p\":0.9,\"seed\":42}。支持嵌套 JSON；model/messages/tools/temperature 等核心字段不能在这里覆盖。",
                "Example: {\"top_p\":0.9,\"seed\":42}. Nested JSON is supported; core fields such as model/messages/tools/temperature cannot be overridden here."),
            Margin = new Thickness(2, 6, 0, 0),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };
        example.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AppMutedTextBrush");
        panel.Children.Add(example);

        return SectionCard(
            T("Settings.Advanced"),
            T("Settings.AdvancedHint"),
            panel);
    }

    private Border BuildDiagnosticsCard()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
        grid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = AiWorkspace.RootDirectory,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = AiWorkspace.RootDirectory
        });

        var log = new TextBlock
        {
            Text = RuntimeLog.LogPath,
            Margin = new Thickness(0, 3, 0, 0),
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = RuntimeLog.LogPath
        };
        log.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AppMutedTextBrush");
        text.Children.Add(log);
        grid.Children.Add(text);

        var openWorkspace = new Button
        {
            Content = T("Settings.AiFolder"),
            Width = 82,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        openWorkspace.Click += (_, __) =>
            OpenDirectory(
                AiWorkspace.RootDirectory,
                T("Settings.AiFolder"));
        Grid.SetColumn(openWorkspace, 1);
        grid.Children.Add(openWorkspace);

        var openLogs = new Button
        {
            Content = T("Settings.Logs"),
            Width = 72,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        openLogs.Click += (_, __) =>
            OpenDirectory(
                RuntimeLog.LogDirectory,
                T("Settings.Logs"));
        Grid.SetColumn(openLogs, 2);
        grid.Children.Add(openLogs);

        return SectionCard(
            T("Settings.Diagnostics"),
            T("Settings.DiagnosticsHint"),
            grid);
    }

    private Border BuildFooter()
    {
        var footer = new Border
        {
            Padding = new Thickness(16, 10, 16, 12),
            BorderThickness = new Thickness(0, 1, 0, 0)
        };
        footer.SetResourceReference(
            Border.BackgroundProperty,
            "AppSurfaceBrush");
        footer.SetResourceReference(
            Border.BorderBrushProperty,
            "AppBorderBrush");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
        grid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        var path = new TextBlock
        {
            Text = AiSettings.SettingsPath,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = AiSettings.SettingsPath
        };
        path.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AppMutedTextBrush");
        grid.Children.Add(path);

        var cancel = new Button
        {
            Content = T("Settings.Cancel"),
            Width = 86,
            Margin = new Thickness(10, 0, 0, 0),
            IsCancel = true
        };
        Grid.SetColumn(cancel, 1);
        grid.Children.Add(cancel);

        var save = new Button
        {
            Content = T("Settings.Save"),
            Width = 110,
            Margin = new Thickness(10, 0, 0, 0),
            IsDefault = true,
            Tag = "Primary",
            FontWeight = FontWeights.SemiBold
        };
        save.Click += Save_Click;
        Grid.SetColumn(save, 2);
        grid.Children.Add(save);

        footer.Child = grid;
        return footer;
    }

    private static Grid CreateForm(int rows)
    {
        var form = new Grid();
        form.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(120) });
        form.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

        for (int i = 0; i < rows; i++)
        {
            form.RowDefinitions.Add(
                new RowDefinition { Height = GridLength.Auto });
        }

        return form;
    }

    private Border SectionCard(
        string title,
        string hint,
        UIElement? content)
    {
        var panel = new StackPanel();

        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold
        });

        if (!string.IsNullOrWhiteSpace(hint))
        {
            var hintText = new TextBlock
            {
                Text = hint,
                Margin = new Thickness(0, 3, 0, content == null ? 0 : 10),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            hintText.SetResourceReference(
                TextBlock.ForegroundProperty,
                "AppMutedTextBrush");
            panel.Children.Add(hintText);
        }

        if (content != null)
            panel.Children.Add(content);

        var card = new Border
        {
            Child = panel,
            Padding = new Thickness(14, 11, 14, 11),
            Margin = new Thickness(0, 0, 0, 10),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2)
        };
        card.SetResourceReference(
            Border.BackgroundProperty,
            "AppSurfaceBrush");
        card.SetResourceReference(
            Border.BorderBrushProperty,
            "AppBorderBrush");
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
            Margin = new Thickness(0, 0, 12, 10)
        };
        labelText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AppSecondaryTextBrush");

        var field = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 10)
        };
        field.Children.Add(control);

        var hintText = new TextBlock
        {
            Text = hint,
            Margin = new Thickness(2, 3, 0, 0),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };
        hintText.SetResourceReference(
            TextBlock.ForegroundProperty,
            "AppMutedTextBrush");
        field.Children.Add(hintText);

        Grid.SetRow(labelText, row);
        Grid.SetColumn(labelText, 0);
        Grid.SetRow(field, row);
        Grid.SetColumn(field, 1);

        grid.Children.Add(labelText);
        grid.Children.Add(field);
    }

    private void ConfigureUiLanguage(string selected)
    {
        AddComboItem(
            _uiLanguageBox,
            UiText.Get(_uiLanguage, "Settings.Chinese"),
            AiSettings.LanguageChinese);
        AddComboItem(
            _uiLanguageBox,
            "English",
            AiSettings.LanguageEnglish);
        SelectComboValue(_uiLanguageBox, selected);
    }

    private void ConfigureResponseLanguage(string selected)
    {
        AddComboItem(
            _responseLanguageBox,
            T("Settings.FollowUi"),
            AiSettings.LanguageFollowUi);
        AddComboItem(
            _responseLanguageBox,
            T("Settings.Chinese"),
            AiSettings.LanguageChinese);
        AddComboItem(
            _responseLanguageBox,
            "English",
            AiSettings.LanguageEnglish);
        SelectComboValue(_responseLanguageBox, selected);
    }

    private static void AddComboItem(
        ComboBox combo,
        string display,
        string value)
    {
        combo.Items.Add(
            new ComboBoxItem
            {
                Content = display,
                Tag = value
            });
    }

    private static void SelectComboValue(
        ComboBox combo,
        string value)
    {
        foreach (object item in combo.Items)
        {
            if (item is ComboBoxItem option &&
                string.Equals(
                    Convert.ToString(option.Tag),
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = option;
                return;
            }
        }

        combo.SelectedIndex = 0;
    }

    private static string SelectedValue(
        ComboBox combo,
        string fallback)
    {
        if (combo.SelectedItem is ComboBoxItem item)
            return Convert.ToString(item.Tag) ?? fallback;

        return fallback;
    }

    private void FormatAdvancedJson()
    {
        var validation = new AiSettings
        {
            UiLanguage = SelectedValue(
                _uiLanguageBox,
                _uiLanguage),
            AdditionalParametersJson =
                (_additionalParameters.Text ?? string.Empty).Trim()
        };

        if (!validation.TryGetAdditionalParameters(
                out _,
                out string error))
        {
            Warn(error);
            return;
        }

        _additionalParameters.Text =
            JsonDisplayFormatter.Format(
                _additionalParameters.Text ?? string.Empty);
    }

    private void Save_Click(
        object sender,
        RoutedEventArgs e)
    {
        string model = (_model.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            Warn(Ui("模型名称不能为空。", "Model name cannot be empty."));
            return;
        }

        string baseUrl =
            AiSettings.NormalizeBaseUrl(_baseUrl.Text);
        if (!Uri.TryCreate(
                baseUrl,
                UriKind.Absolute,
                out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            Warn(Ui(
                "接口地址必须是有效的 http/https 地址。",
                "Base URL must be a valid http/https address."));
            return;
        }

        if (!TryDouble(
                _temperature.Text,
                0,
                2,
                T("Settings.Temperature"),
                out double temperature))
            return;

        if (!TryInt(
                _maxToolCalls.Text,
                1,
                128,
                T("Settings.MaxToolCalls"),
                out int maxToolCalls))
            return;

        if (!TryOptionalInt(
                _contextWindowTokens.Text,
                8192,
                2_000_000,
                T("Settings.ContextWindow"),
                out int contextWindowTokens))
            return;

        if (!TryOptionalInt(
                _maxOutputTokens.Text,
                256,
                262_144,
                T("Settings.MaxOutput"),
                out int maxOutputTokens))
            return;

        if (contextWindowTokens > 0 &&
            maxOutputTokens > 0 &&
            maxOutputTokens + 1024 >= contextWindowTokens)
        {
            Warn(Ui(
                "最大输出 Token 必须明显小于上下文窗口，并至少给输入和工具结果预留空间。",
                "Max output tokens must be well below the context window so input and tool results still have reserved space."));
            return;
        }

        if (!TryInt(
                _timeoutSeconds.Text,
                10,
                3600,
                T("Settings.Timeout"),
                out int timeoutSeconds))
            return;

        if (!TryInt(
                _retryCount.Text,
                0,
                5,
                T("Settings.Retry"),
                out int retryCount))
            return;

        var result = new AiSettings
        {
            BaseUrl = baseUrl,
            ApiKey = (_apiKey.Password ?? string.Empty).Trim(),
            Model = model,
            UiLanguage = SelectedValue(
                _uiLanguageBox,
                AiSettings.LanguageChinese),
            ResponseLanguage = SelectedValue(
                _responseLanguageBox,
                AiSettings.LanguageFollowUi),
            Temperature =
                AiSettings.NormalizeTemperature(temperature),
            ReasoningEnabled = _reasoning.IsChecked == true,
            MaxToolCalls =
                AiSettings.NormalizeMaxToolCalls(maxToolCalls),
            ContextWindowTokens =
                AiSettings.NormalizeContextWindowTokens(
                    contextWindowTokens),
            MaxOutputTokens =
                AiSettings.NormalizeMaxOutputTokens(
                    maxOutputTokens),
            RequestTimeoutSeconds =
                AiSettings.NormalizeRequestTimeoutSeconds(
                    timeoutSeconds),
            RetryCount =
                AiSettings.NormalizeRetryCount(retryCount),
            AdditionalParametersJson =
                (_additionalParameters.Text ?? string.Empty).Trim()
        };

        if (!result.TryGetAdditionalParameters(
                out _,
                out string parameterError))
        {
            Warn(parameterError);
            return;
        }

        try
        {
            result.Save();
            Settings = result;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            RuntimeLog.Error(
                "AI.Settings",
                "AI settings could not be saved.",
                ex);
            Warn(Ui(
                "保存 AI 配置失败：" + ex.Message,
                "Failed to save AI settings: " + ex.Message));
        }
    }

    private bool TryDouble(
        string text,
        double minimum,
        double maximum,
        string name,
        out double value)
    {
        if (double.TryParse(
                (text ?? string.Empty).Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value) &&
            value >= minimum &&
            value <= maximum)
        {
            return true;
        }

        Warn(Ui(
            name + "必须在 " +
            minimum.ToString(CultureInfo.InvariantCulture) +
            " 到 " +
            maximum.ToString(CultureInfo.InvariantCulture) +
            " 之间。",
            name + " must be between " +
            minimum.ToString(CultureInfo.InvariantCulture) +
            " and " +
            maximum.ToString(CultureInfo.InvariantCulture) +
            "."));
        return false;
    }

    private bool TryInt(
        string text,
        int minimum,
        int maximum,
        string name,
        out int value)
    {
        if (int.TryParse(
                (text ?? string.Empty).Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value) &&
            value >= minimum &&
            value <= maximum)
        {
            return true;
        }

        Warn(Ui(
            name + "必须是 " + minimum + " 到 " + maximum + " 之间的整数。",
            name + " must be an integer between " + minimum + " and " + maximum + "."));
        return false;
    }

    private bool TryOptionalInt(
        string text,
        int minimumNonZero,
        int maximum,
        string name,
        out int value)
    {
        if (!int.TryParse(
                (text ?? string.Empty).Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
        {
            Warn(Ui(
                name + "必须是整数；0 表示自动/服务端默认。",
                name + " must be an integer; 0 means Auto/provider default."));
            return false;
        }

        if (value == 0 ||
            (value >= minimumNonZero &&
             value <= maximum))
        {
            return true;
        }

        Warn(Ui(
            name + "必须为 0，或 " + minimumNonZero + " 到 " + maximum + " 之间的整数。",
            name + " must be 0, or an integer between " + minimumNonZero + " and " + maximum + "."));
        return false;
    }

    private void OpenDirectory(
        string path,
        string displayName)
    {
        try
        {
            Process.Start(
                "explorer.exe",
                "\"" + path + "\"");
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "UI.Settings",
                "Could not open " + displayName + ".",
                ex);
            Warn(Ui(
                "无法打开" + displayName + "：" + ex.Message,
                "Could not open " + displayName + ": " + ex.Message));
        }
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

    private string T(string key) =>
        UiText.Get(_uiLanguage, key);

    private string Ui(string chinese, string english) =>
        UiText.IsEnglish(_uiLanguage)
            ? english
            : chinese;
}
