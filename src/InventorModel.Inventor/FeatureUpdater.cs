using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using InventorModel.Core.Diagnostics;
using InventorModel.Core.Dsl;
using DslParameterTable = InventorModel.Core.Dsl.ParameterTable;

namespace InventorModel.Inventor;

public sealed class FeatureUpdater
{
    private readonly Application _application;
    private readonly PartComponentDefinition _component;
    private readonly ModelBindings _bindings;
    private readonly DslParameterTable _parameters =
        new();

    public FeatureUpdater(
        Application application,
        PartDocument document,
        ModelBindings bindings,
        ModelScript targetModel)
    {
        _application = application ??
            throw new ArgumentNullException(nameof(application));
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        _bindings = bindings ??
            throw new ArgumentNullException(nameof(bindings));
        if (targetModel == null)
            throw new ArgumentNullException(nameof(targetModel));

        _component = document.ComponentDefinition;

        foreach (ParameterStatement parameter in
                 targetModel.Statements
                     .OfType<ParameterStatement>())
        {
            _parameters.Add(
                parameter.Name,
                parameter.Expression);
        }
    }

    public bool CanApply(
        IEnumerable<FeatureValueChange> changes)
    {
        if (changes == null)
            throw new ArgumentNullException(nameof(changes));

        foreach (FeatureValueChange change in changes)
        {
            if (!_bindings.Features.ContainsKey(
                    change.Name))
            {
                return false;
            }

            HashSet<string> changedKeys =
                change.NewArguments.Keys
                    .Union(
                        change.OldArguments.Keys,
                        StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(
                        StringComparer.OrdinalIgnoreCase);

            if (!SupportedKeys(
                    change.Kind,
                    changedKeys))
            {
                return false;
            }

            if (change.Kind.Equals(
                    "pattern_rect",
                    StringComparison.OrdinalIgnoreCase) &&
                !PatternShapeCompatible(change))
            {
                return false;
            }
        }

        return true;
    }

    public void Apply(
        PartDocument document,
        ModelScript targetModel,
        IEnumerable<FeatureValueChange> changes)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        if (targetModel == null)
            throw new ArgumentNullException(nameof(targetModel));
        if (changes == null)
            throw new ArgumentNullException(nameof(changes));

        FeatureValueChange[] requested =
            changes.ToArray();

        if (requested.Length == 0)
            return;

        if (!CanApply(requested))
        {
            throw new InvalidOperationException(
                "Requested feature changes are not safe for in-place update.");
        }

        Dictionary<string, FeatureStatement> targets =
            targetModel.Statements
                .OfType<FeatureStatement>()
                .ToDictionary(
                    x => x.Name,
                    StringComparer.OrdinalIgnoreCase);

        Transaction transaction =
            _application.TransactionManager.StartTransaction(
                (_Document)(object)document,
                "InventorModel feature update");

        try
        {
            foreach (FeatureValueChange change in requested)
            {
                if (!targets.TryGetValue(
                        change.Name,
                        out FeatureStatement? target))
                {
                    throw new InvalidOperationException(
                        "Target feature '" +
                        change.Name +
                        "' was not found in the model.");
                }

                UpdateFeature(
                    _bindings.Features[change.Name],
                    target);
            }

            document.Update2(true);
            transaction.End();
        }
        catch (Exception ex)
        {
            try
            {
                transaction.Abort();
            }
            catch (Exception abortException)
            {
                RuntimeLog.Error(
                    "Inventor.Transaction",
                    "Feature update rollback failed.",
                    abortException);
            }

            RuntimeLog.Error(
                "Inventor.Features",
                "Incremental feature update failed.",
                ex);
            throw;
        }
    }

