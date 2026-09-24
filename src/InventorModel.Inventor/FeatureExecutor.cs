using System;
using System.Collections.Generic;
using Inventor;
using InventorModel.Core.Dsl;
using DslParameterTable = InventorModel.Core.Dsl.ParameterTable;

namespace InventorModel.Inventor;

internal sealed class FeatureExecutor
{
    private readonly Application _app;
    private readonly PartComponentDefinition _component;
    private readonly DslParameterTable _parameters;
    private readonly IDictionary<string, PlanarSketch> _sketches;
    private readonly IDictionary<string, PartFeature> _features;

    public FeatureExecutor(
        Application app,
        PartComponentDefinition component,
        DslParameterTable parameters,
        IDictionary<string, PlanarSketch> sketches,
        IDictionary<string, PartFeature> features)
    {
        _app = app;
        _component = component;
        _parameters = parameters;
        _sketches = sketches;
        _features = features;
    }

    public PartFeature Build(FeatureStatement definition)
    {
        PartFeature feature;

        switch (definition.Kind)
        {
            case "extrude":
                feature = AsPartFeature(Extrude(definition));
                break;
            case "revolve":
                feature = AsPartFeature(Revolve(definition));
                break;
            case "sweep":
                feature = AsPartFeature(Sweep(definition));
                break;
            case "loft":
                feature = AsPartFeature(Loft(definition));
                break;
            case "hole":
                feature = AsPartFeature(Hole(definition));
                break;
            case "fillet":
                feature = AsPartFeature(Fillet(definition));
                break;
            case "chamfer":
                feature = AsPartFeature(Chamfer(definition));
                break;
            case "shell":
                feature = AsPartFeature(Shell(definition));
                break;
            case "pattern_rect":
                feature = AsPartFeature(RectangularPattern(definition));
                break;
            case "pattern_circular":
                feature = AsPartFeature(CircularPattern(definition));
                break;
            case "mirror":
                feature = AsPartFeature(Mirror(definition));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported feature '{definition.Kind}'.");
        }

        feature.Name = definition.Name;
        _features[definition.Name] = feature;
        return feature;
    }

    private ExtrudeFeature Extrude(FeatureStatement definition)
    {
        Profile profile = Sketch(Argument(definition, "from")).Profiles.AddForSolid();
        ExtrudeDefinition extrude =
            _component.Features.ExtrudeFeatures.CreateExtrudeDefinition(
                profile,
                Operation(definition));

        if (definition.Args.TryGetValue("extent", out string? extent) &&
            string.Equals(extent, "through", StringComparison.OrdinalIgnoreCase))
        {
            PartFeatureExtentDirectionEnum direction =
                ExtentDirection(definition, allowSymmetric: false);

            extrude.SetThroughAllExtent(direction);
        }
        else
        {
            extrude.SetDistanceExtent(
                _parameters.Length(Argument(definition, "depth", "distance")),
                ExtentDirection(definition, allowSymmetric: true));
        }

        return _component.Features.ExtrudeFeatures.Add(extrude);
    }

    private RevolveFeature Revolve(FeatureStatement definition)
    {
        PlanarSketch sketch =
            Sketch(Argument(definition, "from", "profile"));
        object axis =
            GeometrySelector.RevolveAxis(
                _component,
                sketch,
                Argument(definition, "axis"));
        Profile profile =
            sketch.Profiles.AddForSolid();
        string angle =
            definition.Args.TryGetValue("angle", out string? value) &&
            !string.IsNullOrWhiteSpace(value) ? value : "360";

        return Math.Abs(_parameters.Degrees(angle) - 360.0) < 1e-7
            ? _component.Features.RevolveFeatures.AddFull(
                profile,
                axis,
                Operation(definition))
            : _component.Features.RevolveFeatures.AddByAngle(
                profile,
                axis,
                _parameters.Angle(angle),
                ExtentDirection(definition, allowSymmetric: true),
                Operation(definition));
    }

    private SweepFeature Sweep(FeatureStatement definition)
    {
        Profile profile =
            Sketch(Argument(definition, "profile")).Profiles.AddForSolid();
        PlanarSketch pathSketch = Sketch(Argument(definition, "path"));

        ObjectCollection curves = _app.TransientObjects.CreateObjectCollection();
        foreach (SketchLine line in pathSketch.SketchLines) curves.Add(line);
        foreach (SketchArc arc in pathSketch.SketchArcs) curves.Add(arc);
        foreach (SketchSpline spline in pathSketch.SketchSplines) curves.Add(spline);

        global::Inventor.Path path = _component.Features.CreateSpecifiedPath(curves);
        SweepDefinition sweep =
            _component.Features.SweepFeatures.CreateSweepDefinition(
                SweepTypeEnum.kPathSweepType,
                profile,
                path,
                Operation(definition));

        return _component.Features.SweepFeatures.Add(sweep);
    }

