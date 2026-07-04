using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;
using ObeliskLauncher.Data;

namespace ObeliskLauncher.Platform;

sealed class LinuxTekSteamClientBootstrap : ITekSteamClientBootstrap
{
    const string PrimaryVersionUrl = "https://teknology-hub.com/software/tek-steamclient/version-2";
    const string MirrorVersionUrl = "https://de.teknology-hub.com/software/tek-steamclient/version-2";
    const string PrimaryDownloadUrl = "https://teknology-hub.com/software/tek-steamclient/releases/latest-2/linux-x86_64/tek-sc-cli-x86_64.AppImage";
    const string MirrorDownloadUrl = "https://de.teknology-hub.com/software/tek-steamclient/releases/latest-2/linux-x86_64/tek-sc-cli-x86_64.AppImage";

    public async Task<TekSteamClientBootstrapResult> InitializeAsync(string gamePath)
    {
        CleanupLegacyLibraryArtifacts();

        if (!TryLoadDiscoveredLibrary(out string loadedLibraryPath, out IntPtr loadedLibraryHandle, out string? loadError, out string? attemptedPath))
        {
            var acquireResult = await TryAcquireTekSteamClientLibraryAsync();
            if (acquireResult.LibraryPath is null)
            {
                string message = acquireResult.ErrorMessage ?? TryFindTekSteamClientAppImage() switch
                {
                    null => "Linux tek-steamclient library was not found. Install the Linux `tek-steamclient` package or place the native library where the launcher can discover it.",
                    string appImage => $"Found Linux TEK Steam Client AppImage at '{appImage}', but no native `tek-steamclient` library was found yet. Install the shared library package or copy it into the launcher data directory."
                };
                LauncherLog.Error("LinuxTekSteamClientBootstrap failed before load: {Message}", message);
                return new(false, false, message, acquireResult.DownloadName, acquireResult.DownloadUrl, null);
            }

            if (!TryLoadLibrary(acquireResult.LibraryPath, out loadedLibraryPath, out loadedLibraryHandle, out string? reacquireLoadError))
            {
                string sourcePath = attemptedPath ?? acquireResult.LibraryPath;
                string sourceError = loadError ?? reacquireLoadError ?? "unknown error";
                string message = acquireResult.ErrorMessage
                    ?? $"Failed to load Linux tek-steamclient library. Last attempted path: '{sourcePath}'. Error: {sourceError}";
                return new(false, false, message, acquireResult.DownloadName, acquireResult.DownloadUrl, null);
            }
        }

        string localeDir = Path.Combine(LauncherBootstrap.AppDataFolder, "tsc-locale");
        Directory.CreateDirectory(localeDir);
        TEKSteamClient.LoadLocale(localeDir);

        var ctx = new TEKSteamClient.LibCtx();
        var appMng = new TEKSteamClient.AppManager(ctx, gamePath);
        if (appMng.IsInvalid)
        {
            string appManagerError = string.IsNullOrWhiteSpace(appMng.CreationError.Message)
                ? "tek-steamclient AppManager creation failed with no additional details."
                : appMng.CreationError.Message;
            LauncherLog.Error("LinuxTekSteamClientBootstrap failed creating AppManager. Error={Error}", appManagerError);
            appMng.Dispose();
            ctx.Dispose();
            return new(false, false, appManagerError, null, null, null);
        }

        string modsDir = Path.Combine(gamePath, "Mods");
        try
        {
            if (!Directory.Exists(modsDir))
                Directory.CreateDirectory(modsDir);
        }
        catch { }

        var workshopResult = appMng.SetWorkshopDir(modsDir);
        if (!workshopResult.Success)
        {
            string workshopError = string.IsNullOrWhiteSpace(workshopResult.Message)
                ? $"Failed to set workshop directory to '{modsDir}'."
                : workshopResult.Message;
            LauncherLog.Error("LinuxTekSteamClientBootstrap failed setting workshop dir. Error={Error}", workshopError);
            appMng.Dispose();
            ctx.Dispose();
            return new(false, false, workshopError, null, null, null);
        }

        var primaryS3Result = await Task.Run(() => ctx.SyncS3Manifest("https://api.teknology-hub.com/s3"));
        var mirrorS3Result = await Task.Run(() => ctx.SyncS3Manifest("https://de.api.teknology-hub.com/s3"));

        string? warningMessage = null;
        if (!primaryS3Result.Success && !mirrorS3Result.Success)
            warningMessage = $"Failed to connect to tek-s3u servers: {primaryS3Result.AuxMessage}";
        if (primaryS3Result.Uri != 0)
            Marshal.FreeHGlobal(primaryS3Result.Uri);
        if (mirrorS3Result.Uri != 0)
            Marshal.FreeHGlobal(mirrorS3Result.Uri);

        LauncherServices.TekSteamClient.Initialize(ctx, appMng);
        return new(true, false, null, null, null, warningMessage);
    }

