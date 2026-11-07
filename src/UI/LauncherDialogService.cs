namespace ObeliskLauncher.UI;

static class LauncherDialogService
{
    public static ILauncherDialogService Current { get; private set; } = new AvaloniaLauncherDialogService();

    public static void UseHeadless() => Current = new HeadlessLauncherDialogService();
}
