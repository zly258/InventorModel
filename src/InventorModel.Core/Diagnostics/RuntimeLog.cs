using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using InventorModel.Core;

namespace InventorModel.Core.Diagnostics;

public static class RuntimeLog
{
    private const long MaximumBytes = 2 * 1024 * 1024;
    private static readonly object SyncRoot = new();

    public static string LogDirectory =>
        InventorModelPaths.LogsDirectory;

    public static string LogPath => Path.Combine(LogDirectory, "runtime.log");

    public static void Info(string area, string message) =>
        Write("INFO", area, message, null);

    public static void Warning(
        string area,
        string message,
        Exception? exception = null) =>
        Write("WARN", area, message, exception);

    public static void Error(
        string area,
        string message,
        Exception? exception = null) =>
        Write("ERROR", area, message, exception);

    private static void Write(
        string level,
        string area,
        string message,
        Exception? exception)
    {
        try
        {
            lock (SyncRoot)
            {
                RotateIfNeeded();

                var line = new StringBuilder()
                    .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                    .Append(" [")
                    .Append(level)
                    .Append("] [")
                    .Append(string.IsNullOrWhiteSpace(area) ? "General" : area.Trim())
                    .Append("] ")
                    .Append(message ?? string.Empty);

                if (exception != null)
                {
                    line.Append(" | ")
                        .Append(exception.GetType().Name)
                        .Append(": ")
                        .Append(exception.Message);

                    if (!string.IsNullOrWhiteSpace(exception.StackTrace))
                        line.AppendLine().Append(exception.StackTrace);
                }

                line.AppendLine();
                File.AppendAllText(
                    LogPath,
                    line.ToString(),
                    new UTF8Encoding(false));
            }
        }
        catch (Exception logException)
        {
            Trace.WriteLine(
                "InventorModel logging failed: " +
                logException.GetType().Name +
                ": " +
                logException.Message);
        }
    }

    private static void RotateIfNeeded()
    {
        string path = LogPath;
        if (!File.Exists(path) || new FileInfo(path).Length < MaximumBytes)
            return;

        string previous = Path.Combine(LogDirectory, "runtime.previous.log");
        if (File.Exists(previous))
            File.Delete(previous);

        File.Move(path, previous);
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
