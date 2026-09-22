using System;
using System.IO;
using System.Web.Script.Serialization;

namespace InventorModel.Addin;

internal sealed class AiSettings
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:11434/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "qwen3-vl";
    public double Temperature { get; set; } = 0.2;

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
            if (!File.Exists(SettingsPath)) return new AiSettings();
            var json = new JavaScriptSerializer();
            AiSettings value = json.Deserialize<AiSettings>(File.ReadAllText(SettingsPath)) ?? new AiSettings();
            value.Normalize();
            return value;
        }
        catch
        {
            return new AiSettings();
        }
    }

    public void Save()
    {
        Normalize();
        File.WriteAllText(SettingsPath, new JavaScriptSerializer().Serialize(this));
    }

    public AiSettings Clone() => new AiSettings
    {
        BaseUrl = BaseUrl,
        ApiKey = ApiKey,
        Model = Model,
        Temperature = Temperature
    };

    public void Normalize()
    {
        BaseUrl = NormalizeBaseUrl(BaseUrl);
        ApiKey = (ApiKey ?? string.Empty).Trim();
        Model = string.IsNullOrWhiteSpace(Model) ? "qwen3-vl" : Model.Trim();
        Temperature = NormalizeTemperature(Temperature);
    }

    public static double NormalizeTemperature(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return 0.2;
        return Math.Max(0.0, Math.Min(2.0, value));
    }

    public static string NormalizeBaseUrl(string value)
    {
        string url = string.IsNullOrWhiteSpace(value)
            ? "http://127.0.0.1:11434/v1"
            : value.Trim().TrimEnd('/');

        if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            url = url.Substring(0, url.Length - "/chat/completions".Length).TrimEnd('/');

        return url;
    }
}
