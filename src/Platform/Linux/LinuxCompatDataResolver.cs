using ObeliskLauncher.Data;

namespace ObeliskLauncher.Platform;

static class LinuxCompatDataResolver
{
    public static string GetManagedCompatDataPath(string gameRoot, string? steamAppId = null)
    {
        string normalizedPath = Path.GetFullPath(gameRoot).TrimEnd(Path.DirectorySeparatorChar);
        int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(normalizedPath);
        string folderName = $"{NormalizeSteamAppId(steamAppId)}-{hash:X8}";
        return Path.Combine(LauncherBootstrap.AppDataFolder, "compatdata", folderName);
    }

    public static string GetResolvedCompatDataPath(string gameRoot, string? steamAppId = null)
    {
        string gameId = string.IsNullOrWhiteSpace(steamAppId) ? GameCatalog.DefaultGameId : Settings.GetGameIdBySteamAppId(steamAppId!);
        if (!string.IsNullOrWhiteSpace(Settings.GetLinuxCompatDataPath(gameId)))
            return Path.GetFullPath(Settings.GetLinuxCompatDataPath(gameId));

        string appId = NormalizeSteamAppId(steamAppId);
        string? libraryRoot = TryGetSteamLibraryRoot(gameRoot);
        return libraryRoot is not null
          ? Path.Combine(libraryRoot, "steamapps", "compatdata", appId)
          : GetManagedCompatDataPath(gameRoot, appId);
    }

    static string NormalizeSteamAppId(string? steamAppId)
      => uint.TryParse(steamAppId, out uint appId) && appId > 0
        ? appId.ToString()
                : ActiveGameManager.Current.SteamAppId.ToString();

    public static string? TryGetSteamLibraryRoot(string gameRoot)
    {
        DirectoryInfo? directory = new(gameRoot);
        while (directory is not null)
        {
            DirectoryInfo? parent = directory.Parent;
            if (directory.Name.Equals("common", StringComparison.OrdinalIgnoreCase)
                && parent?.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase) == true
                && parent.Parent is not null)
                return parent.Parent.FullName;

            directory = parent;
        }

        return null;
    }
}