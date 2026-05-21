using System.Reflection;

namespace TEKLauncher;

readonly record struct LauncherBootstrapResult(bool Success, LocCode? ErrorCode);

static class LauncherBootstrap
{
    public static readonly string AppDataFolder = LauncherPlatform.Current.AppDataFolder;

    public static readonly string Version = GetVersion();

    public static LauncherBootstrapResult InitializeCore()
    {
        Directory.CreateDirectory(AppDataFolder);

        string cultureCode = CultureInfo.CurrentUICulture.Name;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

        Settings.Load();
        LocManager.Initialize(cultureCode);

        if (!IPC.Initialize())
            return new(false, LocCode.AnotherInstanceRunning);

        if (!Steam.App.Initialize())
            return new(false, LocCode.SteamMissing);

        string oldExePath = string.Concat(Environment.ProcessPath, ".old");
        if (File.Exists(oldExePath))
            try
            {
                File.Delete(oldExePath);
            }
            catch { }

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