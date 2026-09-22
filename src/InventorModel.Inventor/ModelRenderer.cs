using System.Collections.Generic;
using System.IO;
using Inventor;

namespace InventorModel.Inventor;

public sealed class ModelRenderer
{
    private readonly Application _app;public ModelRenderer(Application app)=>_app=app;
    public IReadOnlyList<string> RenderFourViews(PartDocument doc,string directory,int width=800,int height=800)
    {
        Directory.CreateDirectory(directory);doc.Activate();var view=_app.ActiveView;var list=new List<string>();
        Capture(ViewOrientationTypeEnum.kFrontViewOrientation,"front");Capture(ViewOrientationTypeEnum.kTopViewOrientation,"top");
        Capture(ViewOrientationTypeEnum.kRightViewOrientation,"right");Capture(ViewOrientationTypeEnum.kIsoTopRightViewOrientation,"iso");return list;
        void Capture(ViewOrientationTypeEnum orientation,string name)
        {
            var camera=view.Camera;camera.ViewOrientationType=orientation;camera.Apply();view.Fit();view.Update();
            var file=System.IO.Path.Combine(directory,name+".png");view.SaveAsBitmap(file,width,height);list.Add(file);
        }
    }
}
