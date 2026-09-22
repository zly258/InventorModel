using System;
using System.Collections.Generic;
using System.IO;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using Inventor;
using InventorModel.Core.Ai;
using InventorModel.Core.Dsl;
using InventorModel.Inventor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InventorModel.Mcp;

internal static class Program
{
    private static readonly HashSet<string> HandshakeProtocolVersions = new(StringComparer.Ordinal)
    {
        "2024-11-05", "2025-03-26", "2025-06-18", "2025-11-25"
    };

    private static readonly AiWorkspace Workspace = AiWorkspace.CreateSession("mcp");
    private static InventorSession? _session;

    private static void Main()
    {
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);

        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            JObject? request = null;
            try
            {
                request = JObject.Parse(line);
                JObject? response = Dispatch(request);
                if (response != null)
                    Console.WriteLine(response.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    Error(request?["id"], -32603, ex.Message)
                        .ToString(Formatting.None));
            }
        }

        Workspace.ClearTemp();
    }

    private static JObject? Dispatch(JObject request)
    {
        JToken? id = request["id"];
        string method = request.Value<string>("method") ?? string.Empty;
        JObject parameters = request["params"] as JObject ?? new JObject();

        if (method == "initialize")
        {
            string requested = parameters.Value<string>("protocolVersion") ?? "2025-11-25";
            string negotiated = HandshakeProtocolVersions.Contains(requested)
                ? requested
                : "2025-11-25";

            return Result(id, new JObject
            {
                ["protocolVersion"] = negotiated,
                ["capabilities"] = new JObject
                {
                    ["tools"] = new JObject()
                },
                ["serverInfo"] = new JObject
                {
                    ["name"] = "InventorModel",
                    ["version"] = "0.1.0"
                }
            });
        }

        if (id == null &&
            method.StartsWith("notifications/", StringComparison.Ordinal))
            return null;

        if (method == "ping")
            return Result(id, new JObject());

        if (method == "tools/list")
            return Result(id, new JObject { ["tools"] = Tools() });

        if (method == "tools/call")
        {
            string name = parameters.Value<string>("name") ?? string.Empty;
            JObject arguments =
                parameters["arguments"] as JObject ?? new JObject();

            try
            {
                return Result(id, new JObject
                {
                    ["content"] = Call(name, arguments),
                    ["isError"] = false
                });
            }
            catch (Exception ex)
            {
                return Result(id, new JObject
                {
                    ["content"] = TextContent(ex.Message),
                    ["isError"] = true
                });
            }
        }

        return Error(id, -32601, "Method not found: " + method);
    }

    private static JArray Call(string name, JObject arguments)
    {
        switch (name)
        {
            case "validate":
            {
                string source = arguments.Value<string>("script") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(source))
                    source = IOFile.ReadAllText(IOPath.GetFullPath(Need(arguments, "path")));

                ValidationResult validation = new ModelValidator().Validate(source);
                return TextContent(JsonConvert.SerializeObject(new
                {
                    valid = validation.IsValid,
                    errors = validation.Errors,
                    workspace = Workspace.SessionDirectory
                }));
            }

            case "status":
                return TextContent(Status(Session.Application));

            case "build":
            {
                string source = arguments.Value<string>("script") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(source))
                {
                    string path = IOPath.GetFullPath(Need(arguments, "path"));
                    source = IOFile.ReadAllText(path);
                }

                string scriptPath = Workspace.SaveModelScript(source);
                global::Inventor.Application application = Session.Application;
                PartDocument document =
                    new ScriptExecutor(application).Execute(source);
                document.Activate();

                return TextContent(JsonConvert.SerializeObject(new
                {
                    built = true,
                    script = scriptPath,
                    workspace = Workspace.SessionDirectory,
                    inspection = new ModelInspector().InspectResult(document)
                }));
            }

            case "modify":
            {
                global::Inventor.Application application = Session.Application;
                PartDocument document = ActivePart(application);
                string command = Need(arguments, "command");
                new ScriptExecutor(application).Execute(command, document);
                return TextContent(
                    JsonConvert.SerializeObject(
                        new ModelInspector().InspectResult(document)));
            }

            case "inspect":
                return TextContent(
                    JsonConvert.SerializeObject(
                        new ModelInspector().InspectResult(
                            ActivePart(Session.Application))));

            case "render":
            {
                string requested = arguments.Value<string>("directory") ?? string.Empty;
                string directory = string.IsNullOrWhiteSpace(requested)
                    ? Workspace.CreateRenderDirectory()
                    : IOPath.GetFullPath(requested);

                global::Inventor.Application application = Session.Application;
                IReadOnlyList<string> files = new ModelRenderer(application)
                    .RenderFourViews(ActivePart(application), directory);

                var content = TextContent(JsonConvert.SerializeObject(new
                {
                    directory,
                    images = files,
                    workspace = Workspace.SessionDirectory
                }));

                foreach (string file in files)
                {
                    content.Add(new JObject
                    {
                        ["type"] = "image",
                        ["data"] = Convert.ToBase64String(IOFile.ReadAllBytes(file)),
                        ["mimeType"] = "image/png"
                    });
                }

                return content;
            }

            case "save":
            {
                string requested = arguments.Value<string>("path") ?? string.Empty;
                string path = string.IsNullOrWhiteSpace(requested)
                    ? Workspace.GetDefaultOutputPath()
                    : IOPath.GetFullPath(requested);

                bool overwrite =
                    arguments.Value<bool?>("overwrite") ?? false;

                if (IOFile.Exists(path) && !overwrite)
                    throw new IOException("File already exists: " + path);

                string? parent = IOPath.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);

                ActivePart(Session.Application).SaveAs(path, false);
                return TextContent(JsonConvert.SerializeObject(new
                {
                    saved = true,
                    path,
                    workspace = Workspace.SessionDirectory
                }));
            }

            default:
                throw new InvalidOperationException(
                    "Unknown InventorModel tool: " + name);
        }
    }

    private static JArray Tools()
    {
        return new JArray(
            Tool(
                "validate",
                "Validate .ivmodel syntax and semantics without starting Inventor",
                Props(
                    ("script", "string", "Complete .ivmodel source text"),
                    ("path", "string", "Path used when script is omitted"))),
            Tool(
                "status",
                "Report Inventor, active Part status, and current AI workspace",
                new JObject()),
            Tool(
                "build",
                "Build a native editable Part from complete .ivmodel source or file path; the effective source is kept in the AI workspace",
                Props(
                    ("script", "string", "Complete .ivmodel source text"),
                    ("path", "string", "Path to an .ivmodel script when script is omitted"))),
            Tool(
                "modify",
                "Apply a small conversational edit to the active Part. Examples: 'set width = 120', 'suppress fillet1', 'unsuppress fillet1', 'delete hole1'.",
                Props(("command", "string", "One InventorModel edit statement")),
                "command"),
            Tool(
                "inspect",
                "Inspect active Part as structured JSON: body/sketch/feature counts, bounds, parameters, and feature tree",
                new JObject()),
            Tool(
                "render",
                "Render front/top/right/isometric PNG views. Omit directory to use the current AI workspace.",
                Props(("directory", "string", "Optional output directory; omit for the AI workspace"))),
            Tool(
                "save",
                "Save active Part as native IPT. Omit path to save inside the AI workspace output directory.",
                Props(
                    ("path", "string", "Optional output .ipt path"),
                    ("overwrite", "boolean", "Allow overwrite"))));
    }

    private static JObject Tool(
        string name,
        string description,
        JObject properties,
        params string[] required) =>
        new JObject
        {
            ["name"] = name,
            ["description"] = description,
            ["inputSchema"] = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = new JArray(required),
                ["additionalProperties"] = false
            }
        };

    private static JObject Props(
        params (string Name, string Type, string Description)[] items)
    {
        var result = new JObject();

        foreach ((string name, string type, string description) in items)
        {
            result[name] = new JObject
            {
                ["type"] = type,
                ["description"] = description
            };
        }

        return result;
    }

    private static InventorSession Session =>
        _session ??= InventorSession.Connect();

    private static PartDocument ActivePart(
        global::Inventor.Application application) =>
        application.ActiveDocument as PartDocument ??
        throw new InvalidOperationException(
            "Active document is not an Inventor Part.");

    private static string Status(
        global::Inventor.Application application)
    {
        PartDocument? part =
            application.ActiveDocument as PartDocument;

        return JsonConvert.SerializeObject(new
        {
            connected = true,
            activeDocument = part?.DisplayName,
            documentType = part == null ? "none" : "part",
            workspace = Workspace.SessionDirectory
        }) ?? string.Empty;
    }

    private static string Need(JObject value, string key)
    {
        string? result = value.Value<string>(key);
        if (result is null || string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException(key + " is required.");

        return result;
    }

    private static JArray TextContent(string text) =>
        new JArray(new JObject { ["type"] = "text", ["text"] = text });

    private static JObject Result(JToken? id, JToken value) =>
        new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id ?? JValue.CreateNull(),
            ["result"] = value
        };

    private static JObject Error(
        JToken? id,
        int code,
        string message) =>
        new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id ?? JValue.CreateNull(),
            ["error"] = new JObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
}
