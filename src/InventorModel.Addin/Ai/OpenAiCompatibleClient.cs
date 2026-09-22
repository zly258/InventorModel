using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using InventorModel.Core.Diagnostics;

namespace InventorModel.Addin;

internal sealed class AgentMessage
{
    public string Role { get; set; } = string.Empty;
    public object Content { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? ToolCallId { get; set; }
    public object? ToolCalls { get; set; }
}

internal sealed class AgentToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
    public object? Raw { get; set; }
}

internal sealed class AgentCompletion
{
    public string Content { get; set; } = string.Empty;
    public List<AgentToolCall> ToolCalls { get; } =
        new List<AgentToolCall>();
    public object? RawToolCalls { get; set; }
}

internal sealed class StreamingCallbacks
{
    public Action<string>? OnContentDelta { get; set; }
    public Action? OnStreamReset { get; set; }
    public Action<int, string>? OnRetry { get; set; }
}

internal sealed class AiRequestException : InvalidOperationException
{
    public AiRequestException(
        int statusCode,
        string responseBody)
        : base(
            "AI request failed " +
            statusCode +
            ": " +
            Compact(responseBody))
    {
        StatusCode = statusCode;
        ResponseBody = responseBody ?? string.Empty;
    }

    public int StatusCode { get; }

    public string ResponseBody { get; }

    public bool IsContextLengthExceeded
    {
        get
        {
            string value = (ResponseBody + " " + Message)
                .ToLowerInvariant();

            return value.Contains("context_length_exceeded") ||
                   value.Contains("context length") ||
                   value.Contains("context window") ||
                   value.Contains("maximum context") ||
                   value.Contains("max context") ||
                   value.Contains("prompt is too long") ||
                   value.Contains("input is too long") ||
                   value.Contains("too many tokens") ||
                   value.Contains("exceeds the context") ||
                   value.Contains("context limit");
        }
    }

    private static string Compact(string value)
    {
        string text = (value ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        return text.Length <= 1200
            ? text
            : text.Substring(0, 1200) + "…";
    }
}

internal sealed class OpenAiCompatibleClient : IDisposable
{
    private readonly AiSettings _settings;
    private readonly HttpClient _http;
    private readonly JavaScriptSerializer _json =
        new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

