using System;
using System.Collections.Generic;
using System.IO;
using IOPath = System.IO.Path;
using System.Web.Script.Serialization;
using Inventor;
using InventorModel.Core.Dsl;
using InventorModel.Inventor;

namespace InventorModel.Addin;

internal sealed class ModelToolExecutor
{
    private readonly global::Inventor.Application _application;
    private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

    public ModelToolExecutor(global::Inventor.Application application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
    }

    public IReadOnlyList<object> Tools => new object[]
    {
        Tool("validate", "Validate complete .imodel DSL without invoking Inventor. Call before build.", Props(
            ("script", "string", "Complete .imodel source text.")), "script"),
        Tool("skill_reference", "Load one InventorModel reference on demand.", Props(
            ("name", "string", "Reference name: dsl, sketches, features, tools, verification, or patterns.")), "name"),
        Tool("status", "Report Autodesk Inventor connection and active Part status.", new Dictionary<string, object>()),
        Tool("build", "Build a new native editable Inventor Part from complete .imodel DSL source. Use this for the initial model.", Props(
            ("script", "string", "Complete .imodel source text.")), "script"),
        Tool("modify", "Apply one small edit to the active Part. Supported commands include: set <parameter> = <value>, suppress <feature>, unsuppress <feature>, delete <feature>.", Props(
            ("command", "string", "One InventorModel edit statement.")), "command"),
        Tool("inspect", "Inspect active Part bounds, parameters, and feature tree after modeling or edits.", new Dictionary<string, object>()),
        Tool("render", "Render front, top, right, and isometric PNG verification views of the active Part.", Props(
            ("directory", "string", "Optional output directory. Omit to use the InventorModel local render cache."))),
        Tool("save", "Save the active Part as a native editable IPT file.", Props(
            ("path", "string", "Output .ipt path."),
            ("overwrite", "boolean", "Whether an existing file may be overwritten.")), "path")
    };

    public string Execute(string name, string argumentsJson)
    {
        Dictionary<string, object> arguments = ParseArguments(argumentsJson);

        switch (name ?? string.Empty)
        {
            case "validate":
            {
                ValidationResult validation = new ModelValidator().Validate(Need(arguments, "script"));
                return _json.Serialize(new { valid = validation.IsValid, errors = validation.Errors });
            }

            case "skill_reference":
                return ReadSkillReference(Need(arguments, "name"));

            case "status":
                return SerializeStatus();

            case "build":
            {
                string source = Need(arguments, "script");
                PartDocument document = new ScriptExecutor(_application).Execute(source);
                document.Activate();
                return new ModelInspector().Inspect(document);
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
                string directory = Optional(arguments, "directory");
                if (string.IsNullOrWhiteSpace(directory))
                {
                    directory = IOPath.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                        "InventorModel",
                        "Renders",
                        DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                }

                directory = IOPath.GetFullPath(directory);
                Directory.CreateDirectory(directory);
                var images = new ModelRenderer(_application).RenderFourViews(ActivePart(), directory);
                return _json.Serialize(new { directory, images });
            }

            case "save":
            {
                string path = IOPath.GetFullPath(Need(arguments, "path"));
                bool overwrite = ReadBoolean(arguments, "overwrite");
                if (System.IO.File.Exists(path) && !overwrite)
                    throw new IOException("File already exists: " + path);
                ActivePart().SaveAs(path, false);
                return _json.Serialize(new { saved = true, path });
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
            documentType = part == null ? "none" : "part"
        });
    }

    private PartDocument ActivePart() =>
        _application.ActiveDocument as PartDocument ??
        throw new InvalidOperationException("Active document is not an Inventor Part.");

    private Dictionary<string, object> ParseArguments(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object>();
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
        if (values != null && values.TryGetValue(key, out object raw) && raw != null)
            return Convert.ToString(raw) ?? string.Empty;
        return string.Empty;
    }

    private static bool ReadBoolean(Dictionary<string, object> values, string key)
    {
        if (values == null || !values.TryGetValue(key, out object raw) || raw == null)
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
            IOPath.GetFullPath(IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Skills", "inventor-model"))
        };
        foreach (string root in roots)
        {
            string path = IOPath.Combine(root, "references", safeName + ".md");
            if (System.IO.File.Exists(path)) return System.IO.File.ReadAllText(path);
        }
        throw new FileNotFoundException("Unknown skill reference: " + safeName);
    }
}
