using System.Text.Json.Serialization;
using System.Diagnostics;
using ObeliskLauncher.Data;

namespace ObeliskLauncher.Platform;

static class GameRuntimeBootstrap
{
    const string LatestInjectorReleaseUrl = "https://api.github.com/repos/teknology-hub/tek-injector/releases/latest";
    const string LatestRuntimeReleaseUrl = "https://api.github.com/repos/teknology-hub/tek-game-runtime/releases/latest";
    const string PrimaryMirrorRoot = "https://teknology-hub.com/software";
    const string SecondaryMirrorRoot = "https://de.teknology-hub.com/software";
    const string InjectorRepositoryName = "tek-injector";
    const string RuntimeRepositoryName = "tek-game-runtime";

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
            var injectorResult = AcquireAssetAsync(LatestInjectorReleaseUrl, InjectorAssetName, "TEK Injector", InjectorRepositoryName).GetAwaiter().GetResult();
            injectorStopwatch.Stop();
            if (injectorResult.AssetPath is null)
            {
                LauncherLog.Error("GameRuntimeBootstrap: failed acquiring TEK Injector in {ElapsedMs} ms. Error={Error}", injectorStopwatch.ElapsedMilliseconds, injectorResult.ErrorMessage ?? "unknown");
                return new(null, injectorResult.ErrorMessage ?? "Failed to acquire TEK Injector.");
            }
            LauncherLog.Debug("GameRuntimeBootstrap: TEK Injector ready in {ElapsedMs} ms. Path={AssetPath}", injectorStopwatch.ElapsedMilliseconds, injectorResult.AssetPath);

            var runtimeStopwatch = Stopwatch.StartNew();
            var runtimeResult = AcquireAssetAsync(LatestRuntimeReleaseUrl, "libtek-game-runtime.dll", "TEK Game Runtime", RuntimeRepositoryName).GetAwaiter().GetResult();
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

    static async Task<AssetAcquireResult> AcquireAssetAsync(string latestReleaseUrl, string assetFileName, string displayName, string repositoryName)
    {
        var mirrorResult = await AcquireFromMirrorAsync(assetFileName, displayName, repositoryName).ConfigureAwait(false);
        if (mirrorResult.AssetPath is not null)
            return mirrorResult;

        LauncherLog.Warning("GameRuntimeBootstrap: teknology-hub mirrors failed for {DisplayName}; falling back to GitHub. Error={Error}",
            displayName, mirrorResult.ErrorMessage ?? "unknown");
        return await AcquireFromGitHubAsync(latestReleaseUrl, assetFileName, displayName).ConfigureAwait(false);
    }

    static async Task<AssetAcquireResult> AcquireFromGitHubAsync(string latestReleaseUrl, string assetFileName, string displayName)
    {
        var stopwatch = Stopwatch.StartNew();
        LauncherLog.Debug("GameRuntimeBootstrap: acquiring {DisplayName} from GitHub. Asset={AssetName}", displayName, assetFileName);

        LauncherLog.Debug("GameRuntimeBootstrap: checking latest release for {DisplayName} at {Url}", displayName, latestReleaseUrl);

        GitHubRelease? release = await Downloader.DownloadJsonAsync<GitHubRelease>(latestReleaseUrl).ConfigureAwait(false);
        if (release is not { TagName: not null, Assets: not null })
        {
            stopwatch.Stop();
            LauncherLog.Warning("GameRuntimeBootstrap: failed metadata query for {DisplayName} after {ElapsedMs} ms", displayName, stopwatch.ElapsedMilliseconds);
            return FallBackToCache(assetFileName,
                $"Failed to query latest {displayName} release metadata from '{latestReleaseUrl}'.");
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
            return FallBackToCache(assetFileName,
                $"Latest {displayName} release does not expose '{assetFileName}'.");
        }
        GitHubAsset asset = assetData.Value;

        string releaseDir = Path.Combine(CacheRoot, SanitizePathSegment(releaseData.TagName));
        Directory.CreateDirectory(releaseDir);

        string assetPath = Path.Combine(releaseDir, asset.Name);
        if (File.Exists(assetPath) && (asset.Size is null || new FileInfo(assetPath).Length == asset.Size))
        {
            stopwatch.Stop();
            LauncherLog.Debug("GameRuntimeBootstrap: cache hit for {DisplayName} release {TagName} after {ElapsedMs} ms. Path={AssetPath}", displayName, releaseData.TagName, stopwatch.ElapsedMilliseconds, assetPath);
            return new(assetPath, null);
        }

        if (File.Exists(assetPath))
            File.Delete(assetPath);

        string downloadPath = assetPath + ".download";
        try
        {
            LauncherLog.Debug("GameRuntimeBootstrap: downloading {DisplayName} from {Url} to {AssetPath}", displayName, asset.BrowserDownloadUrl, assetPath);
            if (!await Downloader.DownloadFileAsync(downloadPath, new EventHandlers(), asset.BrowserDownloadUrl).ConfigureAwait(false)
                || asset.Size is long expectedSize && new FileInfo(downloadPath).Length != expectedSize)
            {
                stopwatch.Stop();
                LauncherLog.Warning("GameRuntimeBootstrap: download failed or had an unexpected size for {DisplayName} after {ElapsedMs} ms", displayName, stopwatch.ElapsedMilliseconds);
                File.Delete(downloadPath);
                return FallBackToCache(assetFileName,
                    $"Failed to download {displayName} from '{asset.BrowserDownloadUrl}'.");
            }

            File.Move(downloadPath, assetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(downloadPath))
                File.Delete(downloadPath);
        }

        stopwatch.Stop();
        LauncherLog.Debug("GameRuntimeBootstrap: acquired {DisplayName} from GitHub in {ElapsedMs} ms. Path={AssetPath}", displayName, stopwatch.ElapsedMilliseconds, assetPath);
        return new(assetPath, null);
    }

