using InventorModel.Core.Dsl;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace InventorModel.Core.Tests;

public sealed class DslTests
{
    [Fact] public void ParsesOrderedModel()
    {
        var m=new DslParser().Parse("part Plate\nparam w = 100\nsketch base on XY\ncenterrect 0 0 w 60\nend\nextrude body from base depth 10 join\nsketch holes on face:body:top\ncircle c1 0 0 5\nend");
        Assert.Equal("Plate",m.PartName);Assert.Equal(4,m.Statements.Count);
        Assert.IsType<ParameterStatement>(m.Statements[0]);Assert.IsType<SketchStatement>(m.Statements[1]);
        Assert.IsType<FeatureStatement>(m.Statements[2]);Assert.IsType<SketchStatement>(m.Statements[3]);
    }
    [Fact] public void EvaluatesParameterExpressions()
    {
        var t=new ParameterTable();t.Add("w","100");t.Add("half","w/2");
        Assert.Equal(50,t.Mm("half"));Assert.Equal(30,t.Mm("half/2+5"));
    }
    [Fact] public void ParsesLocalEdits()
    {
        var m=new DslParser().Parse("part P\nset width = 120\nsuppress fillet1\nunsuppress fillet1\ndelete hole1");
        Assert.Equal(4,m.Statements.Count);
    }

    [Fact] public void ValidatesEveryExample()
    {
        string root=FindRepositoryRoot();
        string[] examples=Directory.GetFiles(Path.Combine(root,"examples"),"*.ivmodel");
        Assert.NotEmpty(examples);
        foreach(string path in examples)
        {
            ValidationResult result=new ModelValidator().Validate(File.ReadAllText(path));
            Assert.True(result.IsValid,Path.GetFileName(path)+": "+string.Join("; ",result.Errors));
        }
    }

