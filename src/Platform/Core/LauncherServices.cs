namespace ObeliskLauncher.Platform;

static class LauncherServices
{
    public static IGameLauncher GameLauncher { get; } = OperatingSystem.IsWindows() ? new WindowsGameLauncher() : new LinuxGameLauncher();

    public static IServerBrowserService ServerBrowser { get; } = OperatingSystem.IsWindows() ? new WindowsServerBrowserService() : new LinuxServerBrowserService();

    public static IServerUiService ServerUi { get; } = new AvaloniaServerUiService();

    public static ILauncherLifetimeService Lifetime { get; } = new AvaloniaLauncherLifetimeService();

    public static ITekSteamClientService TekSteamClient { get; } = OperatingSystem.IsWindows() ? new WindowsTekSteamClientService() : new LinuxTekSteamClientService();

    public static ITekSteamClientBootstrap TekSteamClientBootstrap { get; } = OperatingSystem.IsWindows() ? new WindowsTekSteamClientBootstrap() : new LinuxTekSteamClientBootstrap();
}