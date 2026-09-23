using System;
using System.IO;
using System.Linq;
using System.Text;
using InventorModel.Core;
using InventorModel.Core.Diagnostics;

namespace InventorModel.Core.Workspace;

public sealed class ModelWorkspace
{
    private ModelWorkspace(
        string sessionId,
        string sessionDirectory)
    {
        SessionId = sessionId;
        SessionDirectory = sessionDirectory;
        RendersDirectory =
            Ensure(Path.Combine(
                sessionDirectory,
                "renders"));
        ScriptsDirectory =
            Ensure(Path.Combine(
                sessionDirectory,
                "scripts"));
        OutputDirectory =
            Ensure(Path.Combine(
                sessionDirectory,
                "output"));
        TempDirectory =
            Ensure(Path.Combine(
                sessionDirectory,
                "temp"));
    }

    public static string RootDirectory =>
        InventorModelPaths.WorkspaceDirectory;

    public static string SessionsDirectory =>
        Ensure(Path.Combine(
            RootDirectory,
            "Sessions"));

    public string SessionId { get; }

    public string SessionDirectory { get; }

    public string RendersDirectory { get; }

    public string ScriptsDirectory { get; }

    public string OutputDirectory { get; }

    public string TempDirectory { get; }

    public static ModelWorkspace CreateSession(
        string prefix = "mcp")
    {
        string safePrefix =
            SanitizeFileName(prefix);
        string id =
            DateTime.Now.ToString("HHmmss") +
            "-" +
            Guid.NewGuid()
                .ToString("N")
                .Substring(0, 8);

        string directory =
            Path.Combine(
                SessionsDirectory,
                DateTime.Now.ToString("yyyyMMdd"),
                safePrefix + "-" + id);

        return new ModelWorkspace(
            safePrefix + "-" + id,
            Ensure(directory));
    }

    public string CreateRenderDirectory()
    {
        string path =
            Path.Combine(
                RendersDirectory,
                "render-" +
                DateTime.Now.ToString("HHmmssfff") +
                "-" +
                Guid.NewGuid()
                    .ToString("N")
                    .Substring(0, 6));

        return Ensure(path);
    }

    public string SaveModelScript(
        string source)
    {
        string path =
            Path.Combine(
                ScriptsDirectory,
                "model.ivmodel");

        File.WriteAllText(
            path,
            source ?? string.Empty,
            new UTF8Encoding(false));

        return path;
    }

    public string GetDefaultOutputPath(
        string fileName = "model.ipt")
    {
        string safeName =
            SanitizeFileName(
                Path.GetFileName(fileName));

        if (string.IsNullOrWhiteSpace(
                safeName))
        {
            safeName = "model.ipt";
        }

        if (string.IsNullOrWhiteSpace(
                Path.GetExtension(safeName)))
        {
            safeName += ".ipt";
        }

        return Path.Combine(
            OutputDirectory,
            safeName);
    }

    public void ClearTemp()
    {
        try
        {
            if (Directory.Exists(
                    TempDirectory))
            {
                Directory.Delete(
                    TempDirectory,
                    true);
            }
        }
        catch (Exception ex)
        {
            RuntimeLog.Warning(
                "MCP.Workspace",
                "Temporary MCP workspace files could not be cleared: " +
                TempDirectory,
                ex);
        }
    }

    private static string Ensure(
        string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static string SanitizeFileName(
        string value)
    {
        string text =
            value ??
            string.Empty;

        char[] invalid =
            Path.GetInvalidFileNameChars();

        return new string(
            text.Select(
                    ch =>
                        invalid.Contains(ch)
                            ? '_'
                            : ch)
                .ToArray())
            .Trim();
    }
}
