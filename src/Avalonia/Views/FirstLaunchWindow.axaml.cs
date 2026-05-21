using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using TEKLauncher.ARK;
using TEKLauncher.Data;
using TEKLauncher.UI;

namespace TEKLauncher.Avalonia.Views;

public partial class FirstLaunchWindow : Window
{
    string? _existingInstallPath;
    string? _installPath;
    bool _preAquatica = true;

    public event EventHandler<bool>? WorkflowCompleted;

    public FirstLaunchWindow()
    {
        InitializeComponent();

        string? suggestedGamePath = FirstLaunchWorkflow.SuggestedGamePath;
        if (!string.IsNullOrWhiteSpace(suggestedGamePath))
            ExistingPathBox.Text = suggestedGamePath;

        UpdateRequiredSpaceText();
    }

    async void BrowseExistingInstallPath(object? sender, RoutedEventArgs e)
    {
        string? path = await PickFolderAsync("Select existing ARK folder");
        if (path is not null)
            ExistingPathBox.Text = path;
    }

    async void BrowseInstallPath(object? sender, RoutedEventArgs e)
    {
        string? path = await PickFolderAsync("Select installation folder");
        if (path is not null)
            InstallPathBox.Text = path;
    }

    void ContinueWithExistingInstall(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_existingInstallPath))
            return;

        FirstLaunchWorkflow.ApplySelection(_existingInstallPath, _preAquatica);
        WorkflowCompleted?.Invoke(this, false);
    }

    void ContinueWithInstall(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_installPath))
            return;

        FirstLaunchWorkflow.ApplySelection(_installPath, _preAquatica);
        WorkflowCompleted?.Invoke(this, true);
    }

    void ExistingPathChanged(object? sender, RoutedEventArgs e)
    {
        string path = ExistingPathBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(path))
        {
            _existingInstallPath = null;
            ContinueExistingButton.IsEnabled = false;
            SetStatus(ExistingStatusText, "Select a folder to continue.", "#D49B38");
            return;
        }

        var validation = FirstLaunchWorkflow.EvaluateExistingInstall(path);
        ContinueExistingButton.IsEnabled = validation.FilesExist;
        if (validation.FilesExist)
        {
            _existingInstallPath = path;
            _preAquatica = validation.IsPreAquatica;
            SetStatus(ExistingStatusText, Locale.Get("gameFilesFound"), "#0AA63E");
        }
        else
        {
            _existingInstallPath = null;
            SetStatus(ExistingStatusText, Locale.Get("gameFilesNotFound"), "#D4573B");
        }
    }

    void InstallPathChanged(object? sender, RoutedEventArgs e)
    {
        _installPath = InstallPathBox.Text?.Trim();
        RefreshInstallState();
    }

    void ShowExistingInstallFlow(object? sender, RoutedEventArgs e) => SetPanelVisibility(showExistingInstall: true);

    void ShowInstallFlow(object? sender, RoutedEventArgs e) => SetPanelVisibility(showInstall: true);

    void ShowStartFlow(object? sender, RoutedEventArgs e) => SetPanelVisibility();

    void VersionChanged(object? sender, RoutedEventArgs e)
    {
        _preAquatica = PreAquaticaRadio.IsChecked == true;
        UpdateRequiredSpaceText();
        RefreshInstallState();
    }

    async Task<string?> PickFolderAsync(string title)
    {
        if (!StorageProvider.CanOpen)
            return null;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    void RefreshInstallState()
    {
        if (string.IsNullOrWhiteSpace(_installPath))
        {
            BeginInstallButton.IsEnabled = false;
            FreeSpaceText.Text = "Free disk space: unknown";
            SetStatus(InstallStatusText, "Select a folder to continue.", "#D49B38");
            return;
        }

        var validation = FirstLaunchWorkflow.EvaluateInstallTarget(_installPath, _preAquatica);

        if (!validation.PathExists)
        {
            BeginInstallButton.IsEnabled = false;
            FreeSpaceText.Text = "Free disk space: unknown";
            SetStatus(InstallStatusText, "The selected installation folder does not exist.", "#D4573B");
            return;
        }

        long freeSpace = validation.FreeSpace;
        bool enoughSpace = validation.EnoughSpace;

        BeginInstallButton.IsEnabled = enoughSpace;
        FreeSpaceText.Text = $"Free disk space: {Locale.BytesToString(freeSpace)}";

        SetStatus(
          InstallStatusText,
          enoughSpace ? "Enough disk space detected." : "Not enough disk space for this installation.",
          enoughSpace ? "#0AA63E" : "#D4573B");
    }

    void SetPanelVisibility(bool showExistingInstall = false, bool showInstall = false)
    {
        StartPanel.IsVisible = !showExistingInstall && !showInstall;
        ExistingInstallPanel.IsVisible = showExistingInstall;
        InstallPanel.IsVisible = showInstall;
    }

    static void SetStatus(TextBlock block, string text, string color)
    {
        block.Text = text;
        block.Foreground = new SolidColorBrush(Color.Parse(color));
    }

    void UpdateRequiredSpaceText() => RequiredSpaceText.Text = $"Required disk space: {FirstLaunchWorkflow.GetRequiredGigabytes(_preAquatica)} GB";
}