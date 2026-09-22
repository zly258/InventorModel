using System;
using System.Collections.Generic;

namespace InventorModel.Core.Dsl;

public abstract class ScriptStatement {}

public sealed class ModelScript
{
    public string PartName { get; set; } = "Part";
    public List<ScriptStatement> Statements { get; } = new();
}

public sealed class ParameterStatement : ScriptStatement
{
    public string Name { get; set; } = "";
    public string Expression { get; set; } = "";
}

public sealed class SketchStatement : ScriptStatement
{
    public string Name { get; set; } = "";
    public string Plane { get; set; } = "XY";
    public List<SketchLineStatement> Lines { get; } = new();
}

public sealed class SketchLineStatement
{
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Args { get; } = new();
}

public sealed class FeatureStatement : ScriptStatement
{
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string,string> Args { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Items { get; } = new();
}

public sealed class EditStatement : ScriptStatement
{
    public string Kind { get; set; } = "";
    public string Target { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class DslException : Exception
{
    public int Line { get; }
    public DslException(int line,string message) : base($"Line {line}: {message}") => Line=line;
}
