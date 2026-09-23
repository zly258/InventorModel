using System;
using Inventor;

namespace InventorModel.Inventor;

internal static class GeometrySelector
{
    public static object Plane(PartComponentDefinition c,string selector)
    {
        if(selector.Equals("XY",StringComparison.OrdinalIgnoreCase))return c.WorkPlanes[3];
        if(selector.Equals("XZ",StringComparison.OrdinalIgnoreCase))return c.WorkPlanes[2];
        if(selector.Equals("YZ",StringComparison.OrdinalIgnoreCase))return c.WorkPlanes[1];
        if(selector.StartsWith("face:",StringComparison.OrdinalIgnoreCase))
        {
            string payload=selector.Substring("face:".Length);
            if(payload.StartsWith("index:",StringComparison.OrdinalIgnoreCase))
                return Face(c,payload);
            int split=payload.LastIndexOf(':');
            return Face(c,split>=0?payload.Substring(split+1):payload);
        }
        throw new InvalidOperationException($"Unknown sketch plane '{selector}'.");
    }

    public static Face Face(
        PartComponentDefinition c,
        string selector)
    {
        if (c.SurfaceBodies.Count == 0)
            throw new InvalidOperationException(
                "No solid body exists.");

        SurfaceBody body =
            c.SurfaceBodies[1];

        if (selector.StartsWith(
                "index:",
                StringComparison.OrdinalIgnoreCase))
        {
            string raw =
                selector.Substring(
                    "index:".Length);

            if (!int.TryParse(
                    raw,
                    out int index) ||
                index < 1 ||
                index > body.Faces.Count)
            {
                throw new InvalidOperationException(
                    $"Face index must be between 1 and {body.Faces.Count}.");
            }

            Face indexed =
                body.Faces[index];

            if (indexed.SurfaceType !=
                SurfaceTypeEnum.kPlaneSurface)
            {
                throw new InvalidOperationException(
                    $"Face {index} is not planar and cannot host a planar sketch/hole selector.");
            }

            return indexed;
        }

        var direction =
            Direction(selector);

        Face? best = null;
        double bestDot =
            double.NegativeInfinity;
        double bestProjection =
            double.NegativeInfinity;

        foreach (Face face in body.Faces)
        {
            if (face.SurfaceType !=
                SurfaceTypeEnum.kPlaneSurface)
                continue;

            var plane =
                (Plane)face.Geometry;

            double dot =
                plane.Normal.X *
                direction.Item1 +
                plane.Normal.Y *
                direction.Item2 +
                plane.Normal.Z *
                direction.Item3;

            if (dot < 0.8)
                continue;

            double projection =
                FaceProjection(
                    face,
                    direction);

            if (dot > bestDot + 1e-9 ||
                (Math.Abs(
                     dot - bestDot) <= 1e-9 &&
                 projection >
                 bestProjection))
            {
                bestDot = dot;
                bestProjection =
                    projection;
                best = face;
            }
        }

        if (best == null)
        {
            throw new InvalidOperationException(
                $"No planar '{selector}' face found.");
        }

        return best;
    }

    private static double FaceProjection(
        Face face,
        Tuple<double,double,double> direction)
    {
        double sum = 0;
        int count = 0;

        foreach (Edge edge in face.Edges)
        {
            AddVertex(
                edge.StartVertex,
                direction,
                ref sum,
                ref count);
            AddVertex(
                edge.StopVertex,
                direction,
                ref sum,
                ref count);
        }

        return count > 0
            ? sum / count
            : double.NegativeInfinity;
    }

    private static void AddVertex(
        Vertex? vertex,
        Tuple<double,double,double> direction,
        ref double sum,
        ref int count)
    {
        if (vertex == null)
            return;

        Point point =
            vertex.Point;

        sum +=
            point.X * direction.Item1 +
            point.Y * direction.Item2 +
            point.Z * direction.Item3;
        count++;
    }

    public static WorkAxis Axis(PartComponentDefinition c,string axis)
    {
        switch(axis.ToUpperInvariant()){case "X":return c.WorkAxes[1];case "Y":return c.WorkAxes[2];case "Z":return c.WorkAxes[3];default:throw new InvalidOperationException($"Unknown axis '{axis}'.");}
    }

    public static object RevolveAxis(
        PartComponentDefinition component,
        PlanarSketch sketch,
        string selector)
    {
        if (selector.Equals("X", StringComparison.OrdinalIgnoreCase) ||
            selector.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
            selector.Equals("Z", StringComparison.OrdinalIgnoreCase))
        {
            return Axis(component, selector);
        }

        if (selector.StartsWith("line:", StringComparison.OrdinalIgnoreCase))
        {
            string raw = selector.Substring("line:".Length);
            if (!int.TryParse(raw, out int index) ||
                index < 1 ||
                index > sketch.SketchLines.Count)
            {
                throw new InvalidOperationException(
                    $"Revolve axis line index must be between 1 and {sketch.SketchLines.Count}.");
            }

            SketchLine line = sketch.SketchLines[index];
            line.Construction = true;
            return line;
        }

        throw new InvalidOperationException(
            $"Unknown revolve axis '{selector}'. Use X, Y, Z, or line:<1-based-index>.");
    }

    public static WorkPlane WorkPlane(PartComponentDefinition c,string plane)=>(WorkPlane)Plane(c,plane);

    private static Tuple<double,double,double> Direction(string side)
    {
        switch(side.ToLowerInvariant())
        {
            case "top":return Tuple.Create(0d,0d,1d); case "bottom":return Tuple.Create(0d,0d,-1d);
            case "right":return Tuple.Create(1d,0d,0d); case "left":return Tuple.Create(-1d,0d,0d);
            case "front":return Tuple.Create(0d,-1d,0d); case "back":return Tuple.Create(0d,1d,0d);
            default:throw new InvalidOperationException($"Unknown face side '{side}'.");
        }
    }
}
