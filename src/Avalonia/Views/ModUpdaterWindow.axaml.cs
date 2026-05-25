using Avalonia.Controls;
using Avalonia.Interactivity;
using ObeliskLauncher.Avalonia.ViewModels;

namespace ObeliskLauncher.Avalonia.Views;

public partial class ModUpdaterWindow : Window
{
    ModUpdaterWindowViewModel? _viewModel;

    public ModUpdaterWindow()
    {
        InitializeComponent();
    }

    internal ModUpdaterWindow(Mod.ModDetails details, bool validate)
      : this()
    {
        _viewModel = new ModUpdaterWindowViewModel(details, validate);
        DataContext = _viewModel;
        Opened += OpenedHandler;
        Closing += ClosingHandler;
        Closed += ClosedHandler;
    }

    void CloseWindow(object? sender, RoutedEventArgs e) => Close();

    void ClosedHandler(object? sender, System.EventArgs e) => _viewModel?.Dispose();

    void ClosingHandler(object? sender, WindowClosingEventArgs e)
    {
        if (_viewModel is not null && !global::ObeliskLauncher.Avalonia.App.ShuttingDown)
            e.Cancel = !_viewModel.TryClose();
    }

    void OpenedHandler(object? sender, System.EventArgs e) => _viewModel?.Start();

    void PauseOrRetry(object? sender, RoutedEventArgs e) => _viewModel?.PauseOrRetry();
}