    async Task<TekSteamClientAcquireResult> TryAcquireTekSteamClientLibraryAsync()
    {
        string cacheRoot = GetCacheRoot();
        Directory.CreateDirectory(cacheRoot);

        string? localAppImagePath = TryFindTekSteamClientAppImage();
        if (localAppImagePath is not null)
        {
            var localExtractResult = await TryExtractLibraryFromAppImageAsync(localAppImagePath, Path.Combine(cacheRoot, "local"));
            if (localExtractResult.LibraryPath is not null)
                return localExtractResult;
        }

        string? latestTscVer = await Downloader.DownloadStringAsync(PrimaryVersionUrl, MirrorVersionUrl);
        if (latestTscVer is null)
            return new(null, null, null, "Failed to query latest Linux tek-steamclient version.");

        string versionTag = latestTscVer.Trim();
        string releaseDir = Path.Combine(cacheRoot, SanitizePathSegment(versionTag));
        string? existingLibraryPath = FindLibraryInDirectory(releaseDir);
        if (existingLibraryPath is not null)
            return new(existingLibraryPath, null, null, null);

        string appImagePath = Path.Combine(cacheRoot, $"tek-sc-cli-x86_64-{versionTag}.AppImage");
        if (!File.Exists(appImagePath) && !await Downloader.DownloadFileAsync(appImagePath, new EventHandlers(), PrimaryDownloadUrl, MirrorDownloadUrl))
            return new(null, "Linux tek-steamclient AppImage", PrimaryDownloadUrl, "Failed to download Linux tek-steamclient AppImage.");

        return await TryExtractLibraryFromAppImageAsync(appImagePath, releaseDir, PrimaryDownloadUrl);
    }

