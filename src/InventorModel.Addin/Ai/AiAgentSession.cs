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
    private readonly List<AgentMessage> _messages = new List<AgentMessage>();
    private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

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
        Action onStreamReset,
        Action<string> onContentDelta,
        Action<string> onActivity,
        CancellationToken cancellationToken)
    {
        object userContent = BuildUserContent(text, imagePath);
        _messages.Add(new AgentMessage { Role = "user", Content = userContent });
        SaveHistory();

        for (int round = 1; round <= MaxToolRounds; round++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onStreamReset?.Invoke();

            AgentCompletion completion = await _client.CompleteStreamingAsync(
                _messages,
                _toolExecutor.Tools,
                new StreamingCallbacks
                {
                    OnContentDelta = onContentDelta,
                    OnStreamReset = onStreamReset,
                    OnRetry = (attempt, message) =>
                        onActivity?.Invoke("Retrying AI request (" + attempt + "): " + Compact(message))
                },
                cancellationToken).ConfigureAwait(false);

            _messages.Add(new AgentMessage
            {
                Role = "assistant",
                Content = completion.Content ?? string.Empty,
                ToolCalls = completion.RawToolCalls
            });
            SaveHistory();

            if (completion.ToolCalls.Count == 0)
                return completion.Content ?? string.Empty;

            foreach (AgentToolCall call in completion.ToolCalls)
            {
                cancellationToken.ThrowIfCancellationRequested();
                onActivity?.Invoke("Running " + call.Name + "…");

                string toolResult;
                try
                {
                    toolResult = _dispatcher.Invoke(
                        () => _toolExecutor.Execute(call.Name, call.ArgumentsJson));
                }
                catch (Exception ex)
                {
                    toolResult = _json.Serialize(new
                    {
                        ok = false,
                        tool = call.Name,
                        error = ex.Message
                    });
                }

                _messages.Add(new AgentMessage
                {
                    Role = "tool",
                    ToolCallId = call.Id,
                    Name = call.Name,
                    Content = toolResult
                });

                if (string.Equals(call.Name, "render", StringComparison.OrdinalIgnoreCase))
                {
                    object? visual = BuildRenderContent(toolResult);
                    if (visual != null)
                        _messages.Add(new AgentMessage { Role = "user", Content = visual });
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

        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Attached image was not found.", imagePath);

        string extension = Path.GetExtension(imagePath).ToLowerInvariant();
        string mime = extension == ".jpg" || extension == ".jpeg"
            ? "image/jpeg"
            : extension == ".webp"
                ? "image/webp"
                : "image/png";

        string dataUrl = "data:" + mime + ";base64," +
                         Convert.ToBase64String(File.ReadAllBytes(imagePath));

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
                ["image_url"] = new Dictionary<string, object> { ["url"] = dataUrl }
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
            "All internal AI artifacts belong in the current InventorModel AI workspace: " + Workspace.SessionDirectory + ". " +
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
                if (!File.Exists(skillPath))
                    continue;

                return File.ReadAllText(skillPath);
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
            Dictionary<string, object> value = _json.Deserialize<Dictionary<string, object>>(toolResult);
            if (!value.TryGetValue("images", out object raw) || !(raw is IEnumerable paths))
                return null;

            var parts = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = "Visually inspect these newly rendered front, top, right, and isometric views before claiming success."
                }
            };

            foreach (object item in paths)
            {
                string path = Convert.ToString(item) ?? string.Empty;
                if (!File.Exists(path))
                    continue;

                parts.Add(new Dictionary<string, object>
                {
                    ["type"] = "image_url",
                    ["image_url"] = new Dictionary<string, object>
                    {
                        ["url"] = "data:image/png;base64," +
                                  Convert.ToBase64String(File.ReadAllBytes(path))
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

    private void SaveHistory()
    {
        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("# InventorModel AI History").AppendLine();
            builder.AppendLine("- Model: " + _settings.Model);
            builder.AppendLine("- Base URL: " + _settings.BaseUrl);
            builder.AppendLine("- Workspace: " + Workspace.SessionDirectory);
            builder.AppendLine("- Updated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).AppendLine();

            int index = 0;
            foreach (AgentMessage message in _messages.Where(x => x.Role != "system"))
            {
                index++;
                builder.AppendLine("## " + index + ". " + message.Role.ToUpperInvariant()).AppendLine();
                builder.AppendLine(HistoryContent(message.Content));

                if (message.ToolCalls != null)
                    builder.AppendLine("Tool calls: " + _json.Serialize(message.ToolCalls));

                builder.AppendLine();
            }

            File.WriteAllText(HistoryPath, builder.ToString(), new UTF8Encoding(true));
        }
        catch { }
    }

    private string HistoryContent(object content)
    {
        if (content == null) return string.Empty;
        if (content is string text) return text;

        if (content is object[] parts)
        {
            var output = new List<string>();
            foreach (object part in parts)
            {
                if (!(part is Dictionary<string, object> item))
                    continue;

                string type = item.TryGetValue("type", out object rawType)
                    ? Convert.ToString(rawType)
                    : string.Empty;

                if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase) &&
                    item.TryGetValue("text", out object rawText))
                    output.Add(Convert.ToString(rawText) ?? string.Empty);
                else if (string.Equals(type, "image_url", StringComparison.OrdinalIgnoreCase))
                    output.Add("[attached image]");
            }

            return string.Join(Environment.NewLine, output);
        }

        return _json.Serialize(content);
    }

    private static string Compact(string value)
    {
        string text = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length <= 180 ? text : text.Substring(0, 180) + "…";
    }

    public void Dispose()
    {
        try { _client.Dispose(); } catch { }
        try { Workspace.ClearTemp(); } catch { }
    }
}
