using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace TEKLauncher.Platform;

sealed class LinuxLauncherPlatform : ILauncherPlatform
{
    string VersionFilePath => Path.Combine(AppDataFolder, "LastLaunchedVersion.txt");

    public string AppDataFolder { get; } = GetAppDataFolder();

    public string GetGameConfigDirectory(string gamePath) => SelectPreferredDirectory(GetGameConfigDirectoryCandidates(gamePath));

    public string GetGameProfilesDirectory(string gamePath) => SelectPreferredDirectory(GetGameProfilesDirectoryCandidates(gamePath));

    public long GetDiskFreeSpace(string directory)
    {
        string fullPath = Path.GetFullPath(directory);
        string root = Path.GetPathRoot(fullPath) ?? Path.DirectorySeparatorChar.ToString();
        return new DriveInfo(root).AvailableFreeSpace;
    }

    public string? GetLastLaunchedVersion() => File.Exists(VersionFilePath) ? File.ReadAllText(VersionFilePath).Trim() : null;

    public void SetLastLaunchedVersion(string version)
    {
        Directory.CreateDirectory(AppDataFolder);
        File.WriteAllText(VersionFilePath, version);
    }

    public int? GetSteamProcessId()
    {
        try
        {
            Process? steamProcess = Process.GetProcessesByName("steam").FirstOrDefault();
            return steamProcess?.Id;
        }
        catch
        {
            return null;
        }
    }

    public string? GetSteamInstallPath()
    {
        foreach (string path in GetSteamCandidates())
            if (Directory.Exists(path))
                return path;
        return null;
    }

    public string? GetSteamClientDllPath()
    {
        string? installPath = GetSteamInstallPath();
        if (string.IsNullOrWhiteSpace(installPath))
            return null;

        string[] candidates =
        [
          Path.Combine(installPath, "linux64", "steamclient.so"),
      Path.Combine(installPath, "steamrt64", "steamclient.so"),
      Path.Combine(installPath, "ubuntu12_64", "steamclient.so"),
      Path.Combine(installPath, "linux32", "steamclient.so"),
      Path.Combine(installPath, "steamrt32", "steamclient.so"),
      Path.Combine(installPath, "ubuntu12_32", "steamclient.so")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    public bool TryLoadModule(string modulePath) => NativeLibrary.TryLoad(modulePath, out _);

    static string SelectPreferredDirectory(IEnumerable<string> candidates)
    {
        string? fallback = null;
        foreach (string candidate in candidates)
        {
            fallback ??= candidate;
            if (Directory.Exists(candidate))
                return candidate;
        }

        return fallback ?? string.Empty;
    }

    static IEnumerable<string> GetGameConfigDirectoryCandidates(string gamePath)
    {
        foreach (string compatBase in GetProtonSavedRootCandidates(gamePath))
        {
            yield return Path.Combine(compatBase, "Config", "WindowsNoEditor");
            yield return Path.Combine(compatBase, "Config", "LinuxNoEditor");
        }

        yield return Path.Combine(gamePath, "ShooterGame", "Saved", "Config", "WindowsNoEditor");
        yield return Path.Combine(gamePath, "ShooterGame", "Saved", "Config", "LinuxNoEditor");
    }

    static IEnumerable<string> GetGameProfilesDirectoryCandidates(string gamePath)
    {
        foreach (string compatBase in GetProtonSavedRootCandidates(gamePath))
            yield return Path.Combine(compatBase, "LocalProfiles");

        yield return Path.Combine(gamePath, "ShooterGame", "Saved", "LocalProfiles");
    }

    static IEnumerable<string> GetProtonSavedRootCandidates(string gamePath)
    {
        string compatPath = LinuxCompatDataResolver.GetResolvedCompatDataPath(gamePath, ActiveGameManager.Current.SteamAppId.ToString());

        string[] userNames = ["steamuser", Environment.UserName];
        string[] appNames = ["ARK", "ShooterGame"];

        foreach (string userName in userNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string localAppData = Path.Combine(compatPath, "pfx", "drive_c", "users", userName, "AppData", "Local");
            foreach (string appName in appNames)
                yield return Path.Combine(localAppData, appName, "Saved");

            string documentsRoot = Path.Combine(compatPath, "pfx", "drive_c", "users", userName, "Documents", "My Games");
            yield return Path.Combine(documentsRoot, "ARK", "Saved");
            yield return Path.Combine(documentsRoot, "ShooterGame", "Saved");
        }
    }

    static string GetAppDataFolder()
    {
        string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome))
            return Path.Combine(xdgDataHome, "TEK Launcher");
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", "TEK Launcher");
    }

    static IEnumerable<string> GetSteamCandidates()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(home, ".local", "share", "Steam");
        yield return Path.Combine(home, ".steam", "steam");
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
    }
}