    private LoftFeature Loft(FeatureStatement definition)
    {
        ObjectCollection sections = _app.TransientObjects.CreateObjectCollection();
        var names = new List<string>();

        if (definition.Args.TryGetValue("from", out string? first) &&
            !string.IsNullOrWhiteSpace(first))
            names.Add(first);

        names.AddRange(definition.Items);

        if (names.Count < 2)
            throw new InvalidOperationException(
                "loft requires two or more section sketches.");

        foreach (string name in names)
            sections.Add(Sketch(name).Profiles.AddForSolid());

        LoftDefinition loft =
            _component.Features.LoftFeatures.CreateLoftDefinition(
                sections,
                Operation(definition));

        return _component.Features.LoftFeatures.Add(loft);
    }

    private HoleFeature Hole(FeatureStatement definition)
    {
        PlanarSketch sketch = _component.Sketches.Add(
            GeometrySelector.Plane(_component, Argument(definition, "on")),
            false);
        sketch.Name =
            "__InventorModelHole_" +
            definition.Name;

        string[] coordinates = Argument(definition, "at").Split(',');
        if (coordinates.Length != 2)
            throw new InvalidOperationException(
                "hole 'at' requires two comma-separated coordinates.");

        SketchPoint point = sketch.SketchPoints.Add(
            _app.TransientGeometry.CreatePoint2d(
                _parameters.Cm(coordinates[0]),
                _parameters.Cm(coordinates[1])),
            true);

        DrivePoint(sketch, point, coordinates[0], coordinates[1]);

        ObjectCollection points = _app.TransientObjects.CreateObjectCollection();
        points.Add(point);

        SketchHolePlacementDefinition placement =
            _component.Features.HoleFeatures.CreateSketchPlacementDefinition(points);

        string diameter = _parameters.Length(Argument(definition, "diameter"));

        if (definition.Args.TryGetValue("extent", out string? extent) &&
            string.Equals(extent, "through", StringComparison.OrdinalIgnoreCase))
        {
            return _component.Features.HoleFeatures.AddDrilledByThroughAllExtent(
                placement,
                diameter,
                ExtentDirection(definition, allowSymmetric: false));
        }

        return _component.Features.HoleFeatures.AddDrilledByDistanceExtent(
            placement,
            diameter,
            _parameters.Length(Argument(definition, "depth")),
            ExtentDirection(definition, allowSymmetric: false),
            false,
            "118 deg");
    }

    private void DrivePoint(
        PlanarSketch sketch,
        SketchPoint point,
        string xExpression,
        string yExpression)
    {
        SketchPoint origin = sketch.SketchPoints.Add(
            _app.TransientGeometry.CreatePoint2d(0, 0),
            false);

        sketch.GeometricConstraints.AddGround((SketchEntity)(object)origin);

        if (Math.Abs(_parameters.Mm(xExpression)) < 1e-9)
        {
            sketch.GeometricConstraints.AddVerticalAlign(point, origin);
        }
        else
        {
            sketch.DimensionConstraints
                .AddTwoPointDistance(
                    origin,
                    point,
                    DimensionOrientationEnum.kHorizontalDim,
                    _app.TransientGeometry.CreatePoint2d(
                        point.Geometry.X,
                        point.Geometry.Y - 1),
                    false)
                .Parameter.Expression = _parameters.Length(xExpression);
        }

        if (Math.Abs(_parameters.Mm(yExpression)) < 1e-9)
        {
            sketch.GeometricConstraints.AddHorizontalAlign(point, origin);
        }
        else
        {
            sketch.DimensionConstraints
                .AddTwoPointDistance(
                    origin,
                    point,
                    DimensionOrientationEnum.kVerticalDim,
                    _app.TransientGeometry.CreatePoint2d(
                        point.Geometry.X + 1,
                        point.Geometry.Y),
                    false)
                .Parameter.Expression = _parameters.Length(yExpression);
        }
    }

