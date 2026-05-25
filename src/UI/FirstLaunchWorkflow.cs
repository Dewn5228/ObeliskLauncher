using System.Linq;

namespace ObeliskLauncher.UI;

readonly record struct ExistingInstallValidation(bool FilesExist, bool IsPreAquatica);

readonly record struct DetectedGameInstall(string GameId, string? Path, ExistingInstallValidation Validation)
{
    public bool IsDetected => Validation.FilesExist && !string.IsNullOrWhiteSpace(Path);
}

readonly record struct InstallTargetValidation(bool PathExists, long FreeSpace, long RequiredSpace)
{
    public bool EnoughSpace => PathExists && FreeSpace >= RequiredSpace;
}

static class FirstLaunchWorkflow
{
    public const long PreAquaticaBytes = 138512695296;
    public const long LatestBytes = 178241142784;

    static readonly string[] s_supportedExeRelativePaths =
    [
        "ShooterGame/Binaries/Win64/ShooterGame.exe",
        "ShooterGame/Binaries/Win64/ArkAscended.exe",
        "ShooterGame/Binaries/Win64/ArkAscended_BE.exe"
    ];

    public static void ApplySelection(string gameId, string path, bool preAquatica)
    {
        if (string.Equals(gameId, GameCatalog.AseGameId, StringComparison.OrdinalIgnoreCase))
            Settings.AseGamePath = Path.GetFullPath(path);
        else if (string.Equals(gameId, GameCatalog.AsaGameId, StringComparison.OrdinalIgnoreCase))
            Settings.AsaGamePath = Path.GetFullPath(path);

        ActiveGameManager.Configure(gameId, path);
        Settings.PreAquatica = gameId == GameCatalog.AseGameId && preAquatica;
    }

    public static ExistingInstallValidation EvaluateExistingInstall(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new(false, true);

        bool filesExist = HasSupportedExecutable(path);
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

    public static string? GetSuggestedGamePath(string gameId)
    {
        GameCatalogEntry catalog = GameCatalog.GetByGameId(gameId);
        return Steam.App.TryGetGamePath(catalog.SteamAppId, catalog.SteamFolderName);
    }

    static bool HasSupportedExecutable(string rootPath)
    {
        foreach (string relativePath in s_supportedExeRelativePaths)
            if (File.Exists(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar))))
                return true;

        return false;
    }

    public static IReadOnlyList<DetectedGameInstall> DetectInstallPaths()
    {
        var asePath = GetSuggestedGamePath(GameCatalog.AseGameId);
        var asaPath = GetSuggestedGamePath(GameCatalog.AsaGameId);
        return
        [
            new(GameCatalog.AseGameId, asePath, EvaluateExistingInstall(asePath)),
            new(GameCatalog.AsaGameId, asaPath, EvaluateExistingInstall(asaPath))
        ];
    }

    public static DetectedGameInstall? ResolvePreferredDetectedInstall(IReadOnlyList<DetectedGameInstall> detected)
    {
        var ase = detected.FirstOrDefault(item => item.GameId == GameCatalog.AseGameId);
        var asa = detected.FirstOrDefault(item => item.GameId == GameCatalog.AsaGameId);

        if (ase.IsDetected && !asa.IsDetected)
            return ase;
        if (asa.IsDetected && !ase.IsDetected)
            return asa;
        if (ase.IsDetected)
            return ase;

        return null;
    }
}