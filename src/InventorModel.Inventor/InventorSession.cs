using System;
using System.Runtime.InteropServices;
using Inventor;

namespace InventorModel.Inventor;

public sealed class InventorSession
{
    public Application Application { get; }

    public InventorSession(Application app) =>
        Application = app ?? throw new ArgumentNullException(nameof(app));

    public static InventorSession Connect()
    {
        object app;

        try
        {
            app = GetActiveComObject("Inventor.Application");
        }
        catch (COMException)
        {
            var type = Type.GetTypeFromProgID("Inventor.Application")
                ?? throw new InvalidOperationException("Autodesk Inventor is not installed.");

            app = Activator.CreateInstance(type)
                ?? throw new InvalidOperationException("Unable to start Inventor.");

            ((Application)app).Visible = true;
        }

        return new InventorSession((Application)app);
    }

    public PartDocument NewPart()
    {
        var template = Application.FileManager.GetTemplateFile(
            DocumentTypeEnum.kPartDocumentObject);

        return (PartDocument)Application.Documents.Add(
            DocumentTypeEnum.kPartDocumentObject,
            template,
            true);
    }

    private static object GetActiveComObject(string progId)
    {
        int hresult = CLSIDFromProgID(progId, out Guid classId);
        if (hresult < 0)
            Marshal.ThrowExceptionForHR(hresult);

        GetActiveObject(
            ref classId,
            IntPtr.Zero,
            out object instance);

        return instance;
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(
        string lpszProgID,
        out Guid lpclsid);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        IntPtr pvReserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);
}
