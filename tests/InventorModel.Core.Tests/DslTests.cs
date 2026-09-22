using InventorModel.Core.Dsl;
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
}
