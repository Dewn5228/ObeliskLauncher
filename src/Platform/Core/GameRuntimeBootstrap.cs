using System.Text.Json.Serialization;
using System.Diagnostics;
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
        var acquireStopwatch = Stopwatch.StartNew();
        LauncherLog.Debug("GameRuntimeBootstrap: acquire started. CacheRoot={CacheRoot}", CacheRoot);
        try
        {
            Directory.CreateDirectory(CacheRoot);

            var injectorStopwatch = Stopwatch.StartNew();
            var injectorResult = AcquireAssetAsync(LatestInjectorReleaseUrl, InjectorAssetName, "TEK Injector").GetAwaiter().GetResult();
            injectorStopwatch.Stop();
            if (injectorResult.AssetPath is null)
            {
                LauncherLog.Error("GameRuntimeBootstrap: failed acquiring TEK Injector in {ElapsedMs} ms. Error={Error}", injectorStopwatch.ElapsedMilliseconds, injectorResult.ErrorMessage ?? "unknown");
                return new(null, injectorResult.ErrorMessage ?? "Failed to acquire TEK Injector.");
            }
            LauncherLog.Debug("GameRuntimeBootstrap: TEK Injector ready in {ElapsedMs} ms. Path={AssetPath}", injectorStopwatch.ElapsedMilliseconds, injectorResult.AssetPath);

            var runtimeStopwatch = Stopwatch.StartNew();
            var runtimeResult = AcquireAssetAsync(LatestRuntimeReleaseUrl, "libtek-game-runtime.dll", "TEK Game Runtime").GetAwaiter().GetResult();
            runtimeStopwatch.Stop();
            if (runtimeResult.AssetPath is null)
            {
                LauncherLog.Error("GameRuntimeBootstrap: failed acquiring TEK Game Runtime in {ElapsedMs} ms. Error={Error}", runtimeStopwatch.ElapsedMilliseconds, runtimeResult.ErrorMessage ?? "unknown");
                return new(null, runtimeResult.ErrorMessage ?? "Failed to acquire TEK Game Runtime.");
            }
            LauncherLog.Debug("GameRuntimeBootstrap: TEK Game Runtime ready in {ElapsedMs} ms. Path={AssetPath}", runtimeStopwatch.ElapsedMilliseconds, runtimeResult.AssetPath);

            acquireStopwatch.Stop();
            LauncherLog.Information("GameRuntimeBootstrap: acquire completed in {ElapsedMs} ms", acquireStopwatch.ElapsedMilliseconds);
            return new(new(injectorResult.AssetPath, runtimeResult.AssetPath), null);
        }
        catch (Exception ex)
        {
            acquireStopwatch.Stop();
            LauncherLog.Error(ex, "GameRuntimeBootstrap: acquire failed after {ElapsedMs} ms", acquireStopwatch.ElapsedMilliseconds);
            return new(null, $"Failed to prepare launch assets: {ex.Message}");
        }
    }

    static async Task<AssetAcquireResult> AcquireAssetAsync(string latestReleaseUrl, string assetFileName, string displayName)
    {
        var stopwatch = Stopwatch.StartNew();
        LauncherLog.Debug("GameRuntimeBootstrap: acquiring {DisplayName}. Asset={AssetName}", displayName, assetFileName);

        string? cachedAssetPath = TryFindCachedAsset(assetFileName);
        if (cachedAssetPath is not null)
        {
            stopwatch.Stop();
            LauncherLog.Debug("GameRuntimeBootstrap: cache hit for {DisplayName} after {ElapsedMs} ms. Path={AssetPath}", displayName, stopwatch.ElapsedMilliseconds, cachedAssetPath);
            return new(cachedAssetPath, null);
        }

        LauncherLog.Debug("GameRuntimeBootstrap: cache miss for {DisplayName}; requesting release metadata from {Url}", displayName, latestReleaseUrl);

        GitHubRelease? release = await Downloader.DownloadJsonAsync<GitHubRelease>(latestReleaseUrl).ConfigureAwait(false);
        if (release is not { TagName: not null, Assets: not null })
        {
            stopwatch.Stop();
            LauncherLog.Warning("GameRuntimeBootstrap: failed metadata query for {DisplayName} after {ElapsedMs} ms", displayName, stopwatch.ElapsedMilliseconds);
            return new(null, $"Failed to query latest {displayName} release metadata from '{latestReleaseUrl}'.");
        }
        GitHubRelease releaseData = release.Value;

        LauncherLog.Debug("GameRuntimeBootstrap: latest {DisplayName} tag is {TagName}", displayName, releaseData.TagName);

        GitHubAsset? assetData = Array.Find(releaseData.Assets, asset =>
            asset.Name?.Equals(assetFileName, StringComparison.OrdinalIgnoreCase) == true
            && asset.BrowserDownloadUrl is not null);
        if (assetData is not { Name: not null, BrowserDownloadUrl: not null })
        {
            stopwatch.Stop();
            LauncherLog.Warning("GameRuntimeBootstrap: release {TagName} does not contain {AssetName} for {DisplayName}", releaseData.TagName, assetFileName, displayName);
            return new(null, $"Latest {displayName} release does not expose '{assetFileName}'.");
        }
        GitHubAsset asset = assetData.Value;

        string releaseDir = Path.Combine(CacheRoot, SanitizePathSegment(releaseData.TagName));
        Directory.CreateDirectory(releaseDir);

        string assetPath = Path.Combine(releaseDir, asset.Name);
        if (!File.Exists(assetPath))
        {
            LauncherLog.Debug("GameRuntimeBootstrap: downloading {DisplayName} from {Url} to {AssetPath}", displayName, asset.BrowserDownloadUrl, assetPath);
            if (!await Downloader.DownloadFileAsync(assetPath, new EventHandlers(), asset.BrowserDownloadUrl).ConfigureAwait(false))
            {
                stopwatch.Stop();
                LauncherLog.Warning("GameRuntimeBootstrap: download failed for {DisplayName} after {ElapsedMs} ms", displayName, stopwatch.ElapsedMilliseconds);
                return new(null, $"Failed to download {displayName} from '{asset.BrowserDownloadUrl}'.");
            }
        }

        stopwatch.Stop();
        LauncherLog.Debug("GameRuntimeBootstrap: acquired {DisplayName} in {ElapsedMs} ms. Path={AssetPath}", displayName, stopwatch.ElapsedMilliseconds, assetPath);
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