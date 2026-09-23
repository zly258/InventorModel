using System;
using System.Collections.Generic;
using System.IO;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using System.Web.Script.Serialization;
using Inventor;
using InventorModel.Core.Ai;
using InventorModel.Core.Dsl;
using InventorModel.Inventor;

namespace InventorModel.Addin;

internal sealed class ModelToolExecutor
{
    private readonly global::Inventor.Application _application;
    private readonly AiWorkspace _workspace;
    private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    private PartDocument? _workingDocument;
    private int _buildGeneration;

    public ModelToolExecutor(
        global::Inventor.Application application,
        AiWorkspace workspace)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public IReadOnlyList<object> Tools => new object[]
    {
        Tool("validate", "Optional dry-run validation for complete .ivmodel DSL without invoking Inventor. build performs the same validation internally.", Props(
            ("script", "string", "Complete .ivmodel source text.")), "script"),
        Tool("skill_reference", "Load one InventorModel reference on demand.", Props(
            ("name", "string", "Reference name: dsl, sketches, features, tools, verification, or patterns.")), "name"),
        Tool("status", "Report Autodesk Inventor connection, active Part status, the session working Part, and the current AI workspace.", new Dictionary<string, object>()),
        Tool("build", "Validate and build complete .ivmodel source in the session working Part. The result already includes deterministic inspection; do not immediately call inspect again.", Props(
            ("script", "string", "Complete .ivmodel source text.")), "script"),
        Tool("modify", "Apply one small edit to the active Part. Supported commands include: set <parameter> = <value>, suppress <feature>, unsuppress <feature>, delete <feature>.", Props(
            ("command", "string", "One InventorModel edit statement.")), "command"),
        Tool("inspect", "Inspect the session working Part: body/sketch/feature counts, bounds, parameters, sketch constraint status, and feature health.", new Dictionary<string, object>()),
        Tool("geometry", "Query bounded first-body edge/face topology for the current model revision. Keep limits small unless more topology is actually required.", Props(
            ("maxEdges", "integer", "Optional edge limit, 1-256. Default 64."),
            ("maxFaces", "integer", "Optional face limit, 1-128. Default 32."))),
        Tool("render", "Render front, top, right, and isometric PNG verification views into the current AI workspace. Default size is 640 pixels.", Props(
            ("size", "integer", "Optional square image size, 320-1200. Default 640."))),
        Tool("save", "Save the active Part as a native editable IPT. Omit path to save inside the current AI workspace; only use an external path when the user explicitly requested one.", Props(
            ("path", "string", "Optional final .ipt path. Omit to use the AI workspace output directory."),
            ("overwrite", "boolean", "Whether an existing file may be overwritten.")))
    };

