using System;
using System.Collections.Generic;

namespace InventorModel.Core.Dsl;

public sealed class ValidationResult
{
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

public sealed class ModelValidator
{
    private static readonly HashSet<string> FeatureKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "extrude", "revolve", "sweep", "loft", "hole", "fillet", "chamfer",
        "shell", "pattern_rect", "pattern_circular", "mirror"
    };

    private static readonly Dictionary<string, int> SketchArgs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["point"] = 2, ["line"] = 4, ["circle"] = 3, ["arc"] = 5,
        ["ellipse"] = 4, ["rect"] = 4, ["centerrect"] = 4,
        ["slot"] = 5, ["polygon"] = 5
    };

    public ValidationResult Validate(string source)
    {
        try { return Validate(new DslParser().Parse(source)); }
        catch (Exception ex)
        {
            var failed = new ValidationResult();
            failed.Errors.Add(ex.Message);
            return failed;
        }
    }

    public ValidationResult Validate(ModelScript script)
    {
        var result = new ValidationResult();
        var parameters = new ParameterTable();
        var sketches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var features = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ScriptStatement statement in script.Statements)
        {
            if (statement is ParameterStatement parameter)
                Try(result, $"parameter '{parameter.Name}'", () => parameters.Add(parameter.Name, parameter.Expression));
            else if (statement is SketchStatement sketch)
            {
                ValidateSketch(sketch, parameters, features, result);
                sketches.Add(sketch.Name);
            }
            else if (statement is FeatureStatement feature)
            {
                ValidateFeature(feature, parameters, sketches, features, result);
                features.Add(feature.Name);
            }
            // Edit targets can belong to an already-open Inventor document, so standalone
            // validation intentionally does not require them to be declared in this snippet.
        }

        return result;
    }

    private static void ValidateSketch(SketchStatement sketch, ParameterTable parameters, ISet<string> features, ValidationResult result)
    {
        if (!IsBasePlane(sketch.Plane) && !sketch.Plane.StartsWith("face:", StringComparison.OrdinalIgnoreCase))
            result.Errors.Add($"sketch '{sketch.Name}' has invalid plane/face selector '{sketch.Plane}'.");

        string[] selector = sketch.Plane.Split(':');
        if (sketch.Plane.StartsWith(
                "face:index:",
                StringComparison.OrdinalIgnoreCase))
        {
            if (selector.Length != 3 ||
                !int.TryParse(selector[2], out int faceIndex) ||
                faceIndex < 1)
            {
                result.Errors.Add(
                    $"sketch '{sketch.Name}' has invalid indexed face selector '{sketch.Plane}'.");
            }
        }
        else if (selector.Length >= 3 &&
                 !features.Contains(selector[1]))
        {
            result.Errors.Add(
                $"sketch '{sketch.Name}' references unknown feature '{selector[1]}'.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SketchLineStatement line in sketch.Lines)
        {
            if (!string.IsNullOrWhiteSpace(line.Name) && !names.Add(line.Name))
                result.Errors.Add($"sketch '{sketch.Name}' contains duplicate entity '{line.Name}'.");
            if (SketchArgs.TryGetValue(line.Kind, out int expected) && line.Args.Count != expected)
                result.Errors.Add($"sketch '{sketch.Name}' {line.Kind} requires {expected} arguments, got {line.Args.Count}.");
            if (line.Kind == "spline" && (line.Args.Count < 6 || line.Args.Count % 2 != 0))
                result.Errors.Add($"sketch '{sketch.Name}' spline requires at least three coordinate pairs.");

            if (line.Kind != "constraint" && line.Kind != "dim")
                for (int i = 0; i < line.Args.Count; i++)
                    if (!(line.Kind == "polygon" && i == 3))
                    {
                        string expression = line.Args[i];
                        Try(result, $"sketch '{sketch.Name}' {line.Kind}", () => parameters.Mm(expression));
                    }
        }
    }

    private static void ValidateFeature(FeatureStatement f, ParameterTable p, ISet<string> sketches, ISet<string> features, ValidationResult r)
    {
        if (!FeatureKinds.Contains(f.Kind)) { r.Errors.Add($"Unsupported feature '{f.Kind}'."); return; }
        switch (f.Kind)
        {
            case "extrude":
                RequireSketch(f, "from", sketches, r);
                RequireEither(f, "depth", "extent", r);
                Evaluate(f, p, r, "depth", "distance");
                ValidateDirection(
                    f,
                    r,
                    allowSymmetric:
                        !(f.Args.TryGetValue("extent", out string extrudeExtent) &&
                          extrudeExtent.Equals("through", StringComparison.OrdinalIgnoreCase)));
                break;
            case "revolve":
                RequireSketch(f, "from", sketches, r);
                if (Require(f, "axis", r))
                    ValidateRevolveAxis(f.Args["axis"], f, r);
                Evaluate(f, p, r, "angle");
                ValidateDirection(f, r, allowSymmetric: true);
                break;
            case "sweep": RequireSketch(f, "profile", sketches, r); RequireSketch(f, "path", sketches, r); break;
            case "loft":
                var sections = new List<string>();
                if (f.Args.TryGetValue("from", out string first)) sections.Add(first);
                sections.AddRange(f.Items);
                if (sections.Count < 2) r.Errors.Add($"loft '{f.Name}' requires at least two section sketches.");
                foreach (string section in sections) if (!sketches.Contains(section)) r.Errors.Add($"loft '{f.Name}' references unknown sketch '{section}'.");
                break;
            case "hole":
                Require(f, "on", r);
                RequirePair(f, "at", r);
                Require(f, "diameter", r);
                RequireEither(f, "depth", "extent", r);
                Evaluate(f, p, r, "at", "diameter", "depth");
                ValidateDirection(f, r, allowSymmetric: false);
                break;
            case "fillet": RequireEdgeSelector(f, r); EvaluateRequired(f, p, r, "radius"); break;
            case "chamfer": RequireEdgeSelector(f, r); EvaluateRequired(f, p, r, "distance"); break;
            case "shell": Require(f, "faces", r); EvaluateRequired(f, p, r, "thickness"); break;
            case "pattern_rect":
                RequireFeature(f, "source", features, r);
                RequireOneOrTwo(f, "count", r);
                RequireOneOrTwo(f, "spacing", r);
                Evaluate(f, p, r, "count", "spacing");
                ValidateOptionalBaseAxis(f, "axis", r);
                ValidateOptionalBaseAxis(f, "axis2", r);
                break;
            case "pattern_circular":
                RequireFeature(f, "source", features, r);
                if (Require(f, "axis", r))
                    ValidateBaseAxis(f.Args["axis"], f, "axis", r);
                EvaluateRequired(f, p, r, "count");
                Evaluate(f, p, r, "angle");
                break;
            case "mirror": RequireFeature(f, "source", features, r); Require(f, "plane", r); break;
        }
    }

    private static void RequireSketch(FeatureStatement f, string key, ISet<string> values, ValidationResult r)
    { if (Require(f, key, r) && !values.Contains(f.Args[key])) r.Errors.Add($"{f.Kind} '{f.Name}' references unknown sketch '{f.Args[key]}'."); }
    private static void RequireFeature(FeatureStatement f, string key, ISet<string> values, ValidationResult r)
    { if (Require(f, key, r) && !values.Contains(f.Args[key])) r.Errors.Add($"{f.Kind} '{f.Name}' references unknown feature '{f.Args[key]}'."); }
    private static bool Require(FeatureStatement f, string key, ValidationResult r)
    { if (f.Args.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value)) return true; r.Errors.Add($"{f.Kind} '{f.Name}' requires '{key}'."); return false; }
    private static void RequireEither(FeatureStatement f, string one, string two, ValidationResult r)
    { if (!f.Args.ContainsKey(one) && !f.Args.ContainsKey(two)) r.Errors.Add($"{f.Kind} '{f.Name}' requires '{one}' or '{two}'."); }
    private static void RequirePair(FeatureStatement f, string key, ValidationResult r)
    { if (Require(f, key, r) && f.Args[key].Split(',').Length != 2) r.Errors.Add($"{f.Kind} '{f.Name}' requires two '{key}' values."); }
    private static void RequireOneOrTwo(FeatureStatement f, string key, ValidationResult r)
    { if (Require(f, key, r) && f.Args[key].Split(',').Length > 2) r.Errors.Add($"{f.Kind} '{f.Name}' accepts one or two '{key}' values."); }
    private static void RequireEdgeSelector(FeatureStatement f, ValidationResult r)
    {
        if (!Require(f, "edges", r))
            return;

        string value =
            f.Args["edges"];

        if (value.Equals(
                "all",
                StringComparison.OrdinalIgnoreCase))
            return;

        foreach (string token in value.Split(','))
        {
            if (!int.TryParse(
                    token.Trim(),
                    out int index) ||
                index < 1)
            {
                r.Errors.Add(
                    $"{f.Kind} '{f.Name}' edges must be 'all' or a comma-separated list of positive 1-based edge indexes.");
                return;
            }
        }
    }

    private static void ValidateDirection(
        FeatureStatement f,
        ValidationResult r,
        bool allowSymmetric)
    {
        if (!f.Args.TryGetValue("direction", out string value))
            return;

        bool valid =
            value.Equals("positive", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("negative", StringComparison.OrdinalIgnoreCase) ||
            (allowSymmetric &&
             value.Equals("symmetric", StringComparison.OrdinalIgnoreCase));

        if (!valid)
        {
            r.Errors.Add(
                allowSymmetric
                    ? $"{f.Kind} '{f.Name}' direction must be positive, negative, or symmetric."
                    : $"{f.Kind} '{f.Name}' direction must be positive or negative.");
        }
    }

    private static void ValidateRevolveAxis(
        string value,
        FeatureStatement f,
        ValidationResult r)
    {
        if (IsBaseAxis(value))
            return;

        if (value.StartsWith("line:", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(value.Substring("line:".Length), out int index) &&
            index >= 1)
        {
            return;
        }

        r.Errors.Add(
            $"revolve '{f.Name}' axis must be X, Y, Z, or line:<positive-1-based-index>.");
    }

    private static void ValidateOptionalBaseAxis(
        FeatureStatement f,
        string key,
        ValidationResult r)
    {
        if (f.Args.TryGetValue(key, out string value))
            ValidateBaseAxis(value, f, key, r);
    }

    private static void ValidateBaseAxis(
        string value,
        FeatureStatement f,
        string key,
        ValidationResult r)
    {
        if (!IsBaseAxis(value))
            r.Errors.Add(
                $"{f.Kind} '{f.Name}' {key} must be X, Y, or Z.");
    }

    private static bool IsBaseAxis(string value) =>
        value.Equals("X", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("Z", StringComparison.OrdinalIgnoreCase);

    private static void EvaluateRequired(FeatureStatement f, ParameterTable p, ValidationResult r, string key)
    { if (Require(f, key, r)) Evaluate(f, p, r, key); }
    private static void Evaluate(FeatureStatement f, ParameterTable p, ValidationResult r, params string[] keys)
    { foreach (string key in keys) if (f.Args.TryGetValue(key, out string value)) foreach (string expression in value.Split(',')) Try(r, $"{f.Kind} '{f.Name}' {key}", () => p.Mm(expression)); }
    private static bool IsBasePlane(string value) => value.Equals("XY", StringComparison.OrdinalIgnoreCase) || value.Equals("XZ", StringComparison.OrdinalIgnoreCase) || value.Equals("YZ", StringComparison.OrdinalIgnoreCase);
    private static void Try(ValidationResult r, string context, Action action) { try { action(); } catch (Exception ex) { r.Errors.Add(context + ": " + ex.Message); } }
}
