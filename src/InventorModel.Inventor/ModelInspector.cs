using System;
using System.Collections.Generic;
using System.Text;
using Inventor;
using InventorModel.Core.Diagnostics;

namespace InventorModel.Inventor;

public sealed class ModelInspectionResult
{
    public string Part { get; set; } = string.Empty;
    public int BodyCount { get; set; }
    public int SketchCount { get; set; }
    public int FeatureCount { get; set; }
    public ModelSizeMm SizeMm { get; set; } = new ModelSizeMm();
    public List<ModelParameterInfo> Parameters { get; } =
        new List<ModelParameterInfo>();
    public List<ModelFeatureInfo> Features { get; } =
        new List<ModelFeatureInfo>();
}

public sealed class ModelSizeMm
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public sealed class ModelParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
}

public sealed class ModelFeatureInfo
{
    public string Name { get; set; } = string.Empty;
    public bool Suppressed { get; set; }
}

public sealed class ModelInspector
{
    public ModelInspectionResult InspectResult(
        PartDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        PartComponentDefinition component =
            document.ComponentDefinition;
        Box box = component.RangeBox;

        var result = new ModelInspectionResult
        {
            Part = document.DisplayName ?? string.Empty,
            BodyCount = component.SurfaceBodies.Count,
            SketchCount = component.Sketches.Count,
            FeatureCount = component.Features.Count,
            SizeMm = new ModelSizeMm
            {
                X = RoundMm(
                    box.MaxPoint.X -
                    box.MinPoint.X),
                Y = RoundMm(
                    box.MaxPoint.Y -
                    box.MinPoint.Y),
                Z = RoundMm(
                    box.MaxPoint.Z -
                    box.MinPoint.Z)
            }
        };

        foreach (UserParameter parameter in
                 component.Parameters.UserParameters)
        {
            try
            {
                result.Parameters.Add(
                    new ModelParameterInfo
                    {
                        Name =
                            parameter.Name ??
                            string.Empty,
                        Expression =
                            parameter.Expression ??
                            string.Empty,
                        Units =
                            parameter.get_Units() ??
                            string.Empty
                    });
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "Inventor.Inspect",
                    "A user parameter could not be inspected.",
                    ex);
            }
        }

        foreach (PartFeature feature in
                 component.Features)
        {
            try
            {
                result.Features.Add(
                    new ModelFeatureInfo
                    {
                        Name =
                            feature.Name ??
                            string.Empty,
                        Suppressed =
                            feature.Suppressed
                    });
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "Inventor.Inspect",
                    "A Part feature could not be inspected.",
                    ex);
            }
        }

        return result;
    }

    public string Inspect(
        PartDocument document)
    {
        ModelInspectionResult result =
            InspectResult(document);

        var builder = new StringBuilder();
        builder.AppendLine(
            "part " +
            result.Part);
        builder.AppendLine(
            "bodies " +
            result.BodyCount);
        builder.AppendLine(
            "sketches " +
            result.SketchCount);
        builder.AppendLine(
            "features " +
            result.FeatureCount);
        builder.AppendLine(
            "size_mm " +
            result.SizeMm.X.ToString("0.###") +
            " " +
            result.SizeMm.Y.ToString("0.###") +
            " " +
            result.SizeMm.Z.ToString("0.###"));

        builder.AppendLine("parameters");
        foreach (ModelParameterInfo parameter in
                 result.Parameters)
        {
            builder.AppendLine(
                "  " +
                parameter.Name +
                " = " +
                parameter.Expression);
        }

        builder.AppendLine("feature_tree");
        foreach (ModelFeatureInfo feature in
                 result.Features)
        {
            builder.AppendLine(
                "  " +
                feature.Name +
                " suppressed=" +
                feature.Suppressed);
        }

        return builder.ToString();
    }

    private static double RoundMm(
        double centimeters) =>
        Math.Round(
            centimeters * 10.0,
            6,
            MidpointRounding.AwayFromZero);
}