    static AssetAcquireResult FallBackToCache(string assetFileName, string errorMessage)
    {
        string? fallbackPath = TryFindCachedAsset(assetFileName);
        return fallbackPath is not null
            ? new(fallbackPath, null)
            : new(null, errorMessage);
    }

    static async Task<AssetAcquireResult> AcquireFromMirrorAsync(string assetFileName, string displayName, string repositoryName)
    {
        var stopwatch = Stopwatch.StartNew();

        string? latestVersion = await Downloader.DownloadStringAsync(
            BuildVersionUrl(PrimaryMirrorRoot, repositoryName),
            BuildVersionUrl(SecondaryMirrorRoot, repositoryName)).ConfigureAwait(false);
        latestVersion = NormalizeMirrorVersion(latestVersion);
        if (latestVersion is null)
        {
            stopwatch.Stop();
            LauncherLog.Warning("GameRuntimeBootstrap: failed to query {DisplayName} version from teknology-hub mirrors after {ElapsedMs} ms",
                displayName, stopwatch.ElapsedMilliseconds);
            return new(null, $"Failed to query latest {displayName} version from teknology-hub mirrors.");
        }

        string versionDirectory = Path.Combine(CacheRoot, $"v{latestVersion}");
        string assetPath = Path.Combine(versionDirectory, assetFileName);
        if (File.Exists(assetPath) && new FileInfo(assetPath).Length > 0)
        {
            stopwatch.Stop();
            LauncherLog.Debug("GameRuntimeBootstrap: mirror cache hit for {DisplayName} v{Version} after {ElapsedMs} ms. Path={AssetPath}",
                displayName, latestVersion, stopwatch.ElapsedMilliseconds, assetPath);
            return new(assetPath, null);
        }

        string primaryUrl = BuildMirrorUrl(PrimaryMirrorRoot, repositoryName, assetFileName);
        string secondaryUrl = BuildMirrorUrl(SecondaryMirrorRoot, repositoryName, assetFileName);
        string downloadPath = assetPath + ".download";
        try
        {
            Directory.CreateDirectory(versionDirectory);
            LauncherLog.Debug("GameRuntimeBootstrap: downloading {DisplayName} v{Version} from mirrors. Primary={PrimaryUrl}, Secondary={SecondaryUrl}",
                displayName, latestVersion, primaryUrl, secondaryUrl);

            if (!await Downloader.DownloadFileAsync(downloadPath, new EventHandlers(), primaryUrl, secondaryUrl).ConfigureAwait(false)
                || !File.Exists(downloadPath)
                || new FileInfo(downloadPath).Length == 0)
            {
                stopwatch.Stop();
                LauncherLog.Warning("GameRuntimeBootstrap: mirror download failed for {DisplayName} v{Version} after {ElapsedMs} ms",
                    displayName, latestVersion, stopwatch.ElapsedMilliseconds);
                return new(null, $"Failed to download {displayName} v{latestVersion} from teknology-hub mirrors.");
            }

            File.Move(downloadPath, assetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(downloadPath))
                File.Delete(downloadPath);
        }

        stopwatch.Stop();
        LauncherLog.Information("GameRuntimeBootstrap: acquired {DisplayName} v{Version} from mirror in {ElapsedMs} ms. Path={AssetPath}",
            displayName, latestVersion, stopwatch.ElapsedMilliseconds, assetPath);
        return new(assetPath, null);
    }

    internal static string BuildVersionUrl(string root, string repositoryName)
        => $"{root}/{repositoryName}/version";

    internal static string? NormalizeMirrorVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string version = value.Trim();
        if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            version = version[1..];
        version = version.Split('-')[0].Trim();
        return Version.TryParse(version, out _) ? version : null;
    }

    static string? TryFindCachedAsset(string fileName)
    {
        IEnumerable<string> candidates;
        try
        {
            candidates = Directory.EnumerateFiles(CacheRoot, fileName, SearchOption.AllDirectories);
        }
        catch
        {
            return null;
        }

        string? bestPath = null;
        Version? bestVersion = null;
        try
        {
            foreach (string path in candidates)
            {
                if (!File.Exists(path))
                    continue;

                Version? version = TryGetVersionFromPath(path);
                if (bestPath is null || version is not null && (bestVersion is null || version > bestVersion))
                {
                    bestPath = path;
                    bestVersion = version;
                }
            }
        }
        catch
        {
        }

        return bestPath;
    }

    internal static Version? TryGetVersionFromPath(string path)
    {
        string? directoryName = Path.GetFileName(Path.GetDirectoryName(path));
        if (string.IsNullOrWhiteSpace(directoryName))
            return null;

        string value = directoryName.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? directoryName[1..]
            : directoryName;
        return Version.TryParse(value, out Version? version) ? version : null;
    }

    internal static string BuildMirrorUrl(string root, string repositoryName, string assetFileName)
        => $"{root}/{repositoryName}/releases/latest/win-x86_64-static/{assetFileName}";

    static string SanitizePathSegment(string value)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidChar, '_');
        return value;
    }

    readonly record struct AssetAcquireResult(string? AssetPath, string? ErrorMessage);

    readonly record struct GitHubAsset
    {
        [JsonPropertyName("size")]
        public long? Size { get; init; }

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
