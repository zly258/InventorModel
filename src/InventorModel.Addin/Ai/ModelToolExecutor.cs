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

    public ModelToolExecutor(
        global::Inventor.Application application,
        AiWorkspace workspace)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public IReadOnlyList<object> Tools => new object[]
    {
        Tool("validate", "Validate complete .imodel DSL without invoking Inventor. Call before build.", Props(
            ("script", "string", "Complete .imodel source text.")), "script"),
        Tool("skill_reference", "Load one InventorModel reference on demand.", Props(
            ("name", "string", "Reference name: dsl, sketches, features, tools, verification, or patterns.")), "name"),
        Tool("status", "Report Autodesk Inventor connection, active Part status, and the current AI workspace.", new Dictionary<string, object>()),
        Tool("build", "Build a new native editable Inventor Part from complete .imodel DSL source. The source is also kept in the current AI workspace.", Props(
            ("script", "string", "Complete .imodel source text.")), "script"),
        Tool("modify", "Apply one small edit to the active Part. Supported commands include: set <parameter> = <value>, suppress <feature>, unsuppress <feature>, delete <feature>.", Props(
            ("command", "string", "One InventorModel edit statement.")), "command"),
        Tool("inspect", "Inspect active Part bounds, parameters, and feature tree after modeling or edits.", new Dictionary<string, object>()),
        Tool("render", "Render front, top, right, and isometric PNG verification views into the current AI workspace.", new Dictionary<string, object>()),
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
                PartDocument document = new ScriptExecutor(_application).Execute(source);
                document.Activate();
                return _json.Serialize(new
                {
                    built = true,
                    script = scriptPath,
                    workspace = _workspace.SessionDirectory,
                    inspection = new ModelInspector().Inspect(document)
                });
            }

            case "modify":
            {
                string command = Need(arguments, "command");
                PartDocument document = ActivePart();
                new ScriptExecutor(_application).Execute(command, document);
                return new ModelInspector().Inspect(document);
            }

            case "inspect":
                return new ModelInspector().Inspect(ActivePart());

            case "render":
            {
                string directory = _workspace.CreateRenderDirectory();
                IReadOnlyList<string> images =
                    new ModelRenderer(_application).RenderFourViews(ActivePart(), directory);
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
        PartDocument? part = _application.ActiveDocument as PartDocument;
        return _json.Serialize(new
        {
            connected = true,
            activeDocument = part?.DisplayName,
            documentType = part == null ? "none" : "part",
            workspace = _workspace.SessionDirectory
        });
    }

    private PartDocument ActivePart() =>
        _application.ActiveDocument as PartDocument ??
        throw new InvalidOperationException("Active document is not an Inventor Part.");

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

        try { return Convert.ToBoolean(raw); }
        catch { return false; }
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
