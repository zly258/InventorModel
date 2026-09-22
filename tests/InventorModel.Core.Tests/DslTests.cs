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
        string[] examples=Directory.GetFiles(Path.Combine(root,"examples"),"*.imodel");
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

    private static string FindRepositoryRoot()
    {
        var directory=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while(directory!=null&&!Directory.Exists(Path.Combine(directory.FullName,"examples")))
            directory=directory.Parent;
        return directory?.FullName??throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