    static string? TryFindTekSteamClientAppImage()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        {
      Path.Combine(home, "Applications", "tek-sc-cli-x86_64.AppImage"),
      Path.Combine(home, "Downloads", "tek-sc-cli-x86_64.AppImage"),
      Path.Combine(LauncherPlatform.Current.AppDataFolder, "tek-sc-cli-x86_64.AppImage")
    };

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string appImagePath = Path.Combine(directory, "tek-sc-cli-x86_64.AppImage");
                if (File.Exists(appImagePath))
                    return appImagePath;
            }

        return Array.Find(candidates, File.Exists);
    }

    static bool TryLoadLibrary(string sourcePath, out string loadedLibraryPath, out IntPtr loadedLibraryHandle, out string? error)
    {
        loadedLibraryPath = string.Empty;
        loadedLibraryHandle = IntPtr.Zero;
        error = null;

        try
        {
            loadedLibraryPath = PrepareLibraryForLoading(sourcePath);
            if (!IsSystemLibraryPath(loadedLibraryPath))
            {
                ConfigureBundledLibrarySearchPath(loadedLibraryPath);
                PreloadBundledLibraries(loadedLibraryPath);
            }
            loadedLibraryHandle = NativeLibrary.Load(loadedLibraryPath);
            TEKSteamClient.RegisterLibrary(loadedLibraryPath, loadedLibraryHandle);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            LauncherLog.Error(ex, "LinuxTekSteamClientBootstrap failed loading library. Path={LibraryPath}", loadedLibraryPath);
            return false;
        }
    }

    static string PrepareLibraryForLoading(string sourcePath)
    {
        string sourceFullPath = Path.GetFullPath(sourcePath);
        return sourceFullPath;
    }

    static bool TryLoadDiscoveredLibrary(out string loadedLibraryPath, out IntPtr loadedLibraryHandle, out string? loadError, out string? attemptedPath)
    {
        loadedLibraryPath = string.Empty;
        loadedLibraryHandle = IntPtr.Zero;
        loadError = null;
        attemptedPath = null;

        foreach (string candidate in GetExistingLibraryCandidates())
        {
            attemptedPath = candidate;
            if (TryLoadLibrary(candidate, out loadedLibraryPath, out loadedLibraryHandle, out loadError))
                return true;

            LauncherLog.Warning("LinuxTekSteamClientBootstrap skipped failing candidate. Path={LibraryPath}. Error={Error}", candidate, loadError ?? "unknown");
        }

        return false;
    }

    static IEnumerable<string> GetExistingLibraryCandidates()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string candidate in GetLibraryCandidates())
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch
            {
                continue;
            }

            if (seen.Add(fullPath) && File.Exists(fullPath))
                yield return fullPath;
        }
    }

    static IEnumerable<string> GetLibraryCandidates()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] directories =
        [
            "/usr/local/lib64",
            "/usr/local/lib/x86_64-linux-gnu",
            "/usr/lib/x86_64-linux-gnu",
            "/lib/x86_64-linux-gnu",
            "/usr/local/lib",
            "/usr/lib",
            "/usr/lib64",
            "/lib",
            "/lib64",
            Path.Combine(home, ".local", "lib")
        ];

        foreach (string directory in directories)
            yield return Path.Combine(directory, "libtek-steamclient.so.2");

        string? ldLibraryPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
        if (!string.IsNullOrWhiteSpace(ldLibraryPath))
            foreach (string directory in ldLibraryPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return Path.Combine(directory, "libtek-steamclient.so.2");

        yield return Path.Combine(LauncherBootstrap.AppDataFolder, "libtek-steamclient.so.2");

        string cacheRoot = GetCacheRoot();
        if (Directory.Exists(cacheRoot))
            foreach (string libraryPath in EnumerateLibraryFiles(cacheRoot))
                yield return libraryPath;
    }

    static void CleanupLegacyLibraryArtifacts()
    {
        string legacyPath = Path.Combine(LauncherBootstrap.AppDataFolder, "libtek-steamclient-2.dll");
        if (!File.Exists(legacyPath))
            return;

        try
        {
            File.Delete(legacyPath);
            LauncherLog.Information("LinuxTekSteamClientBootstrap removed legacy Windows-named library artifact. Path={LibraryPath}", legacyPath);
        }
        catch (Exception ex)
        {
            LauncherLog.Warning(ex, "LinuxTekSteamClientBootstrap failed deleting legacy Windows-named library artifact; continuing while ignoring it. Path={LibraryPath}", legacyPath);
        }
    }

    static IEnumerable<string> EnumerateLibraryFiles(string rootDirectory)
    {
        try
        {
            return Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories)
                            .Where(static path => path.EndsWith("libtek-steamclient.so.2", StringComparison.Ordinal))
              .OrderBy(GetLibraryCandidatePriority)
              .ToArray();
        }
        catch
        {
            return [];
        }
    }

    static string? FindLibraryInDirectory(string directory)
    {
        foreach (string libraryPath in EnumerateLibraryFiles(directory))
            if (File.Exists(libraryPath))
                return libraryPath;

        return null;
    }

    static string GetCacheRoot() => Path.Combine(LauncherBootstrap.AppDataFolder, "tek-steamclient-linux");

    static int GetLibraryCandidatePriority(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized switch
        {
            var candidate when candidate.Contains("/usr/lib/", StringComparison.Ordinal) && candidate.EndsWith("libtek-steamclient.so.2", StringComparison.Ordinal) => 0,
            var candidate when candidate.Contains("/lib/", StringComparison.Ordinal) && candidate.EndsWith("libtek-steamclient.so.2", StringComparison.Ordinal) => 1,
            var candidate when candidate.EndsWith("libtek-steamclient.so.2", StringComparison.Ordinal) => 2,
            _ => 3
        };
    }

    static bool IsSystemLibraryPath(string libraryPath)
    {
        string normalized = libraryPath.Replace('\\', '/');
        return normalized.StartsWith("/usr/lib/", StringComparison.Ordinal)
            || normalized.StartsWith("/lib/", StringComparison.Ordinal)
            || normalized.StartsWith("/usr/local/lib/", StringComparison.Ordinal)
            || normalized.StartsWith("/usr/lib64/", StringComparison.Ordinal)
            || normalized.StartsWith("/lib64/", StringComparison.Ordinal);
    }

    static void ConfigureBundledLibrarySearchPath(string libraryPath)
    {
        string[] candidateDirectories = GetBundledLibraryDirectories(libraryPath);
        if (candidateDirectories.Length == 0)
            return;

        string? existing = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
        var merged = new List<string>(candidateDirectories.Length + 4);
        merged.AddRange(candidateDirectories);

        if (!string.IsNullOrWhiteSpace(existing))
            foreach (string directory in existing.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (!merged.Any(existingDirectory => string.Equals(existingDirectory, directory, StringComparison.Ordinal)))
                    merged.Add(directory);

        Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", string.Join(Path.PathSeparator, merged));
    }

    static void PreloadBundledLibraries(string libraryPath)
    {
        foreach (string directory in GetBundledLibraryDirectories(libraryPath))
        {
            string[] libraries;
            try
            {
                libraries = Directory.EnumerateFiles(directory, "*.so*")
                  .Where(static path => !Path.GetFileName(path).StartsWith("libtek-steamclient", StringComparison.Ordinal))
                  .OrderBy(static path => Path.GetFileName(path), StringComparer.Ordinal)
                  .ToArray();
            }
            catch
            {
                continue;
            }

            foreach (string dependencyPath in libraries)
            {
                try
                {
                    NativeLibrary.Load(dependencyPath);
                }
                catch
                {
                }
            }
        }
    }

    static string[] GetBundledLibraryDirectories(string libraryPath)
    {
        var directories = new List<string>();

        void AddDirectory(string? directory)
        {
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) && !directories.Any(existingDirectory => string.Equals(existingDirectory, directory, StringComparison.Ordinal)))
                directories.Add(directory);
        }

        string? libraryDirectory = Path.GetDirectoryName(libraryPath);
        AddDirectory(libraryDirectory);

        if (libraryDirectory is null)
            return directories.ToArray();

        string[] segments = libraryDirectory.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        int squashfsRootIndex = Array.FindIndex(segments, static segment => segment == "squashfs-root");
        if (squashfsRootIndex == -1)
            return directories.ToArray();

        string squashfsRoot = string.Join(Path.DirectorySeparatorChar, segments.Take(squashfsRootIndex + 1));
        if (libraryPath.StartsWith(Path.DirectorySeparatorChar))
            squashfsRoot = Path.DirectorySeparatorChar + squashfsRoot;

        AddDirectory(Path.Combine(squashfsRoot, "usr", "lib", "x86_64-linux-gnu"));
        AddDirectory(Path.Combine(squashfsRoot, "lib", "x86_64-linux-gnu"));
        AddDirectory(Path.Combine(squashfsRoot, "usr", "lib"));
        AddDirectory(Path.Combine(squashfsRoot, "lib"));

        return directories.ToArray();
    }

    static string SanitizePathSegment(string value)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidChar, '_');
        return value;
    }

    [SupportedOSPlatform("linux")]
    static void TryEnsureExecutable(string appImagePath)
    {
        if (!OperatingSystem.IsLinux())
            return;

        try
        {
            File.SetUnixFileMode(appImagePath,
              UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
              UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
              UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch { }
    }

    static async Task<TekSteamClientAcquireResult> TryExtractLibraryFromAppImageAsync(string appImagePath, string destinationDirectory, string? downloadUrl = null)
    {
        string? existingLibraryPath = FindLibraryInDirectory(destinationDirectory);
        if (existingLibraryPath is not null)
            return new(existingLibraryPath, null, null, null);

        if (OperatingSystem.IsLinux())
            TryEnsureExecutable(appImagePath);

        string tempDirectory = destinationDirectory + ".tmp";
        try
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, true);
            Directory.CreateDirectory(tempDirectory);

            using var process = new Process();
            process.StartInfo = new()
            {
                FileName = appImagePath,
                Arguments = "--appimage-extract",
                WorkingDirectory = tempDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            if (!process.Start())
                return new(null, "Linux tek-steamclient AppImage", downloadUrl, "Failed to start Linux tek-steamclient AppImage extraction.");

            await process.WaitForExitAsync();
            string output = (await process.StandardOutput.ReadToEndAsync()).Trim();
            string error = (await process.StandardError.ReadToEndAsync()).Trim();
            if (process.ExitCode != 0)
            {
                string details = string.IsNullOrWhiteSpace(error) ? output : error;
                return new(null, "Linux tek-steamclient AppImage", downloadUrl, $"Failed to extract Linux tek-steamclient AppImage: {details}");
            }

            string extractedRoot = Path.Combine(tempDirectory, "squashfs-root");
            string? extractedLibraryPath = FindLibraryInDirectory(extractedRoot);
            if (extractedLibraryPath is null)
                return new(null, "Linux tek-steamclient AppImage", downloadUrl, "Linux tek-steamclient AppImage did not contain a loadable shared library.");

            if (Directory.Exists(destinationDirectory))
                Directory.Delete(destinationDirectory, true);
            Directory.Move(tempDirectory, destinationDirectory);
            tempDirectory = string.Empty;

            string? finalLibraryPath = FindLibraryInDirectory(destinationDirectory);
            return finalLibraryPath is null
              ? new(null, "Linux tek-steamclient AppImage", downloadUrl, "Linux tek-steamclient AppImage was extracted but the shared library could not be located afterward.")
              : new(finalLibraryPath, null, null, null);
        }
        catch (Exception ex)
        {
            return new(null, "Linux tek-steamclient AppImage", downloadUrl, $"Failed to extract Linux tek-steamclient AppImage: {ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempDirectory) && Directory.Exists(tempDirectory))
                try
                {
                    Directory.Delete(tempDirectory, true);
                }
                catch { }
        }
    }

    readonly record struct TekSteamClientAcquireResult(string? LibraryPath, string? DownloadName, string? DownloadUrl, string? ErrorMessage);
}