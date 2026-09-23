using System;
using System.Collections.Generic;
using System.Text;
using Inventor;
using InventorModel.Core.Diagnostics;

namespace InventorModel.Inventor;

public sealed class ModelInspectionResult
{
    public string Part { get; set; } = string.Empty;
    public int BodyCount { get; set; }
    public int SketchCount { get; set; }
    public int FeatureCount { get; set; }
    public int UnderConstrainedSketchCount { get; set; }
    public int UnhealthyFeatureCount { get; set; }
    public ModelSizeMm SizeMm { get; set; } = new ModelSizeMm();
    public List<ModelParameterInfo> Parameters { get; } =
        new List<ModelParameterInfo>();
    public List<ModelSketchInfo> Sketches { get; } =
        new List<ModelSketchInfo>();
    public List<ModelFeatureInfo> Features { get; } =
        new List<ModelFeatureInfo>();
}

public sealed class ModelSizeMm
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class ModelParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
}

public sealed class ModelSketchInfo
{
    public string Name { get; set; } = string.Empty;
    public string ConstraintStatus { get; set; } = string.Empty;
}

public sealed class ModelFeatureInfo
{
    public string Name { get; set; } = string.Empty;
    public bool Suppressed { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
}

public sealed class ModelInspectionSummary
{
    public string Part { get; set; } = string.Empty;
    public int BodyCount { get; set; }
    public int SketchCount { get; set; }
    public int FeatureCount { get; set; }
    public int UnderConstrainedSketchCount { get; set; }
    public int UnhealthyFeatureCount { get; set; }
    public ModelSizeMm SizeMm { get; set; } = new ModelSizeMm();
}

public sealed class ModelGeometryFilter
{
    public string Entity { get; set; } = "all";
    public string? CurveType { get; set; }
    public string? SurfaceType { get; set; }
    public string? Axis { get; set; }
    public double? NearX { get; set; }
    public double? NearY { get; set; }
    public double? NearZ { get; set; }
    public double ToleranceMm { get; set; } = 1.0;
    public double? MinLengthMm { get; set; }
    public double? MaxLengthMm { get; set; }
    public double? RadiusMm { get; set; }
    public int MaxEdges { get; set; } = 64;
    public int MaxFaces { get; set; } = 32;
}

public sealed class ModelGeometryInspectionResult
{
    public int BodyIndex { get; set; }
    public int TotalBodies { get; set; }
    public int TotalEdges { get; set; }
    public int ReturnedEdges { get; set; }
    public int OmittedEdges { get; set; }
    public int TotalFaces { get; set; }
    public int ReturnedFaces { get; set; }
    public int OmittedFaces { get; set; }
    public bool Truncated { get; set; }
    public List<ModelEdgeInfo> Edges { get; } =
        new List<ModelEdgeInfo>();
    public List<ModelFaceInfo> Faces { get; } =
        new List<ModelFaceInfo>();
}

public sealed class ModelPointMm
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class ModelVector
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class ModelEdgeInfo
{
    public int Index { get; set; }
    public string CurveType { get; set; } = string.Empty;
    public double? LengthMm { get; set; }
    public double? RadiusMm { get; set; }
    public ModelPointMm? Center { get; set; }
    public ModelPointMm Start { get; set; } = new ModelPointMm();
    public ModelPointMm Stop { get; set; } = new ModelPointMm();
}

public sealed class ModelFaceInfo
{
    public int Index { get; set; }
    public string SurfaceType { get; set; } = string.Empty;
    public int EdgeCount { get; set; }
    public int LoopCount { get; set; }
    public ModelVector? Normal { get; set; }
    public ModelPointMm? Center { get; set; }
}

public sealed class ModelInspector
{
    public ModelInspectionResult InspectResult(
        PartDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        PartComponentDefinition component =
            document.ComponentDefinition;
        Box box = component.RangeBox;

        var result = new ModelInspectionResult
        {
            Part = document.DisplayName ?? string.Empty,
            BodyCount = component.SurfaceBodies.Count,
            SketchCount = component.Sketches.Count,
            FeatureCount = component.Features.Count,
            SizeMm = new ModelSizeMm
            {
                X = RoundMm(
                    box.MaxPoint.X -
                    box.MinPoint.X),
                Y = RoundMm(
                    box.MaxPoint.Y -
                    box.MinPoint.Y),
                Z = RoundMm(
                    box.MaxPoint.Z -
                    box.MinPoint.Z)
            }
        };

        foreach (UserParameter parameter in
                 component.Parameters.UserParameters)
        {
            try
            {
                result.Parameters.Add(
                    new ModelParameterInfo
                    {
                        Name =
                            parameter.Name ??
                            string.Empty,
                        Expression =
                            parameter.Expression ??
                            string.Empty,
                        Units =
                            parameter.get_Units() ??
                            string.Empty
                    });
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "Inventor.Inspect",
                    "A user parameter could not be inspected.",
                    ex);
            }
        }

