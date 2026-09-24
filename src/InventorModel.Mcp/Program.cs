using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using Inventor;
using InventorModel.Core.Workspace;
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

    private static readonly ModelWorkspace Workspace = ModelWorkspace.CreateSession("mcp");
    private static InventorSession? _session;
    private static WorkingDocumentManager? _documents;
    private static readonly WorkingModelState ModelState = new();

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
                object structuredError = CreateStructuredError(name, ex, arguments);
                return Result(id, new JObject
                {
                    ["content"] = TextContent(JsonConvert.SerializeObject(structuredError)),
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
                return TextContent(Status());

            case "start_inventor":
            {
                global::Inventor.Application application =
                    Session.Application;
                application.Visible = true;

                return TextContent(
                    JsonConvert.SerializeObject(new
                    {
                        started = true,
                        visible = application.Visible,
                        version = application.SoftwareVersion.DisplayVersion
                    }));
            }

            case "new_part":
            {
                global::Inventor.Application application =
                    Session.Application;
                application.Visible = true;

                PartDocument document =
                    Documents.CreateNewWorkingPart();
                ModelState.ResetForDocument(document);
                document.Activate();

                return TextContent(
                    JsonConvert.SerializeObject(new
                    {
                        created = true,
                        workingDocument = document.DisplayName,
                        workingDocumentOwned = true,
                        revision = ModelState.Revision,
                        workspace = Workspace.SessionDirectory
                    }));
            }

            case "build":
            {
                string source =
                    arguments.Value<string>("script") ??
                    string.Empty;
                if (string.IsNullOrWhiteSpace(source))
                {
                    string path =
                        IOPath.GetFullPath(
                            Need(arguments, "path"));
                    source =
                        IOFile.ReadAllText(path);
                }

                ModelScript parsedModel =
                    new DslParser().Parse(source);
                ValidationResult validation =
                    new ModelValidator().Validate(parsedModel);
                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(
                        "DSL validation failed: " +
                        string.Join(
                            "; ",
                            validation.Errors));
                }

                string sourceHash =
                    ComputeSourceHash(source);

                if (Documents.TryGetWorkingDocument(
                        out PartDocument? existingWorking) &&
                    existingWorking != null &&
                    ModelState.MatchesSource(sourceHash) &&
                    ModelState.LastInspection != null)
                {
                    return TextContent(
                        JsonConvert.SerializeObject(new
                        {
                            built = false,
                            unchanged = true,
                            strategy = "no_op",
                            revision = ModelState.Revision,
                            reusedDocument = true,
                            buildGeneration =
                                ModelState.BuildGeneration,
                            workingDocument =
                                existingWorking.DisplayName,
                            workspace =
                                Workspace.SessionDirectory,
                            inspection =
                                ModelState.LastInspection
                        }));
                }

                string scriptPath =
                    Workspace.SaveModelScript(source);
                global::Inventor.Application application =
                    Session.Application;

                bool reuseWorkingDocument =
                    Documents.TryGetWorkingDocument(
                        out PartDocument? working);

                PartDocument document =
                    working ??
                    Documents.AcquireForBuild();

                ModelState.AttachDocument(document);

                ModelDiffResult? diff = null;
                if (reuseWorkingDocument &&
                    ModelState.ParsedModel != null)
                {
                    diff =
                        new ModelDiffer().Compare(
                            ModelState.ParsedModel,
                            parsedModel);

                    if (diff.IsUnchanged &&
                        ModelState.LastInspection != null)
                    {
                        ModelState.MarkSourceSynchronized(
                            source,
                            sourceHash,
                            parsedModel);

                        return TextContent(
                            JsonConvert.SerializeObject(new
                            {
                                built = false,
                                unchanged = true,
                                semanticUnchanged = true,
                                strategy = "no_op",
                                revision =
                                    ModelState.Revision,
                                reusedDocument = true,
                                buildGeneration =
                                    ModelState.BuildGeneration,
                                workingDocument =
                                    document.DisplayName,
                                script = scriptPath,
                                workspace =
                                    Workspace.SessionDirectory,
                                inspection =
                                    ModelState.LastInspection
                            }));
                    }
                }

                string strategy;
                IReadOnlyList<string> changedParameters;

                if (diff != null &&
                    diff.IsParameterOnly)
                {
                    new ParameterUpdater(application).Apply(
                        document,
                        parsedModel,
                        diff.ParameterChanges);

                    strategy = "parameter_update";
                    changedParameters =
                        diff.ParameterChanges
                            .Select(x => x.Name)
                            .ToArray();
                }
                else
                {
                    new ScriptExecutor(application).Execute(
                        source,
                        document,
                        replaceExisting: true);

                    strategy =
                        reuseWorkingDocument
                            ? "full_rebuild_same_document"
                            : "initial_build";
                    changedParameters =
                        Array.Empty<string>();
                }

                document.Activate();

                var inspector =
                    new ModelInspector();
                var summary =
                    inspector.InspectSummary(document);

                ModelState.MarkBuildSuccess(
                    document,
                    source,
                    sourceHash,
                    parsedModel,
                    summary,
                    strategy);

                return TextContent(
                    JsonConvert.SerializeObject(new
                    {
                        built = true,
                        unchanged = false,
                        strategy,
                        changedParameters,
                        revision = ModelState.Revision,
                        reusedDocument =
                            reuseWorkingDocument,
                        recreatedDocument = false,
                        buildGeneration =
                            ModelState.BuildGeneration,
                        workingDocument =
                            document.DisplayName,
                        script = scriptPath,
                        workspace =
                            Workspace.SessionDirectory,
                        inspection = summary
                    }));
            }

            case "modify":
            {
                global::Inventor.Application application = Session.Application;
                PartDocument document = ActivePart(application);
                string command = Need(arguments, "command");
                new ScriptExecutor(application).Execute(command, document);

                var inspector = new ModelInspector();
                var summary = inspector.InspectSummary(document);
                ModelState.MarkModify(
                    document,
                    summary);

                return TextContent(
                    JsonConvert.SerializeObject(new
                    {
                        modified = true,
                        revision = ModelState.Revision,
                        workingDocument = document.DisplayName,
                        inspection = summary
                    }));
            }

            case "inspect":
            {
                string detail = arguments.Value<string>("detail") ?? "summary";
                return TextContent(
                    JsonConvert.SerializeObject(
                        new ModelInspector().InspectDetailed(
                            ActivePart(Session.Application),
                            detail)));
            }

            case "geometry":
            {
                var filter = new ModelGeometryFilter
                {
                    Entity = arguments.Value<string>("entity") ?? "all",
                    CurveType = arguments.Value<string>("curveType"),
                    SurfaceType = arguments.Value<string>("surfaceType"),
                    Axis = arguments.Value<string>("axis"),
                    NearX = arguments.Value<double?>("nearX"),
                    NearY = arguments.Value<double?>("nearY"),
                    NearZ = arguments.Value<double?>("nearZ"),
                    ToleranceMm = arguments.Value<double?>("tolerance") ?? 1.0,
                    MinLengthMm = arguments.Value<double?>("minLength"),
                    MaxLengthMm = arguments.Value<double?>("maxLength"),
                    RadiusMm = arguments.Value<double?>("radius"),
                    MaxEdges = ReadInteger(arguments, "maxEdges", 64, 1, 256),
                    MaxFaces = ReadInteger(arguments, "maxFaces", 32, 1, 128)
                };

                return TextContent(
                    JsonConvert.SerializeObject(
                        new ModelInspector().InspectGeometry(
                            ActivePart(Session.Application),
                            filter)));
            }

            case "render":
            {
                int size =
                    ReadInteger(arguments, "size", 640, 320, 1200);
                string requested = arguments.Value<string>("directory") ?? string.Empty;
                string directory = string.IsNullOrWhiteSpace(requested)
                    ? Workspace.CreateRenderDirectory()
                    : IOPath.GetFullPath(requested);

                List<string>? requestedViews = null;
                if (arguments.TryGetValue("views", out JToken? viewsToken))
                {
                    if (viewsToken is JArray viewsArr)
                    {
                        requestedViews = viewsArr.ToObject<List<string>>();
                    }
                    else if (viewsToken.Type == JTokenType.String)
                    {
                        string str = viewsToken.Value<string>() ?? string.Empty;
                        requestedViews = new List<string>(
                            str.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
                    }
                }

                global::Inventor.Application application = Session.Application;
                IReadOnlyList<string> files = new ModelRenderer(application)
                    .RenderViews(
                        ActivePart(application),
                        directory,
                        requestedViews,
                        size,
                        size);

                var content = TextContent(JsonConvert.SerializeObject(new
                {
                    directory,
                    views = requestedViews ?? new List<string> { "front", "top", "right", "iso" },
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
                "Optional dry-run validation for .ivmodel syntax and semantics without starting Inventor. build validates internally.",
                Props(
                    ("script", "string", "Complete .ivmodel source text"),
                    ("path", "string", "Path used when script is omitted"))),
            Tool(
                "status",
                "Pure status query. Report whether Inventor is running plus active/working Part state without starting Inventor.",
                new JObject()),
            Tool(
                "start_inventor",
                "Start Autodesk Inventor when needed, or attach to the running instance, and make it visible. Does not create a document.",
                new JObject()),
            Tool(
                "new_part",
                "Explicitly create and activate a new session working Part. Starts Inventor visibly when needed. Do not call before every build; build automatically reuses the current working Part.",
                new JObject()),
            Tool(
                "build",
                "Validate and build complete .ivmodel source in one session working Part. Identical builds are suppressed on the server. Returns deterministic summary inspection.",
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
                "Inspect the working Part as structured JSON. Defaults to compact summary. Use 'detail' for parameters, sketches, features, or all.",
                Props(
                    ("detail", "string", "Inspection detail level: 'summary' (default), 'parameters', 'sketches', 'features', or 'all'"))),
            Tool(
                "geometry",
                "Query bounded revision-local edge/face topology with optional deterministic geometric filters (curveType, surfaceType, axis, nearZ, radius, etc.).",
                Props(
                    ("entity", "string", "Entity type: 'all' (default), 'edge', or 'face'"),
                    ("curveType", "string", "Filter edges by curve type: 'circle', 'line', 'circulararc', etc."),
                    ("surfaceType", "string", "Filter faces by surface type: 'plane', 'cylinder', etc."),
                    ("axis", "string", "Filter entities along or normal to global axis: 'X', 'Y', or 'Z'"),
                    ("nearX", "number", "Filter entities near X coordinate in mm"),
                    ("nearY", "number", "Filter entities near Y coordinate in mm"),
                    ("nearZ", "number", "Filter entities near Z coordinate in mm"),
                    ("tolerance", "number", "Coordinate tolerance in mm. Default 1.0"),
                    ("radius", "number", "Filter circular or arc edges by radius in mm"),
                    ("minLength", "number", "Minimum edge length in mm"),
                    ("maxLength", "number", "Maximum edge length in mm"),
                    ("maxEdges", "integer", "Optional edge limit, 1-256. Default 64."),
                    ("maxFaces", "integer", "Optional face limit, 1-128. Default 32."))),
            Tool(
                "render",
                "Render PNG verification views. Defaults to four views ('front', 'top', 'right', 'iso'). Intermediate checks can specify a subset like 'front,iso'.",
                Props(
                    ("views", "string", "Comma-separated view names or array: 'front', 'top', 'right', 'iso'"),
                    ("directory", "string", "Optional output directory; omit for the MCP workspace"),
                    ("size", "integer", "Optional square image size, 320-1200. Default 640."))),
            Tool(
                "save",
                "Save active Part as native IPT. Omit path to save inside the MCP workspace output directory.",
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

    private static InventorSession Session
    {
        get
        {
            if (_session != null)
            {
                try
                {
                    _ = _session.Application.Visible;
                    return _session;
                }
                catch
                {
                    _session = null;
                    _documents = null;
                }
            }

            _session = InventorSession.Connect();
            return _session;
        }
    }

    private static WorkingDocumentManager Documents =>
        _documents ??=
            new WorkingDocumentManager(
                Session.Application);

    private static PartDocument ActivePart(
        global::Inventor.Application application)
    {
        PartDocument document =
            Documents.GetRequiredOrAttachActive();

        ModelState.AttachDocument(document);
        return document;
    }

    private static string Status()
    {
        if (!InventorSession.TryConnect(
                out InventorSession? running) ||
            running == null)
        {
            return JsonConvert.SerializeObject(new
            {
                connected = false,
                activeDocument = (string?)null,
                workingDocument = (string?)null,
                workingDocumentOwned = false,
                buildGeneration = ModelState.BuildGeneration,
                revision = ModelState.Revision,
                lastBuildMode = ModelState.LastBuildMode,
                bindings = new
                {
                    parameters = ModelState.Bindings.Parameters.Count,
                    sketches = ModelState.Bindings.Sketches.Count,
                    features = ModelState.Bindings.Features.Count
                },
                workspace = Workspace.SessionDirectory
            }) ?? string.Empty;
        }

        global::Inventor.Application application =
            running.Application;
        PartDocument? active =
            application.ActiveDocument as PartDocument;

        PartDocument? working = null;
        bool owned = false;
        if (_documents != null)
        {
            _documents.TryGetWorkingDocument(
                out working);
            owned =
                _documents.OwnsWorkingDocument;
        }

        return JsonConvert.SerializeObject(new
        {
            connected = true,
            visible = application.Visible,
            version = application.SoftwareVersion.DisplayVersion,
            activeDocument = active?.DisplayName,
            workingDocument = working?.DisplayName,
            workingDocumentOwned = owned,
            buildGeneration = ModelState.BuildGeneration,
            revision = ModelState.Revision,
            lastBuildMode = ModelState.LastBuildMode,
            bindings = new
            {
                parameters = ModelState.Bindings.Parameters.Count,
                sketches = ModelState.Bindings.Sketches.Count,
                features = ModelState.Bindings.Features.Count
            },
            documentType = active == null
                ? "none"
                : "part",
            workspace = Workspace.SessionDirectory
        }) ?? string.Empty;
    }

    private static string ComputeSourceHash(string source)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        string normalized = source.Replace("\r\n", "\n").Trim();
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(normalized);
        byte[] hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static object CreateStructuredError(string toolName, Exception ex, JObject arguments)
    {
        string message = ex.Message ?? string.Empty;
        string errorCode = "unknown_error";
        string stage = "unknown";
        bool retryable = false;
        string recommendedAction = "check_error_details";
        string? feature = null;

        if (message.Contains("DSL validation failed"))
        {
            errorCode = "dsl_validation";
            stage = "dsl_validation";
            retryable = true;
            recommendedAction = "fix_dsl_validation_errors";
        }
        else if (message.Contains("syntax error") ||
                 message.Contains("Unknown sketch plane") ||
                 message.Contains("Unknown revolve axis") ||
                 message.Contains("Unknown face side") ||
                 message.Contains("Unknown axis"))
        {
            errorCode = "dsl_parse";
            stage = "dsl_parse";
            retryable = true;
            recommendedAction = "check_dsl_grammar_and_arguments";
        }
        else if (message.Contains("Edge index") ||
                 message.Contains("Face index") ||
                 message.Contains("No planar") ||
                 message.Contains("selector"))
        {
            errorCode = "selector_not_found";
            stage = "inventor_feature";
            retryable = true;
            recommendedAction = "query_geometry_with_filters_before_selecting";
        }
        else if (message.Contains("No solid body") ||
                 message.Contains("No active or session working"))
        {
            errorCode = "document_invalid";
            stage = "inventor_session";
            retryable = true;
            recommendedAction = "check_status_or_rebuild_base_solid";
        }
        else if (message.Contains("already exists") || ex is IOException)
        {
            errorCode = "save_failed";
            stage = "file_io";
            retryable = true;
            recommendedAction = "use_overwrite_or_new_path";
        }
        else if (ex is System.Runtime.InteropServices.COMException || message.Contains("COM"))
        {
            errorCode = "inventor_com";
            stage = "inventor_api";
            retryable = false;
            recommendedAction = "check_inventor_status_or_restart";
        }
        else if (toolName == "build" || toolName == "modify")
        {
            errorCode = "feature_failed";
            stage = "inventor_feature";
            retryable = true;
            recommendedAction = "check_feature_definition_and_profiles";
        }

        return new
        {
            success = false,
            errorCode,
            stage,
            feature,
            retryable,
            recommendedAction,
            detail = message
        };
    }

    private static int ReadInteger(
        JObject value,
        string key,
        int defaultValue,
        int min,
        int max)
    {
        int result =
            value.Value<int?>(key) ??
            defaultValue;

        if (result < min || result > max)
        {
            throw new InvalidOperationException(
                $"{key} must be between {min} and {max}.");
        }

        return result;
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
