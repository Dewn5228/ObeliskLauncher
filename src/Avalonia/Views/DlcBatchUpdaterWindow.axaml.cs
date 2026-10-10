using Avalonia.Controls;
using Avalonia.Interactivity;
using ObeliskLauncher.Avalonia.ViewModels;
using System.Collections.Generic;

namespace ObeliskLauncher.Avalonia.Views;

public partial class DlcBatchUpdaterWindow : Window
{
    DlcBatchUpdaterWindowViewModel? _viewModel;

    public DlcBatchUpdaterWindow()
    {
        InitializeComponent();
    }

    internal DlcBatchUpdaterWindow(IReadOnlyList<DLC> dlcs)
      : this()
    {
        _viewModel = new DlcBatchUpdaterWindowViewModel(dlcs);
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
