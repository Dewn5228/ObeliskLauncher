using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ObeliskLauncher.ARK;
using ObeliskLauncher.Data;
using ObeliskLauncher.UI;

namespace ObeliskLauncher.Avalonia.Views;

public partial class FirstLaunchWindow : Window
{
    string? _existingInstallPath;
    string _existingGameId = GameCatalog.AseGameId;
    string? _installPath;
    string _installGameId = GameCatalog.AseGameId;
    bool _preAquatica = true;

    public event EventHandler<bool>? WorkflowCompleted;

    public FirstLaunchWindow()
    {
        InitializeComponent();

        ApplyDetectedInstallDefaults();

        SyncExistingSuggestedPath();
        UpdateInstallVersionToggle();

        UpdateRequiredSpaceText();
    }

    async void BrowseExistingInstallPath(object? sender, RoutedEventArgs e)
    {
        string? path = await PickFolderAsync(Locale.Get("firstLaunchWindow.selectExistingFolder"));
        if (path is not null)
            ExistingPathBox.Text = path;
    }

    async void BrowseInstallPath(object? sender, RoutedEventArgs e)
    {
        string? path = await PickFolderAsync(Locale.Get("firstLaunchWindow.chooseInstallFolder"));
        if (path is not null)
            InstallPathBox.Text = path;
    }

    void ContinueWithExistingInstall(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_existingInstallPath))
            return;

        FirstLaunchWorkflow.ApplySelection(_existingGameId, _existingInstallPath, _preAquatica);
        WorkflowCompleted?.Invoke(this, false);
    }

    void ContinueWithInstall(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_installPath))
            return;

        FirstLaunchWorkflow.ApplySelection(_installGameId, _installPath, _preAquatica);
        WorkflowCompleted?.Invoke(this, true);
    }

    void ExistingPathChanged(object? sender, RoutedEventArgs e)
    {
        string path = ExistingPathBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(path))
        {
            _existingInstallPath = null;
            ContinueExistingButton.IsEnabled = false;
            SetStatus(ExistingStatusText, Locale.Get("firstLaunchWindow.selectFolderToContinue"), "#D49B38");
            return;
        }

        var validation = FirstLaunchWorkflow.EvaluateExistingInstall(path);
        ContinueExistingButton.IsEnabled = validation.FilesExist;
        if (validation.FilesExist)
        {
            _existingInstallPath = path;
            _preAquatica = _existingGameId == GameCatalog.AseGameId && validation.IsPreAquatica;
            SetStatus(ExistingStatusText, Locale.Get("status.gameFilesFound"), "#0AA63E");
        }
        else
        {
            _existingInstallPath = null;
            SetStatus(ExistingStatusText, Locale.Get("status.gameFilesNotFound"), "#D4573B");
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

    void ExistingGameChanged(object? sender, RoutedEventArgs e)
    {
        _existingGameId = ExistingAsaRadio.IsChecked == true ? GameCatalog.AsaGameId : GameCatalog.AseGameId;
        SyncExistingSuggestedPath();
        ExistingPathChanged(null, e);
    }

    void InstallGameChanged(object? sender, RoutedEventArgs e)
    {
        _installGameId = InstallAsaRadio.IsChecked == true ? GameCatalog.AsaGameId : GameCatalog.AseGameId;
        UpdateInstallVersionToggle();
        RefreshInstallState();
    }

    void VersionChanged(object? sender, RoutedEventArgs e)
    {
        _preAquatica = _installGameId == GameCatalog.AseGameId && PreAquaticaRadio.IsChecked == true;
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
            FreeSpaceText.Text = $"{Locale.Get("gameOptionsTab.freeDiskSpace")}: {Locale.Get("common.na")}";
            SetStatus(InstallStatusText, Locale.Get("firstLaunchWindow.selectFolderToContinue"), "#D49B38");
            return;
        }

        var validation = FirstLaunchWorkflow.EvaluateInstallTarget(_installPath, _preAquatica);

        if (!validation.PathExists)
        {
            BeginInstallButton.IsEnabled = false;
            FreeSpaceText.Text = $"{Locale.Get("gameOptionsTab.freeDiskSpace")}: {Locale.Get("common.na")}";
            SetStatus(InstallStatusText, Locale.Get("errors.noPathSelected"), "#D4573B");
            return;
        }

        long freeSpace = validation.FreeSpace;
        bool enoughSpace = validation.EnoughSpace;

        BeginInstallButton.IsEnabled = enoughSpace;
        FreeSpaceText.Text = $"{Locale.Get("gameOptionsTab.freeDiskSpace")}: {Locale.BytesToString(freeSpace)}";

        SetStatus(
          InstallStatusText,
                    enoughSpace ? Locale.Get("common.ok") : Locale.Get("modsTab.notEnoughSpace"),
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

    void UpdateRequiredSpaceText() => RequiredSpaceText.Text = $"{Locale.Get("gameOptionsTab.requiredDiskSpace")}: {FirstLaunchWorkflow.GetRequiredGigabytes(_preAquatica)} GB";

    void ApplyDetectedInstallDefaults()
    {
        IReadOnlyList<DetectedGameInstall> detected = FirstLaunchWorkflow.DetectInstallPaths();
        DetectedGameInstall? preferred = FirstLaunchWorkflow.ResolvePreferredDetectedInstall(detected);
        if (preferred is null)
            return;

        bool asa = preferred.Value.GameId == GameCatalog.AsaGameId;
        _existingGameId = preferred.Value.GameId;
        _installGameId = preferred.Value.GameId;
        _preAquatica = preferred.Value.GameId == GameCatalog.AseGameId && preferred.Value.Validation.IsPreAquatica;

        ExistingAsaRadio.IsChecked = asa;
        ExistingAseRadio.IsChecked = !asa;
        InstallAsaRadio.IsChecked = asa;
        InstallAseRadio.IsChecked = !asa;

        if (!string.IsNullOrWhiteSpace(preferred.Value.Path))
            ExistingPathBox.Text = preferred.Value.Path;
    }

    void SyncExistingSuggestedPath()
    {
        if (!string.IsNullOrWhiteSpace(ExistingPathBox.Text))
            return;

        string? suggestedGamePath = FirstLaunchWorkflow.GetSuggestedGamePath(_existingGameId);
        if (!string.IsNullOrWhiteSpace(suggestedGamePath))
            ExistingPathBox.Text = suggestedGamePath;
    }

    void UpdateInstallVersionToggle()
    {
        bool ase = _installGameId == GameCatalog.AseGameId;
        PreAquaticaRadio.IsEnabled = ase;
        LatestRadio.IsEnabled = ase;
        if (!ase)
        {
            _preAquatica = false;
            LatestRadio.IsChecked = true;
        }
        else if (PreAquaticaRadio.IsChecked != true && LatestRadio.IsChecked != true)
            PreAquaticaRadio.IsChecked = true;

        UpdateRequiredSpaceText();
    }
}