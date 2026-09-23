using System;
using System.IO;
using System.Linq;
using System.Text;
using InventorModel.Core.Diagnostics;
using InventorModel.Core;

namespace InventorModel.Core.Ai;

public sealed class AiWorkspace
{
    private AiWorkspace(string sessionId, string sessionDirectory)
    {
        SessionId = sessionId;
        SessionDirectory = sessionDirectory;
        AttachmentsDirectory = Ensure(Path.Combine(sessionDirectory, "attachments"));
        RendersDirectory = Ensure(Path.Combine(sessionDirectory, "renders"));
        ScriptsDirectory = Ensure(Path.Combine(sessionDirectory, "scripts"));
        OutputDirectory = Ensure(Path.Combine(sessionDirectory, "output"));
        TempDirectory = Ensure(Path.Combine(sessionDirectory, "temp"));
        HistoryPath = Path.Combine(sessionDirectory, "history.md");
    }

    public static string RootDirectory =>
        InventorModelPaths.WorkspaceDirectory;

    public static string SessionsDirectory =>
        Ensure(Path.Combine(RootDirectory, "Sessions"));

    public string SessionId { get; }

    public string SessionDirectory { get; }

    public string AttachmentsDirectory { get; }

    public string RendersDirectory { get; }

    public string ScriptsDirectory { get; }

    public string OutputDirectory { get; }

    public string TempDirectory { get; }

    public string HistoryPath { get; }

    public static AiWorkspace CreateSession(string prefix = "session")
    {
        string safePrefix = SanitizeFileName(prefix);
        string id = DateTime.Now.ToString("HHmmss") + "-" +
                    Guid.NewGuid().ToString("N").Substring(0, 8);
        string directory = Path.Combine(
            SessionsDirectory,
            DateTime.Now.ToString("yyyyMMdd"),
            safePrefix + "-" + id);

        return new AiWorkspace(safePrefix + "-" + id, Ensure(directory));
    }

    public string ImportAttachment(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Attachment path is required.", nameof(sourcePath));

        string fullSource = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSource))
            throw new FileNotFoundException("Attachment was not found.", fullSource);

        string extension = NormalizeImageExtension(Path.GetExtension(fullSource));
        string name = SanitizeFileName(Path.GetFileNameWithoutExtension(fullSource));
        if (string.IsNullOrWhiteSpace(name))
            name = "image";

        string destination = UniquePath(
            AttachmentsDirectory,
            name,
            extension);

        File.Copy(fullSource, destination, false);
        return destination;
    }

    public string CreateAttachmentPath(string extension = ".png")
    {
        string normalized = NormalizeImageExtension(extension);
        return UniquePath(
            AttachmentsDirectory,
            "clipboard-" + DateTime.Now.ToString("HHmmssfff"),
            normalized);
    }

    public string CreateRenderDirectory()
    {
        string path = Path.Combine(
            RendersDirectory,
            "render-" + DateTime.Now.ToString("HHmmssfff") + "-" +
            Guid.NewGuid().ToString("N").Substring(0, 6));
        return Ensure(path);
    }

    public string SaveModelScript(string source)
    {
        string path = Path.Combine(ScriptsDirectory, "model.ivmodel");
        File.WriteAllText(path, source ?? string.Empty, new UTF8Encoding(false));
        return path;
    }

    public string GetDefaultOutputPath(string fileName = "model.ipt")
    {
        string safeName = SanitizeFileName(Path.GetFileName(fileName));
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "model.ipt";
        if (string.IsNullOrWhiteSpace(Path.GetExtension(safeName)))
            safeName += ".ipt";
        return Path.Combine(OutputDirectory, safeName);
    }

    public void ClearTemp()
    {
        try
        {
            if (Directory.Exists(TempDirectory))
                Directory.Delete(TempDirectory, true);
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "AI.Workspace",
                "Temporary AI workspace files could not be cleared: " +
                TempDirectory,
                ex);
        }
    }

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static string NormalizeImageExtension(string extension)
    {
        string value = (extension ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            ".jpg" => ".jpg",
            ".jpeg" => ".jpeg",
            ".webp" => ".webp",
            _ => ".png"
        };
    }

    private static string UniquePath(string directory, string baseName, string extension)
    {
        string safeBase = SanitizeFileName(baseName);
        if (string.IsNullOrWhiteSpace(safeBase))
            safeBase = "file";

        string path = Path.Combine(directory, safeBase + extension);
        if (!File.Exists(path))
            return path;

        for (int i = 2; i < 10000; i++)
        {
            path = Path.Combine(directory, safeBase + "-" + i + extension);
            if (!File.Exists(path))
                return path;
        }

        return Path.Combine(
            directory,
            safeBase + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + extension);
    }

    private static string SanitizeFileName(string value)
    {
        string text = value ?? string.Empty;
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(text.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
    }
}
