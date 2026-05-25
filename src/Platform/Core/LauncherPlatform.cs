namespace ObeliskLauncher.Platform;

static class LauncherPlatform
{
    public static ILauncherPlatform Current { get; } = OperatingSystem.IsWindows() ? new WindowsLauncherPlatform() : new LinuxLauncherPlatform();
}