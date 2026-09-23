using System;
using System.Collections.Generic;
using System.Globalization;
using Inventor;
using InventorModel.Core.Diagnostics;
using InventorModel.Core.Dsl;
using DslParameterTable = InventorModel.Core.Dsl.ParameterTable;

namespace InventorModel.Inventor;

public sealed class ScriptExecutor
{
    private readonly Application _app;

    public ScriptExecutor(Application app)
    {
        _app = app ??
               throw new ArgumentNullException(nameof(app));
    }

    public PartDocument Execute(
        string source,
        PartDocument? document = null,
        bool replaceExisting = false)
    {
        ModelScript script =
            new DslParser().Parse(source);

        ValidationResult validation =
            new ModelValidator().Validate(script);

        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "DSL validation failed: " +
                string.Join("; ", validation.Errors));
        }

        document ??=
            new InventorSession(_app).NewPart();

        PartComponentDefinition component =
            document.ComponentDefinition;

        Transaction transaction =
            _app.TransactionManager.StartTransaction(
                (_Document)(object)document,
                "InventorModel");

        try
        {
            if (replaceExisting)
                ResetModel(component);

            var parameters = new DslParameterTable();
            ImportExistingParameters(
                component,
                parameters);

            Dictionary<string, PlanarSketch> sketches =
                ReadExistingSketches(component);
            Dictionary<string, PartFeature> features =
                ReadExistingFeatures(component);

            var sketchExecutor =
                new SketchExecutor(
                    _app,
                    component,
                    parameters);
            var featureExecutor =
                new FeatureExecutor(
                    _app,
                    component,
                    parameters,
                    sketches,
                    features);

            foreach (ScriptStatement statement in
                     script.Statements)
            {
                switch (statement)
                {
                    case ParameterStatement parameter:
                        parameters.Add(
                            parameter.Name,
                            parameter.Expression);
                        AddOrUpdateParameter(
                            component,
                            parameter);
                        break;

                    case SketchStatement sketch:
                        sketches[sketch.Name] =
                            sketchExecutor.Build(sketch);
                        break;

                    case FeatureStatement feature:
                        featureExecutor.Build(feature);
                        break;

                    case EditStatement edit:
                        ApplyEdit(
                            component,
                            edit,
                            parameters,
                            features);
                        break;
                }
            }

            document.Update2(true);
            transaction.End();
            return document;
        }
        catch (Exception ex)
        {
            try
            {
                transaction.Abort();
            }
            catch (Exception abortException)
            {
                RuntimeLog.Error(
                    "Inventor.Transaction",
                    "Model transaction rollback failed.",
                    abortException);
            }

            RuntimeLog.Error(
                "Inventor.Execute",
                "InventorModel script execution failed.",
                ex);
            throw;
        }
    }

    private static void ResetModel(
        PartComponentDefinition component)
    {
        for (int i = component.Features.Count; i >= 1; i--)
        {
            PartFeature feature = component.Features[i];
            feature.Delete();
        }

        for (int i = component.Sketches.Count; i >= 1; i--)
        {
            PlanarSketch sketch = component.Sketches[i];
            sketch.Delete();
        }

        UserParameters userParameters =
            component.Parameters.UserParameters;

        for (int i = userParameters.Count; i >= 1; i--)
        {
            UserParameter parameter = userParameters[i];
            parameter.Delete();
        }
    }

    private static Dictionary<string, PlanarSketch>
        ReadExistingSketches(
            PartComponentDefinition component)
    {
        var result =
            new Dictionary<string, PlanarSketch>(
                StringComparer.OrdinalIgnoreCase);

        foreach (PlanarSketch sketch in component.Sketches)
        {
            try
            {
                string name = sketch.Name;
                if (!string.IsNullOrWhiteSpace(name))
                    result[name] = sketch;
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "Inventor.COM",
                    "An existing sketch name could not be read.",
                    ex);
            }
        }

        return result;
    }

    private static Dictionary<string, PartFeature>
        ReadExistingFeatures(
            PartComponentDefinition component)
    {
        var result =
            new Dictionary<string, PartFeature>(
                StringComparer.OrdinalIgnoreCase);

        foreach (PartFeature feature in component.Features)
        {
            try
            {
                string name = feature.Name;
                if (!string.IsNullOrWhiteSpace(name))
                    result[name] = feature;
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "Inventor.COM",
                    "An existing feature name could not be read.",
                    ex);
            }
        }

        return result;
    }

    private static void ImportExistingParameters(
        PartComponentDefinition component,
        DslParameterTable table)
    {
        foreach (UserParameter parameter in
                 component.Parameters.UserParameters)
        {
            try
            {
                // Inventor database length unit is cm.
                // Keep the DSL length convention in mm.
                double value =
                    Convert.ToDouble(
                        parameter.Value,
                        CultureInfo.InvariantCulture);

                string units =
                    parameter.get_Units() ??
                    string.Empty;

                if (units.IndexOf(
                        "deg",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    table.Import(
                        parameter.Name,
                        value * 180.0 / Math.PI);
                }
                else
                {
                    table.Import(
                        parameter.Name,
                        value * 10.0);
                }
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "Inventor.Parameters",
                    "An existing Inventor user parameter could not be imported.",
                    ex);
            }
        }
    }

    private static void AddOrUpdateParameter(
        PartComponentDefinition component,
        ParameterStatement parameter)
    {
        string expression =
            InventorExpression(parameter.Expression);

        UserParameter? existing =
            FindUserParameter(
                component,
                parameter.Name);

        if (existing != null)
        {
            existing.Expression = expression;
            return;
        }

        UnitsTypeEnum unit =
            parameter.Expression.IndexOf(
                "deg",
                StringComparison.OrdinalIgnoreCase) >= 0
                ? UnitsTypeEnum.kDegreeAngleUnits
                : UnitsTypeEnum.kMillimeterLengthUnits;

        component.Parameters.UserParameters.AddByExpression(
            parameter.Name,
            expression,
            unit);
    }

    private static void ApplyEdit(
        PartComponentDefinition component,
        EditStatement edit,
        DslParameterTable table,
        IDictionary<string, PartFeature> features)
    {
        if (edit.Kind == "set")
        {
            table.Set(
                edit.Target,
                edit.Value);

            UserParameter? parameter =
                FindUserParameter(
                    component,
                    edit.Target);

            if (parameter == null)
            {
                throw new KeyNotFoundException(
                    "Unknown parameter '" +
                    edit.Target +
                    "'.");
            }

            parameter.Expression =
                InventorExpression(edit.Value);
            return;
        }

        if (!features.TryGetValue(
                edit.Target,
                out PartFeature? feature))
        {
            foreach (PartFeature candidate in
                     component.Features)
            {
                if (string.Equals(
                        candidate.Name,
                        edit.Target,
                        StringComparison.OrdinalIgnoreCase))
                {
                    feature = candidate;
                    features[edit.Target] =
                        candidate;
                    break;
                }
            }
        }

        if (feature is null)
        {
            throw new KeyNotFoundException(
                "Unknown feature '" +
                edit.Target +
                "'.");
        }

        switch (edit.Kind)
        {
            case "suppress":
                feature.Suppressed = true;
                break;

            case "unsuppress":
                feature.Suppressed = false;
                break;

            case "delete":
                feature.Delete();
                features.Remove(edit.Target);
                break;

            default:
                throw new InvalidOperationException(
                    "Unsupported edit '" +
                    edit.Kind +
                    "'.");
        }
    }

    private static UserParameter? FindUserParameter(
        PartComponentDefinition component,
        string name)
    {
        foreach (UserParameter parameter in
                 component.Parameters.UserParameters)
        {
            if (string.Equals(
                    parameter.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }
        }

        return null;
    }

    private static string InventorExpression(
        string expression)
    {
        string text =
            expression.Trim();

        if (text.IndexOf(
                "mm",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf(
                "deg",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return text;
        }

        if (double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out _))
        {
            return text + " mm";
        }

        return text;
    }
}
