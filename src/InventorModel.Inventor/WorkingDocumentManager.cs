using System;
using Inventor;

namespace InventorModel.Inventor;

/// <summary>
/// Owns the lifetime reference for the single Inventor Part used by one MCP session.
/// Script execution never creates documents; creation and attachment are centralized here.
/// </summary>
public sealed class WorkingDocumentManager
{
    private readonly Application _application;
    private PartDocument? _workingDocument;
    private bool _ownsWorkingDocument;

    public WorkingDocumentManager(Application application)
    {
        _application = application ??
            throw new ArgumentNullException(nameof(application));
    }

    public bool OwnsWorkingDocument =>
        _ownsWorkingDocument &&
        TryGetWorkingDocument(out _);

    public bool TryGetWorkingDocument(
        out PartDocument? document)
    {
        document = _workingDocument;

        if (document == null)
            return false;

        try
        {
            _ = document.DisplayName;
            _ = document.DocumentType;
            return true;
        }
        catch
        {
            _workingDocument = null;
            _ownsWorkingDocument = false;
            document = null;
            return false;
        }
    }

    public PartDocument AcquireForBuild()
    {
        if (TryGetWorkingDocument(
                out PartDocument? existing) &&
            existing != null)
        {
            return existing;
        }

        // Register the new Part immediately. A failed build must not cause
        // the next retry to create another document.
        PartDocument created =
            new InventorSession(_application).NewPart();

        _workingDocument = created;
        _ownsWorkingDocument = true;
        return created;
    }

    public PartDocument GetRequiredOrAttachActive()
    {
        if (TryGetWorkingDocument(
                out PartDocument? working) &&
            working != null)
        {
            return working;
        }

        PartDocument? active =
            _application.ActiveDocument as PartDocument;

        if (active != null)
        {
            _workingDocument = active;
            _ownsWorkingDocument = false;
            return active;
        }

        throw new InvalidOperationException(
            "No active or session working Inventor Part is available.");
    }

    public void Attach(
        PartDocument document,
        bool ownedByInventorModel = false)
    {
        _workingDocument = document ??
            throw new ArgumentNullException(nameof(document));
        _ownsWorkingDocument = ownedByInventorModel;
    }
}