    private void UpdateFeature(
        PartFeature feature,
        FeatureStatement target)
    {
        switch (target.Kind)
        {
            case "extrude":
                UpdateExtrude(
                    (ExtrudeFeature)(object)feature,
                    target);
                break;

            case "revolve":
                UpdateRevolve(
                    (RevolveFeature)(object)feature,
                    target);
                break;

            case "hole":
                UpdateHole(
                    (HoleFeature)(object)feature,
                    target);
                break;

            case "fillet":
                UpdateFillet(
                    (FilletFeature)(object)feature,
                    target);
                break;

            case "chamfer":
                UpdateChamfer(
                    (ChamferFeature)(object)feature,
                    target);
                break;

            case "shell":
                UpdateShell(
                    (ShellFeature)(object)feature,
                    target);
                break;

            case "pattern_rect":
                UpdateRectangularPattern(
                    (RectangularPatternFeature)(object)feature,
                    target);
                break;

            case "pattern_circular":
                UpdateCircularPattern(
                    (CircularPatternFeature)(object)feature,
                    target);
                break;

            case "mirror":
                UpdateMirror(
                    (MirrorFeature)(object)feature,
                    target);
                break;

            default:
                throw new InvalidOperationException(
                    "Unsupported in-place feature update '" +
                    target.Kind +
                    "'.");
        }
    }

    private void UpdateExtrude(
        ExtrudeFeature feature,
        FeatureStatement target)
    {
        ExtrudeDefinition definition =
            feature.Definition;

        if (IsThrough(target))
        {
            definition.SetThroughAllExtent(
                ExtentDirection(
                    target,
                    allowSymmetric: false));
        }
        else
        {
            definition.SetDistanceExtent(
                _parameters.Length(
                    Argument(
                        target,
                        "depth",
                        "distance")),
                ExtentDirection(
                    target,
                    allowSymmetric: true));
        }

        feature.Definition = definition;
    }

    private void UpdateRevolve(
        RevolveFeature feature,
        FeatureStatement target)
    {
        string angle =
            target.Args.TryGetValue(
                "angle",
                out string? value)
                ? value
                : "360";

        if (Math.Abs(
                _parameters.Degrees(angle) -
                360.0) < 1e-7)
        {
            feature.SetFullExtent();
            return;
        }

        feature.SetAngleExtent(
            _parameters.Angle(angle),
            ExtentDirection(
                target,
                allowSymmetric: true));
    }

    private void UpdateHole(
        HoleFeature feature,
        FeatureStatement target)
    {
        feature.HoleDiameter.Expression =
            _parameters.Length(
                Argument(
                    target,
                    "diameter"));

        if (IsThrough(target))
        {
            feature.SetThroughAllExtent(
                ExtentDirection(
                    target,
                    allowSymmetric: false));
            return;
        }

        feature.SetDistanceExtent(
            _parameters.Length(
                Argument(
                    target,
                    "depth")),
            ExtentDirection(
                target,
                allowSymmetric: false),
            false,
            "118 deg");
    }

    private void UpdateFillet(
        FilletFeature feature,
        FeatureStatement target)
    {
        FilletDefinition definition =
            feature.FilletDefinition;

        if (definition.EdgeSetCount != 1)
        {
            throw new InvalidOperationException(
                "In-place fillet radius update requires exactly one edge set.");
        }

        var edgeSet =
            definition.EdgeSetItem[1]
                as FilletConstantRadiusEdgeSet;

        if (edgeSet == null)
        {
            throw new InvalidOperationException(
                "In-place fillet update requires a constant-radius edge set.");
        }

        edgeSet.Radius.Expression =
            _parameters.Length(
                Argument(
                    target,
                    "radius"));
    }

    private void UpdateChamfer(
        ChamferFeature feature,
        FeatureStatement target)
    {
        ChamferDefinition definition =
            feature.Definition;

        definition.Distance.Expression =
            _parameters.Length(
                Argument(
                    target,
                    "distance"));
    }

    private void UpdateShell(
        ShellFeature feature,
        FeatureStatement target)
    {
        ShellDefinition definition =
            feature.Definition;

        definition.Thickness.Expression =
            _parameters.Length(
                Argument(
                    target,
                    "thickness"));
    }

    private void UpdateRectangularPattern(
        RectangularPatternFeature feature,
        FeatureStatement target)
    {
        RectangularPatternFeatureDefinition definition =
            feature.Definition;

        string[] counts =
            Argument(
                target,
                "count")
            .Split(',');
        string[] spacing =
            Argument(
                target,
                "spacing")
            .Split(',');

        definition.XCount =
            _parameters.Integer(counts[0]);
        definition.XSpacing =
            _parameters.Length(spacing[0]);

        if (counts.Length > 1)
        {
            if (spacing.Length < 2)
            {
                throw new InvalidOperationException(
                    "pattern_rect Y count requires Y spacing.");
            }

            definition.YCount =
                _parameters.Integer(counts[1]);
            definition.YSpacing =
                _parameters.Length(spacing[1]);
        }
    }

