using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using InventorModel.Core.Diagnostics;

namespace InventorModel.Addin;

internal sealed class AiSettings
{
    public const string LanguageChinese = "zh-CN";
    public const string LanguageEnglish = "en";
    public const string LanguageFollowUi = "ui";
    public const string LanguageAuto = "auto";

    private static readonly HashSet<string> ReservedRequestParameters =
        new HashSet<string>(
            new[]
            {
                "model",
                "messages",
                "stream",
                "tools",
                "tool_choice",
                "temperature",
                "reasoning_effort",
                "max_tokens"
            },
            StringComparer.OrdinalIgnoreCase);

    public string BaseUrl { get; set; } = "http://127.0.0.1:11434/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "qwen3-vl";
    public string UiLanguage { get; set; } = LanguageChinese;
    public string ResponseLanguage { get; set; } = LanguageFollowUi;
    public double Temperature { get; set; } = 0.2;
    public bool ReasoningEnabled { get; set; } = true;
    public int MaxToolCalls { get; set; } = 64;

    // 0 = automatic. In automatic mode the active conversation is kept intact
    // until the provider explicitly reports that the context window is exceeded.
    public int ContextWindowTokens { get; set; }

    // 0 = provider default.
    public int MaxOutputTokens { get; set; }

    public int RequestTimeoutSeconds { get; set; } = 1800;
    public int RetryCount { get; set; } = 2;

    // Provider-specific top-level OpenAI-compatible request options.
    public string AdditionalParametersJson { get; set; } = "{}";

    public static event EventHandler? Changed;

