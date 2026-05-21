using System.Linq;

namespace TEKLauncher.UI;

readonly record struct ExistingInstallValidation(bool FilesExist, bool IsPreAquatica);

readonly record struct InstallTargetValidation(bool PathExists, long FreeSpace, long RequiredSpace)
{
    public bool EnoughSpace => PathExists && FreeSpace >= RequiredSpace;
}

static class FirstLaunchWorkflow
{
    public const long PreAquaticaBytes = 138512695296;
    public const long LatestBytes = 178241142784;

    public static void ApplySelection(string path, bool preAquatica)
    {
        Settings.PreAquatica = preAquatica;
        Game.Path = path;
    }

    public static ExistingInstallValidation EvaluateExistingInstall(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new(false, true);

        bool filesExist = File.Exists(Path.Combine(path, "ShooterGame", "Binaries", "Win64", "ShooterGame.exe"));
        bool isPreAquatica = filesExist && !Directory.Exists(Path.Combine(path, "ShooterGame", "Content", "Abyss"));
        return new(filesExist, isPreAquatica);
    }

    public static InstallTargetValidation EvaluateInstallTarget(string? path, bool preAquatica)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return new(false, 0, GetRequiredSpace(preAquatica));

        return new(true, LauncherPlatform.Current.GetDiskFreeSpace(path), GetRequiredSpace(preAquatica));
    }

    public static int GetRequiredGigabytes(bool preAquatica) => preAquatica ? 129 : 166;

    public static long GetRequiredSpace(bool preAquatica) => preAquatica ? PreAquaticaBytes : LatestBytes;

    public static bool IsInstallDirectoryNonEmpty(string path) => Directory.EnumerateFileSystemEntries(path).Any();

    public static bool IsPathInUserProfile(string path)
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase);
    }

    public static string? SuggestedGamePath => Steam.App.GamePath;
}