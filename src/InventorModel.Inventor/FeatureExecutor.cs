using System;
using System.Collections.Generic;
using Inventor;
using InventorModel.Core.Dsl;

namespace InventorModel.Inventor;

internal sealed class FeatureExecutor
{
    private readonly Application _app;private readonly PartComponentDefinition _c;private readonly ParameterTable _p;
    private readonly IDictionary<string,PlanarSketch> _sketches;private readonly IDictionary<string,PartFeature> _features;
    public FeatureExecutor(Application app,PartComponentDefinition c,ParameterTable p,IDictionary<string,PlanarSketch> sketches,IDictionary<string,PartFeature> features)
    {_app=app;_c=c;_p=p;_sketches=sketches;_features=features;}

    public PartFeature Build(FeatureStatement f)
    {
        PartFeature r;
        switch(f.Kind)
        {
            case "extrude":r=Extrude(f);break;case "revolve":r=Revolve(f);break;case "sweep":r=Sweep(f);break;case "loft":r=Loft(f);break;
            case "hole":r=Hole(f);break;case "fillet":r=Fillet(f);break;case "chamfer":r=Chamfer(f);break;case "shell":r=Shell(f);break;
            case "pattern_rect":r=RectPattern(f);break;case "pattern_circular":r=CircularPattern(f);break;case "mirror":r=Mirror(f);break;
            default:throw new InvalidOperationException($"Unsupported feature '{f.Kind}'.");
        }
        r.Name=f.Name;_features[f.Name]=r;return r;
    }
    private ExtrudeFeature Extrude(FeatureStatement f){var profile=Sketch(Arg(f,"from")).Profiles.AddForSolid();var d=_c.Features.ExtrudeFeatures.CreateExtrudeDefinition(profile,Operation(f));if(f.Args.TryGetValue("extent",out var ex)&&ex=="through")d.SetThroughAllExtent(PartFeatureExtentDirectionEnum.kPositiveExtentDirection);else d.SetDistanceExtent(_p.Length(Arg(f,"depth","distance")),PartFeatureExtentDirectionEnum.kPositiveExtentDirection);return _c.Features.ExtrudeFeatures.Add(d);}
    private RevolveFeature Revolve(FeatureStatement f){var profile=Sketch(Arg(f,"from","profile")).Profiles.AddForSolid();var axis=GeometrySelector.Axis(_c,Arg(f,"axis"));var a=f.Args.TryGetValue("angle",out var v)?v:"360";return Math.Abs(_p.Degrees(a)-360)<1e-7?_c.Features.RevolveFeatures.AddFull(profile,axis,Operation(f)):_c.Features.RevolveFeatures.AddByAngle(profile,axis,_p.Angle(a),PartFeatureExtentDirectionEnum.kPositiveExtentDirection,Operation(f));}
    private SweepFeature Sweep(FeatureStatement f){var profile=Sketch(Arg(f,"profile")).Profiles.AddForSolid();var pathSketch=Sketch(Arg(f,"path"));var curves=_app.TransientObjects.CreateObjectCollection();foreach(SketchLine x in pathSketch.SketchLines)curves.Add(x);foreach(SketchArc x in pathSketch.SketchArcs)curves.Add(x);foreach(SketchSpline x in pathSketch.SketchSplines)curves.Add(x);var path=_c.Features.CreateSpecifiedPath(curves);return _c.Features.SweepFeatures.Add(_c.Features.SweepFeatures.CreateSweepDefinition(SweepTypeEnum.kPathSweepType,profile,path,Operation(f)));}
    private LoftFeature Loft(FeatureStatement f){var sections=_app.TransientObjects.CreateObjectCollection();var names=new List<string>();if(f.Args.TryGetValue("from",out var x))names.Add(x);names.AddRange(f.Items);if(names.Count<2)throw new InvalidOperationException("loft requires two or more section sketches.");foreach(var n in names)sections.Add(Sketch(n).Profiles.AddForSolid());return _c.Features.LoftFeatures.Add(_c.Features.LoftFeatures.CreateLoftDefinition(sections,Operation(f)));}
    private HoleFeature Hole(FeatureStatement f)
    {
        var s=_c.Sketches.Add(GeometrySelector.Plane(_c,Arg(f,"on")),false);var xy=Arg(f,"at").Split(',');
        var point=s.SketchPoints.Add(_app.TransientGeometry.CreatePoint2d(_p.Cm(xy[0]),_p.Cm(xy[1])),true);DrivePoint(s,point,xy[0],xy[1]);
        var points=_app.TransientObjects.CreateObjectCollection();points.Add(point);var placement=_c.Features.HoleFeatures.CreateSketchPlacementDefinition(points);var dia=_p.Length(Arg(f,"diameter"));
        if(f.Args.TryGetValue("extent",out var e)&&e=="through")return _c.Features.HoleFeatures.AddDrilledByThroughAllExtent(placement,dia,PartFeatureExtentDirectionEnum.kPositiveExtentDirection);
        return _c.Features.HoleFeatures.AddDrilledByDistanceExtent(placement,dia,_p.Length(Arg(f,"depth")),PartFeatureExtentDirectionEnum.kPositiveExtentDirection,"118 deg");
    }
    private void DrivePoint(PlanarSketch s,SketchPoint point,string x,string y)
    {
        var origin=s.SketchPoints.Add(_app.TransientGeometry.CreatePoint2d(0,0),false);s.GeometricConstraints.AddGround((SketchEntity)(object)origin);
        if(Math.Abs(_p.Mm(x))<1e-9)s.GeometricConstraints.AddVerticalAlign(point,origin);
        else{s.DimensionConstraints.AddTwoPointDistance(origin,point,DimensionOrientationEnum.kHorizontalDim,_app.TransientGeometry.CreatePoint2d(point.Geometry.X,point.Geometry.Y-1),false).Parameter.Expression=_p.Length(x);}
        if(Math.Abs(_p.Mm(y))<1e-9)s.GeometricConstraints.AddHorizontalAlign(point,origin);
        else{s.DimensionConstraints.AddTwoPointDistance(origin,point,DimensionOrientationEnum.kVerticalDim,_app.TransientGeometry.CreatePoint2d(point.Geometry.X+1,point.Geometry.Y),false).Parameter.Expression=_p.Length(y);}
    }
    private FilletFeature Fillet(FeatureStatement f){var d=_c.Features.FilletFeatures.CreateFilletDefinition();d.EdgeSetSettings.AddConstantRadiusEdgeSet(AllEdges(),_p.Length(Arg(f,"radius")));return _c.Features.FilletFeatures.Add(d);}
    private ChamferFeature Chamfer(FeatureStatement f)=>_c.Features.ChamferFeatures.AddUsingDistance(AllEdges(),_p.Length(Arg(f,"distance")),false,false,false);
    private ShellFeature Shell(FeatureStatement f){var faces=_app.TransientObjects.CreateFaceCollection();faces.Add(GeometrySelector.Face(_c,Arg(f,"faces")));return _c.Features.ShellFeatures.Add(_c.Features.ShellFeatures.CreateShellDefinition(faces,_p.Length(Arg(f,"thickness")),ShellDirectionEnum.kInsideShellDirection));}
    private RectangularPatternFeature RectPattern(FeatureStatement f)
    {
        var src=_app.TransientObjects.CreateObjectCollection();src.Add(Feature(Arg(f,"source")));var counts=Arg(f,"count").Split(',');var spaces=Arg(f,"spacing").Split(',');
        dynamic d=_c.Features.RectangularPatternFeatures.CreateDefinition(src,GeometrySelector.Axis(_c,"X"),true,_p.Integer(counts[0]),_p.Length(spaces[0]),PatternSpacingTypeEnum.kDefault);
        if(counts.Length>1&&_p.Integer(counts[1])>1){d.YDirectionEntity=GeometrySelector.Axis(_c,"Y");d.NaturalYDirection=true;d.YCount=_p.Integer(counts[1]);d.YSpacing=_p.Length(spaces[1]);d.YDirectionSpacingType=PatternSpacingTypeEnum.kDefault;}
        d.ComputeType=PatternComputeTypeEnum.kIdenticalCompute;d.XDirectionMidPlanePattern=false;d.YDirectionMidPlanePattern=false;return _c.Features.RectangularPatternFeatures.AddByDefinition(d);
    }
    private CircularPatternFeature CircularPattern(FeatureStatement f){var src=_app.TransientObjects.CreateObjectCollection();src.Add(Feature(Arg(f,"source")));dynamic d=_c.Features.CircularPatternFeatures.CreateDefinition(src,GeometrySelector.Axis(_c,Arg(f,"axis")),true,_p.Integer(Arg(f,"count")),f.Args.TryGetValue("angle",out var a)?_p.Angle(a):"360 deg",true);d.ComputeType=PatternComputeTypeEnum.kIdenticalCompute;d.MidPlanePattern=false;return _c.Features.CircularPatternFeatures.AddByDefinition(d);}
    private MirrorFeature Mirror(FeatureStatement f){var src=_app.TransientObjects.CreateObjectCollection();src.Add(Feature(Arg(f,"source")));dynamic d=_c.Features.MirrorFeatures.CreateDefinition(src,GeometrySelector.WorkPlane(_c,Arg(f,"plane")),PatternComputeTypeEnum.kIdenticalCompute);d.RemoveOriginal=false;d.ComputeType=PatternComputeTypeEnum.kIdenticalCompute;return _c.Features.MirrorFeatures.AddByDefinition(d);}
    private EdgeCollection AllEdges(){if(_c.SurfaceBodies.Count==0)throw new InvalidOperationException("No solid body exists.");var c=_app.TransientObjects.CreateEdgeCollection();foreach(Edge e in _c.SurfaceBodies[1].Edges)c.Add(e);return c;}
    private PlanarSketch Sketch(string n)=>_sketches.TryGetValue(n,out var s)?s:throw new KeyNotFoundException($"Unknown sketch '{n}'.");
    private PartFeature Feature(string n)=>_features.TryGetValue(n,out var f)?f:throw new KeyNotFoundException($"Unknown feature '{n}'.");
    private static string Arg(FeatureStatement f,params string[] keys){foreach(var k in keys)if(f.Args.TryGetValue(k,out var v))return v;throw new InvalidOperationException($"{f.Kind} {f.Name} requires {string.Join("/",keys)}.");}
    private static PartFeatureOperationEnum Operation(FeatureStatement f){var o=f.Args.TryGetValue("operation",out var x)?x:"join";return o=="cut"?PartFeatureOperationEnum.kCutOperation:o=="new"?PartFeatureOperationEnum.kNewBodyOperation:PartFeatureOperationEnum.kJoinOperation;}
}
