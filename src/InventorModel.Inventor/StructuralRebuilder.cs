using System;
using System.Collections.Generic;
using Inventor;
using InventorModel.Core.Diagnostics;
using InventorModel.Core.Dsl;

namespace InventorModel.Inventor;

public sealed class StructuralRebuilder
{
    private readonly Application _application;

    public StructuralRebuilder(
        Application application)
    {
        _application = application ??
            throw new ArgumentNullException(
                nameof(application));
    }

    public void Rebuild(
        PartDocument document,
        ModelScript targetModel,
        StructuralRebuildPlan plan)
    {
        if (document == null)
            throw new ArgumentNullException(
                nameof(document));
        if (targetModel == null)
            throw new ArgumentNullException(
                nameof(targetModel));
        if (plan == null)
            throw new ArgumentNullException(
                nameof(plan));
        if (!plan.CanRebuildLocally ||
            plan.StartStatementIndex < 0)
        {
            throw new InvalidOperationException(
                "The requested model change does not have a safe local rebuild plan.");
        }

        PartComponentDefinition component =
            document.ComponentDefinition;

        Transaction transaction =
            _application.TransactionManager
                .StartTransaction(
                    (_Document)(object)document,
                    "InventorModel local rebuild");

        try
        {
            DeleteFeatures(
                component,
                plan.FeatureNamesToRemove);

            DeleteSketches(
                component,
                plan.SketchNamesToRemove);

            new ScriptExecutor(_application)
                .ExecuteWithinTransaction(
                    targetModel,
                    document,
                    replaceExisting: false,
                    startStatementIndex:
                        plan.StartStatementIndex);

            document.Update2(true);
            transaction.End();
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
                    "Local rebuild rollback failed.",
                    abortException);
            }

            RuntimeLog.Error(
                "Inventor.Rebuild",
                "Local structural rebuild failed.",
                ex);
            throw;
        }
    }

    private static void DeleteFeatures(
        PartComponentDefinition component,
        IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            PartFeature? feature =
                FindFeature(
                    component,
                    name);

            if (feature == null)
                continue;

            // Keep consumed sketches. Explicit dirty sketches are
            // removed separately after all dependent features are gone.
            feature.Delete(
                true,
                false,
                false);
        }
    }

    private static void DeleteSketches(
        PartComponentDefinition component,
        IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            PlanarSketch? sketch =
                FindSketch(
                    component,
                    name);

            if (sketch == null)
                continue;

            sketch.Delete();
        }
    }

    private static PartFeature? FindFeature(
        PartComponentDefinition component,
        string name)
    {
        foreach (PartFeature feature in
                 component.Features)
        {
            try
            {
                if (string.Equals(
                        feature.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return feature;
                }
            }
            catch
            {
                // Invalid COM objects can disappear while deleting
                // dependent suffix features. Continue with the live tree.
            }
        }

        return null;
    }

    private static PlanarSketch? FindSketch(
        PartComponentDefinition component,
        string name)
    {
        foreach (PlanarSketch sketch in
                 component.Sketches)
        {
            try
            {
                if (string.Equals(
                        sketch.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return sketch;
                }
            }
            catch
            {
                // Continue scanning the current live sketch collection.
            }
        }

        return null;
    }
}
