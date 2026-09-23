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
using InventorModel.Core.Diagnostics;

namespace InventorModel.Addin;

internal sealed class AiAgentSession : IDisposable
{
    private readonly AiSettings _settings;
    private readonly OpenAiCompatibleClient _client;
    private readonly ModelToolExecutor _toolExecutor;
    private readonly Dispatcher _dispatcher;
    private readonly AiContextManager _contextManager;
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
        _contextManager = new AiContextManager(
            _settings.ContextWindowTokens,
            _settings.MaxOutputTokens);
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
        Action<bool>? onStreamReset,
        Action<string>? onContentDelta,
        Action<string>? onActivity,
        Action<AgentToolTrace> onToolTrace,
        Action<IReadOnlyList<string>>? onPreview,
        Action<ContextPreparation>? onContext,
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

        int toolCallCount = 0;
        int buildCallCount = 0;
        int modelRevision = 0;
        var attemptedStateCalls =
            new HashSet<string>(StringComparer.Ordinal);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ContextPreparation context =
                _contextManager.Prepare(_messages);
            ReportContextPreparation(
                context,
                onContext,
                onActivity);

            onStreamReset?.Invoke(true);

            AgentCompletion completion;
            try
            {
                completion =
                    await CompleteAsync(
                            onStreamReset,
                            onContentDelta,
                            onActivity,
                            cancellationToken)
                        .ConfigureAwait(false);
            }
            catch (AiRequestException ex)
                when (ex.IsContextLengthExceeded)
            {
                ContextPreparation forced =
                    _contextManager.Prepare(
                        _messages,
                        force: true);

                if (!forced.Compressed)
                    throw;

                ReportContextPreparation(
                    forced,
                    onContext,
                    onActivity);

                onStreamReset?.Invoke(false);
                completion =
                    await CompleteAsync(
                            onStreamReset,
                            onContentDelta,
                            onActivity,
                            cancellationToken)
                        .ConfigureAwait(false);
            }

            if (completion.ToolCalls.Count > 0 &&
                toolCallCount + completion.ToolCalls.Count >
                _settings.MaxToolCalls)
            {
                throw new InvalidOperationException(
                    Ui(
                        "本轮需要调用 " +
                        completion.ToolCalls.Count +
                        " 个工具，但剩余调用额度只有 " +
                        (_settings.MaxToolCalls - toolCallCount) +
                        "。请提高“最大调用次数”，或把任务拆成更小的步骤。",
                        "This round requires " +
                        completion.ToolCalls.Count +
                        " tool calls, but only " +
                        (_settings.MaxToolCalls - toolCallCount) +
                        " remain. Increase Max tool calls or split the task into smaller steps."));
            }

            var assistantMessage = new AgentMessage
            {
                Role = "assistant",
                Content = completion.Content ?? string.Empty,
                ToolCalls = completion.RawToolCalls
            };
            _messages.Add(assistantMessage);
            RecordHistory(assistantMessage);

            if (completion.ToolCalls.Count == 0)
            {
                SaveHistory();
                return completion.Content ?? string.Empty;
            }

