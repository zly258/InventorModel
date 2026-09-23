using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using InventorModel.Core.Diagnostics;

namespace InventorModel.Addin;

internal sealed class ContextPreparation
{
    public int EstimatedTokens { get; set; }
    public int BudgetTokens { get; set; }
    public bool Compressed { get; set; }
    public int RemovedMessages { get; set; }

    public string StatusText =>
        "上下文 " +
        FormatTokens(EstimatedTokens) +
        " / " +
        (BudgetTokens > 0 ? FormatTokens(BudgetTokens) : "自动") +
        (Compressed ? " · 已自动压缩" : string.Empty);

    private static string FormatTokens(int value) =>
        value >= 1000
            ? (value / 1000.0).ToString("0.#") + "k"
            : value.ToString();
}

internal sealed class AiContextManager
{
    private const int RecentMessagesToKeep = 14;
    private const int MaximumSummaryCharacters = 16000;
    private const int MaximumCurrentScriptCharacters = 10000;

    private readonly int _contextWindowTokens;
    private readonly int _reservedOutputTokens;
    private readonly JavaScriptSerializer _json =
        new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

    public AiContextManager(
        int contextWindowTokens,
        int maxOutputTokens)
    {
        _contextWindowTokens =
            AiSettings.NormalizeContextWindowTokens(contextWindowTokens);

        int output = AiSettings.NormalizeMaxOutputTokens(maxOutputTokens);
        _reservedOutputTokens = Math.Max(
            3072,
            (output > 0 ? output : 4096) + 1024);
    }

    public ContextPreparation Prepare(
        List<AgentMessage> messages,
        bool force = false)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        int budget = InputBudgetTokens();

        // Model-state payloads from an older successful revision are no longer
        // authoritative. Compact only those superseded tool/image payloads even
        // in Auto mode, while preserving natural-language turns, user source
        // images, and the latest successful build source.
        CompactSupersededModelState(messages);

        int originalEstimate = Estimate(messages);

        // Auto mode does not summarize natural-language conversation proactively.
        // Only superseded model-state payloads are compacted above because they are
        // no longer authoritative after a successful model mutation.
        if (!force &&
            (budget == 0 || originalEstimate <= budget))
        {
            return new ContextPreparation
            {
                EstimatedTokens = originalEstimate,
                BudgetTokens = budget
            };
        }

        StripOldImages(messages);
        int imageReducedEstimate = Estimate(messages);
        bool changed = imageReducedEstimate < originalEstimate;

        if (!force &&
            budget > 0 &&
            imageReducedEstimate <= budget)
        {
            return new ContextPreparation
            {
                EstimatedTokens = imageReducedEstimate,
                BudgetTokens = budget,
                Compressed = changed,
                RemovedMessages = 0
            };
        }

        int keepIndex = FindSafeKeepIndex(messages);
        if (keepIndex <= 1)
        {
            return new ContextPreparation
            {
                EstimatedTokens = imageReducedEstimate,
                BudgetTokens = budget,
                Compressed = changed,
                RemovedMessages = 0
            };
        }

        List<AgentMessage> older = messages
            .Skip(1)
            .Take(keepIndex - 1)
            .ToList();

        string summary = BuildSummary(older);
        int removed = older.Count;

        messages.RemoveRange(1, removed);
        messages.Insert(1, new AgentMessage
        {
            Role = "system",
            Content =
                "Compressed conversation memory. Treat this as prior-session context, " +
                "not as a new user request.\n\n" + summary
        });

        StripOldImages(messages);

