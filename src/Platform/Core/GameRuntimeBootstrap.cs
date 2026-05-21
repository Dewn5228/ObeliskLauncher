using System.Text.Json.Serialization;
using TEKLauncher.Data;

namespace TEKLauncher.Platform;

static class GameRuntimeBootstrap
{
    const string LatestInjectorReleaseUrl = "https://api.github.com/repos/teknology-hub/tek-injector/releases/latest";
    const string LatestRuntimeReleaseUrl = "https://api.github.com/repos/teknology-hub/tek-game-runtime/releases/latest";

    static string CacheRoot => Path.Combine(LauncherBootstrap.AppDataFolder,
        OperatingSystem.IsWindows() ? "tek-game-runtime-windows" : "tek-game-runtime-linux");

    static string InjectorAssetName => OperatingSystem.IsWindows() ? "libtek-injector.dll" : "tek-injector.exe";

    public static GameRuntimeAcquireResult Acquire()
    {
        try
        {
            Directory.CreateDirectory(CacheRoot);

            var injectorResult = AcquireAssetAsync(LatestInjectorReleaseUrl, InjectorAssetName, "TEK Injector").GetAwaiter().GetResult();
            if (injectorResult.AssetPath is null)
                return new(null, injectorResult.ErrorMessage ?? "Failed to acquire TEK Injector.");

            var runtimeResult = AcquireAssetAsync(LatestRuntimeReleaseUrl, "libtek-game-runtime.dll", "TEK Game Runtime").GetAwaiter().GetResult();
            if (runtimeResult.AssetPath is null)
                return new(null, runtimeResult.ErrorMessage ?? "Failed to acquire TEK Game Runtime.");

            return new(new(injectorResult.AssetPath, runtimeResult.AssetPath), null);
        }
        catch (Exception ex)
        {
            return new(null, $"Failed to prepare launch assets: {ex.Message}");
        }
    }

    static async Task<AssetAcquireResult> AcquireAssetAsync(string latestReleaseUrl, string assetFileName, string displayName)
    {
        string? cachedAssetPath = TryFindCachedAsset(assetFileName);
        if (cachedAssetPath is not null)
            return new(cachedAssetPath, null);

        GitHubRelease? release = await Downloader.DownloadJsonAsync<GitHubRelease>(latestReleaseUrl);
        if (release is not { TagName: not null, Assets: not null })
            return new(null, $"Failed to query latest {displayName} release metadata from '{latestReleaseUrl}'.");
        GitHubRelease releaseData = release.Value;

        GitHubAsset? assetData = Array.Find(releaseData.Assets, asset =>
            asset.Name?.Equals(assetFileName, StringComparison.OrdinalIgnoreCase) == true
            && asset.BrowserDownloadUrl is not null);
        if (assetData is not { Name: not null, BrowserDownloadUrl: not null })
            return new(null, $"Latest {displayName} release does not expose '{assetFileName}'.");
        GitHubAsset asset = assetData.Value;

        string releaseDir = Path.Combine(CacheRoot, SanitizePathSegment(releaseData.TagName));
        Directory.CreateDirectory(releaseDir);

        string assetPath = Path.Combine(releaseDir, asset.Name);
        if (!File.Exists(assetPath))
        {
            if (!await Downloader.DownloadFileAsync(assetPath, new EventHandlers(), asset.BrowserDownloadUrl))
                return new(null, $"Failed to download {displayName} from '{asset.BrowserDownloadUrl}'.");
        }

        return new(assetPath, null);
    }

    static string? TryFindCachedAsset(string fileName)
    {
        IEnumerator<string>? enumerator = null;
        try
        {
            enumerator = Directory.EnumerateFiles(CacheRoot, fileName, SearchOption.AllDirectories).GetEnumerator();
        }
        catch
        {
            return null;
        }

        using (enumerator)
            while (true)
                try
                {
                    if (!enumerator.MoveNext())
                        return null;
                    if (File.Exists(enumerator.Current))
                        return enumerator.Current;
                }
                catch
                {
                    return null;
                }
    }

    static string SanitizePathSegment(string value)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidChar, '_');
        return value;
    }

    readonly record struct AssetAcquireResult(string? AssetPath, string? ErrorMessage);

    readonly record struct GitHubAsset
    {
        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    readonly record struct GitHubRelease
    {
        [JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; init; }

        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }
    }
}

readonly record struct GameRuntimeAssets(string InjectorExecutablePath, string RuntimeLibraryPath);

readonly record struct GameRuntimeAcquireResult(GameRuntimeAssets? Assets, string? ErrorMessage);