    public OpenAiCompatibleClient(AiSettings settings)
    {
        _settings = settings ??
                    throw new ArgumentNullException(nameof(settings));
        _settings.Normalize();

        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(
                _settings.RequestTimeoutSeconds)
        };

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _settings.ApiKey);
        }
    }

    public async Task<AgentCompletion> CompleteStreamingAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<object> tools,
        StreamingCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        callbacks ??= new StreamingCallbacks();
        int maxAttempts = _settings.RetryCount + 1;

        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await StreamOnceAsync(
                        messages,
                        tools,
                        callbacks,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                !cancellationToken.IsCancellationRequested &&
                attempt < maxAttempts &&
                IsRetryable(ex))
            {
                RuntimeLog.Warning(
                    "AI.Request",
                    "Retrying AI request " +
                    attempt +
                    " of " +
                    _settings.RetryCount +
                    ".",
                    ex);

                callbacks.OnStreamReset?.Invoke();
                callbacks.OnRetry?.Invoke(
                    attempt,
                    ex.Message);

                await Task.Delay(
                        TimeSpan.FromSeconds(
                            Math.Pow(2, attempt - 1)),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task<AgentCompletion> StreamOnceAsync(
        IReadOnlyList<AgentMessage> messages,
        IReadOnlyList<object> tools,
        StreamingCallbacks callbacks,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object>(
            StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, object> pair in
                 _settings.GetAdditionalParameters())
        {
            payload[pair.Key] = pair.Value;
        }

        payload["model"] = _settings.Model;
        payload["messages"] = messages.Select(ToApiMessage).ToArray();
        payload["stream"] = true;
        payload["temperature"] =
            AiSettings.NormalizeTemperature(_settings.Temperature);

        if (tools != null && tools.Count > 0)
        {
            payload["tools"] = tools.ToArray();
            payload["tool_choice"] = "auto";
        }

        if (!_settings.ReasoningEnabled)
            payload["reasoning_effort"] = "none";

        if (_settings.MaxOutputTokens > 0)
            payload["max_tokens"] = _settings.MaxOutputTokens;

        string requestJson = _json.Serialize(payload);
        var result = new AgentCompletion();
        var content = new StringBuilder();
        var toolParts = new Dictionary<int, ToolCallAccumulator>();

        using var request =
            new HttpRequestMessage(HttpMethod.Post, GetEndpoint());
        request.Content =
            new StringContent(
                requestJson,
                Encoding.UTF8,
                "application/json");

        using HttpResponseMessage response =
            await _http.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string error =
                await response.Content
                    .ReadAsStringAsync()
                    .ConfigureAwait(false);

            throw new AiRequestException(
                (int)response.StatusCode,
                error);
        }

        using Stream stream =
            await response.Content
                .ReadAsStreamAsync()
                .ConfigureAwait(false);
        using var reader =
            new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string line =
                await reader.ReadLineAsync()
                    .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(line) ||
                !line.StartsWith(
                    "data:",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string data = line.Substring(5).Trim();
            if (data == "[DONE]")
                break;

            Dictionary<string, object> root;
            try
            {
                root =
                    _json.Deserialize<Dictionary<string, object>>(data) ??
                    new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "AI.Stream",
                    "Ignored an invalid streaming JSON event.",
                    ex);
                continue;
            }

            Dictionary<string, object> choice =
                FirstDictionary(GetValue(root, "choices"));
            Dictionary<string, object> delta =
                AsDictionary(GetValue(choice, "delta"));

            if (delta.Count == 0)
                continue;

            string text = Convert.ToString(
                GetValue(delta, "content") ??
                string.Empty) ?? string.Empty;

            if (!string.IsNullOrEmpty(text))
            {
                content.Append(text);
                callbacks.OnContentDelta?.Invoke(text);
            }

            foreach (Dictionary<string, object> call in
                     Dictionaries(GetValue(delta, "tool_calls")))
            {
                int index = ConvertToInt(
                    GetValue(call, "index"),
                    toolParts.Count);

                if (!toolParts.TryGetValue(
                        index,
                        out ToolCallAccumulator? accumulator))
                {
                    accumulator = new ToolCallAccumulator();
                    toolParts[index] = accumulator;
                }

                string id = Convert.ToString(
                    GetValue(call, "id") ??
                    string.Empty) ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(id))
                    accumulator.Id = id;

                Dictionary<string, object> function =
                    AsDictionary(GetValue(call, "function"));

                string name = Convert.ToString(
                    GetValue(function, "name") ??
                    string.Empty) ?? string.Empty;
                string arguments = Convert.ToString(
                    GetValue(function, "arguments") ??
                    string.Empty) ?? string.Empty;

                if (!string.IsNullOrEmpty(name))
                    accumulator.Name.Append(name);
                if (!string.IsNullOrEmpty(arguments))
                    accumulator.Arguments.Append(arguments);
            }
        }

        result.Content = content.ToString();
        var rawCalls = new List<object>();

        foreach (KeyValuePair<int, ToolCallAccumulator> pair in
                 toolParts.OrderBy(x => x.Key))
        {
            ToolCallAccumulator accumulator = pair.Value;

            string id =
                string.IsNullOrWhiteSpace(accumulator.Id)
                    ? Guid.NewGuid().ToString("N")
                    : accumulator.Id;

            string name = accumulator.Name.ToString();
            string arguments =
                accumulator.Arguments.Length == 0
                    ? "{}"
                    : accumulator.Arguments.ToString();

            var raw = new Dictionary<string, object>
            {
                ["id"] = id,
                ["type"] = "function",
                ["function"] =
                    new Dictionary<string, object>
                    {
                        ["name"] = name,
                        ["arguments"] = arguments
                    }
            };

            rawCalls.Add(raw);
            result.ToolCalls.Add(
                new AgentToolCall
                {
                    Id = id,
                    Name = name,
                    ArgumentsJson = arguments,
                    Raw = raw
                });
        }

        result.RawToolCalls =
            rawCalls.Count == 0
                ? null
                : rawCalls.ToArray();

        return result;
    }

    private string GetEndpoint()
    {
        string baseUrl =
            AiSettings.NormalizeBaseUrl(_settings.BaseUrl);

        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException(
                "AI service URL is not configured.");

        return baseUrl.TrimEnd('/') +
               "/chat/completions";
    }

    private static bool IsRetryable(Exception exception)
    {
        if (exception is AiRequestException request)
        {
            return request.StatusCode == 408 ||
                   request.StatusCode == 429 ||
                   request.StatusCode == 500 ||
                   request.StatusCode == 502 ||
                   request.StatusCode == 503 ||
                   request.StatusCode == 504;
        }

        return exception is HttpRequestException ||
               exception is TimeoutException ||
               exception is TaskCanceledException;
    }

    private static Dictionary<string, object> ToApiMessage(
        AgentMessage message)
    {
        var result = new Dictionary<string, object>
        {
            ["role"] = message.Role,
            ["content"] =
                message.Content ??
                string.Empty
        };

        if (!string.IsNullOrWhiteSpace(message.Name))
            result["name"] = message.Name!;
        if (!string.IsNullOrWhiteSpace(message.ToolCallId))
            result["tool_call_id"] = message.ToolCallId!;
        if (message.ToolCalls != null)
            result["tool_calls"] = message.ToolCalls;

        return result;
    }

    private static object? GetValue(
        Dictionary<string, object> dictionary,
        string key)
    {
        if (dictionary == null ||
            string.IsNullOrEmpty(key))
            return null;

        dictionary.TryGetValue(
            key,
            out object? value);
        return value;
    }

    private static Dictionary<string, object> AsDictionary(
        object? value) =>
        value as Dictionary<string, object> ??
        new Dictionary<string, object>();

    private static Dictionary<string, object> FirstDictionary(
        object? value) =>
        Dictionaries(value).FirstOrDefault() ??
        new Dictionary<string, object>();

    private static IEnumerable<Dictionary<string, object>>
        Dictionaries(object? value)
    {
        if (value == null)
            yield break;

        if (value is Dictionary<string, object> one)
        {
            yield return one;
            yield break;
        }

        if (value is IEnumerable sequence &&
            !(value is string))
        {
            foreach (object item in sequence)
            {
                if (item is Dictionary<string, object> dictionary)
                    yield return dictionary;
            }
        }
    }

    private static int ConvertToInt(
        object? value,
        int fallback)
    {
        try
        {
            return Convert.ToInt32(value);
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "AI.Stream",
                "A tool-call index could not be converted to an integer.",
                ex);
            return fallback;
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed class ToolCallAccumulator
    {
        public string Id { get; set; } = string.Empty;
        public StringBuilder Name { get; } =
            new StringBuilder();
        public StringBuilder Arguments { get; } =
            new StringBuilder();
    }
}
