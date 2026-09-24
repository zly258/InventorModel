using System;
using System.Runtime.InteropServices;
using Inventor;

namespace InventorModel.Inventor;

public sealed class InventorSession
{
    public Application Application { get; }

    public InventorSession(Application app) =>
        Application = app ?? throw new ArgumentNullException(nameof(app));

    public static bool TryConnect(
        out InventorSession? session)
    {
        try
        {
            var application =
                (Application)GetActiveComObject(
                    "Inventor.Application");

            session = new InventorSession(application);
            return true;
        }
        catch (COMException)
        {
            session = null;
            return false;
        }
    }

    public static InventorSession Connect()
    {
        if (TryConnect(out InventorSession? running) &&
            running != null)
        {
            running.Application.Visible = true;
            return running;
        }

        var type = Type.GetTypeFromProgID("Inventor.Application")
            ?? throw new InvalidOperationException(
                "Autodesk Inventor is not installed.");

        object app = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException(
                "Unable to start Inventor.");

        var application = (Application)app;
        application.Visible = true;
        return new InventorSession(application);
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
