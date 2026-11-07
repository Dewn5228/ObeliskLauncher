using Avalonia;
using System.Threading;

namespace ObeliskLauncher.Avalonia;

static class Program
{
    static Mutex? s_singleInstanceMutex;

    public static bool IsAnotherInstanceRunning { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        s_singleInstanceMutex = new Mutex(true, "ObeliskLauncher_SingleInstance", out bool isNewInstance);
        if (!isNewInstance)
            IsAnotherInstanceRunning = true;

        LauncherLog.Initialize();
        LauncherLog.Information("ObeliskLauncher starting. Version={Version}, Args={Args}", LauncherBootstrap.Version, args);

        if (CatalogRefreshCommand.TryRun(args, out int exitCode))
        {
            LauncherLog.Information("Catalog refresh command completed with exit code {ExitCode}", exitCode);
            Environment.ExitCode = exitCode;
            return;
        }

        if (PlayCommand.TryRun(args, out int playExitCode))
        {
            LauncherLog.Information("Headless play command completed with exit code {ExitCode}", playExitCode);
            Environment.ExitCode = playExitCode;
            return;
        }

        LauncherLog.Information("Launching Avalonia desktop lifetime");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}