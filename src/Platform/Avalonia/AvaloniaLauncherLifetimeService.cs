using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace ObeliskLauncher.Platform;

sealed class AvaloniaLauncherLifetimeService : ILauncherLifetimeService
{
    public void Shutdown()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        if (Dispatcher.UIThread.CheckAccess())
            desktop.Shutdown();
        else
            Dispatcher.UIThread.Post(() => desktop.Shutdown());
    }
}