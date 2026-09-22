using System;
using System.Runtime.InteropServices;
using Inventor;

namespace InventorModel.Inventor;

public sealed class InventorSession
{
    public Application Application { get; }
    public InventorSession(Application app)=>Application=app??throw new ArgumentNullException(nameof(app));
    public static InventorSession Connect()
    {
        object app;
        try{app=Marshal.GetActiveObject("Inventor.Application");}
        catch(COMException)
        {
            var type=Type.GetTypeFromProgID("Inventor.Application")??throw new InvalidOperationException("Autodesk Inventor is not installed.");
            app=Activator.CreateInstance(type)??throw new InvalidOperationException("Unable to start Inventor.");
            ((Application)app).Visible=true;
        }
        return new InventorSession((Application)app);
    }
    public PartDocument NewPart()
    {
        var template=Application.FileManager.GetTemplateFile(DocumentTypeEnum.kPartDocumentObject);
        return (PartDocument)Application.Documents.Add(DocumentTypeEnum.kPartDocumentObject,template,true);
    }
}
