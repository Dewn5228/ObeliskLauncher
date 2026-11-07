using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace ObeliskLauncher.Platform;

[SupportedOSPlatform("windows")]
static class WindowsShortcutService
{
    public static string CreateGameShortcut(string destinationFolder, string displayName, string gameId)
    {
        string targetPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Unable to determine the launcher executable path.");

        string shortcutPath = Path.Combine(destinationFolder, SanitizeFileName(displayName) + ".lnk");
        string arguments = $"--play --game {gameId}";
        string workingDirectory = Path.GetDirectoryName(targetPath) ?? destinationFolder;

        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(targetPath);
            link.SetArguments(arguments);
            link.SetWorkingDirectory(workingDirectory);
            link.SetIconLocation(targetPath, 0);
            link.SetDescription($"{displayName} - Obelisk Launcher");
            ((IPersistFile)link).Save(shortcutPath, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }

        return shortcutPath;
    }

    static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (char ch in name)
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        return builder.ToString().Trim();
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    class ShellLink
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    interface IShellLinkW
    {
        void GetPath();
        void GetIDList();
        void SetIDList();
        void GetDescription();
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory();
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string workingDirectory);
        void GetArguments();
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey();
        void SetHotkey();
        void GetShowCmd();
        void SetShowCmd();
        void GetIconLocation();
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath();
        void Resolve();
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    interface IPersistFile
    {
        void GetClassID(out Guid classId);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile();
    }
}
