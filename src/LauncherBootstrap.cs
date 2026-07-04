using System.Reflection;

namespace ObeliskLauncher;

readonly record struct LauncherBootstrapResult(bool Success, string? ErrorCode);

static class LauncherBootstrap
{
    public static readonly string AppDataFolder = LauncherPlatform.Current.AppDataFolder;

    public static readonly string Version = GetVersion();

    public static LauncherBootstrapResult InitializeCore()
    {
        LauncherLog.Information("InitializeCore started. AppDataFolder={AppDataFolder}", AppDataFolder);
        Directory.CreateDirectory(AppDataFolder);
        GameCatalog.InitializeAndSync();
        if (!string.IsNullOrWhiteSpace(GameCatalog.StartupNotice))
            LauncherLog.Warning("Catalog startup notice: {Notice}", GameCatalog.StartupNotice);

        string cultureCode = CultureInfo.CurrentUICulture.Name;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        LauncherLog.Debug("Culture forced to en-US from {OriginalCulture}", cultureCode);

        Settings.Load();
        Locale.Init(cultureCode);
        LauncherLog.Information("Settings and locale initialized. Locale={Locale}", Locale.CurrentLanguage);

        if (!IPC.Initialize())
        {
            LauncherLog.Error("InitializeCore failed: another launcher instance is already running");
            return new(false, "errors.anotherInstanceRunning");
        }

        if (!Steam.App.Initialize())
        {
            LauncherLog.Error("InitializeCore failed: Steam initialization failed");
            return new(false, "errors.steamMissing");
        }

        string oldExePath = string.Concat(Environment.ProcessPath, ".old");
        if (File.Exists(oldExePath))
            try
            {
                File.Delete(oldExePath);
                LauncherLog.Debug("Deleted stale old executable: {OldExePath}", oldExePath);
            }
            catch (Exception ex)
            {
                LauncherLog.Warning(ex, "Failed to delete stale old executable: {OldExePath}", oldExePath);
            }

        LauncherLog.Information("InitializeCore completed successfully");
        return new(true, null);
    }

    static string GetVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version!;
        if (version.Revision == 0)
            version = new(version.Major, version.Minor, version.Build);
        return version.ToString();
    }
}