using Avalonia.Controls;
using Avalonia.Interactivity;
using TEKLauncher.Avalonia.ViewModels;

namespace TEKLauncher.Avalonia.Views;

public partial class ServerModsUpdaterWindow : Window
{
    ServerModsUpdaterWindowViewModel? _viewModel;

    public ServerModsUpdaterWindow()
    {
        InitializeComponent();
    }

    internal ServerModsUpdaterWindow(ulong[] modIds)
      : this()
    {
        _viewModel = new ServerModsUpdaterWindowViewModel(modIds);
        DataContext = _viewModel;
        Opened += OpenedHandler;
        Closing += ClosingHandler;
        Closed += ClosedHandler;
    }

    void CloseWindow(object? sender, RoutedEventArgs e) => Close();

    void ClosedHandler(object? sender, System.EventArgs e) => _viewModel?.Dispose();

    void ClosingHandler(object? sender, WindowClosingEventArgs e)
    {
        if (_viewModel is not null && !global::TEKLauncher.Avalonia.App.ShuttingDown)
            e.Cancel = !_viewModel.TryClose();
    }

    void OpenedHandler(object? sender, System.EventArgs e) => _viewModel?.Start();

    void PauseOrRetry(object? sender, RoutedEventArgs e) => _viewModel?.PauseOrRetry();
}