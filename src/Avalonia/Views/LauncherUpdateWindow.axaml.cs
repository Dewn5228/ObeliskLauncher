using Avalonia.Controls;
using Avalonia.Interactivity;
using TEKLauncher.Avalonia.ViewModels;

namespace TEKLauncher.Avalonia.Views;

public partial class LauncherUpdateWindow : Window
{
    LauncherUpdateWindowViewModel? _viewModel;

    public LauncherUpdateWindow()
    {
        InitializeComponent();
    }

    internal LauncherUpdateWindow(Window owner)
      : this()
    {
        _viewModel = new LauncherUpdateWindowViewModel();
        DataContext = _viewModel;
        Opened += OpenedHandler;
        Closing += ClosingHandler;
    }

    void CloseWindow(object? sender, RoutedEventArgs e) => Close();

    void ClosingHandler(object? sender, WindowClosingEventArgs e)
    {
        if (_viewModel is not null && !_viewModel.CanClose)
            e.Cancel = true;
    }

    async void OpenedHandler(object? sender, System.EventArgs e)
    {
        if (_viewModel is not null)
            await _viewModel.StartAsync();
    }

    void OpenReleasePage(object? sender, RoutedEventArgs e) => _viewModel?.OpenReleasePage();
}