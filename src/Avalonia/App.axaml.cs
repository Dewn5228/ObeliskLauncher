using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ObeliskLauncher.ARK;
using ObeliskLauncher.Avalonia.ViewModels;
using ObeliskLauncher.Avalonia.Views;
using ObeliskLauncher.Servers;
using ObeliskLauncher.UI;

namespace ObeliskLauncher.Avalonia;

public partial class App : Application
{
    IClassicDesktopStyleApplicationLifetime? _desktop;

    public static bool ShuttingDown { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            AppDomain.CurrentDomain.UnhandledException += DomainExceptionHandler;
            TaskScheduler.UnobservedTaskException += UnobservedTaskExceptionHandler;
            desktop.Exit += DesktopExitHandler;
            Dispatcher.UIThread.UnhandledException += UiUnhandledExceptionHandler;

            var bootstrapResult = LauncherBootstrap.InitializeCore();
            if (Program.IsAnotherInstanceRunning)
            {
                var dialog = new AvaloniaDialogWindow(Locale.Get("common.error"), "Another instance of Obelisk Launcher is already running.", false);
                dialog.Closed += (_, _) => desktop.Shutdown();
                desktop.MainWindow = dialog;
                base.OnFrameworkInitializationCompleted();
                return;
            }

            if (!bootstrapResult.Success)
            {
                var dialog = new AvaloniaDialogWindow(Locale.Get("common.error"), Locale.Get(bootstrapResult.ErrorCode!), false);
                dialog.Closed += (_, _) => desktop.Shutdown();
                desktop.MainWindow = dialog;
                base.OnFrameworkInitializationCompleted();
                return;
            }

            if (!ActiveGameManager.IsConfigured || !Directory.Exists(ActiveGameManager.Current.RootPath))
            {
                var firstLaunchWindow = new FirstLaunchWindow();
                firstLaunchWindow.WorkflowCompleted += (_, beginInstallation) =>
                {
                    desktop.MainWindow = CreateInitializedMainWindow(beginInstallation, null);
                    desktop.MainWindow.Show();
                    firstLaunchWindow.Close();
                };
                desktop.MainWindow = firstLaunchWindow;
            }
            else
                desktop.MainWindow = CreateInitializedMainWindow(false, null);
        }

        base.OnFrameworkInitializationCompleted();
    }

    static MainWindow CreateMainWindow(bool beginInstallation, string? startupMessage) => new()
    {
        DataContext = new MainWindowViewModel(beginInstallation, startupMessage)
    };

    MainWindow CreateInitializedMainWindow(bool beginInstallation, string? startupMessage)
    {
        InitializeLauncherState();
        var mainWindow = CreateMainWindow(beginInstallation, startupMessage);
        return mainWindow;
    }

    static void InitializeLauncherState()
    {
        Task.Run(Mod.InitializeList);
        if (OperatingSystem.IsLinux())
            LauncherLog.Information("Skipping automatic cluster reload on Linux startup; server list will load on demand.");
        else
            Task.Delay(2000).ContinueWith(delegate { Cluster.ReloadLists(); });
    }

    static void TryScheduleWhatsNew(Window owner)
    {
        string? lastLaunchedVersion = LauncherPlatform.Current.GetLastLaunchedVersion();
        LauncherPlatform.Current.SetLastLaunchedVersion(LauncherBootstrap.Version);
    }

    void DesktopExitHandler(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        ShuttingDown = true;
        SteamTaskUpdaterWindowViewModelBase.PauseAllActiveTasks();
        UdpClient.Dispose();
        LauncherServices.ServerBrowser.Shutdown();
        Settings.Save();
        LauncherServices.TekSteamClient.Close();
        IPC.Dispose();
    }

    static void DomainExceptionHandler(object? sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            File.WriteAllText(Path.Combine(LauncherBootstrap.AppDataFolder, "DomainException.txt"), e.ExceptionObject.ToString() ?? string.Empty);
        }
        catch
        {
        }
    }

    void UiUnhandledExceptionHandler(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        TryWriteException("DispatcherException.txt", e.Exception);
        e.Handled = true;
        _ = ShowFatalErrorAndShutdownAsync(e.Exception);
    }

    void UnobservedTaskExceptionHandler(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        TryWriteException("TaskException.txt", e.Exception);
        e.SetObserved();
    }

    async Task ShowFatalErrorAndShutdownAsync(Exception exception)
    {
        if (_desktop?.MainWindow is { } mainWindow)
            await new FatalErrorWindow(exception).ShowDialog(mainWindow);
        else
            new FatalErrorWindow(exception).Show();

        _desktop?.Shutdown();
    }

    static void TryWriteException(string fileName, Exception exception)
    {
        try
        {
            File.WriteAllText(Path.Combine(LauncherBootstrap.AppDataFolder, fileName), exception.ToString());
        }
        catch
        {
        }
    }
}