    private FilletFeature Fillet(FeatureStatement definition)
    {
        FilletDefinition fillet =
            _component.Features.FilletFeatures.CreateFilletDefinition();

        fillet.AddConstantRadiusEdgeSet(
            SelectedEdges(definition),
            _parameters.Length(Argument(definition, "radius")));

        return _component.Features.FilletFeatures.Add(fillet);
    }

    private ChamferFeature Chamfer(FeatureStatement definition)
    {
        return _component.Features.ChamferFeatures.AddUsingDistance(
            SelectedEdges(definition),
            _parameters.Length(Argument(definition, "distance")),
            false,
            false,
            false);
    }

    private ShellFeature Shell(FeatureStatement definition)
    {
        FaceCollection faces = _app.TransientObjects.CreateFaceCollection();
        faces.Add(
            GeometrySelector.Face(
                _component,
                Argument(definition, "faces")));

        ShellDefinition shell =
            _component.Features.ShellFeatures.CreateShellDefinition(
                faces,
                _parameters.Length(Argument(definition, "thickness")),
                ShellDirectionEnum.kInsideShellDirection);

        return _component.Features.ShellFeatures.Add(shell);
    }

    private RectangularPatternFeature RectangularPattern(
        FeatureStatement definition)
    {
        ObjectCollection source = _app.TransientObjects.CreateObjectCollection();
        source.Add(Feature(Argument(definition, "source")));

        string[] counts = Argument(definition, "count").Split(',');
        string[] spacing = Argument(definition, "spacing").Split(',');

        if (counts.Length == 0 || spacing.Length == 0)
            throw new InvalidOperationException(
                "pattern_rect requires count and spacing.");

        RectangularPatternFeatureDefinition pattern =
            _component.Features.RectangularPatternFeatures.CreateDefinition(
                source,
                GeometrySelector.Axis(
                    _component,
                    definition.Args.TryGetValue("axis", out string? firstAxis) &&
                    !string.IsNullOrWhiteSpace(firstAxis)
                        ? firstAxis
                        : "X"),
                true,
                _parameters.Integer(counts[0]),
                _parameters.Length(spacing[0]),
                PatternSpacingTypeEnum.kDefault);

        if (counts.Length > 1 && _parameters.Integer(counts[1]) > 1)
        {
            if (spacing.Length < 2)
                throw new InvalidOperationException(
                    "pattern_rect Y count requires Y spacing.");

            pattern.YDirectionEntity =
                GeometrySelector.Axis(
                    _component,
                    definition.Args.TryGetValue("axis2", out string? secondAxis) &&
                    !string.IsNullOrWhiteSpace(secondAxis)
                        ? secondAxis
                        : "Y");
            pattern.NaturalYDirection = true;
            pattern.YCount = _parameters.Integer(counts[1]);
            pattern.YSpacing = _parameters.Length(spacing[1]);
            pattern.YDirectionSpacingType = PatternSpacingTypeEnum.kDefault;
        }

        pattern.ComputeType = PatternComputeTypeEnum.kIdenticalCompute;
        pattern.XDirectionMidPlanePattern = false;
        pattern.YDirectionMidPlanePattern = false;

        return _component.Features.RectangularPatternFeatures.AddByDefinition(pattern);
    }

    private CircularPatternFeature CircularPattern(
        FeatureStatement definition)
    {
        ObjectCollection source = _app.TransientObjects.CreateObjectCollection();
        source.Add(Feature(Argument(definition, "source")));

        string angle = definition.Args.TryGetValue("angle", out string? value) &&
                       !string.IsNullOrWhiteSpace(value)
            ? _parameters.Angle(value)
            : "360 deg";

        CircularPatternFeatureDefinition pattern =
            _component.Features.CircularPatternFeatures.CreateDefinition(
                source,
                GeometrySelector.Axis(
                    _component,
                    Argument(definition, "axis")),
                true,
                _parameters.Integer(Argument(definition, "count")),
                angle,
                true);

        pattern.ComputeType = PatternComputeTypeEnum.kIdenticalCompute;
        pattern.MidPlanePattern = false;

        return _component.Features.CircularPatternFeatures.AddByDefinition(pattern);
    }

