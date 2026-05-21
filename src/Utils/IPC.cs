using System.Threading;
using System.Runtime.Versioning;
using TEKLauncher.Platform;

namespace TEKLauncher.Utils;

/// <summary>Inter-process communication manager.</summary>
static class IPC
{
    static EventWaitHandle? s_inputEvent;
    static FileStream? s_lockFile;
    /// <summary>Closes and releases all IPC objects.</summary>
    public static void Dispose()
    {
        s_inputEvent?.Dispose();
        s_inputEvent = null;

        s_lockFile?.Dispose();
        s_lockFile = null;
    }
    /// <summary>Creates IPC objects and checks whether another instance of the launcher is already running.</summary>
    /// <returns><see langword="false"/> if another instance of the launcher is already running; otherwise, <see langword="true"/>.</returns>
    public static bool Initialize()
    {
        if (OperatingSystem.IsWindows())
            return InitializeWindows();

        return InitializeLockFile();
    }

    [SupportedOSPlatform("windows")]
    static bool InitializeWindows()
    {
        if (EventWaitHandle.TryOpenExisting("TEKLauncherInput", out var result))
        {
            result.Dispose();
            return false;
        }

        s_inputEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "TEKLauncherInput");
        return true;
    }

    static bool InitializeLockFile()
    {
        string appDataFolder = LauncherPlatform.Current.AppDataFolder;
        Directory.CreateDirectory(appDataFolder);

        string lockFilePath = Path.Combine(appDataFolder, "launcher.lock");

        try
        {
            s_lockFile = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            s_lockFile.SetLength(0);
            using var writer = new StreamWriter(s_lockFile, leaveOpen: true);
            writer.Write(Environment.ProcessId);
            writer.Flush();
            s_lockFile.Position = 0;
            return true;
        }
        catch (IOException)
        {
            s_lockFile?.Dispose();
            s_lockFile = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            s_lockFile?.Dispose();
            s_lockFile = null;
            return false;
        }
    }
}