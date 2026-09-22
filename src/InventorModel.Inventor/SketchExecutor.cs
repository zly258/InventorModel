using System;
using System.Collections.Generic;
using System.Linq;
using Inventor;
using InventorModel.Core.Dsl;
using DslParameterTable = InventorModel.Core.Dsl.ParameterTable;

namespace InventorModel.Inventor;

internal sealed class SketchExecutor
{
    private readonly Application _app; private readonly PartComponentDefinition _c; private readonly DslParameterTable _p;
    private readonly TransientGeometry _g;
    public SketchExecutor(Application app,PartComponentDefinition c,DslParameterTable p){_app=app;_c=c;_p=p;_g=app.TransientGeometry;}

    public PlanarSketch Build(SketchStatement def)
    {
        var sketch=_c.Sketches.Add(GeometrySelector.Plane(_c,def.Plane),false);sketch.Name=def.Name;
        var entities=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
        foreach(var line in def.Lines)Execute(sketch,line,entities);
        return sketch;
    }

    private void Execute(PlanarSketch s,SketchLineStatement x,IDictionary<string,object> named)
    {
        switch(x.Kind)
        {
            case "point":Put(named,x.Name,s.SketchPoints.Add(P(x.Args,0),false));break;
            case "line":Put(named,x.Name,s.SketchLines.AddByTwoPoints(P(x.Args,0),P(x.Args,2)));break;
            case "circle":
            {
                var c=s.SketchCircles.AddByCenterRadius(P(x.Args,0),_p.Cm(x.Args[2]));Put(named,x.Name,c);
                var d=s.DimensionConstraints.AddRadius((SketchEntity)(object)c,_g.CreatePoint2d(c.CenterSketchPoint.Geometry.X+1,c.CenterSketchPoint.Geometry.Y+1),false);
                d.Parameter.Expression=_p.Length(x.Args[2]);break;
            }
            case "arc":
            {
                var a=s.SketchArcs.AddByCenterStartSweepAngle(P(x.Args,0),_p.Cm(x.Args[2]),Rad(x.Args[3]),Rad(x.Args[4])-Rad(x.Args[3]));Put(named,x.Name,a);
                var d=s.DimensionConstraints.AddRadius((SketchEntity)(object)a,_g.CreatePoint2d(a.CenterSketchPoint.Geometry.X+1,a.CenterSketchPoint.Geometry.Y+1),false);
                d.Parameter.Expression=_p.Length(x.Args[2]);break;
            }
            case "ellipse":Put(named,x.Name,((dynamic)s.SketchEllipses).Add(P(x.Args,0),_g.CreateUnitVector2d(1,0),_p.Cm(x.Args[2]),_p.Cm(x.Args[3])));break;
            case "rect":Rectangle(s,x.Args,false);break;
            case "centerrect":Rectangle(s,x.Args,true);break;
            case "slot":Slot(s,x.Args);break;
            case "polygon":Polygon(s,x.Args);break;
            case "spline":Spline(s,x,named);break;
            case "constraint":Constraint(s,x.Args,named);break;
            case "dim":Dimension(s,x.Args,named);break;
            default:throw new InvalidOperationException($"Unsupported sketch command '{x.Kind}'.");
        }
    }

    private void Constraint(PlanarSketch s,IReadOnlyList<string> a,IDictionary<string,object> n)
    {
        if(a.Count<2)throw new InvalidOperationException("constraint <kind> <entity> [entity]");
        dynamic g=s.GeometricConstraints;var kind=a[0].ToLowerInvariant();dynamic one=Get(n,a[1]);
        switch(kind)
        {
            case "horizontal":g.AddHorizontal(one,false);break;case "vertical":g.AddVertical(one,false);break;
            case "parallel":g.AddParallel(one,Get(n,a[2]),false,false);break;case "perpendicular":g.AddPerpendicular(one,Get(n,a[2]));break;
            case "tangent":g.AddTangent(one,Get(n,a[2]));break;case "concentric":g.AddConcentric(one,Get(n,a[2]));break;
            case "equal":g.AddEqualLength(one,Get(n,a[2]));break;case "coincident":g.AddCoincident(one,Get(n,a[2]));break;
            default:throw new InvalidOperationException($"Unsupported constraint '{kind}'.");
        }
    }

    private void Dimension(PlanarSketch s,IReadOnlyList<string> a,IDictionary<string,object> n)
    {
        if(a.Count<3)throw new InvalidOperationException("dim <length|radius|diameter> <entity> <expression>");
        dynamic d=s.DimensionConstraints;dynamic e=Get(n,a[1]);var kind=a[0].ToLowerInvariant();dynamic c;
        if(kind=="radius")c=d.AddRadius(e,_g.CreatePoint2d(1,1),false);
        else if(kind=="diameter")c=d.AddDiameter(e,_g.CreatePoint2d(1,1),false);
        else if(kind=="length")c=d.AddTwoPointDistance(e.StartSketchPoint,e.EndSketchPoint,DimensionOrientationEnum.kAlignedDim,_g.CreatePoint2d(1,1),false);
        else throw new InvalidOperationException($"Unsupported dimension '{kind}'.");
        c.Parameter.Expression=_p.Length(a[2]);
    }

