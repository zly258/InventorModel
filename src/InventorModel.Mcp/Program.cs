using System;
using System.Collections.Generic;
using System.IO;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using Inventor;
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
    }

    private static JObject? Dispatch(JObject request)
    {
        JToken? id = request["id"];
        string method = request.Value<string>("method") ?? string.Empty;
        JObject parameters = request["params"] as JObject ?? new JObject();

        if (method == "initialize")
        {
            string requested = parameters.Value<string>("protocolVersion") ?? "2025-11-25";
            string negotiated = HandshakeProtocolVersions.Contains(requested) ? requested : "2025-11-25";
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

        if (id == null && method.StartsWith("notifications/", StringComparison.Ordinal))
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
                return TextContent(JsonConvert.SerializeObject(new { valid = validation.IsValid, errors = validation.Errors }));
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

                global::Inventor.Application application = Session.Application;
                PartDocument document =
                    new ScriptExecutor(application).Execute(source);
                document.Activate();
                return TextContent(new ModelInspector().Inspect(document));
            }

            case "modify":
            {
                global::Inventor.Application application = Session.Application;
                PartDocument document = ActivePart(application);
                string command = Need(arguments, "command");
                new ScriptExecutor(application).Execute(command, document);
                return TextContent(new ModelInspector().Inspect(document));
            }

            case "inspect":
                return TextContent(new ModelInspector().Inspect(ActivePart(Session.Application)));

            case "render":
            {
                string directory =
                    IOPath.GetFullPath(Need(arguments, "directory"));
                global::Inventor.Application application = Session.Application;
                IReadOnlyList<string> files = new ModelRenderer(application)
                    .RenderFourViews(ActivePart(application), directory);
                var content = TextContent(JsonConvert.SerializeObject(new { directory, images = files }));
                foreach (string file in files)
                    content.Add(new JObject
                    {
                        ["type"] = "image",
                        ["data"] = Convert.ToBase64String(IOFile.ReadAllBytes(file)),
                        ["mimeType"] = "image/png"
                    });
                return content;
            }

            case "save":
            {
                string path = IOPath.GetFullPath(Need(arguments, "path"));
                bool overwrite =
                    arguments.Value<bool?>("overwrite") ?? false;

                if (IOFile.Exists(path) && !overwrite)
                    throw new IOException("File already exists: " + path);

                ActivePart(Session.Application).SaveAs(path, false);
                return TextContent(JsonConvert.SerializeObject(new
                {
                    saved = true,
                    path
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
                "Validate .imodel syntax and semantics without starting Inventor",
                Props(("script", "string", "Complete .imodel source text"), ("path", "string", "Path used when script is omitted"))),
            Tool(
                "status",
                "Report Inventor and active Part status",
                new JObject()),
            Tool(
                "build",
                "Build a native editable Part from complete .imodel source or from an .imodel file path",
                Props(
                    ("script", "string", "Complete .imodel source text"),
                    ("path", "string", "Path to an .imodel script when script is omitted"))),
            Tool(
                "modify",
                "Apply a small conversational edit to the active Part. Examples: 'set width = 120', 'suppress fillet1', 'unsuppress fillet1', 'delete hole1'.",
                Props(("command", "string", "One InventorModel edit statement")), "command"),
            Tool(
                "inspect",
                "Inspect active Part size, parameters and feature tree",
                new JObject()),
            Tool(
                "render",
                "Render front/top/right/isometric PNG views",
                Props(("directory", "string", "Output directory")), "directory"),
            Tool(
                "save",
                "Save active Part as native IPT",
                Props(
                    ("path", "string", "Output .ipt path"),
                    ("overwrite", "boolean", "Allow overwrite")), "path"));
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
            documentType = part == null ? "none" : "part"
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
