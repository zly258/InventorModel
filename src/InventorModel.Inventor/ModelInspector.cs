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
                result.Sketches.Add(
                    new ModelSketchInfo
                    {
                        Name =
                            sketch.Name ??
                            string.Empty,
                        ConstraintStatus =
                            sketch.ConstraintStatus.ToString()
                    });
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
                result.Features.Add(
                    new ModelFeatureInfo
                    {
                        Name =
                            feature.Name ??
                            string.Empty,
                        Suppressed =
                            feature.Suppressed,
                        HealthStatus =
                            feature.HealthStatus.ToString()
                    });
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

    public ModelGeometryInspectionResult InspectGeometry(
        PartDocument document,
        int maxEdges = 128,
        int maxFaces = 64)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        PartComponentDefinition component =
            document.ComponentDefinition;

        if (component.SurfaceBodies.Count == 0)
            throw new InvalidOperationException(
                "The part has no solid body to inspect.");

        SurfaceBody body =
            component.SurfaceBodies[1];

        int edgeLimit =
            Math.Max(1, Math.Min(256, maxEdges));
        int faceLimit =
            Math.Max(1, Math.Min(128, maxFaces));

        var result =
            new ModelGeometryInspectionResult
            {
                BodyIndex = 1,
                TotalBodies =
                    component.SurfaceBodies.Count,
                TotalEdges =
                    body.Edges.Count,
                ReturnedEdges =
                    Math.Min(
                        body.Edges.Count,
                        edgeLimit),
                TotalFaces =
                    body.Faces.Count,
                ReturnedFaces =
                    Math.Min(
                        body.Faces.Count,
                        faceLimit)
            };

        result.OmittedEdges =
            Math.Max(
                0,
                result.TotalEdges -
                result.ReturnedEdges);
        result.OmittedFaces =
            Math.Max(
                0,
                result.TotalFaces -
                result.ReturnedFaces);
        result.Truncated =
            result.OmittedEdges > 0 ||
            result.OmittedFaces > 0;

        for (int i = 1;
             i <= result.ReturnedEdges;
             i++)
        {
            result.Edges.Add(
                ReadEdge(
                    body.Edges[i],
                    i));
        }

        for (int i = 1;
             i <= result.ReturnedFaces;
             i++)
        {
            result.Faces.Add(
                ReadFace(
                    body.Faces[i],
                    i));
        }

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

        return new ModelEdgeInfo
        {
            Index = index,
            CurveType =
                edge.GeometryType.ToString(),
            LengthMm = lengthMm,
            Start = start,
            Stop = stop
        };
    }

    private static ModelFaceInfo ReadFace(
        Face face,
        int index)
    {
        ModelVector? normal = null;

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
            Normal = normal
        };
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

    private static double RoundMm(
        double centimeters) =>
        Math.Round(
            centimeters * 10.0,
            6,
            MidpointRounding.AwayFromZero);
}