    private void UpdateCircularPattern(
        CircularPatternFeature feature,
        FeatureStatement target)
    {
        CircularPatternFeatureDefinition definition =
            feature.Definition;

        definition.Count =
            _parameters.Integer(
                Argument(
                    target,
                    "count"));

        string angle =
            target.Args.TryGetValue(
                "angle",
                out string? value)
                ? value
                : "360";

        definition.Angle =
            _parameters.Angle(angle);
    }

    private void UpdateMirror(
        MirrorFeature feature,
        FeatureStatement target)
    {
        MirrorFeatureDefinition definition =
            feature.Definition;

        definition.MirrorPlaneEntity =
            GeometrySelector.WorkPlane(
                _component,
                Argument(
                    target,
                    "plane"));
    }

    private static bool SupportedKeys(
        string kind,
        ISet<string> changedKeys)
    {
        string[] allowed =
            kind.ToLowerInvariant() switch
            {
                "extrude" =>
                    new[]
                    {
                        "depth",
                        "distance",
                        "extent",
                        "direction"
                    },
                "revolve" =>
                    new[]
                    {
                        "angle",
                        "direction"
                    },
                "hole" =>
                    new[]
                    {
                        "diameter",
                        "depth",
                        "extent",
                        "direction"
                    },
                "fillet" =>
                    new[] { "radius" },
                "chamfer" =>
                    new[] { "distance" },
                "shell" =>
                    new[] { "thickness" },
                "pattern_rect" =>
                    new[]
                    {
                        "count",
                        "spacing"
                    },
                "pattern_circular" =>
                    new[]
                    {
                        "count",
                        "angle"
                    },
                "mirror" =>
                    new[] { "plane" },
                _ =>
                    Array.Empty<string>()
            };

        if (allowed.Length == 0)
            return false;

        var allowedSet =
            allowed.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        return changedKeys.All(
            allowedSet.Contains);
    }

    private static bool PatternShapeCompatible(
        FeatureValueChange change)
    {
        foreach (string key in
                 new[] { "count", "spacing" })
        {
            if (!change.OldArguments.TryGetValue(
                    key,
                    out string? oldValue) ||
                !change.NewArguments.TryGetValue(
                    key,
                    out string? newValue) ||
                oldValue == null ||
                newValue == null)
            {
                continue;
            }

            if (oldValue.Split(',').Length !=
                newValue.Split(',').Length)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsThrough(
        FeatureStatement definition) =>
        definition.Args.TryGetValue(
            "extent",
            out string? extent) &&
        string.Equals(
            extent,
            "through",
            StringComparison.OrdinalIgnoreCase);

    private static string Argument(
        FeatureStatement definition,
        params string[] keys)
    {
        foreach (string key in keys)
        {
            if (definition.Args.TryGetValue(
                    key,
                    out string? value))
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            definition.Kind +
            " " +
            definition.Name +
            " requires " +
            string.Join("/", keys) +
            ".");
    }

    private static PartFeatureExtentDirectionEnum
        ExtentDirection(
            FeatureStatement definition,
            bool allowSymmetric)
    {
        string value =
            definition.Args.TryGetValue(
                "direction",
                out string? direction)
                ? direction
                : "positive";

        if (value.Equals(
                "positive",
                StringComparison.OrdinalIgnoreCase))
        {
            return PartFeatureExtentDirectionEnum
                .kPositiveExtentDirection;
        }

        if (value.Equals(
                "negative",
                StringComparison.OrdinalIgnoreCase))
        {
            return PartFeatureExtentDirectionEnum
                .kNegativeExtentDirection;
        }

        if (allowSymmetric &&
            value.Equals(
                "symmetric",
                StringComparison.OrdinalIgnoreCase))
        {
            return PartFeatureExtentDirectionEnum
                .kSymmetricExtentDirection;
        }

        throw new InvalidOperationException(
            allowSymmetric
                ? "Invalid direction '" +
                  value +
                  "'. Use positive, negative, or symmetric."
                : "Invalid direction '" +
                  value +
                  "'. Use positive or negative.");
    }
}