    [Fact] public void RejectsUnknownParameterAndFeature()
    {
        ValidationResult result=new ModelValidator().Validate(
            "part Bad\nsketch base on XY\nrect 0 0 missing 20\nend\nmagic body from base");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,x=>x.Contains("Unknown parameter"));
        Assert.Contains(result.Errors,x=>x.Contains("Unsupported feature"));
    }

    [Fact] public void RejectsMissingFeatureArguments()
    {
        ValidationResult result=new ModelValidator().Validate(
            "part Bad\nsketch base on XY\nrect 0 0 10 20\nend\nextrude body from base");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors,x=>x.Contains("requires 'depth' or 'extent'"));
    }

    [Fact] public void RequiresExplicitFinishingEdges()
    {
        const string prefix=
            "part Finish\nsketch base on XY\nrect 0 0 20 20\nend\nextrude body from base depth 10 join\n";

        ValidationResult missing=
            new ModelValidator().Validate(prefix+"fillet rounds radius 2");
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Errors,x=>x.Contains("requires 'edges'"));

        ValidationResult selected=
            new ModelValidator().Validate(prefix+"fillet rounds edges 1,2 radius 2");
        Assert.True(selected.IsValid,string.Join("; ",selected.Errors));

        ValidationResult all=
            new ModelValidator().Validate(prefix+"chamfer bevels edges all distance 1");
        Assert.True(all.IsValid,string.Join("; ",all.Errors));
    }

    [Fact] public void ValidatesDirectionsAndSketchLineRevolveAxis()
    {
        ValidationResult result=new ModelValidator().Validate(
            "part P\n"+
            "param l = 100\n"+
            "param r = 20\n"+
            "sketch profile on XY\n"+
            "line axisLine 0 0 0 l\n"+
            "rect 0 0 r l\n"+
            "end\n"+
            "revolve body from profile axis line:1 angle 180 direction negative join\n");

        Assert.True(result.IsValid,string.Join("; ",result.Errors));

        ValidationResult symmetricExtrude=new ModelValidator().Validate(
            "part P\nsketch base on XY\nrect 0 0 20 20\nend\n"+
            "extrude body from base depth 10 direction symmetric join");

        Assert.True(symmetricExtrude.IsValid,string.Join("; ",symmetricExtrude.Errors));
    }

    [Fact] public void RejectsInvalidFeatureDirectionsAndAxes()
    {
        const string prefix=
            "part Bad\nsketch base on XY\nrect 0 0 20 20\nend\n";

        ValidationResult throughSymmetric=
            new ModelValidator().Validate(
                prefix+"extrude body from base through direction symmetric join");
        Assert.False(throughSymmetric.IsValid);
        Assert.Contains(throughSymmetric.Errors,x=>x.Contains("direction"));

        ValidationResult badRevolveAxis=
            new ModelValidator().Validate(
                prefix+"revolve body from base axis line:0 angle 90 join");
        Assert.False(badRevolveAxis.IsValid);
        Assert.Contains(badRevolveAxis.Errors,x=>x.Contains("axis"));

        ValidationResult badPatternAxis=
            new ModelValidator().Validate(
                prefix+
                "extrude body from base depth 10 join\n"+
                "hole h1 on face:body:top at 0 0 diameter 5 through\n"+
                "pattern_rect holes source h1 count 2 spacing 10 axis Q");
        Assert.False(badPatternAxis.IsValid);
        Assert.Contains(badPatternAxis.Errors,x=>x.Contains("axis"));
    }

    [Fact] public void DetectsParameterOnlyModelChanges()
    {
        var parser=new DslParser();
        ModelScript before=parser.Parse(
            "part P\nparam width = 100\nsketch base on XY\nrect 0 0 width 20\nend\nextrude body from base depth 10 join");
        ModelScript after=parser.Parse(
            "part P\nparam width = 120\nsketch base on XY\nrect 0 0 width 20\nend\nextrude body from base depth 10 join");

        ModelDiffResult diff=
            new ModelDiffer().Compare(before,after);

        Assert.True(diff.IsParameterOnly);
        Assert.Single(diff.ParameterChanges);
        Assert.Equal("width",diff.ParameterChanges[0].Name);
        Assert.Empty(diff.StructuralChanges);
    }

    [Fact] public void DetectsFeatureValueChangesSeparately()
    {
        var parser=new DslParser();
        ModelScript before=parser.Parse(
            "part P\nsketch base on XY\nrect 0 0 20 20\nend\nextrude body from base depth 10 join");
        ModelScript after=parser.Parse(
            "part P\nsketch base on XY\nrect 0 0 20 20\nend\nextrude body from base depth 20 join");

        ModelDiffResult diff=
            new ModelDiffer().Compare(before,after);

        Assert.False(diff.IsParameterOnly);
        Assert.Single(diff.FeatureChanges);
        Assert.Equal("body",diff.FeatureChanges[0].Name);
        Assert.Empty(diff.StructuralChanges);
    }

    [Fact] public void TreatsSketchTopologyChangesAsStructural()
    {
        var parser=new DslParser();
        ModelScript before=parser.Parse(
            "part P\nsketch base on XY\nrect 0 0 20 20\nend\nextrude body from base depth 10 join");
        ModelScript after=parser.Parse(
            "part P\nsketch base on XY\nrect 0 0 30 20\nend\nextrude body from base depth 10 join");

        ModelDiffResult diff=
            new ModelDiffer().Compare(before,after);

        Assert.Contains(
            diff.StructuralChanges,
            x=>x=="sketch:changed:base");
    }

    [Fact] public void PlansLocalSuffixForAddedFeature()
    {
        var parser=new DslParser();
        ModelScript before=parser.Parse(
            "part P\nsketch base on XY\nrect 0 0 20 20\nend\nextrude body from base depth 10 join");
        ModelScript after=parser.Parse(
            "part P\nsketch base on XY\nrect 0 0 20 20\nend\nextrude body from base depth 10 join\nfillet f1 edges all radius 2");

        ModelDiffResult diff=
            new ModelDiffer().Compare(before,after);
        StructuralRebuildPlan plan=
            new StructuralRebuildPlanner().Plan(
                before,
                after,
                diff);

        Assert.True(plan.CanRebuildLocally);
        Assert.Equal(2,plan.StartStatementIndex);
        Assert.Empty(plan.FeatureNamesToRemove);
    }

    [Fact] public void PlansLocalSuffixForSketchChange()
    {
        var parser=new DslParser();
        ModelScript before=parser.Parse(
            "part P\nsketch base on XY\nrect 0 0 20 20\nend\nextrude body from base depth 10 join\nfillet f1 edges all radius 2");
        ModelScript after=parser.Parse(
            "part P\nsketch base on XY\nrect 0 0 30 20\nend\nextrude body from base depth 10 join\nfillet f1 edges all radius 2");

        ModelDiffResult diff=
            new ModelDiffer().Compare(before,after);
        StructuralRebuildPlan plan=
            new StructuralRebuildPlanner().Plan(
                before,
                after,
                diff);

        Assert.True(plan.CanRebuildLocally);
        Assert.Equal(0,plan.StartStatementIndex);
        Assert.Contains("base",plan.SketchNamesToRemove);
        Assert.Contains("body",plan.FeatureNamesToRemove);
        Assert.Contains("f1",plan.FeatureNamesToRemove);
    }

    [Fact] public void ParameterAndStructureChangeFallsBackToFullRebuild()
    {
        var parser=new DslParser();
        ModelScript before=parser.Parse(
            "part P\nparam w = 20\nsketch base on XY\nrect 0 0 w 20\nend\nextrude body from base depth 10 join");
        ModelScript after=parser.Parse(
            "part P\nparam w = 30\nsketch base on XY\nrect 0 0 w 30\nend\nextrude body from base depth 10 join");

        ModelDiffResult diff=
            new ModelDiffer().Compare(before,after);
        StructuralRebuildPlan plan=
            new StructuralRebuildPlanner().Plan(
                before,
                after,
                diff);

        Assert.False(plan.CanRebuildLocally);
    }

    private static string FindRepositoryRoot()
    {
        var directory=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while(directory!=null&&!Directory.Exists(Path.Combine(directory.FullName,"examples")))
            directory=directory.Parent;
        return directory?.FullName??throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