    private void Spline(PlanarSketch s,SketchLineStatement x,IDictionary<string,object> named)
    {
        if(x.Args.Count<6||x.Args.Count%2!=0)throw new InvalidOperationException("spline name x1 y1 x2 y2 x3 y3 ...");
        var points=_app.TransientObjects.CreateObjectCollection();for(int i=0;i<x.Args.Count;i+=2)points.Add(P(x.Args,i));
        dynamic spline=((dynamic)s.SketchSplines).Add(points);Put(named,x.Name,spline);
    }

    private void Rectangle(PlanarSketch s,IReadOnlyList<string> a,bool centered)
    {
        var x=_p.Cm(a[0]);var y=_p.Cm(a[1]);var w=_p.Cm(a[2]);var h=_p.Cm(a[3]);if(centered){x-=w/2;y-=h/2;}
        var lines=s.SketchLines.AddAsTwoPointRectangle(_g.CreatePoint2d(x,y),_g.CreatePoint2d(x+w,y+h)).Cast<SketchLine>().ToList();
        var horizontal=lines.OrderByDescending(l=>Math.Abs(l.EndSketchPoint.Geometry.X-l.StartSketchPoint.Geometry.X)).First();
        var vertical=lines.OrderByDescending(l=>Math.Abs(l.EndSketchPoint.Geometry.Y-l.StartSketchPoint.Geometry.Y)).First();
        var dw=s.DimensionConstraints.AddTwoPointDistance(horizontal.StartSketchPoint,horizontal.EndSketchPoint,DimensionOrientationEnum.kHorizontalDim,_g.CreatePoint2d(x+w/2,y-1),false);
        dw.Parameter.Expression=_p.Length(a[2]);
        var dh=s.DimensionConstraints.AddTwoPointDistance(vertical.StartSketchPoint,vertical.EndSketchPoint,DimensionOrientationEnum.kVerticalDim,_g.CreatePoint2d(x+w+1,y+h/2),false);
        dh.Parameter.Expression=_p.Length(a[3]);
    }

    private void Slot(PlanarSketch s,IReadOnlyList<string> a)
    {
        var cx=_p.Cm(a[0]);var cy=_p.Cm(a[1]);var len=_p.Cm(a[2]);var width=_p.Cm(a[3]);var angle=Rad(a[4]);
        var ux=Math.Cos(angle);var uy=Math.Sin(angle);var nx=-uy;var ny=ux;var r=width/2;var half=(len-width)/2;
        var p1=_g.CreatePoint2d(cx-ux*half,cy-uy*half);var p2=_g.CreatePoint2d(cx+ux*half,cy+uy*half);
        var l1=s.SketchLines.AddByTwoPoints(_g.CreatePoint2d(p1.X+nx*r,p1.Y+ny*r),_g.CreatePoint2d(p2.X+nx*r,p2.Y+ny*r));
        var l2=s.SketchLines.AddByTwoPoints(_g.CreatePoint2d(p2.X-nx*r,p2.Y-ny*r),_g.CreatePoint2d(p1.X-nx*r,p1.Y-ny*r));
        s.SketchArcs.AddByCenterStartEndPoint(p2,l1.EndSketchPoint,l2.StartSketchPoint,false);s.SketchArcs.AddByCenterStartEndPoint(p1,l2.EndSketchPoint,l1.StartSketchPoint,false);
    }

    private void Polygon(PlanarSketch s,IReadOnlyList<string> a)
    {
        var cx=_p.Cm(a[0]);var cy=_p.Cm(a[1]);var r=_p.Cm(a[2]);var count=_p.Integer(a[3]);var rot=a.Count>4?Rad(a[4]):0;
        Point2d? first=null,prev=null;
        for(int i=0;i<count;i++){var t=rot+2*Math.PI*i/count;var p=_g.CreatePoint2d(cx+r*Math.Cos(t),cy+r*Math.Sin(t));if(first==null)first=p;if(prev!=null)s.SketchLines.AddByTwoPoints(prev,p);prev=p;}
        if(first!=null&&prev!=null)s.SketchLines.AddByTwoPoints(prev,first);
    }

    private Point2d P(IReadOnlyList<string> a,int i)=>_g.CreatePoint2d(_p.Cm(a[i]),_p.Cm(a[i+1]));
    private double Rad(string e)=>_p.Degrees(e)*Math.PI/180.0;
    private static object Get(IDictionary<string,object> n,string key)=>n.TryGetValue(key,out var v)?v:throw new KeyNotFoundException($"Unknown sketch entity '{key}'.");
    private static void Put(IDictionary<string,object> n,string key,object value){if(!string.IsNullOrWhiteSpace(key))n[key]=value;}
}
