using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using TEKLauncher.Data;

namespace TEKLauncher.Avalonia.ViewModels;

public sealed class LauncherUpdateWindowViewModel : INotifyPropertyChanged
{
    const string LatestReleaseUrl = "https://github.com/Nuclearistt/TEKLauncher/releases/latest";

    bool _canOpenReleasePage;
    bool _isBusy;
    bool _isProgressIndeterminate = true;
    double _progressMaximum = 100;
    double _progressValue;
    string _statusBrush = "Yellow";
    string _statusText = LocManager.GetString(LocCode.Downloading);

    public bool CanClose => !IsBusy;

    public bool CanOpenReleasePage
    {
        get => _canOpenReleasePage;
        private set => SetProperty(ref _canOpenReleasePage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(CanClose));
        }
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public double ProgressMaximum
    {
        get => _progressMaximum;
        private set => SetProperty(ref _progressMaximum, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public string StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string Title => LocManager.GetString(LocCode.Update);

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task StartAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        CanOpenReleasePage = false;

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                StatusText = "Launcher self-update is only packaged for Windows right now. Open the latest release page and replace the launcher binary manually after Linux release artifacts are published.";
                StatusBrush = "#D49B38";
                CanOpenReleasePage = true;
                return;
            }

            using var currentProcess = Process.GetCurrentProcess();
            string path = currentProcess.MainModule?.FileName ?? Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                StatusText = LocManager.GetString(LocCode.LauncherUpdateFail);
                StatusBrush = "#9E2313";
                CanOpenReleasePage = true;
                return;
            }

            string newPath = string.Concat(path, ".new");
            var eventHandlers = new EventHandlers
            {
                PrepareProgress = (_, total) => Dispatcher.UIThread.Post(() =>
                {
                    ProgressMaximum = total > 0 ? total : 100;
                    ProgressValue = 0;
                    IsProgressIndeterminate = total <= 0;
                }),
                UpdateProgress = increment => Dispatcher.UIThread.Post(() =>
                {
                    if (!IsProgressIndeterminate)
                        ProgressValue += increment;
                })
            };

            bool success = await Downloader.DownloadFileAsync(
              newPath,
              eventHandlers,
              "https://teknology-hub.com/software/tek-launcher/releases/latest/win-x86_64-static/tek-launcher.exe",
              "https://de.teknology-hub.com/software/tek-launcher/releases/latest/win-x86_64-static/tek-launcher.exe",
              "https://github.com/Nuclearistt/TEKLauncher/releases/latest/download/TEKLauncher.exe");

            if (!success)
            {
                StatusText = LocManager.GetString(LocCode.LauncherUpdateFail);
                StatusBrush = "#9E2313";
                CanOpenReleasePage = true;
                return;
            }

            File.Move(path, string.Concat(path, ".old"), true);
            File.Move(newPath, path, true);
            IPC.Dispose();
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            LauncherServices.Lifetime.Shutdown();
        }
        catch
        {
            StatusText = LocManager.GetString(LocCode.LauncherUpdateFail);
            StatusBrush = "#9E2313";
            CanOpenReleasePage = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void OpenReleasePage() => Process.Start(new ProcessStartInfo(LatestReleaseUrl) { UseShellExecute = true });

    void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new(propertyName));

    bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}