using System;
using System.Collections.Generic;
using System.Linq;

namespace InventorModel.Core.Dsl;

public sealed class ParameterValueChange
{
    public string Name { get; init; } = "";
    public string OldExpression { get; init; } = "";
    public string NewExpression { get; init; } = "";
}

public sealed class FeatureValueChange
{
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public Dictionary<string, string?> OldArguments { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string?> NewArguments { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ModelDiffResult
{
    public List<ParameterValueChange> ParameterChanges { get; } = new();
    public List<FeatureValueChange> FeatureChanges { get; } = new();
    public List<string> StructuralChanges { get; } = new();

    public bool IsUnchanged =>
        ParameterChanges.Count == 0 &&
        FeatureChanges.Count == 0 &&
        StructuralChanges.Count == 0;

    public bool IsParameterOnly =>
        ParameterChanges.Count > 0 &&
        FeatureChanges.Count == 0 &&
        StructuralChanges.Count == 0;
}

public sealed class ModelDiffer
{
    public ModelDiffResult Compare(
        ModelScript previous,
        ModelScript current)
    {
        if (previous == null)
            throw new ArgumentNullException(nameof(previous));
        if (current == null)
            throw new ArgumentNullException(nameof(current));

        var result = new ModelDiffResult();

        if (!string.Equals(
                previous.PartName,
                current.PartName,
                StringComparison.OrdinalIgnoreCase))
        {
            result.StructuralChanges.Add("part:name");
        }

        CompareParameters(previous, current, result);
        CompareSketches(previous, current, result);
        CompareFeatures(previous, current, result);
        CompareEdits(previous, current, result);

        return result;
    }

    private static void CompareParameters(
        ModelScript previous,
        ModelScript current,
        ModelDiffResult result)
    {
        Dictionary<string, ParameterStatement> oldValues =
            previous.Statements
                .OfType<ParameterStatement>()
                .ToDictionary(
                    x => x.Name,
                    StringComparer.OrdinalIgnoreCase);

        Dictionary<string, ParameterStatement> newValues =
            current.Statements
                .OfType<ParameterStatement>()
                .ToDictionary(
                    x => x.Name,
                    StringComparer.OrdinalIgnoreCase);

        foreach (string name in oldValues.Keys)
        {
            if (!newValues.TryGetValue(
                    name,
                    out ParameterStatement? next))
            {
                result.StructuralChanges.Add(
                    "parameter:removed:" + name);
                continue;
            }

            string oldExpression =
                Normalize(oldValues[name].Expression);
            string newExpression =
                Normalize(next.Expression);

            if (!string.Equals(
                    oldExpression,
                    newExpression,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.ParameterChanges.Add(
                    new ParameterValueChange
                    {
                        Name = name,
                        OldExpression =
                            oldValues[name].Expression,
                        NewExpression =
                            next.Expression
                    });
            }
        }

        foreach (string name in newValues.Keys)
        {
            if (!oldValues.ContainsKey(name))
            {
                result.StructuralChanges.Add(
                    "parameter:added:" + name);
            }
        }
    }

    private static void CompareSketches(
        ModelScript previous,
        ModelScript current,
        ModelDiffResult result)
    {
        Dictionary<string, SketchStatement> oldValues =
            previous.Statements
                .OfType<SketchStatement>()
                .ToDictionary(
                    x => x.Name,
                    StringComparer.OrdinalIgnoreCase);

        Dictionary<string, SketchStatement> newValues =
            current.Statements
                .OfType<SketchStatement>()
                .ToDictionary(
                    x => x.Name,
                    StringComparer.OrdinalIgnoreCase);

        foreach (string name in oldValues.Keys)
        {
            if (!newValues.TryGetValue(
                    name,
                    out SketchStatement? next))
            {
                result.StructuralChanges.Add(
                    "sketch:removed:" + name);
                continue;
            }

            if (!SketchEquals(
                    oldValues[name],
                    next))
            {
                result.StructuralChanges.Add(
                    "sketch:changed:" + name);
            }
        }

        foreach (string name in newValues.Keys)
        {
            if (!oldValues.ContainsKey(name))
            {
                result.StructuralChanges.Add(
                    "sketch:added:" + name);
            }
        }
    }

    private static void CompareFeatures(
        ModelScript previous,
        ModelScript current,
        ModelDiffResult result)
    {
        Dictionary<string, FeatureStatement> oldValues =
            previous.Statements
                .OfType<FeatureStatement>()
                .ToDictionary(
                    x => x.Name,
                    StringComparer.OrdinalIgnoreCase);

        Dictionary<string, FeatureStatement> newValues =
            current.Statements
                .OfType<FeatureStatement>()
                .ToDictionary(
                    x => x.Name,
                    StringComparer.OrdinalIgnoreCase);

        foreach (string name in oldValues.Keys)
        {
            FeatureStatement oldFeature =
                oldValues[name];

            if (!newValues.TryGetValue(
                    name,
                    out FeatureStatement? next))
            {
                result.StructuralChanges.Add(
                    "feature:removed:" + name);
                continue;
            }

            if (!string.Equals(
                    oldFeature.Kind,
                    next.Kind,
                    StringComparison.OrdinalIgnoreCase) ||
                !SequenceEquals(
                    oldFeature.Items,
                    next.Items))
            {
                result.StructuralChanges.Add(
                    "feature:changed:" + name);
                continue;
            }

            if (!ArgumentsEqual(
                    oldFeature.Args,
                    next.Args))
            {
                var change = new FeatureValueChange
                {
                    Name = name,
                    Kind = next.Kind
                };

                foreach (string key in
                         oldFeature.Args.Keys
                             .Union(
                                 next.Args.Keys,
                                 StringComparer.OrdinalIgnoreCase))
                {
                    oldFeature.Args.TryGetValue(
                        key,
                        out string? oldValue);
                    next.Args.TryGetValue(
                        key,
                        out string? newValue);

                    if (!string.Equals(
                            Normalize(oldValue),
                            Normalize(newValue),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        change.OldArguments[key] =
                            oldValue;
                        change.NewArguments[key] =
                            newValue;
                    }
                }

                result.FeatureChanges.Add(change);
            }
        }

        foreach (string name in newValues.Keys)
        {
            if (!oldValues.ContainsKey(name))
            {
                result.StructuralChanges.Add(
                    "feature:added:" + name);
            }
        }
    }

    private static void CompareEdits(
        ModelScript previous,
        ModelScript current,
        ModelDiffResult result)
    {
        string[] oldEdits =
            previous.Statements
                .OfType<EditStatement>()
                .Select(EditSignature)
                .ToArray();

        string[] newEdits =
            current.Statements
                .OfType<EditStatement>()
                .Select(EditSignature)
                .ToArray();

        if (!oldEdits.SequenceEqual(
                newEdits,
                StringComparer.OrdinalIgnoreCase))
        {
            result.StructuralChanges.Add(
                "edits:changed");
        }
    }

    private static bool SketchEquals(
        SketchStatement left,
        SketchStatement right)
    {
        if (!string.Equals(
                left.Plane,
                right.Plane,
                StringComparison.OrdinalIgnoreCase) ||
            left.Lines.Count != right.Lines.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Lines.Count; i++)
        {
            SketchLineStatement a = left.Lines[i];
            SketchLineStatement b = right.Lines[i];

            if (!string.Equals(
                    a.Kind,
                    b.Kind,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    a.Name,
                    b.Name,
                    StringComparison.OrdinalIgnoreCase) ||
                !SequenceEquals(a.Args, b.Args))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ArgumentsEqual(
        IDictionary<string, string> left,
        IDictionary<string, string> right)
    {
        if (left.Count != right.Count)
            return false;

        foreach ((string key, string value) in left)
        {
            if (!right.TryGetValue(
                    key,
                    out string? other) ||
                !string.Equals(
                    Normalize(value),
                    Normalize(other),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SequenceEquals(
        IEnumerable<string> left,
        IEnumerable<string> right) =>
        left.Select(Normalize)
            .SequenceEqual(
                right.Select(Normalize),
                StringComparer.OrdinalIgnoreCase);

    private static string EditSignature(
        EditStatement edit) =>
        string.Join(
            "|",
            Normalize(edit.Kind),
            Normalize(edit.Target),
            Normalize(edit.Value));

    private static string Normalize(
        string? value) =>
        (value ?? string.Empty).Trim();
}
