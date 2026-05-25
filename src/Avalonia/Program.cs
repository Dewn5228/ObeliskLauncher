using Avalonia;

namespace ObeliskLauncher.Avalonia;

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        LauncherLog.Initialize();
        LauncherLog.Information("ObeliskLauncher starting. Version={Version}, Args={Args}", LauncherBootstrap.Version, args);

        if (CatalogRefreshCommand.TryRun(args, out int exitCode))
        {
            LauncherLog.Information("Catalog refresh command completed with exit code {ExitCode}", exitCode);
            Environment.ExitCode = exitCode;
            return;
        }

        LauncherLog.Information("Launching Avalonia desktop lifetime");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}