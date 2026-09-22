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
            return Face(c,selector.Substring(selector.LastIndexOf(':')+1));
        throw new InvalidOperationException($"Unknown sketch plane '{selector}'.");
    }

    public static Face Face(PartComponentDefinition c,string side)
    {
        if(c.SurfaceBodies.Count==0)throw new InvalidOperationException("No solid body exists.");
        var d=Direction(side); Face? best=null; var score=double.NegativeInfinity;
        foreach(Face f in c.SurfaceBodies[1].Faces)
        {
            if(f.SurfaceType!=SurfaceTypeEnum.kPlaneSurface)continue;
            var p=(Plane)f.Geometry; var dot=p.Normal.X*d.Item1+p.Normal.Y*d.Item2+p.Normal.Z*d.Item3;
            if(dot>score){score=dot;best=f;}
        }
        if(best==null||score<0.8)throw new InvalidOperationException($"No planar '{side}' face found.");
        return best;
    }

    public static WorkAxis Axis(PartComponentDefinition c,string axis)
    {
        switch(axis.ToUpperInvariant()){case "X":return c.WorkAxes[1];case "Y":return c.WorkAxes[2];case "Z":return c.WorkAxes[3];default:throw new InvalidOperationException($"Unknown axis '{axis}'.");}
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