        foreach (PlanarSketch sketch in
                 component.Sketches)
        {
            try
            {
                string constraintStatus =
                    sketch.ConstraintStatus.ToString();

                result.Sketches.Add(
                    new ModelSketchInfo
                    {
                        Name =
                            sketch.Name ??
                            string.Empty,
                        ConstraintStatus =
                            constraintStatus
                    });

                if (constraintStatus.IndexOf(
                        "Fully",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    result.UnderConstrainedSketchCount++;
                }
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "Inventor.Inspect",
                    "A sketch could not be inspected.",
                    ex);
            }
        }

        foreach (PartFeature feature in
                 component.Features)
        {
            try
            {
                string healthStatus =
                    feature.HealthStatus.ToString();

                result.Features.Add(
                    new ModelFeatureInfo
                    {
                        Name =
                            feature.Name ??
                            string.Empty,
                        Suppressed =
                            feature.Suppressed,
                        HealthStatus =
                            healthStatus
                    });

                if (!IsHealthyFeatureStatus(healthStatus))
                    result.UnhealthyFeatureCount++;
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "Inventor.Inspect",
                    "A Part feature could not be inspected.",
                    ex);
            }
        }

        return result;
    }

    public ModelInspectionSummary InspectSummary(
        PartDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        PartComponentDefinition component =
            document.ComponentDefinition;
        Box box = component.RangeBox;

        int underConstrainedCount = 0;
        foreach (PlanarSketch sketch in component.Sketches)
        {
            try
            {
                if (sketch.ConstraintStatus.ToString().IndexOf(
                        "Fully",
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    underConstrainedCount++;
                }
            }
            catch
            {
            }
        }

        int unhealthyCount = 0;
        foreach (PartFeature feature in component.Features)
        {
            try
            {
                if (!IsHealthyFeatureStatus(feature.HealthStatus.ToString()))
                    unhealthyCount++;
            }
            catch
            {
            }
        }

        return new ModelInspectionSummary
        {
            Part = document.DisplayName ?? string.Empty,
            BodyCount = component.SurfaceBodies.Count,
            SketchCount = component.Sketches.Count,
            FeatureCount = component.Features.Count,
            UnderConstrainedSketchCount = underConstrainedCount,
            UnhealthyFeatureCount = unhealthyCount,
            SizeMm = new ModelSizeMm
            {
                X = RoundMm(box.MaxPoint.X - box.MinPoint.X),
                Y = RoundMm(box.MaxPoint.Y - box.MinPoint.Y),
                Z = RoundMm(box.MaxPoint.Z - box.MinPoint.Z)
            }
        };
    }

    public object InspectDetailed(
        PartDocument document,
        string detail = "summary")
    {
        if (string.IsNullOrWhiteSpace(detail) ||
            detail.Equals("summary", StringComparison.OrdinalIgnoreCase))
        {
            return InspectSummary(document);
        }

        ModelInspectionResult full = InspectResult(document);

        if (detail.Equals("parameters", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                part = full.Part,
                bodyCount = full.BodyCount,
                sketchCount = full.SketchCount,
                featureCount = full.FeatureCount,
                underConstrainedSketchCount = full.UnderConstrainedSketchCount,
                unhealthyFeatureCount = full.UnhealthyFeatureCount,
                sizeMm = full.SizeMm,
                parameters = full.Parameters
            };
        }

        if (detail.Equals("sketches", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                part = full.Part,
                bodyCount = full.BodyCount,
                sketchCount = full.SketchCount,
                featureCount = full.FeatureCount,
                underConstrainedSketchCount = full.UnderConstrainedSketchCount,
                unhealthyFeatureCount = full.UnhealthyFeatureCount,
                sizeMm = full.SizeMm,
                sketches = full.Sketches
            };
        }

        if (detail.Equals("features", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                part = full.Part,
                bodyCount = full.BodyCount,
                sketchCount = full.SketchCount,
                featureCount = full.FeatureCount,
                underConstrainedSketchCount = full.UnderConstrainedSketchCount,
                unhealthyFeatureCount = full.UnhealthyFeatureCount,
                sizeMm = full.SizeMm,
                features = full.Features
            };
        }

        return full;
    }

    public ModelGeometryInspectionResult InspectGeometry(
        PartDocument document,
        int maxEdges = 64,
        int maxFaces = 32) =>
        InspectGeometry(
            document,
            new ModelGeometryFilter
            {
                MaxEdges = maxEdges,
                MaxFaces = maxFaces
            });

    public ModelGeometryInspectionResult InspectGeometry(
        PartDocument document,
        ModelGeometryFilter filter)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        filter ??= new ModelGeometryFilter();

        PartComponentDefinition component =
            document.ComponentDefinition;

        if (component.SurfaceBodies.Count == 0)
            throw new InvalidOperationException(
                "The part has no solid body to inspect.");

        SurfaceBody body =
            component.SurfaceBodies[1];

        int edgeLimit =
            Math.Max(1, Math.Min(256, filter.MaxEdges));
        int faceLimit =
            Math.Max(1, Math.Min(128, filter.MaxFaces));

        var result =
            new ModelGeometryInspectionResult
            {
                BodyIndex = 1,
                TotalBodies =
                    component.SurfaceBodies.Count,
                TotalEdges =
                    body.Edges.Count,
                TotalFaces =
                    body.Faces.Count
            };

        int matchedEdges = 0;
        bool skipEdges =
            filter.Entity.Equals("face", StringComparison.OrdinalIgnoreCase);

        if (!skipEdges)
        {
            for (int i = 1; i <= body.Edges.Count; i++)
            {
                Edge edge = body.Edges[i];
                ModelEdgeInfo info = ReadEdge(edge, i);
                if (MatchesEdgeFilter(info, edge, filter))
                {
                    matchedEdges++;
                    if (result.Edges.Count < edgeLimit)
                        result.Edges.Add(info);
                }
            }
        }

        int matchedFaces = 0;
        bool skipFaces =
            filter.Entity.Equals("edge", StringComparison.OrdinalIgnoreCase);

        if (!skipFaces)
        {
            for (int i = 1; i <= body.Faces.Count; i++)
            {
                Face face = body.Faces[i];
                ModelFaceInfo info = ReadFace(face, i);
                if (MatchesFaceFilter(info, face, filter))
                {
                    matchedFaces++;
                    if (result.Faces.Count < faceLimit)
                        result.Faces.Add(info);
                }
            }
        }

        result.ReturnedEdges = result.Edges.Count;
        result.ReturnedFaces = result.Faces.Count;
        result.OmittedEdges = Math.Max(0, matchedEdges - result.ReturnedEdges);
        result.OmittedFaces = Math.Max(0, matchedFaces - result.ReturnedFaces);
        result.Truncated = result.OmittedEdges > 0 || result.OmittedFaces > 0;

        return result;
    }

    public string Inspect(
        PartDocument document)
    {
        ModelInspectionResult result =
            InspectResult(document);

        var builder = new StringBuilder();
        builder.AppendLine(
            "part " +
            result.Part);
        builder.AppendLine(
            "bodies " +
            result.BodyCount);
        builder.AppendLine(
            "sketches " +
            result.SketchCount);
        builder.AppendLine(
            "features " +
            result.FeatureCount);
        builder.AppendLine(
            "under_constrained_sketches " +
            result.UnderConstrainedSketchCount);
        builder.AppendLine(
            "unhealthy_features " +
            result.UnhealthyFeatureCount);
        builder.AppendLine(
            "size_mm " +
            result.SizeMm.X.ToString("0.###") +
            " " +
            result.SizeMm.Y.ToString("0.###") +
            " " +
            result.SizeMm.Z.ToString("0.###"));

        builder.AppendLine("parameters");
        foreach (ModelParameterInfo parameter in
                 result.Parameters)
        {
            builder.AppendLine(
                "  " +
                parameter.Name +
                " = " +
                parameter.Expression);
        }

        builder.AppendLine("sketches");
        foreach (ModelSketchInfo sketch in
                 result.Sketches)
        {
            builder.AppendLine(
                "  " +
                sketch.Name +
                " constraint_status=" +
                sketch.ConstraintStatus);
        }

        builder.AppendLine("feature_tree");
        foreach (ModelFeatureInfo feature in
                 result.Features)
        {
            builder.AppendLine(
                "  " +
                feature.Name +
                " suppressed=" +
                feature.Suppressed +
                " health=" +
                feature.HealthStatus);
        }

        return builder.ToString();
    }

    private static ModelEdgeInfo ReadEdge(
        Edge edge,
        int index)
    {
        Vertex? startVertex =
            SafeVertex(() => edge.StartVertex);
        Vertex? stopVertex =
            SafeVertex(() => edge.StopVertex);

        ModelPointMm start =
            ReadVertex(startVertex);
        ModelPointMm stop =
            ReadVertex(stopVertex);

        double? lengthMm = null;
        double? radiusMm = null;
        ModelPointMm? center = null;

        if (edge.GeometryType ==
            CurveTypeEnum.kLineCurve)
        {
            double dx = stop.X - start.X;
            double dy = stop.Y - start.Y;
            double dz = stop.Z - start.Z;

            lengthMm =
                Math.Round(
                    Math.Sqrt(
                        dx * dx +
                        dy * dy +
                        dz * dz),
                    6,
                    MidpointRounding.AwayFromZero);
        }
        else if (edge.GeometryType == CurveTypeEnum.kCircleCurve)
        {
            try
            {
                Circle circle = (Circle)edge.Geometry;
                radiusMm = RoundMm(circle.Radius);
                center = new ModelPointMm
                {
                    X = RoundMm(circle.Center.X),
                    Y = RoundMm(circle.Center.Y),
                    Z = RoundMm(circle.Center.Z)
                };
                lengthMm = Math.Round(2 * Math.PI * radiusMm.Value, 6, MidpointRounding.AwayFromZero);
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning("Inventor.Inspect", "Circle geometry inspection failed.", ex);
            }
        }
        else if (edge.GeometryType == CurveTypeEnum.kCircularArcCurve)
        {
            try
            {
                Arc3d arc = (Arc3d)edge.Geometry;
                radiusMm = RoundMm(arc.Radius);
                center = new ModelPointMm
                {
                    X = RoundMm(arc.Center.X),
                    Y = RoundMm(arc.Center.Y),
                    Z = RoundMm(arc.Center.Z)
                };
                double dx = stop.X - start.X;
                double dy = stop.Y - start.Y;
                double dz = stop.Z - start.Z;
                double chord = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (radiusMm.Value > 0)
                {
                    double sinHalf = Math.Min(1.0, (chord / 2.0) / radiusMm.Value);
                    double sweep = 2 * Math.Asin(sinHalf);
                    lengthMm = Math.Round(radiusMm.Value * sweep, 6, MidpointRounding.AwayFromZero);
                }
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning("Inventor.Inspect", "Arc geometry inspection failed.", ex);
            }
        }

        return new ModelEdgeInfo
        {
            Index = index,
            CurveType =
                edge.GeometryType.ToString(),
            LengthMm = lengthMm,
            RadiusMm = radiusMm,
            Center = center,
            Start = start,
            Stop = stop
        };
    }

    private static ModelFaceInfo ReadFace(
        Face face,
        int index)
    {
        ModelVector? normal = null;
        ModelPointMm? center = null;

        try
        {
            if (face.SurfaceType ==
                SurfaceTypeEnum.kPlaneSurface)
            {
                Plane plane =
                    (Plane)face.Geometry;

                normal =
                    new ModelVector
                    {
                        X =
                            Math.Round(
                                plane.Normal.X,
                                6),
                        Y =
                            Math.Round(
                                plane.Normal.Y,
                                6),
                        Z =
                            Math.Round(
                                plane.Normal.Z,
                                6)
                    };

                center = new ModelPointMm
                {
                    X = RoundMm(plane.RootPoint.X),
                    Y = RoundMm(plane.RootPoint.Y),
                    Z = RoundMm(plane.RootPoint.Z)
                };
            }
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "Inventor.Inspect",
                "A face normal could not be inspected.",
                ex);
        }

        return new ModelFaceInfo
        {
            Index = index,
            SurfaceType =
                face.SurfaceType.ToString(),
            EdgeCount =
                face.Edges.Count,
            LoopCount =
                face.EdgeLoops.Count,
            Normal = normal,
            Center = center
        };
    }

    private static bool MatchesEdgeFilter(
        ModelEdgeInfo info,
        Edge edge,
        ModelGeometryFilter filter)
    {
        if (filter.Entity.Equals("face", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(filter.CurveType))
        {
            if (info.CurveType.IndexOf(
                    filter.CurveType,
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        if (filter.RadiusMm.HasValue)
        {
            if (!info.RadiusMm.HasValue ||
                Math.Abs(info.RadiusMm.Value - filter.RadiusMm.Value) > filter.ToleranceMm)
            {
                return false;
            }
        }

        if (filter.MinLengthMm.HasValue)
        {
            if (!info.LengthMm.HasValue ||
                info.LengthMm.Value < filter.MinLengthMm.Value - 1e-4)
            {
                return false;
            }
        }

        if (filter.MaxLengthMm.HasValue)
        {
            if (!info.LengthMm.HasValue ||
                info.LengthMm.Value > filter.MaxLengthMm.Value + 1e-4)
            {
                return false;
            }
        }

        if (filter.NearX.HasValue || filter.NearY.HasValue || filter.NearZ.HasValue)
        {
            ModelPointMm refPoint = info.Center ?? new ModelPointMm
            {
                X = (info.Start.X + info.Stop.X) / 2.0,
                Y = (info.Start.Y + info.Stop.Y) / 2.0,
                Z = (info.Start.Z + info.Stop.Z) / 2.0
            };

            if (filter.NearX.HasValue &&
                Math.Abs(refPoint.X - filter.NearX.Value) > filter.ToleranceMm &&
                Math.Abs(info.Start.X - filter.NearX.Value) > filter.ToleranceMm &&
                Math.Abs(info.Stop.X - filter.NearX.Value) > filter.ToleranceMm)
            {
                return false;
            }

            if (filter.NearY.HasValue &&
                Math.Abs(refPoint.Y - filter.NearY.Value) > filter.ToleranceMm &&
                Math.Abs(info.Start.Y - filter.NearY.Value) > filter.ToleranceMm &&
                Math.Abs(info.Stop.Y - filter.NearY.Value) > filter.ToleranceMm)
            {
                return false;
            }

            if (filter.NearZ.HasValue &&
                Math.Abs(refPoint.Z - filter.NearZ.Value) > filter.ToleranceMm &&
                Math.Abs(info.Start.Z - filter.NearZ.Value) > filter.ToleranceMm &&
                Math.Abs(info.Stop.Z - filter.NearZ.Value) > filter.ToleranceMm)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Axis))
        {
            string axis = filter.Axis.Trim().ToUpperInvariant();
            if (edge.GeometryType == CurveTypeEnum.kLineCurve)
            {
                double dx = info.Stop.X - info.Start.X;
                double dy = info.Stop.Y - info.Start.Y;
                double dz = info.Stop.Z - info.Start.Z;
                double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (len > 1e-6)
                {
                    double dot = axis == "X" ? Math.Abs(dx / len) :
                                 axis == "Y" ? Math.Abs(dy / len) :
                                 axis == "Z" ? Math.Abs(dz / len) : 0;
                    if (dot < 0.9) return false;
                }
            }
            else if (edge.GeometryType == CurveTypeEnum.kCircleCurve)
            {
                try
                {
                    Circle circle = (Circle)edge.Geometry;
                    UnitVector norm = circle.Normal;
                    double dot = axis == "X" ? Math.Abs(norm.X) :
                                 axis == "Y" ? Math.Abs(norm.Y) :
                                 axis == "Z" ? Math.Abs(norm.Z) : 0;
                    if (dot < 0.9) return false;
                }
                catch
                {
                }
            }
        }

        return true;
    }

    private static bool MatchesFaceFilter(
        ModelFaceInfo info,
        Face face,
        ModelGeometryFilter filter)
    {
        if (filter.Entity.Equals("edge", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(filter.SurfaceType))
        {
            if (info.SurfaceType.IndexOf(
                    filter.SurfaceType,
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Axis) && info.Normal is { } normal)
        {
            string axis = filter.Axis.Trim().ToUpperInvariant();
            double dot = axis == "X" ? Math.Abs(normal.X) :
                         axis == "Y" ? Math.Abs(normal.Y) :
                         axis == "Z" ? Math.Abs(normal.Z) : 0;
            if (dot < 0.9) return false;
        }

        if ((filter.NearX.HasValue || filter.NearY.HasValue || filter.NearZ.HasValue) && info.Center is { } center)
        {
            if (filter.NearX.HasValue &&
                Math.Abs(center.X - filter.NearX.Value) > filter.ToleranceMm)
            {
                return false;
            }
            if (filter.NearY.HasValue &&
                Math.Abs(center.Y - filter.NearY.Value) > filter.ToleranceMm)
            {
                return false;
            }
            if (filter.NearZ.HasValue &&
                Math.Abs(center.Z - filter.NearZ.Value) > filter.ToleranceMm)
            {
                return false;
            }
        }

        return true;
    }

    private static Vertex? SafeVertex(
        Func<Vertex> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }

    private static ModelPointMm ReadVertex(
        Vertex? vertex)
    {
        if (vertex == null)
            return new ModelPointMm();

        try
        {
            Point point =
                vertex.Point;

            return new ModelPointMm
            {
                X = RoundMm(point.X),
                Y = RoundMm(point.Y),
                Z = RoundMm(point.Z)
            };
        }
        catch
        {
            return new ModelPointMm();
        }
    }

    private static bool IsHealthyFeatureStatus(
        string value) =>
        string.IsNullOrWhiteSpace(value) ||
        value.Equals(
            "kUpToDateHealthStatus",
            StringComparison.OrdinalIgnoreCase) ||
        value.Equals(
            "kUnknownHealthStatus",
            StringComparison.OrdinalIgnoreCase);

    private static double RoundMm(
        double centimeters) =>
        Math.Round(
            centimeters * 10.0,
            6,
            MidpointRounding.AwayFromZero);
}
