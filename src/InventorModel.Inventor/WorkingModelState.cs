using System;
using System.Collections.Generic;
using Inventor;
using InventorModel.Core.Diagnostics;
using InventorModel.Core.Dsl;

namespace InventorModel.Inventor;

public sealed class ModelBindings
{
    private readonly Dictionary<string, UserParameter> _parameters =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PlanarSketch> _sketches =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PartFeature> _features =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, UserParameter> Parameters => _parameters;
    public IReadOnlyDictionary<string, PlanarSketch> Sketches => _sketches;
    public IReadOnlyDictionary<string, PartFeature> Features => _features;

    public void Clear()
    {
        _parameters.Clear();
        _sketches.Clear();
        _features.Clear();
    }

    public void Refresh(PartDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        Clear();
        PartComponentDefinition component =
            document.ComponentDefinition;

        foreach (UserParameter parameter in
                 component.Parameters.UserParameters)
        {
            TryBind(
                "parameter",
                () => parameter.Name,
                name => _parameters[name] = parameter);
        }

        foreach (PlanarSketch sketch in component.Sketches)
        {
            TryBind(
                "sketch",
                () => sketch.Name,
                name => _sketches[name] = sketch);
        }

        foreach (PartFeature feature in component.Features)
        {
            TryBind(
                "feature",
                () => feature.Name,
                name => _features[name] = feature);
        }
    }

    private static void TryBind(
        string kind,
        Func<string> getName,
        Action<string> bind)
    {
        try
        {
            string name = getName();
            if (!string.IsNullOrWhiteSpace(name))
                bind(name);
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "Inventor.Bindings",
                "An Inventor " + kind +
                " could not be added to the working model bindings.",
                ex);
        }
    }
}

public sealed class WorkingModelState
{
    public PartDocument? Document { get; private set; }
    public string? Source { get; private set; }
    public string? SourceHash { get; private set; }
    public ModelScript? ParsedModel { get; private set; }
    public int Revision { get; private set; } = 1;
    public int BuildGeneration { get; private set; }
    public string LastBuildMode { get; private set; } = "none";
    public object? LastInspection { get; private set; }
    public ModelBindings Bindings { get; } = new();

    public bool MatchesSource(string sourceHash) =>
        !string.IsNullOrWhiteSpace(SourceHash) &&
        string.Equals(
            SourceHash,
            sourceHash,
            StringComparison.Ordinal);

    public void AttachDocument(PartDocument document)
    {
        Document = document ??
            throw new ArgumentNullException(nameof(document));
        Bindings.Refresh(document);
    }

    public void ResetForDocument(PartDocument document)
    {
        AttachDocument(document);
        Source = null;
        SourceHash = null;
        ParsedModel = null;
        LastInspection = null;
        LastBuildMode = "new_part";
    }

    public void MarkSourceSynchronized(
        string source,
        string sourceHash,
        ModelScript parsedModel)
    {
        Source = source ??
            throw new ArgumentNullException(nameof(source));
        SourceHash = sourceHash ??
            throw new ArgumentNullException(nameof(sourceHash));
        ParsedModel = parsedModel ??
            throw new ArgumentNullException(nameof(parsedModel));
    }

    public void MarkBuildSuccess(
        PartDocument document,
        string source,
        string sourceHash,
        ModelScript parsedModel,
        object inspection,
        string buildMode)
    {
        Document = document ??
            throw new ArgumentNullException(nameof(document));
        Source = source ??
            throw new ArgumentNullException(nameof(source));
        SourceHash = sourceHash ??
            throw new ArgumentNullException(nameof(sourceHash));
        ParsedModel = parsedModel ??
            throw new ArgumentNullException(nameof(parsedModel));
        LastInspection = inspection ??
            throw new ArgumentNullException(nameof(inspection));
        LastBuildMode = buildMode;
        BuildGeneration++;
        Revision++;
        Bindings.Refresh(document);
    }

    public void MarkIncrementalModify(
        PartDocument document,
        object inspection)
    {
        Document = document ??
            throw new ArgumentNullException(nameof(document));
        Source = null;
        SourceHash = null;
        LastInspection = inspection ??
            throw new ArgumentNullException(nameof(inspection));
        LastBuildMode = "modify_incremental";
        Revision++;
        Bindings.Refresh(document);
    }

    public void MarkModify(
        PartDocument document,
        object inspection)
    {
        Document = document ??
            throw new ArgumentNullException(nameof(document));
        Source = null;
        SourceHash = null;
        ParsedModel = null;
        LastInspection = inspection ??
            throw new ArgumentNullException(nameof(inspection));
        LastBuildMode = "modify";
        Revision++;
        Bindings.Refresh(document);
    }
}