    private MirrorFeature Mirror(FeatureStatement definition)
    {
        ObjectCollection source = _app.TransientObjects.CreateObjectCollection();
        source.Add(Feature(Argument(definition, "source")));

        MirrorFeatureDefinition mirror =
            _component.Features.MirrorFeatures.CreateDefinition(
                source,
                GeometrySelector.WorkPlane(
                    _component,
                    Argument(definition, "plane")),
                PatternComputeTypeEnum.kIdenticalCompute);

        mirror.RemoveOriginal = false;
        mirror.ComputeType = PatternComputeTypeEnum.kIdenticalCompute;

        return _component.Features.MirrorFeatures.AddByDefinition(mirror);
    }

    private EdgeCollection SelectedEdges(
        FeatureStatement definition)
    {
        string selector =
            Argument(
                definition,
                "edges");

        if (string.Equals(
                selector,
                "all",
                StringComparison.OrdinalIgnoreCase))
        {
            return AllEdges();
        }

        if (_component.SurfaceBodies.Count == 0)
            throw new InvalidOperationException(
                "No solid body exists.");

        SurfaceBody body =
            _component.SurfaceBodies[1];

        EdgeCollection result =
            _app.TransientObjects.CreateEdgeCollection();

        var seen =
            new HashSet<int>();

        foreach (string token in
                 selector.Split(','))
        {
            string raw =
                token.Trim();

            if (!int.TryParse(
                    raw,
                    out int index) ||
                index < 1 ||
                index > body.Edges.Count)
            {
                throw new InvalidOperationException(
                    $"Edge index '{raw}' must be between 1 and {body.Edges.Count}.");
            }

            if (seen.Add(index))
                result.Add(
                    body.Edges[index]);
        }

        if (result.Count == 0)
            throw new InvalidOperationException(
                $"{definition.Kind} requires at least one edge index.");

        return result;
    }

    private EdgeCollection AllEdges()
    {
        if (_component.SurfaceBodies.Count == 0)
            throw new InvalidOperationException("No solid body exists.");

        EdgeCollection result = _app.TransientObjects.CreateEdgeCollection();
        foreach (Edge edge in _component.SurfaceBodies[1].Edges)
            result.Add(edge);

        return result;
    }

    private PlanarSketch Sketch(string name)
    {
        return _sketches.TryGetValue(name, out PlanarSketch? sketch) && sketch != null
            ? sketch
            : throw new KeyNotFoundException($"Unknown sketch '{name}'.");
    }

    private PartFeature Feature(string name)
    {
        return _features.TryGetValue(name, out PartFeature? feature) && feature != null
            ? feature
            : throw new KeyNotFoundException($"Unknown feature '{name}'.");
    }

    private static PartFeature AsPartFeature(object feature)
    {
        return (PartFeature)feature;
    }

    private static string Argument(
        FeatureStatement definition,
        params string[] keys)
    {
        foreach (string key in keys)
            if (definition.Args.TryGetValue(key, out string? value) &&
                !string.IsNullOrWhiteSpace(value))
                return value;

        throw new InvalidOperationException(
            $"{definition.Kind} {definition.Name} requires {string.Join("/", keys)}.");
    }

    private static PartFeatureExtentDirectionEnum ExtentDirection(
        FeatureStatement definition,
        bool allowSymmetric)
    {
        string value =
            definition.Args.TryGetValue(
                "direction",
                out string? direction) &&
            !string.IsNullOrWhiteSpace(direction)
                ? direction
                : "positive";

        if (value.Equals("positive", StringComparison.OrdinalIgnoreCase))
            return PartFeatureExtentDirectionEnum.kPositiveExtentDirection;

        if (value.Equals("negative", StringComparison.OrdinalIgnoreCase))
            return PartFeatureExtentDirectionEnum.kNegativeExtentDirection;

        if (allowSymmetric &&
            value.Equals("symmetric", StringComparison.OrdinalIgnoreCase))
        {
            return PartFeatureExtentDirectionEnum.kSymmetricExtentDirection;
        }

        throw new InvalidOperationException(
            allowSymmetric
                ? $"Invalid direction '{value}'. Use positive, negative, or symmetric."
                : $"Invalid direction '{value}'. Use positive or negative.");
    }

    private static PartFeatureOperationEnum Operation(FeatureStatement definition)
    {
        string operation = definition.Args.TryGetValue(
            "operation",
            out string? value) &&
            !string.IsNullOrWhiteSpace(value)
            ? value
            : "join";

        return operation == "cut"
            ? PartFeatureOperationEnum.kCutOperation
            : operation == "new"
                ? PartFeatureOperationEnum.kNewBodyOperation
                : PartFeatureOperationEnum.kJoinOperation;
    }
}
