using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Threading;
using InventorModel.Core.Ai;

namespace InventorModel.Addin;

internal sealed class AiAgentSession : IDisposable
{
    private const int MaxToolRounds = 10;

    private readonly AiSettings _settings;
    private readonly OpenAiCompatibleClient _client;
    private readonly ModelToolExecutor _toolExecutor;
    private readonly Dispatcher _dispatcher;
    private readonly AiContextManager _contextManager = new AiContextManager();
    private readonly List<AgentMessage> _messages = new List<AgentMessage>();
    private readonly List<AgentMessage> _historyMessages = new List<AgentMessage>();
    private int _contextCompressionCount;
    private readonly JavaScriptSerializer _json =
        new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

    public AiAgentSession(
        global::Inventor.Application application,
        Dispatcher dispatcher,
        AiSettings settings)
    {
        _settings = (settings ?? new AiSettings()).Clone();
        _settings.Normalize();
        Workspace = AiWorkspace.CreateSession("chat");
        _client = new OpenAiCompatibleClient(_settings);
        _toolExecutor = new ModelToolExecutor(application, Workspace);
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _messages.Add(new AgentMessage
        {
            Role = "system",
            Content = BuildSystemPrompt()
        });
    }

    public AiWorkspace Workspace { get; }

    public string HistoryPath => Workspace.HistoryPath;

    public async Task<string> SendAsync(
        string text,
        string imagePath,
        Action<bool> onStreamReset,
        Action<string> onContentDelta,
        Action<string> onActivity,
        Action<AgentToolTrace> onToolTrace,
        Action<ContextPreparation> onContext,
        CancellationToken cancellationToken)
    {
        object userContent = BuildUserContent(text, imagePath);
        var userMessage = new AgentMessage
        {
            Role = "user",
            Content = userContent
        };
        _messages.Add(userMessage);
        RecordHistory(userMessage);
        SaveHistory();

        for (int round = 1; round <= MaxToolRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ContextPreparation context = _contextManager.Prepare(_messages);
            onContext?.Invoke(context);
            if (context.Compressed)
            {
                _contextCompressionCount++;
                onActivity?.Invoke(
                    "上下文已自动压缩：" +
                    context.RemovedMessages +
                    " 条旧消息已合并，最近对话和当前模型状态已保留。");
                SaveHistory();
            }

            onStreamReset?.Invoke(true);

            AgentCompletion completion = await _client.CompleteStreamingAsync(
                _messages,
                _toolExecutor.Tools,
                new StreamingCallbacks
                {
                    OnContentDelta = onContentDelta,
                    OnStreamReset = () => onStreamReset?.Invoke(false),
                    OnRetry = (attempt, message) =>
                        onActivity?.Invoke(
                            "正在重试 AI 请求 (" + attempt + "): " +
                            Compact(message))
                },
                cancellationToken).ConfigureAwait(false);

            var assistantMessage = new AgentMessage
            {
                Role = "assistant",
                Content = completion.Content ?? string.Empty,
                ToolCalls = completion.RawToolCalls
            };
            _messages.Add(assistantMessage);
            RecordHistory(assistantMessage);
            SaveHistory();

            if (completion.ToolCalls.Count == 0)
                return completion.Content ?? string.Empty;

            foreach (AgentToolCall call in completion.ToolCalls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string formattedArguments =
                    JsonDisplayFormatter.Format(call.ArgumentsJson);

                onActivity?.Invoke("调用工具 " + call.Name + "…");
                onToolTrace?.Invoke(new AgentToolTrace
                {
                    Id = call.Id,
                    Name = call.Name,
                    Arguments = formattedArguments,
                    Completed = false
                });

                string toolResult;
                bool succeeded = true;

                try
                {
                    toolResult = _dispatcher.Invoke(
                        () => _toolExecutor.Execute(
                            call.Name,
                            call.ArgumentsJson));
                }
                catch (Exception ex)
                {
                    succeeded = false;
                    toolResult = _json.Serialize(new
                    {
                        ok = false,
                        tool = call.Name,
                        error = ex.Message
                    });
                }

                var toolMessage = new AgentMessage
                {
                    Role = "tool",
                    ToolCallId = call.Id,
                    Name = call.Name,
                    Content = toolResult
                };
                _messages.Add(toolMessage);
                RecordHistory(toolMessage);

                onToolTrace?.Invoke(new AgentToolTrace
                {
                    Id = call.Id,
                    Name = call.Name,
                    Arguments = formattedArguments,
                    Result = JsonDisplayFormatter.Format(toolResult),
                    Completed = true,
                    Succeeded = succeeded
                });

                if (string.Equals(
                        call.Name,
                        "render",
                        StringComparison.OrdinalIgnoreCase))
                {
                    object? visual = BuildRenderContent(toolResult);
                    if (visual != null)
                        _messages.Add(new AgentMessage
                        {
                            Role = "user",
                            Content = visual
                        });
                }

                SaveHistory();
            }
        }

        throw new InvalidOperationException(
            "AI exceeded the maximum tool-call rounds. Refine the request or inspect the current model.");
    }

