using System;
using System.Collections.Generic;
using System.Linq;

namespace InventorModel.Core.Dsl;

public sealed class StructuralRebuildPlan
{
    public bool CanRebuildLocally { get; init; }
    public int StartStatementIndex { get; init; } = -1;
    public List<string> FeatureNamesToRemove { get; } = new();
    public List<string> SketchNamesToRemove { get; } = new();
    public string Reason { get; init; } = "";
}

public sealed class StructuralRebuildPlanner
{
    private const string InternalHoleSketchPrefix =
        "__InventorModelHole_";

    public StructuralRebuildPlan Plan(
        ModelScript previous,
        ModelScript current,
        ModelDiffResult diff)
    {
        if (previous == null)
            throw new ArgumentNullException(nameof(previous));
        if (current == null)
            throw new ArgumentNullException(nameof(current));
        if (diff == null)
            throw new ArgumentNullException(nameof(diff));

        if (diff.ParameterChanges.Count > 0)
        {
            return Full(
                "parameter_and_structural_changes");
        }

        if (diff.StructuralChanges.Any(
                x =>
                    x.StartsWith(
                        "parameter:",
                        StringComparison.OrdinalIgnoreCase) ||
                    x.StartsWith(
                        "part:",
                        StringComparison.OrdinalIgnoreCase) ||
                    x.StartsWith(
                        "edits:",
                        StringComparison.OrdinalIgnoreCase)))
        {
            return Full(
                "global_model_structure_changed");
        }

        int start =
            FirstDifferentStatement(
                previous,
                current);

        if (start < 0)
        {
            return Full(
                "no_local_dirty_node_found");
        }

        var result =
            new StructuralRebuildPlan
            {
                CanRebuildLocally = true,
                StartStatementIndex = start,
                Reason = "dependency_safe_suffix"
            };

        for (int i =
                 previous.Statements.Count - 1;
             i >= start;
             i--)
        {
            switch (previous.Statements[i])
            {
                case FeatureStatement feature:
                    result.FeatureNamesToRemove.Add(
                        feature.Name);

                    if (feature.Kind.Equals(
                            "hole",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        result.SketchNamesToRemove.Add(
                            InternalHoleSketchPrefix +
                            feature.Name);
                    }
                    break;

                case SketchStatement sketch:
                    result.SketchNamesToRemove.Add(
                        sketch.Name);
                    break;
            }
        }

        return result;
    }

    private static StructuralRebuildPlan Full(
        string reason) =>
        new()
        {
            CanRebuildLocally = false,
            Reason = reason
        };

    private static int FirstDifferentStatement(
        ModelScript previous,
        ModelScript current)
    {
        int common =
            Math.Min(
                previous.Statements.Count,
                current.Statements.Count);

        for (int i = 0; i < common; i++)
        {
            if (!StatementEquals(
                    previous.Statements[i],
                    current.Statements[i]))
            {
                return i;
            }
        }

        return previous.Statements.Count ==
               current.Statements.Count
            ? -1
            : common;
    }

    private static bool StatementEquals(
        ScriptStatement left,
        ScriptStatement right)
    {
        if (left.GetType() != right.GetType())
            return false;

        switch (left)
        {
            case ParameterStatement a
                when right is ParameterStatement b:
                return Equal(a.Name, b.Name) &&
                       Equal(
                           a.Expression,
                           b.Expression);

            case SketchStatement a
                when right is SketchStatement b:
                return SketchEquals(a, b);

            case FeatureStatement a
                when right is FeatureStatement b:
                return FeatureEquals(a, b);

            case EditStatement a
                when right is EditStatement b:
                return Equal(a.Kind, b.Kind) &&
                       Equal(a.Target, b.Target) &&
                       Equal(a.Value, b.Value);

            default:
                return false;
        }
    }

    private static bool SketchEquals(
        SketchStatement left,
        SketchStatement right)
    {
        if (!Equal(left.Name, right.Name) ||
            !Equal(left.Plane, right.Plane) ||
            left.Lines.Count != right.Lines.Count)
        {
            return false;
        }

        for (int i = 0;
             i < left.Lines.Count;
             i++)
        {
            SketchLineStatement a =
                left.Lines[i];
            SketchLineStatement b =
                right.Lines[i];

            if (!Equal(a.Kind, b.Kind) ||
                !Equal(a.Name, b.Name) ||
                !SequenceEqual(
                    a.Args,
                    b.Args))
            {
                return false;
            }
        }

        return true;
    }

    private static bool FeatureEquals(
        FeatureStatement left,
        FeatureStatement right)
    {
        if (!Equal(left.Kind, right.Kind) ||
            !Equal(left.Name, right.Name) ||
            !SequenceEqual(
                left.Items,
                right.Items) ||
            left.Args.Count != right.Args.Count)
        {
            return false;
        }

        foreach ((string key, string value) in
                 left.Args)
        {
            if (!right.Args.TryGetValue(
                    key,
                    out string? other) ||
                !Equal(value, other))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SequenceEqual(
        IEnumerable<string> left,
        IEnumerable<string> right) =>
        left.Select(Normalize)
            .SequenceEqual(
                right.Select(Normalize),
                StringComparer.OrdinalIgnoreCase);

    private static bool Equal(
        string? left,
        string? right) =>
        string.Equals(
            Normalize(left),
            Normalize(right),
            StringComparison.OrdinalIgnoreCase);

    private static string Normalize(
        string? value) =>
        (value ?? string.Empty).Trim();
}