            foreach (AgentToolCall call in completion.ToolCalls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                toolCallCount++;

                string formattedArguments =
                    JsonDisplayFormatter.Format(call.ArgumentsJson);

                onActivity?.Invoke(
                    Ui("调用工具 ", "Calling tool ") +
                    call.Name +
                    "…");
                onToolTrace?.Invoke(new AgentToolTrace
                {
                    Id = call.Id,
                    Name = call.Name,
                    Arguments = formattedArguments,
                    Completed = false
                });

                string toolResult;
                bool succeeded = true;
                string normalizedTool =
                    (call.Name ?? string.Empty).Trim().ToLowerInvariant();
                bool stateSensitive =
                    normalizedTool == "build" ||
                    normalizedTool == "modify" ||
                    normalizedTool == "inspect" ||
                    normalizedTool == "geometry" ||
                    normalizedTool == "render";
                string stateCallSignature =
                    modelRevision + "|" +
                    normalizedTool + "|" +
                    (call.ArgumentsJson ?? string.Empty);

                if (stateSensitive &&
                    !attemptedStateCalls.Add(stateCallSignature))
                {
                    succeeded = false;
                    toolResult = _json.Serialize(new
                    {
                        ok = false,
                        blocked = true,
                        tool = call.Name,
                        reason = "identical_retry",
                        modelRevision,
                        nextAction =
                            "Do not repeat the same action on unchanged model state. Inspect current facts, choose a materially different correction, or stop and report unsupported/uncertain geometry."
                    });
                    onActivity?.Invoke(
                        Ui(
                            "已阻止同一模型状态下的重复工具调用。",
                            "Blocked an identical tool retry on unchanged model state."));
                }
                else if (normalizedTool == "build" &&
                         buildCallCount >= 2)
                {
                    succeeded = false;
                    toolResult = _json.Serialize(new
                    {
                        ok = false,
                        blocked = true,
                        tool = call.Name,
                        reason = "structural_rebuild_limit",
                        buildLimit = 2,
                        nextAction =
                            "The initial build plus one structural correction is the limit for this turn. Report the remaining shape mismatch or unsupported capability instead of creating another approximation."
                    });
                    onActivity?.Invoke(
                        Ui(
                            "已达到本轮结构重建上限，停止盲目重试。",
                            "Structural rebuild limit reached; blind retrying was stopped."));
                }
                else
                {
                    try
                    {
                        toolResult = _dispatcher.Invoke(
                            () => _toolExecutor.Execute(
                                call.Name,
                                call.ArgumentsJson));

                        if (normalizedTool == "build")
                        {
                            buildCallCount++;
                            modelRevision++;
                        }
                        else if (normalizedTool == "modify")
                        {
                            modelRevision++;
                        }
                    }
                    catch (Exception ex)
                    {
                        succeeded = false;
                        toolResult = _json.Serialize(new
                        {
                            ok = false,
                            tool = call.Name,
                            error = ex.Message,
                            retryable = false,
                            nextAction =
                                "Read deterministic model state and choose a different correction. Do not repeat the same failed mutation unchanged."
                        });
                    }
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

                if (succeeded &&
                    string.Equals(
                        call.Name,
                        "render",
                        StringComparison.OrdinalIgnoreCase))
                {
                    IReadOnlyList<string> renderPaths =
                        ExtractRenderPaths(toolResult);

                    if (renderPaths.Count > 0)
                    {
                        onPreview?.Invoke(renderPaths);

                        object? visual =
                            BuildRenderContent(renderPaths);

                        if (visual != null)
                            _messages.Add(new AgentMessage
                            {
                                Role = "user",
                                Content = visual
                            });
                    }
                }

            }

            SaveHistory();
        }

    }

    private async Task<AgentCompletion> CompleteAsync(
        Action<bool>? onStreamReset,
        Action<string>? onContentDelta,
        Action<string>? onActivity,
        CancellationToken cancellationToken)
    {
        return await _client.CompleteStreamingAsync(
                _messages,
                _toolExecutor.Tools,
                new StreamingCallbacks
                {
                    OnContentDelta = onContentDelta,
                    OnStreamReset =
                        () => onStreamReset?.Invoke(false),
                    OnRetry = (attempt, message) =>
                        onActivity?.Invoke(
                            Ui("正在重试 AI 请求 (", "Retrying AI request (") +
                            attempt +
                            "): " +
                            Compact(message))
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private void ReportContextPreparation(
        ContextPreparation context,
        Action<ContextPreparation>? onContext,
        Action<string>? onActivity)
    {
        onContext?.Invoke(context);

        if (!context.Compressed)
            return;

        _contextCompressionCount++;
        onActivity?.Invoke(
            context.RemovedMessages > 0
                ? Ui(
                    "上下文不足，已自动压缩 " +
                    context.RemovedMessages +
                    " 条旧消息；最近对话和当前模型状态已保留。",
                    "Context was insufficient. " +
                    context.RemovedMessages +
                    " older messages were compacted; recent turns and current model state were retained.")
                : Ui(
                    "上下文不足，已移除旧图片负载；图片文件仍保留在 AI 工作目录。",
                    "Context was insufficient. Older image payloads were removed from active context; the image files remain in the AI workspace."));
        SaveHistory();
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
            "You are InventorModel, a focused Autodesk Inventor Part-modeling agent.\n" +
            BuildLanguageInstruction() +
            "Your job is to turn text or engineering-drawing images into native editable Inventor Part geometry.\n" +
            "There is exactly one modeling representation: .ivmodel DSL. Do not invent a second whole-model JSON format.\n" +
            "Use the provided tools for every model read/write. For a new model, write complete .ivmodel source and call build directly; build validates the DSL internally. Use validate only for an explicit dry run or when debugging syntax before touching Inventor. " +
            "A modeling task owns exactly one session working Part: the first build creates it and every later structural build replaces geometry inside that same Part. Never create another Part as a retry or visual variant. " +
            "For a small correction, prefer modify with set/suppress/unsuppress/delete instead of rebuilding. " +
            "The build result already contains deterministic inspection, and modify returns the updated inspection; do not immediately call inspect again after either tool. Treat unhealthyFeatureCount > 0 as a hard deterministic failure. Treat underConstrainedSketchCount as a parametric-quality warning unless the user or skill explicitly requires fully constrained sketches; do not rebuild geometry solely because this count is nonzero. Call inspect only when state is otherwise unclear. Query geometry immediately before any edge-index or face-index finishing operation and keep topology limits small; if geometry reports truncated=true, increase only the needed limit. Never guess transient indexes. Render four 640px views once after deterministic facts are valid, and use them as final visible-shape confirmation. " +
            "Never repeat an identical tool call on unchanged model state. Failed validation/execution does not consume a successful build slot. In one user turn, allow at most two successful structural builds: the initial build and one materially different correction. If the shape is still wrong, stop instead of guessing repeatedly and state the exact remaining mismatch or unsupported geometry. " +
            "Do not claim success until deterministic gates pass and, when shape matters, the rendered silhouette also matches. " +
            "Keep feature names stable and dimensions parameterized. Stop when the user's requested geometry is satisfied.\n" +
            "The chat keeps the active conversation intact while it fits the configured context budget. " +
            "When context-window mode is Auto, do not compact proactively; compact only after the provider reports that the request exceeds its context window. " +
            "After compaction, rely on the retained summary, current model source, recent tool chain, and fresh inspect/render results rather than assuming omitted old details.\n" +
            "All internal AI artifacts belong in the current InventorModel AI workspace: " +
            Workspace.SessionDirectory + ". " +
            "Do not create scratch scripts, verification images, or temporary files elsewhere. " +
            "Only save a final IPT outside this workspace when the user explicitly asks for a destination.\n\n" +
            "InventorModel skill guidance:\n" + skill;
    }

    private string BuildLanguageInstruction()
    {
        if (string.Equals(
                _settings.EffectiveResponseLanguage,
                AiSettings.LanguageEnglish,
                StringComparison.OrdinalIgnoreCase))
        {
            return
                "All user-facing natural-language replies MUST be in English. " +
                "Keep DSL syntax, code, API identifiers, file paths, and tool names unchanged. " +
                "Tool output, diagnostics, system instructions, and skill references may use another language; they MUST NOT change the reply language.\n";
        }

        return
            "All user-facing natural-language replies MUST use Simplified Chinese. " +
            "Do not switch to English merely because tool output, diagnostics, code comments, system instructions, or skill references are English. " +
            "Keep DSL syntax, code, API identifiers, file paths, and tool names unchanged.\n";
    }

    private string Ui(string chinese, string english) =>
        UiText.IsEnglish(_settings.UiLanguage)
            ? english
            : chinese;

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
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "AI.Skill",
                    "Failed to read skill guidance from " + root + ".",
                    ex);
            }
        }

        return
            "Use only the implemented .ivmodel DSL for native Inventor Part modeling.\n" +
            "Sketch: point line circle arc ellipse rect centerrect slot polygon spline constraint dim.\n" +
            "Features: extrude revolve sweep loft hole fillet chamfer shell pattern_rect pattern_circular mirror.\n" +
            "Before selective fillet/chamfer or indexed face operations, query geometry with small limits and use current revision-local indexes.\n" +
            "Build validates internally and returns inspection; modify also returns inspection, so avoid duplicate inspect calls.\n" +
            "Edits: set, suppress, unsuppress, delete.\n" +
            "Use one final four-view render after deterministic state is valid.";
    }

    private IReadOnlyList<string> ExtractRenderPaths(
        string toolResult)
    {
        var result = new List<string>();

        try
        {
            Dictionary<string, object> value =
                _json.Deserialize<Dictionary<string, object>>(toolResult);

            if (!value.TryGetValue("images", out object raw) ||
                !(raw is IEnumerable paths))
                return result;

            foreach (object item in paths)
            {
                string path = Convert.ToString(item) ?? string.Empty;
                if (System.IO.File.Exists(path))
                    result.Add(path);
            }
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "AI.Render",
                "Rendered verification paths could not be read.",
                ex);
        }

        return result;
    }

    private object? BuildRenderContent(
        IReadOnlyList<string> renderPaths)
    {
        try
        {
            var parts = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] =
                        "These are the current working Part's front, top, right, and isometric views. " +
                        "Compare silhouette, proportions, hole/pattern placement, cuts, and feature presence against the request. " +
                        "Do not blindly rebuild. Identify the exact mismatch first. Use modify for parameter/state errors; use at most one structurally different replacement build after the initial build. " +
                        "If the remaining mismatch cannot be expressed by the implemented DSL, stop and report the limitation instead of creating another approximate Part."
                }
            };

            foreach (string path in renderPaths)
            {
                if (!System.IO.File.Exists(path))
                    continue;

                parts.Add(new Dictionary<string, object>
                {
                    ["type"] = "image_url",
                    ["image_url"] =
                        new Dictionary<string, object>
                        {
                            ["url"] =
                                "data:image/png;base64," +
                                Convert.ToBase64String(
                                    System.IO.File.ReadAllBytes(path))
                        }
                });
            }

            return parts.Count > 1 ? parts.ToArray() : null;
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "AI.Render",
                "Rendered verification images could not be reinjected into the conversation.",
                ex);
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
            builder.AppendLine("- UI language: " + _settings.UiLanguage);
            builder.AppendLine("- Response language: " + _settings.ResponseLanguage);
            builder.AppendLine("- Effective response language: " + _settings.EffectiveResponseLanguage);
            builder.AppendLine("- Reasoning: " + (_settings.ReasoningEnabled ? "enabled" : "disabled"));
            builder.AppendLine("- Max tool calls: " + _settings.MaxToolCalls);
            builder.AppendLine("- Context window: " + (_settings.ContextWindowTokens > 0 ? _settings.ContextWindowTokens.ToString() : "auto"));
            builder.AppendLine("- Max output tokens: " + (_settings.MaxOutputTokens > 0 ? _settings.MaxOutputTokens.ToString() : "provider-default"));
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
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "AI.History",
                "Conversation history could not be saved.",
                ex);
        }
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
        try
        {
            _client.Dispose();
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "AI.Session",
                "AI HTTP client disposal failed.",
                ex);
        }

        try
        {
            Workspace.ClearTemp();
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "AI.Session",
                "AI workspace temporary cleanup failed.",
                ex);
        }
    }
}