    private object BuildUserContent(string text, string imagePath)
    {
        string prompt = text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(imagePath))
            return prompt;

        if (!System.IO.File.Exists(imagePath))
            throw new FileNotFoundException(
                "Attached image was not found.",
                imagePath);

        string extension = Path.GetExtension(imagePath).ToLowerInvariant();
        string mime = extension == ".jpg" || extension == ".jpeg"
            ? "image/jpeg"
            : extension == ".webp"
                ? "image/webp"
                : "image/png";

        string dataUrl = "data:" + mime + ";base64," +
                         Convert.ToBase64String(
                             System.IO.File.ReadAllBytes(imagePath));

        return new object[]
        {
            new Dictionary<string, object>
            {
                ["type"] = "text",
                ["text"] = string.IsNullOrWhiteSpace(prompt)
                    ? "Analyze the attached engineering image and build or modify the requested Inventor Part."
                    : prompt
            },
            new Dictionary<string, object>
            {
                ["type"] = "image_url",
                ["image_url"] = new Dictionary<string, object>
                {
                    ["url"] = dataUrl
                }
            }
        };
    }

    private string BuildSystemPrompt()
    {
        string skill = LoadSkillText();
        return
            "You are InventorModel, a focused Autodesk Inventor Part-modeling agent. " +
            "Your job is to turn text or engineering-drawing images into native editable Inventor Part geometry.\n" +
            "There is exactly one modeling representation: .imodel DSL. Do not invent a second whole-model JSON format.\n" +
            "Use the provided tools for every model read/write. For a new model, write complete .imodel source, call validate, then call build. " +
            "For a small correction, prefer modify with set/suppress/unsuppress/delete instead of rebuilding. " +
            "After meaningful geometry changes, inspect the model. Render four views when visual verification will help. " +
            "Do not claim success until the tool result confirms the operation. " +
            "Keep feature names stable and dimensions parameterized. Stop when the user's requested geometry is satisfied.\n" +
            "The chat automatically compacts older conversation context when it grows large. " +
            "Rely on the retained summary, current model source, recent tool chain, and fresh inspect/render results rather than assuming omitted old details.\n" +
            "All internal AI artifacts belong in the current InventorModel AI workspace: " +
            Workspace.SessionDirectory + ". " +
            "Do not create scratch scripts, verification images, or temporary files elsewhere. " +
            "Only save a final IPT outside this workspace when the user explicitly asks for a destination.\n\n" +
            "InventorModel skill guidance:\n" + skill;
    }

    private static string LoadSkillText()
    {
        string[] roots =
        {
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Skills",
                "inventor-model"),
            Path.GetFullPath(
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "Skills",
                    "inventor-model"))
        };

        foreach (string root in roots)
        {
            try
            {
                string skillPath = Path.Combine(root, "SKILL.md");
                if (!System.IO.File.Exists(skillPath))
                    continue;

                return System.IO.File.ReadAllText(skillPath);
            }
            catch { }
        }

        return
            "Use only the implemented .imodel DSL for native Inventor Part modeling.\n" +
            "Sketch: point line circle arc ellipse rect centerrect slot polygon spline constraint dim.\n" +
            "Features: extrude revolve sweep loft hole fillet chamfer shell pattern_rect pattern_circular mirror.\n" +
            "Edits: set, suppress, unsuppress, delete.\n" +
            "Verify with inspect and render after meaningful geometry changes.";
    }

    private object? BuildRenderContent(string toolResult)
    {
        try
        {
            Dictionary<string, object> value =
                _json.Deserialize<Dictionary<string, object>>(toolResult);

            if (!value.TryGetValue("images", out object raw) ||
                !(raw is IEnumerable paths))
                return null;

            var parts = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] =
                        "Visually inspect these newly rendered front, top, right, and isometric views before claiming success."
                }
            };

            foreach (object item in paths)
            {
                string path = Convert.ToString(item) ?? string.Empty;
                if (!System.IO.File.Exists(path))
                    continue;

                parts.Add(new Dictionary<string, object>
                {
                    ["type"] = "image_url",
                    ["image_url"] = new Dictionary<string, object>
                    {
                        ["url"] = "data:image/png;base64," +
                                  Convert.ToBase64String(
                                      System.IO.File.ReadAllBytes(path))
                    }
                });
            }

            return parts.Count > 1 ? parts.ToArray() : null;
        }
        catch
        {
            return null;
        }
    }

    private void RecordHistory(AgentMessage message)
    {
        if (message == null || string.Equals(
                message.Role,
                "system",
                StringComparison.OrdinalIgnoreCase))
            return;

        _historyMessages.Add(new AgentMessage
        {
            Role = message.Role,
            Name = message.Name,
            ToolCallId = message.ToolCallId,
            ToolCalls = message.ToolCalls,
            Content = HistorySafeContent(message.Content)
        });
    }

    private static object HistorySafeContent(object content)
    {
        if (!(content is object[] parts))
            return content ?? string.Empty;

        var safe = new List<object>();

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
                safe.Add(new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = "[attached image]"
                });
                continue;
            }

            safe.Add(item);
        }

        return safe.ToArray();
    }

    private void SaveHistory()
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("# InventorModel AI History").AppendLine();
            builder.AppendLine("- Model: " + _settings.Model);
            builder.AppendLine("- Base URL: " + _settings.BaseUrl);
            builder.AppendLine("- Workspace: " + Workspace.SessionDirectory);
            builder.AppendLine("- Context compactions: " + _contextCompressionCount);
            builder.AppendLine(
                "- Updated: " +
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .AppendLine();

            int index = 0;
            foreach (AgentMessage message in _historyMessages)
            {
                index++;
                builder.AppendLine(
                    "## " + index + ". " +
                    message.Role.ToUpperInvariant())
                    .AppendLine();

                string history = HistoryContent(message.Content);
                if (string.Equals(
                        message.Role,
                        "tool",
                        StringComparison.OrdinalIgnoreCase))
                {
                    builder.AppendLine("~~~json");
                    builder.AppendLine(JsonDisplayFormatter.Format(history));
                    builder.AppendLine("~~~");
                }
                else
                {
                    builder.AppendLine(history);
                }

                if (message.ToolCalls != null)
                {
                    builder.AppendLine();
                    builder.AppendLine("Tool calls:");
                    builder.AppendLine("~~~json");
                    builder.AppendLine(
                        JsonDisplayFormatter.FormatObject(
                            message.ToolCalls));
                    builder.AppendLine("~~~");
                }

                builder.AppendLine();
            }

            System.IO.File.WriteAllText(
                HistoryPath,
                builder.ToString(),
                new UTF8Encoding(true));
        }
        catch { }
    }

    private string HistoryContent(object content)
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

                string type =
                    item.TryGetValue("type", out object rawType)
                        ? Convert.ToString(rawType)
                        : string.Empty;

                if (string.Equals(
                        type,
                        "text",
                        StringComparison.OrdinalIgnoreCase) &&
                    item.TryGetValue("text", out object rawText))
                {
                    output.Add(
                        Convert.ToString(rawText) ??
                        string.Empty);
                }
                else if (string.Equals(
                             type,
                             "image_url",
                             StringComparison.OrdinalIgnoreCase))
                {
                    output.Add("[attached image]");
                }
            }

            return string.Join(
                Environment.NewLine,
                output);
        }

        return JsonDisplayFormatter.FormatObject(content);
    }

    private static string Compact(string value)
    {
        string text = (value ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        return text.Length <= 180
            ? text
            : text.Substring(0, 180) + "…";
    }

    public void Dispose()
    {
        try { _client.Dispose(); } catch { }
        try { Workspace.ClearTemp(); } catch { }
    }
}