        return new ContextPreparation
        {
            EstimatedTokens = Estimate(messages),
            BudgetTokens = budget,
            Compressed = true,
            RemovedMessages = removed
        };
    }

    private void CompactSupersededModelState(
        List<AgentMessage> messages)
    {
        int latestMutation =
            FindLatestSuccessfulMutationIndex(
                messages);

        if (latestMutation <= 1)
            return;

        string latestBuildCallId =
            FindLatestSuccessfulBuildCallId(
                messages);

        for (int i = 1;
             i < latestMutation;
             i++)
        {
            AgentMessage message =
                messages[i];

            if (string.Equals(
                    message.Role,
                    "tool",
                    StringComparison.OrdinalIgnoreCase) &&
                IsModelStateTool(message.Name))
            {
                message.Content =
                    "{\"compacted\":true," +
                    "\"reason\":\"superseded_model_revision\"," +
                    "\"tool\":\"" +
                    EscapeJson(message.Name) +
                    "\"}";
                continue;
            }

            if (string.Equals(
                    message.Role,
                    "assistant",
                    StringComparison.OrdinalIgnoreCase))
            {
                CompactSupersededBuildArguments(
                    message.ToolCalls,
                    latestBuildCallId);
                continue;
            }

            if (string.Equals(
                    message.Role,
                    "user",
                    StringComparison.OrdinalIgnoreCase) &&
                IsRenderedVerificationContent(
                    message.Content))
            {
                message.Content =
                    "[Earlier rendered model verification images removed from active context after a newer model revision. The files remain in the AI workspace.]";
            }
        }
    }

    private static int FindLatestSuccessfulMutationIndex(
        IReadOnlyList<AgentMessage> messages)
    {
        for (int i = messages.Count - 1;
             i >= 1;
             i--)
        {
            AgentMessage message =
                messages[i];

            if (!string.Equals(
                    message.Role,
                    "tool",
                    StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsSuccessfulMutationResult(
                    message.Name,
                    ExtractText(message.Content)))
            {
                return i;
            }
        }

        return -1;
    }

    private static string FindLatestSuccessfulBuildCallId(
        IReadOnlyList<AgentMessage> messages)
    {
        for (int i = messages.Count - 1;
             i >= 1;
             i--)
        {
            AgentMessage message =
                messages[i];

            if (!string.Equals(
                    message.Role,
                    "tool",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    message.Name,
                    "build",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsSuccessfulMutationResult(
                    message.Name,
                    ExtractText(message.Content)))
            {
                return message.ToolCallId ??
                       string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool IsSuccessfulMutationResult(
        string? toolName,
        string content)
    {
        string name =
            toolName ?? string.Empty;
        string value =
            content ?? string.Empty;

        if (string.Equals(
                name,
                "build",
                StringComparison.OrdinalIgnoreCase))
        {
            return value.IndexOf(
                       "\"built\":true",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        if (!string.Equals(
                name,
                "modify",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return value.IndexOf(
                   "\"ok\":false",
                   StringComparison.OrdinalIgnoreCase) < 0 &&
               value.IndexOf(
                   "\"error\":",
                   StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static bool IsModelStateTool(
        string? name)
    {
        string value =
            name ?? string.Empty;

        return value.Equals(
                   "build",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "modify",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "inspect",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "geometry",
                   StringComparison.OrdinalIgnoreCase) ||
               value.Equals(
                   "render",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void CompactSupersededBuildArguments(
        object? toolCalls,
        string latestBuildCallId)
    {
        if (!(toolCalls is IEnumerable sequence) ||
            toolCalls is string)
            return;

        foreach (object raw in sequence)
        {
            if (!(raw is Dictionary<string, object> call))
                continue;

            string id =
                call.TryGetValue(
                    "id",
                    out object rawId)
                    ? Convert.ToString(rawId) ??
                      string.Empty
                    : string.Empty;

            if (string.Equals(
                    id,
                    latestBuildCallId,
                    StringComparison.Ordinal))
                continue;

            Dictionary<string, object> function =
                call.TryGetValue(
                    "function",
                    out object rawFunction)
                    ? rawFunction as Dictionary<string, object> ??
                      new Dictionary<string, object>()
                    : new Dictionary<string, object>();

            string name =
                function.TryGetValue(
                    "name",
                    out object rawName)
                    ? Convert.ToString(rawName) ??
                      string.Empty
                    : string.Empty;

            if (!string.Equals(
                    name,
                    "build",
                    StringComparison.OrdinalIgnoreCase))
                continue;

            function["arguments"] =
                "{\"script\":\"[superseded build source removed; latest successful build source retained]\"}";
        }
    }

    private static bool IsRenderedVerificationContent(
        object? content)
    {
        if (!(content is object[] parts))
            return false;

        foreach (object part in parts)
        {
            if (!(part is Dictionary<string, object> item))
                continue;

            string type =
                item.TryGetValue(
                    "type",
                    out object rawType)
                    ? Convert.ToString(rawType) ??
                      string.Empty
                    : string.Empty;

            if (!string.Equals(
                    type,
                    "text",
                    StringComparison.OrdinalIgnoreCase) ||
                !item.TryGetValue(
                    "text",
                    out object rawText))
            {
                continue;
            }

            string text =
                Convert.ToString(rawText) ??
                string.Empty;

            if (text.StartsWith(
                    "These are the current working Part's",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string EscapeJson(
        string? value) =>
        (value ?? string.Empty)
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");

    private int InputBudgetTokens()
    {
        if (_contextWindowTokens <= 0)
            return 0;

        return Math.Max(
            2048,
            _contextWindowTokens - _reservedOutputTokens);
    }

    private int FindSafeKeepIndex(List<AgentMessage> messages)
    {
        int latestUser = -1;
        for (int i = messages.Count - 1; i >= 1; i--)
        {
            if (string.Equals(
                    messages[i].Role,
                    "user",
                    StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(messages[i].ToolCallId))
            {
                latestUser = i;
                break;
            }
        }

        if (latestUser <= 1)
            return 1;

        int candidate = Math.Max(2, messages.Count - RecentMessagesToKeep);
        candidate = Math.Min(candidate, latestUser);

        for (int i = candidate; i <= latestUser; i++)
        {
            if (string.Equals(
                    messages[i].Role,
                    "user",
                    StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(messages[i].ToolCallId))
                return i;
        }

        for (int i = candidate - 1; i >= 1; i--)
        {
            if (string.Equals(
                    messages[i].Role,
                    "user",
                    StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(messages[i].ToolCallId))
                return i;
        }

        return 1;
    }

    private void StripOldImages(List<AgentMessage> messages)
    {
        int preserveFrom = Math.Max(1, messages.Count - 8);

        for (int i = 1; i < preserveFrom; i++)
        {
            object content = messages[i].Content;
            if (!(content is object[] parts))
                continue;

            var textParts = new List<object>();
            int images = 0;

            foreach (object part in parts)
            {
                if (!(part is Dictionary<string, object> item))
                    continue;

                string type = item.TryGetValue("type", out object rawType)
                    ? Convert.ToString(rawType) ?? string.Empty
                    : string.Empty;

                if (string.Equals(
                        type,
                        "image_url",
                        StringComparison.OrdinalIgnoreCase))
                {
                    images++;
                    continue;
                }

                textParts.Add(item);
            }

            if (images == 0)
                continue;

            textParts.Add(new Dictionary<string, object>
            {
                ["type"] = "text",
                ["text"] =
                    "[Earlier image content removed from active context after visual analysis. " +
                    images +
                    " image(s) remain stored in the AI workspace.]"
            });

            messages[i].Content = textParts.ToArray();
        }
    }

    private string BuildSummary(IReadOnlyList<AgentMessage> messages)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Earlier conversation summary:");

        string latestScript = FindLatestBuildScript(messages);
        int userIndex = 0;

        foreach (AgentMessage message in messages)
        {
            if (builder.Length >= MaximumSummaryCharacters)
                break;

            if (string.Equals(
                    message.Role,
                    "system",
                    StringComparison.OrdinalIgnoreCase))
            {
                string memory = ExtractText(message.Content);
                if (!string.IsNullOrWhiteSpace(memory))
                    builder.AppendLine(Compact(memory, 8000));
                continue;
            }

            if (string.Equals(
                    message.Role,
                    "user",
                    StringComparison.OrdinalIgnoreCase))
            {
                string text = ExtractText(message.Content);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                userIndex++;
                builder.Append("- User ")
                    .Append(userIndex)
                    .Append(": ")
                    .AppendLine(Compact(text, 900));
                continue;
            }

            if (string.Equals(
                    message.Role,
                    "assistant",
                    StringComparison.OrdinalIgnoreCase))
            {
                string text = ExtractText(message.Content);
                if (!string.IsNullOrWhiteSpace(text))
                    builder.Append("- Assistant: ")
                        .AppendLine(Compact(text, 700));

                foreach (ToolCallSummary tool in ExtractToolCalls(message.ToolCalls))
                {
                    builder.Append("- Tool requested: ")
                        .Append(tool.Name)
                        .Append(" ")
                        .AppendLine(Compact(tool.Arguments, 500));
                }

                continue;
            }

            if (string.Equals(
                    message.Role,
                    "tool",
                    StringComparison.OrdinalIgnoreCase))
            {
                string text = ExtractText(message.Content);
                builder.Append("- Tool result ")
                    .Append(string.IsNullOrWhiteSpace(message.Name)
                        ? "unknown"
                        : message.Name)
                    .Append(": ")
                    .AppendLine(Compact(text, 650));
            }
        }

        if (!string.IsNullOrWhiteSpace(latestScript))
        {
            builder.AppendLine();
            builder.AppendLine("Latest complete .ivmodel source before compression:");
            builder.AppendLine(Compact(
                latestScript,
                MaximumCurrentScriptCharacters));
        }

        string value = builder.ToString();
        return value.Length <= MaximumSummaryCharacters
            ? value
            : value.Substring(0, MaximumSummaryCharacters) +
              "\n[Older memory summary truncated.]";
    }

    private string FindLatestBuildScript(
        IReadOnlyList<AgentMessage> messages)
    {
        string successfulCallId =
            FindLatestSuccessfulBuildCallId(
                messages);

        if (!string.IsNullOrWhiteSpace(
                successfulCallId))
        {
            string successful =
                FindBuildScript(
                    messages,
                    successfulCallId);

            if (!string.IsNullOrWhiteSpace(
                    successful))
                return successful;
        }

        return FindBuildScript(
            messages,
            null);
    }

    private string FindBuildScript(
        IReadOnlyList<AgentMessage> messages,
        string? requiredCallId)
    {
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            foreach (ToolCallSummary call in
                     ExtractToolCalls(messages[i].ToolCalls))
            {
                if (!string.Equals(
                        call.Name,
                        "build",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(
                        requiredCallId) &&
                    !string.Equals(
                        call.Id,
                        requiredCallId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    Dictionary<string, object> args =
                        _json.Deserialize<Dictionary<string, object>>(
                            call.Arguments) ??
                        new Dictionary<string, object>();

                    if (args.TryGetValue("script", out object raw))
                        return Convert.ToString(raw) ?? string.Empty;
                }
                catch (Exception ex)
                {
                    RuntimeLog.Warning(
                        "AI.Context",
                        "A previous build tool call could not be parsed while compacting context.",
                        ex);
                }
            }
        }

        return string.Empty;
    }

    private IEnumerable<ToolCallSummary> ExtractToolCalls(object? toolCalls)
    {
        if (!(toolCalls is IEnumerable sequence) ||
            toolCalls is string)
            yield break;

        foreach (object raw in sequence)
        {
            if (!(raw is Dictionary<string, object> call))
                continue;

            Dictionary<string, object> function =
                call.TryGetValue("function", out object rawFunction)
                    ? rawFunction as Dictionary<string, object> ??
                      new Dictionary<string, object>()
                    : new Dictionary<string, object>();

            yield return new ToolCallSummary
            {
                Id = call.TryGetValue("id", out object rawId)
                    ? Convert.ToString(rawId) ?? string.Empty
                    : string.Empty,
                Name = function.TryGetValue("name", out object rawName)
                    ? Convert.ToString(rawName) ?? string.Empty
                    : string.Empty,
                Arguments =
                    function.TryGetValue(
                        "arguments",
                        out object rawArguments)
                        ? Convert.ToString(rawArguments) ?? "{}"
                        : "{}"
            };
        }
    }

    private int Estimate(IEnumerable<AgentMessage> messages)
    {
        int total = 0;

        foreach (AgentMessage message in messages)
        {
            total += 8;
            total += EstimateText(message.Role);
            total += EstimateContent(message.Content);
            total += EstimateContent(message.ToolCalls);
            total += EstimateText(message.Name);
            total += EstimateText(message.ToolCallId);
        }

        return Math.Max(1, total);
    }

    private int EstimateContent(object? value)
    {
        if (value == null)
            return 0;

        if (value is string text)
        {
            if (text.StartsWith(
                    "data:image/",
                    StringComparison.OrdinalIgnoreCase))
                return 1400;

            return EstimateText(text);
        }

        if (value is Dictionary<string, object> dictionary)
        {
            if (dictionary.TryGetValue("type", out object rawType) &&
                string.Equals(
                    Convert.ToString(rawType),
                    "image_url",
                    StringComparison.OrdinalIgnoreCase))
                return 1400;

            int total = 0;
            foreach (KeyValuePair<string, object> pair in dictionary)
                total += EstimateText(pair.Key) +
                         EstimateContent(pair.Value);
            return total;
        }

        if (value is IEnumerable sequence)
        {
            int total = 0;
            foreach (object item in sequence)
                total += EstimateContent(item);
            return total;
        }

        return EstimateText(Convert.ToString(value));
    }

    private static int EstimateText(string? text)
    {
        string value = text ?? string.Empty;
        if (value.Length == 0)
            return 0;

        int ascii = 0;
        int nonAscii = 0;

        foreach (char ch in value)
        {
            if (ch <= 0x7F)
                ascii++;
            else
                nonAscii++;
        }

        return Math.Max(1, (ascii + 3) / 4 + nonAscii);
    }

    private static string ExtractText(object? content)
    {
        if (content == null)
            return string.Empty;

        if (content is string text)
            return text;

        if (content is object[] parts)
        {
            var output = new List<string>();

            foreach (object part in parts)
            {
                if (!(part is Dictionary<string, object> item))
                    continue;

                if (item.TryGetValue("type", out object rawType) &&
                    string.Equals(
                        Convert.ToString(rawType),
                        "text",
                        StringComparison.OrdinalIgnoreCase) &&
                    item.TryGetValue("text", out object rawText))
                {
                    output.Add(Convert.ToString(rawText) ??
                               string.Empty);
                }
                else if (item.TryGetValue("type", out rawType) &&
                         string.Equals(
                             Convert.ToString(rawType),
                             "image_url",
                             StringComparison.OrdinalIgnoreCase))
                {
                    output.Add("[image]");
                }
            }

            return string.Join("\n", output);
        }

        return JsonDisplayFormatter.FormatObject(content);
    }

    private static string Compact(string value, int maximum)
    {
        string text = (value ?? string.Empty).Trim();

        return text.Length <= maximum
            ? text
            : text.Substring(0, maximum) + "…";
    }

    private sealed class ToolCallSummary
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Arguments { get; set; } = "{}";
    }
}
