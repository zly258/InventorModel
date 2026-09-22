using System.Text;
using Inventor;

namespace InventorModel.Inventor;

public sealed class ModelInspector
{
    public string Inspect(PartDocument doc)
    {
        var c=doc.ComponentDefinition;var b=new StringBuilder();var box=c.RangeBox;
        b.AppendLine($"part {doc.DisplayName}");b.AppendLine($"bodies {c.SurfaceBodies.Count}");b.AppendLine($"features {c.Features.Count}");
        b.AppendLine($"size_mm {(box.MaxPoint.X-box.MinPoint.X)*10:0.###} {(box.MaxPoint.Y-box.MinPoint.Y)*10:0.###} {(box.MaxPoint.Z-box.MinPoint.Z)*10:0.###}");
        b.AppendLine("parameters");foreach(UserParameter p in c.Parameters.UserParameters)b.AppendLine($"  {p.Name} = {p.Expression}");
        b.AppendLine("feature_tree");foreach(PartFeature f in c.Features)b.AppendLine($"  {f.Name} suppressed={f.Suppressed}");
        return b.ToString();
    }
}