    public string Execute(string name, string argumentsJson)
    {
        Dictionary<string, object> arguments = ParseArguments(argumentsJson);

        switch (name ?? string.Empty)
        {
            case "validate":
            {
                ValidationResult validation = new ModelValidator().Validate(Need(arguments, "script"));
                return _json.Serialize(new
                {
                    valid = validation.IsValid,
                    errors = validation.Errors,
                    workspace = _workspace.SessionDirectory
                });
            }

            case "skill_reference":
                return ReadSkillReference(Need(arguments, "name"));

            case "status":
                return SerializeStatus();

            case "build":
            {
                string source = Need(arguments, "script");
                string scriptPath = _workspace.SaveModelScript(source);

                bool reuseWorkingDocument =
                    TryGetWorkingDocument(out PartDocument? working);

                var executor = new ScriptExecutor(_application);
                PartDocument document =
                    reuseWorkingDocument && working != null
                        ? executor.Execute(source, working, replaceExisting: true)
                        : executor.Execute(source);

                _workingDocument = document;
                _buildGeneration++;
                document.Activate();

                return _json.Serialize(new
                {
                    built = true,
                    reusedDocument = reuseWorkingDocument,
                    buildGeneration = _buildGeneration,
                    workingDocument = document.DisplayName,
                    script = scriptPath,
                    workspace = _workspace.SessionDirectory,
                    inspection = new ModelInspector().InspectResult(document)
                });
            }

            case "modify":
            {
                string command = Need(arguments, "command");
                PartDocument document = ActivePart();
                new ScriptExecutor(_application).Execute(command, document);
                return _json.Serialize(
                    new ModelInspector().InspectResult(document));
            }

            case "inspect":
                return _json.Serialize(
                    new ModelInspector().InspectResult(ActivePart()));

            case "geometry":
            {
                int maxEdges =
                    ReadInteger(arguments, "maxEdges", 64, 1, 256);
                int maxFaces =
                    ReadInteger(arguments, "maxFaces", 32, 1, 128);

                return _json.Serialize(
                    new ModelInspector().InspectGeometry(
                        ActivePart(),
                        maxEdges,
                        maxFaces));
            }

            case "render":
            {
                int size =
                    ReadInteger(arguments, "size", 640, 320, 1200);
                string directory = _workspace.CreateRenderDirectory();
                IReadOnlyList<string> images =
                    new ModelRenderer(_application).RenderFourViews(
                        ActivePart(),
                        directory,
                        size,
                        size);
                return _json.Serialize(new
                {
                    directory,
                    images,
                    workspace = _workspace.SessionDirectory
                });
            }

            case "save":
            {
                string requested = Optional(arguments, "path");
                string path = string.IsNullOrWhiteSpace(requested)
                    ? _workspace.GetDefaultOutputPath()
                    : IOPath.GetFullPath(requested);

                bool overwrite = ReadBoolean(arguments, "overwrite");
                if (IOFile.Exists(path) && !overwrite)
                    throw new IOException("File already exists: " + path);

                string? parent = IOPath.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);

                ActivePart().SaveAs(path, false);
                return _json.Serialize(new
                {
                    saved = true,
                    path,
                    workspace = _workspace.SessionDirectory
                });
            }

            default:
                throw new InvalidOperationException("Unknown InventorModel tool: " + name);
        }
    }

    private string SerializeStatus()
    {
        PartDocument? active =
            _application.ActiveDocument as PartDocument;

        TryGetWorkingDocument(out PartDocument? working);

        return _json.Serialize(new
        {
            connected = true,
            activeDocument = active?.DisplayName,
            workingDocument = working?.DisplayName,
            buildGeneration = _buildGeneration,
            documentType = active == null ? "none" : "part",
            workspace = _workspace.SessionDirectory
        });
    }

    private PartDocument ActivePart()
    {
        if (TryGetWorkingDocument(out PartDocument? working) &&
            working != null)
        {
            return working;
        }

        PartDocument? active =
            _application.ActiveDocument as PartDocument;

        if (active != null)
        {
            _workingDocument = active;
            return active;
        }

        throw new InvalidOperationException(
            "No active or session working Inventor Part is available.");
    }

    private bool TryGetWorkingDocument(out PartDocument? document)
    {
        document = _workingDocument;

        if (document == null)
            return false;

        try
        {
            _ = document.DisplayName;
            _ = document.DocumentType;
            return true;
        }
        catch
        {
            _workingDocument = null;
            document = null;
            return false;
        }
    }

    private Dictionary<string, object> ParseArguments(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, object>();

        return _json.Deserialize<Dictionary<string, object>>(json) ??
               new Dictionary<string, object>();
    }

    private static string Need(Dictionary<string, object> values, string key)
    {
        string value = Optional(values, key);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(key + " is required.");
        return value;
    }

    private static string Optional(Dictionary<string, object> values, string key)
    {
        if (values != null &&
            values.TryGetValue(key, out object raw) &&
            raw != null)
            return Convert.ToString(raw) ?? string.Empty;

        return string.Empty;
    }

    private static bool ReadBoolean(Dictionary<string, object> values, string key)
    {
        if (values == null ||
            !values.TryGetValue(key, out object raw) ||
            raw == null)
            return false;

        try
        {
            return Convert.ToBoolean(raw);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                key + " must be a boolean value.",
                ex);
        }
    }

    private static int ReadInteger(
        Dictionary<string, object> values,
        string key,
        int defaultValue,
        int min,
        int max)
    {
        if (values == null ||
            !values.TryGetValue(key, out object raw) ||
            raw == null)
        {
            return defaultValue;
        }

        try
        {
            int value = Convert.ToInt32(raw);
            if (value < min || value > max)
            {
                throw new InvalidOperationException(
                    $"{key} must be between {min} and {max}.");
            }

            return value;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                key + " must be an integer value.",
                ex);
        }
    }

    private static object Tool(
        string name,
        string description,
        Dictionary<string, object> properties,
        params string[] required) =>
        new Dictionary<string, object>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object>
            {
                ["name"] = name,
                ["description"] = description,
                ["parameters"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = required,
                    ["additionalProperties"] = false
                }
            }
        };

    private static Dictionary<string, object> Props(
        params (string Name, string Type, string Description)[] items)
    {
        var result = new Dictionary<string, object>();

        foreach (var item in items)
        {
            result[item.Name] = new Dictionary<string, object>
            {
                ["type"] = item.Type,
                ["description"] = item.Description
            };
        }

        return result;
    }

    private static string ReadSkillReference(string name)
    {
        string safeName = IOPath.GetFileNameWithoutExtension(name);
        string[] roots =
        {
            IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "Skills", "inventor-model"),
            IOPath.GetFullPath(IOPath.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "Skills",
                "inventor-model"))
        };

        foreach (string root in roots)
        {
            string path = IOPath.Combine(root, "references", safeName + ".md");
            if (IOFile.Exists(path))
                return IOFile.ReadAllText(path);
        }

        throw new FileNotFoundException("Unknown skill reference: " + safeName);
    }
}
