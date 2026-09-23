using System;
using System.IO;

namespace InventorModel.Core;

public static class InventorModelPaths
{
    private const string ProductFolder = "InventorModel";

    public static string ProductDirectory =>
        Ensure(Path.Combine(
            ResolveDocumentsDirectory(),
            ProductFolder));

    public static string WorkspaceDirectory =>
        Ensure(Path.Combine(
            ProductDirectory,
            "Workspace"));

    public static string LogsDirectory =>
        Ensure(Path.Combine(
            ProductDirectory,
            "Logs"));

    private static string ResolveDocumentsDirectory()
    {
        string documents =
            Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);

        if (!string.IsNullOrWhiteSpace(documents))
            return documents;

        string profile =
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);

        if (!string.IsNullOrWhiteSpace(profile))
            return Path.Combine(profile, "Documents");

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
