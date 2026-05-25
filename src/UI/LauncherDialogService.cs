namespace ObeliskLauncher.UI;

static class LauncherDialogService
{
    public static ILauncherDialogService Current { get; } = new AvaloniaLauncherDialogService();
}