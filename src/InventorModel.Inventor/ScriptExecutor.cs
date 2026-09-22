using System;
using System.Collections.Generic;
using System.Globalization;
using Inventor;
using InventorModel.Core.Dsl;
using DslParameterTable = InventorModel.Core.Dsl.ParameterTable;

namespace InventorModel.Inventor;

public sealed class ScriptExecutor
{
    private readonly Application _app;
    public ScriptExecutor(Application app)=>_app=app;

    public PartDocument Execute(string source,PartDocument? document=null)
    {
        var script=new DslParser().Parse(source);
        document=document??new InventorSession(_app).NewPart();

        var c=document.ComponentDefinition;
        var parameters=new DslParameterTable();
        ImportExistingParameters(c,parameters);

        var sketches=new Dictionary<string,PlanarSketch>(StringComparer.OrdinalIgnoreCase);
        foreach(PlanarSketch sketch in c.Sketches)
        {
            try { if(!string.IsNullOrWhiteSpace(sketch.Name)) sketches[sketch.Name]=sketch; } catch {}
        }

        var features=new Dictionary<string,PartFeature>(StringComparer.OrdinalIgnoreCase);
        foreach(PartFeature feature in c.Features)
        {
            try { if(!string.IsNullOrWhiteSpace(feature.Name)) features[feature.Name]=feature; } catch {}
        }

        var sketchExecutor=new SketchExecutor(_app,c,parameters);
        var featureExecutor=new FeatureExecutor(_app,c,parameters,sketches,features);
        var tx=_app.TransactionManager.StartTransaction((_Document)(object)document,"InventorModel");

        try
        {
            foreach(var statement in script.Statements)
            {
                if(statement is ParameterStatement p)
                {
                    parameters.Add(p.Name,p.Expression);
                    AddParameter(c,p);
                }
                else if(statement is SketchStatement s)
                    sketches[s.Name]=sketchExecutor.Build(s);
                else if(statement is FeatureStatement f)
                    featureExecutor.Build(f);
                else if(statement is EditStatement e)
                    ApplyEdit(c,e,parameters,features);
            }

            document.Update2(true);
            tx.End();
            return document;
        }
        catch
        {
            tx.Abort();
            throw;
        }
    }

    private static void ImportExistingParameters(PartComponentDefinition c,DslParameterTable table)
    {
        foreach(UserParameter p in c.Parameters.UserParameters)
        {
            try
            {
                // Inventor database length unit is cm. Keep the DSL's length convention in mm.
                double value=Convert.ToDouble(p.Value,CultureInfo.InvariantCulture);
                string units=p.get_Units()??"";
                if(units.IndexOf("deg",StringComparison.OrdinalIgnoreCase)>=0)
                    table.Import(p.Name,value*180.0/Math.PI);
                else
                    table.Import(p.Name,value*10.0);
            }
            catch {}
        }
    }

    private static void AddParameter(PartComponentDefinition c,ParameterStatement p)
    {
        string expression=InventorExpression(p.Expression);
        var unit=p.Expression.IndexOf("deg",StringComparison.OrdinalIgnoreCase)>=0
            ? UnitsTypeEnum.kDegreeAngleUnits
            : UnitsTypeEnum.kMillimeterLengthUnits;
        try { c.Parameters.UserParameters[p.Name].Expression=expression; }
        catch { c.Parameters.UserParameters.AddByExpression(p.Name,expression,unit); }
    }

    private static void ApplyEdit(
        PartComponentDefinition c,
        EditStatement e,
        DslParameterTable table,
        IDictionary<string,PartFeature> features)
    {
        if(e.Kind=="set")
        {
            table.Set(e.Target,e.Value);
            try { c.Parameters.UserParameters[e.Target].Expression=InventorExpression(e.Value); }
            catch { throw new KeyNotFoundException($"Unknown parameter '{e.Target}'."); }
            return;
        }

        if(!features.TryGetValue(e.Target,out PartFeature? f))
        {
            foreach(PartFeature candidate in c.Features)
            {
                if(string.Equals(candidate.Name,e.Target,StringComparison.OrdinalIgnoreCase))
                {
                    f=candidate;
                    features[e.Target]=candidate;
                    break;
                }
            }
        }

        if(f is null) throw new KeyNotFoundException($"Unknown feature '{e.Target}'.");

        if(e.Kind=="suppress") f.Suppressed=true;
        else if(e.Kind=="unsuppress") f.Suppressed=false;
        else if(e.Kind=="delete")
        {
            f.Delete();
            features.Remove(e.Target);
        }
        else throw new InvalidOperationException($"Unsupported edit '{e.Kind}'.");
    }

    private static string InventorExpression(string e)
    {
        string t=e.Trim();
        if(t.IndexOf("mm",StringComparison.OrdinalIgnoreCase)>=0||
           t.IndexOf("deg",StringComparison.OrdinalIgnoreCase)>=0)
            return t;

        if(double.TryParse(t,NumberStyles.Float,CultureInfo.InvariantCulture,out _))
            return t+" mm";

        return t;
    }
}
