using TEKLauncher.Data;

namespace TEKLauncher.Platform;

static class LinuxCompatDataResolver
{
    const string ArkAppId = "346110";

    public static string GetManagedCompatDataPath(string gameRoot)
    {
        string normalizedPath = Path.GetFullPath(gameRoot).TrimEnd(Path.DirectorySeparatorChar);
        int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(normalizedPath);
        string folderName = $"{ArkAppId}-{hash:X8}";
        return Path.Combine(LauncherBootstrap.AppDataFolder, "compatdata", folderName);
    }

    public static string GetResolvedCompatDataPath(string gameRoot)
    {
        if (!string.IsNullOrWhiteSpace(Settings.LinuxCompatDataPath))
            return Path.GetFullPath(Settings.LinuxCompatDataPath);

        string? libraryRoot = TryGetSteamLibraryRoot(gameRoot);
        return libraryRoot is not null
          ? Path.Combine(libraryRoot, "steamapps", "compatdata", ArkAppId)
          : GetManagedCompatDataPath(gameRoot);
    }

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