    public static string SettingsPath
    {
        get
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "InventorModel");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "ai-settings.json");
        }
    }

    public static AiSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AiSettings();

            var json = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue
            };

            AiSettings value =
                json.Deserialize<AiSettings>(File.ReadAllText(SettingsPath)) ??
                new AiSettings();
            value.Normalize();
            return value;
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "AI.Settings",
                "AI settings could not be loaded. Defaults will be used.",
                ex);
            return new AiSettings();
        }
    }

    public void Save()
    {
        Normalize();

        if (!TryGetAdditionalParameters(
                out _,
                out string error))
        {
            throw new InvalidOperationException(error);
        }

        File.WriteAllText(
            SettingsPath,
            new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue
            }.Serialize(this));

        try
        {
            Changed?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "AI.Settings",
                "A settings-changed listener failed after the settings file was saved.",
                ex);
        }
    }

    public AiSettings Clone() => new AiSettings
    {
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        Model = Model,
        UiLanguage = UiLanguage,
        ResponseLanguage = ResponseLanguage,
        Temperature = Temperature,
        ReasoningEnabled = ReasoningEnabled,
        MaxToolCalls = MaxToolCalls,
        ContextWindowTokens = ContextWindowTokens,
        MaxOutputTokens = MaxOutputTokens,
        RequestTimeoutSeconds = RequestTimeoutSeconds,
        RetryCount = RetryCount,
        AdditionalParametersJson = AdditionalParametersJson
    };

    public void Normalize()
    {
        BaseUrl = NormalizeBaseUrl(BaseUrl);
        ApiKey = (ApiKey ?? string.Empty).Trim();
        Model = string.IsNullOrWhiteSpace(Model)
            ? "qwen3-vl"
            : Model.Trim();
        UiLanguage = NormalizeUiLanguage(UiLanguage);
        ResponseLanguage = NormalizeResponseLanguage(ResponseLanguage);
        Temperature = NormalizeTemperature(Temperature);
        MaxToolCalls = NormalizeMaxToolCalls(MaxToolCalls);
        ContextWindowTokens = NormalizeContextWindowTokens(ContextWindowTokens);
        MaxOutputTokens = NormalizeMaxOutputTokens(MaxOutputTokens);
        RequestTimeoutSeconds = NormalizeRequestTimeoutSeconds(RequestTimeoutSeconds);
        RetryCount = NormalizeRetryCount(RetryCount);
        AdditionalParametersJson = string.IsNullOrWhiteSpace(AdditionalParametersJson)
            ? "{}"
            : AdditionalParametersJson.Trim();
    }

    public bool TryGetAdditionalParameters(
        out Dictionary<string, object> parameters,
        out string error)
    {
        parameters = new Dictionary<string, object>(
            StringComparer.OrdinalIgnoreCase);
        error = string.Empty;

        string jsonText = string.IsNullOrWhiteSpace(AdditionalParametersJson)
            ? "{}"
            : AdditionalParametersJson.Trim();

        try
        {
            var serializer = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue
            };

            object? parsed = serializer.DeserializeObject(jsonText);
            if (!(parsed is Dictionary<string, object> dictionary))
            {
                error = IsEnglishUi()
                    ? "Advanced request parameters must be a JSON object, for example {\"top_p\":0.9,\"seed\":42}."
                    : "高级请求参数必须是 JSON 对象，例如 {\"top_p\":0.9,\"seed\":42}。";
                return false;
            }

            string[] reserved = dictionary.Keys
                .Where(ReservedRequestParameters.Contains)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (reserved.Length > 0)
            {
                error =
                    (IsEnglishUi()
                        ? "Advanced request parameters cannot override fields managed by the UI: "
                        : "高级请求参数不能覆盖这些由界面单独管理的字段：") +
                    string.Join(", ", reserved);
                return false;
            }

            foreach (KeyValuePair<string, object> pair in dictionary)
                parameters[pair.Key] = pair.Value;

            return true;
        }
        catch (Exception ex)
        {
            error =
                (IsEnglishUi()
                    ? "Invalid advanced request-parameter JSON: "
                    : "高级请求参数 JSON 无效：") +
                ex.Message;
            return false;
        }
    }

    private bool IsEnglishUi() =>
        string.Equals(
            UiLanguage,
            LanguageEnglish,
            StringComparison.OrdinalIgnoreCase);

    public Dictionary<string, object> GetAdditionalParameters()
    {
        if (!TryGetAdditionalParameters(
                out Dictionary<string, object> parameters,
                out string error))
        {
            throw new InvalidOperationException(error);
        }

        return parameters;
    }

    public string EffectiveResponseLanguage =>
        string.Equals(
            ResponseLanguage,
            LanguageFollowUi,
            StringComparison.OrdinalIgnoreCase)
            ? UiLanguage
            : ResponseLanguage;

    public static string NormalizeUiLanguage(string value)
    {
        string text = (value ?? string.Empty).Trim();

        return string.Equals(
            text,
            LanguageEnglish,
            StringComparison.OrdinalIgnoreCase)
            ? LanguageEnglish
            : LanguageChinese;
    }

    public static string NormalizeResponseLanguage(string value)
    {
        string text = (value ?? string.Empty).Trim();

        if (string.Equals(
                text,
                LanguageEnglish,
                StringComparison.OrdinalIgnoreCase))
            return LanguageEnglish;

        if (string.Equals(
                text,
                LanguageChinese,
                StringComparison.OrdinalIgnoreCase))
            return LanguageChinese;

        // Backward compatibility: the old Auto option now means follow UI.
        if (string.Equals(
                text,
                LanguageAuto,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                text,
                LanguageFollowUi,
                StringComparison.OrdinalIgnoreCase))
            return LanguageFollowUi;

        return LanguageFollowUi;
    }

    public static double NormalizeTemperature(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return 0.2;

        return Math.Max(0.0, Math.Min(2.0, value));
    }

    public static int NormalizeMaxToolCalls(int value) =>
        Math.Max(1, Math.Min(128, value <= 0 ? 64 : value));

    public static int NormalizeContextWindowTokens(int value)
    {
        if (value <= 0)
            return 0;

        return Math.Max(8192, Math.Min(2_000_000, value));
    }

    public static int NormalizeMaxOutputTokens(int value)
    {
        if (value <= 0)
            return 0;

        return Math.Max(256, Math.Min(262_144, value));
    }

    public static int NormalizeRequestTimeoutSeconds(int value) =>
        Math.Max(10, Math.Min(3600, value <= 0 ? 1800 : value));

    public static int NormalizeRetryCount(int value) =>
        Math.Max(0, Math.Min(5, value));

    public static string NormalizeBaseUrl(string value)
    {
        string url = string.IsNullOrWhiteSpace(value)
            ? "http://127.0.0.1:11434/v1"
            : value.Trim().TrimEnd('/');

        if (url.EndsWith(
                "/chat/completions",
                StringComparison.OrdinalIgnoreCase))
        {
            url = url
                .Substring(
                    0,
                    url.Length - "/chat/completions".Length)
                .TrimEnd('/');
        }

        return url;
    }
}
