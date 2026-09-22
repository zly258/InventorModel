using System;
using System.Collections.Generic;
using System.IO;
using IOPath = System.IO.Path;
using Inventor;
using InventorModel.Inventor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InventorModel.Mcp;

internal static class Program
{
    private static InventorSession _session;

    private static void Main()
    {
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);

        string line;
        while ((line = Console.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            JObject request = null;
            try
            {
                request = JObject.Parse(line);
                JObject response = Dispatch(request);
                Console.WriteLine(response.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    Error(request?["id"], -32603, ex.Message).ToString(Formatting.None));
            }
        }
    }

    private static JObject Dispatch(JObject request)
    {
        JToken id = request["id"];
        string method = request.Value<string>("method") ?? "";
        JObject parameters = request["params"] as JObject ?? new JObject();

        if (method == "initialize")
            return Result(id, new JObject
            {
                ["protocolVersion"] = "2025-06-18",
                ["capabilities"] = new JObject { ["tools"] = new JObject() },
                ["serverInfo"] = new JObject
                {
                    ["name"] = "InventorModel",
                    ["version"] = "0.1.0"
                }
            });

        if (method == "notifications/initialized")
            return new JObject();

        if (method == "ping")
            return Result(id, new JObject());

        if (method == "tools/list")
            return Result(id, new JObject { ["tools"] = Tools() });

        if (method == "tools/call")
        {
            string name = parameters.Value<string>("name") ?? "";
            JObject arguments = parameters["arguments"] as JObject ?? new JObject();

            return Result(id, new JObject
            {
                ["content"] = new JArray(new JObject
                {
                    ["type"] = "text",
                    ["text"] = Call(name, arguments)
                }),
                ["isError"] = false
            });
        }

        return Error(id, -32601, "Method not found: " + method);
    }

    private static string Call(string name, JObject arguments)
    {
        EnsureSession();
        Application application = _session.Application;

        switch (name)
        {
            case "status":
                return Status(application);

            case "build":
            {
                string source = arguments.Value<string>("script");
                if (string.IsNullOrWhiteSpace(source))
                {
                    string path = IOPath.GetFullPath(Need(arguments, "path"));
                    source = File.ReadAllText(path);
                }

                PartDocument document = new ScriptExecutor(application).Execute(source);
                document.Activate();
                return new ModelInspector().Inspect(document);
            }

            case "modify":
            {
                PartDocument document = ActivePart(application);
                string command = Need(arguments, "command");
                new ScriptExecutor(application).Execute(command, document);
                return new ModelInspector().Inspect(document);
            }

            case "inspect":
                return new ModelInspector().Inspect(ActivePart(application));

            case "render":
            {
                string directory = IOPath.GetFullPath(Need(arguments, "directory"));
                var files = new ModelRenderer(application)
                    .RenderFourViews(ActivePart(application), directory);
                return JsonConvert.SerializeObject(new { directory, images = files });
            }

            case "save":
            {
                string path = IOPath.GetFullPath(Need(arguments, "path"));
                bool overwrite = arguments.Value<bool?>("overwrite") ?? false;
                if (File.Exists(path) && !overwrite)
                    throw new IOException("File already exists: " + path);

                ActivePart(application).SaveAs(path, false);
                return JsonConvert.SerializeObject(new { saved = true, path });
            }

            default:
                throw new InvalidOperationException("Unknown InventorModel tool: " + name);
        }
    }

    private static JArray Tools()
    {
        return new JArray(
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
                Props(("command", "string", "One InventorModel edit statement"))),
            Tool(
                "inspect",
                "Inspect active Part size, parameters and feature tree",
                new JObject()),
            Tool(
                "render",
                "Render front/top/right/isometric PNG views",
                Props(("directory", "string", "Output directory"))),
            Tool(
                "save",
                "Save active Part as native IPT",
                Props(
                    ("path", "string", "Output .ipt path"),
                    ("overwrite", "boolean", "Allow overwrite")))
        );
    }

    private static JObject Tool(string name, string description, JObject properties) =>
        new JObject
        {
            ["name"] = name,
            ["description"] = description,
            ["inputSchema"] = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["additionalProperties"] = false
            }
        };

    private static JObject Props(
        params (string Name, string Type, string Description)[] items)
    {
        var result = new JObject();
        foreach (var item in items)
            result[item.Name] = new JObject
            {
                ["type"] = item.Type,
                ["description"] = item.Description
            };
        return result;
    }

    private static void EnsureSession()
    {
        if (_session == null) _session = InventorSession.Connect();
    }

    private static PartDocument ActivePart(Application application) =>
        application.ActiveDocument as PartDocument ??
        throw new InvalidOperationException(
            "Active document is not an Inventor Part.");

    private static string Status(Application application)
    {
        var part = application.ActiveDocument as PartDocument;
        return JsonConvert.SerializeObject(new
        {
            connected = true,
            activeDocument = part?.DisplayName,
            documentType = part == null ? "none" : "part"
        });
    }

    private static string Need(JObject value, string key)
    {
        string result = value.Value<string>(key);
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException(key + " is required.");
        return result;
    }

    private static JObject Result(JToken id, JToken value) =>
        new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = value
        };

    private static JObject Error(JToken id, int code, string message) =>
        new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
}
