using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Inventor;

namespace InventorModel.Inventor;

public sealed class InventorSession
{
    private const string ProgId = "Inventor.Application";
    private const string ExecutableEnvironmentVariable =
        "INVENTORMODEL_INVENTOR_EXE";
    private const string InstallRootEnvironmentVariable =
        "InventorInstallRoot";
    private const int StartupTimeoutMilliseconds = 60000;
    private const int PollIntervalMilliseconds = 250;

    public Application Application { get; }

    public InventorSession(Application app) =>
        Application = app ??
            throw new ArgumentNullException(nameof(app));

    public static bool TryConnect(
        out InventorSession? session)
    {
        try
        {
            object instance =
                GetActiveComObject(ProgId);

            if (instance is not Application application)
            {
                session = null;
                return false;
            }

            session =
                new InventorSession(application);
            return true;
        }
        catch (COMException)
        {
            session = null;
            return false;
        }
        catch (InvalidCastException)
        {
            session = null;
            return false;
        }
    }

    public static InventorSession Connect()
    {
        if (TryConnect(
                out InventorSession? running) &&
            running != null)
        {
            running.Application.Visible = true;
            return running;
        }

        Process[] existingProcesses =
            Process.GetProcessesByName("Inventor");

        Process? startedProcess = null;

        if (existingProcesses.Length == 0)
        {
            string executable =
                ResolveInventorExecutable();

            startedProcess =
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = executable,
                        UseShellExecute = true,
                        WorkingDirectory =
                            Path.GetDirectoryName(
                                executable) ??
                            Environment.CurrentDirectory
                    });

            if (startedProcess == null)
            {
                throw new InvalidOperationException(
                    "Inventor.exe could not be started.");
            }
        }

        var stopwatch =
            Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds <
               StartupTimeoutMilliseconds)
        {
            if (TryConnect(
                    out InventorSession? connected) &&
                connected != null)
            {
                connected.Application.Visible = true;
                return connected;
            }

            if (startedProcess != null &&
                startedProcess.HasExited)
            {
                throw new InvalidOperationException(
                    "Inventor.exe exited before its COM application object became available. " +
                    "Exit code: " +
                    startedProcess.ExitCode +
                    ".");
            }

            Thread.Sleep(
                PollIntervalMilliseconds);
        }

        throw new TimeoutException(
            existingProcesses.Length > 0
                ? "An Inventor process is running but its COM application object was not registered within 60 seconds."
                : "Inventor.exe was started but its COM application object was not registered within 60 seconds.");
    }

    public PartDocument NewPart()
    {
        string template =
            Application.FileManager.GetTemplateFile(
                DocumentTypeEnum.kPartDocumentObject);

        return (PartDocument)
            Application.Documents.Add(
                DocumentTypeEnum.kPartDocumentObject,
                template,
                true);
    }

    private static string ResolveInventorExecutable()
    {
        var candidates =
            new List<string>();

        string? explicitExecutable =
            Environment.GetEnvironmentVariable(
                ExecutableEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(
                explicitExecutable))
        {
            candidates.Add(
                explicitExecutable);
        }

        string? installRoot =
            Environment.GetEnvironmentVariable(
                InstallRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(
                installRoot))
        {
            candidates.Add(
                Path.Combine(
                    installRoot,
                    "Bin",
                    "Inventor.exe"));
        }

        candidates.Add(
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),
                "Autodesk",
                "Inventor 2023",
                "Bin",
                "Inventor.exe"));

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(
            "Autodesk Inventor executable was not found. " +
            "Set INVENTORMODEL_INVENTOR_EXE or InventorInstallRoot. Checked: " +
            string.Join("; ", candidates));
    }

    private static object GetActiveComObject(
        string progId)
    {
        int hresult =
            CLSIDFromProgID(
                progId,
                out Guid classId);

        if (hresult < 0)
            Marshal.ThrowExceptionForHR(hresult);

        GetActiveObject(
            ref classId,
            IntPtr.Zero,
            out object instance);

        return instance;
    }

    [DllImport(
        "ole32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(
        string lpszProgID,
        out Guid lpclsid);

    [DllImport(
        "oleaut32.dll",
        PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        IntPtr pvReserved,
        [MarshalAs(UnmanagedType.IUnknown)]
        out object ppunk);
}
