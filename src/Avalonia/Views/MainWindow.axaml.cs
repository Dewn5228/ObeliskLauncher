using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Primitives;
using System.Diagnostics;
using ObeliskLauncher.ARK;
using ObeliskLauncher.Platform;
using ObeliskLauncher.Avalonia.ViewModels;
using ObeliskLauncher.Data;
using ObeliskLauncher.UI;
using Avalonia.Input.Platform;

namespace ObeliskLauncher.Avalonia.Views;

public partial class MainWindow : Window
{
    bool _shutdownRequested;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OpenedHandler;
        Closing += ClosingHandler;
    }

    void ClosingHandler(object? sender, WindowClosingEventArgs e)
    {
        if (SteamTaskUpdaterWindowViewModelBase.IsAnySteamTaskActive && !Messages.ShowOptions("common.warning", Locale.Get("errors.launcherClosePrompt")))
        {
            e.Cancel = true;
            return;
        }

        if (_shutdownRequested || App.ShuttingDown)
            return;

        _shutdownRequested = true;
        e.Cancel = true;
        LauncherServices.Lifetime.Shutdown();
    }

    async void OpenedHandler(object? sender, System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.InitializeAsync();
            viewModel.GetScreen<PlaySectionScreenViewModel>(LauncherSection.Play)?.Activate();
            if (viewModel.DownloadFailureName is not null && viewModel.DownloadFailureUrl is not null)
            {
                Messages.ShowDownloadErr(viewModel.DownloadFailureName, viewModel.DownloadFailureUrl);
                return;
            }

            if (viewModel.ShouldRestartAfterInitialization)
            {
                Messages.Show("common.info", Locale.Get("status.restartRequiredForSteamClient"));
                LauncherServices.Lifetime.Shutdown();
                return;
            }

            if (viewModel.ShouldStartInstallationAfterInitialization)
                OpenGameUpdater(false, navigateToGameOptions: true);
        }
    }

    void OpenLauncherSettingsSection(object? sender, RoutedEventArgs e) => SelectSection(LauncherSection.GlobalSettings);

    void SelectSection(LauncherSection section)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (!viewModel.TrySelectSection(section, out string? warningMessage) && !string.IsNullOrWhiteSpace(warningMessage))
            Messages.Show("common.warning", warningMessage);
    }

    async void SelectNavigationNode(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || sender is not Control { DataContext: LauncherNavNodeViewModel node })
            return;

        string? warningMessage = await viewModel.TrySelectNavigationNodeAsync(node);
        if (!string.IsNullOrWhiteSpace(warningMessage))
            Messages.Show("common.warning", warningMessage);
    }

    void GameLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { DataContext: PlaySectionScreenViewModel playScreen } comboBox && comboBox.SelectedIndex >= 0)
            playScreen.GameLanguageIndex = comboBox.SelectedIndex;
    }

    async void LaunchGame(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: PlaySectionScreenViewModel playScreen })
            return;

        try
        {
            await playScreen.LaunchAsync();
        }
        catch (Exception ex)
        {
            Messages.Show("common.error", ex.Message);
        }
    }

    void LauncherLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { DataContext: PlaySectionScreenViewModel playScreen } comboBox && comboBox.SelectedIndex >= 0)
        {
            if (!playScreen.LauncherLanguageSelectionEnabled)
                return;

            int previousIndex = playScreen.LauncherLanguageIndex;
            if (previousIndex == comboBox.SelectedIndex)
                return;

            playScreen.LauncherLanguageIndex = comboBox.SelectedIndex;
            if (DataContext is MainWindowViewModel viewModel)
                viewModel.RefreshLocale();
        }
    }

    void AseLinuxLaunchToolChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox)
            return;

        LauncherLog.Debug("ASE linux tool combobox changed. SelectedIndex={SelectedIndex}, SelectedItem={SelectedItemType}",
            comboBox.SelectedIndex,
            comboBox.SelectedItem?.GetType().Name ?? "null");

        if (comboBox.DataContext is not LauncherSettingsSectionScreenViewModel screen)
        {
            LauncherLog.Warning("ASE linux tool combobox DataContext is not LauncherSettingsSectionScreenViewModel. DataContextType={DataContextType}",
                comboBox.DataContext?.GetType().FullName ?? "null");
            return;
        }

        if (comboBox.SelectedIndex >= 0)
            screen.SelectedAseLinuxLaunchToolIndex = comboBox.SelectedIndex;
    }

    void AsaLinuxLaunchToolChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox)
            return;

        LauncherLog.Debug("ASA linux tool combobox changed. SelectedIndex={SelectedIndex}, SelectedItem={SelectedItemType}",
            comboBox.SelectedIndex,
            comboBox.SelectedItem?.GetType().Name ?? "null");

        if (comboBox.DataContext is not LauncherSettingsSectionScreenViewModel screen)
        {
            LauncherLog.Warning("ASA linux tool combobox DataContext is not LauncherSettingsSectionScreenViewModel. DataContextType={DataContextType}",
                comboBox.DataContext?.GetType().FullName ?? "null");
            return;
        }

        if (comboBox.SelectedIndex >= 0)
            screen.SelectedAsaLinuxLaunchToolIndex = comboBox.SelectedIndex;
    }

    void OpenDiscordLink(object? sender, RoutedEventArgs e) => OpenUrl("https://discord.gg/JBUgcwvpfc");

    void OpenLicenseLink(object? sender, RoutedEventArgs e) => OpenUrl("https://github.com/Dewn5228/ObeliskLauncher/blob/main/LICENSE.TXT");

    void OpenRepoLink(object? sender, RoutedEventArgs e) => OpenUrl("https://github.com/Dewn5228/ObeliskLauncher");

    async void BrowseSettingsAseGamePath(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.CurrentScreen is not LauncherSettingsSectionScreenViewModel screen)
            return;

        string? path = await PickFolderPathAsync(Locale.Get("mainWindow.gamePath"));
        if (!string.IsNullOrWhiteSpace(path))
        {
            screen.AseGamePath = path;
            screen.SaveGamePathDrafts();
            viewModel.RefreshNavigation();
        }
    }

    async void BrowseSettingsAsaGamePath(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.CurrentScreen is not LauncherSettingsSectionScreenViewModel screen)
            return;

        string? path = await PickFolderPathAsync(Locale.Get("mainWindow.gamePath"));
        if (!string.IsNullOrWhiteSpace(path))
        {
            screen.AsaGamePath = path;
            screen.SaveGamePathDrafts();
            viewModel.RefreshNavigation();
        }
    }

    void SettingsGamePathEdited(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.CurrentScreen is not LauncherSettingsSectionScreenViewModel screen)
            return;

        screen.SaveGamePathDrafts();
        viewModel.RefreshNavigation();
    }

    async void BrowseAseLinuxCompatPrefix(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { CurrentScreen: LauncherSettingsSectionScreenViewModel screen })
            return;

        string? path = await PickFolderPathAsync(Locale.Get("mainWindow.customPrefix"));
        if (!string.IsNullOrWhiteSpace(path))
            screen.AseLinuxCustomPrefixPath = path;
    }

    async void BrowseAsaLinuxCompatPrefix(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { CurrentScreen: LauncherSettingsSectionScreenViewModel screen })
            return;

        string? path = await PickFolderPathAsync(Locale.Get("mainWindow.customPrefix"));
        if (!string.IsNullOrWhiteSpace(path))
            screen.AsaLinuxCustomPrefixPath = path;
    }

    async void ImportAseLinuxProtonTool(object? sender, RoutedEventArgs e) => await ImportLinuxLaunchToolForGameAsync(GameCatalog.AseGameId, LinuxLaunchToolKind.Proton, Locale.Get("mainWindow.importProtonPath"));

    async void ImportAseLinuxWineTool(object? sender, RoutedEventArgs e) => await ImportLinuxLaunchToolForGameAsync(GameCatalog.AseGameId, LinuxLaunchToolKind.Wine, Locale.Get("mainWindow.importWinePath"));

    async void ImportAsaLinuxProtonTool(object? sender, RoutedEventArgs e) => await ImportLinuxLaunchToolForGameAsync(GameCatalog.AsaGameId, LinuxLaunchToolKind.Proton, Locale.Get("mainWindow.importProtonPath"));

    async void ImportAsaLinuxWineTool(object? sender, RoutedEventArgs e) => await ImportLinuxLaunchToolForGameAsync(GameCatalog.AsaGameId, LinuxLaunchToolKind.Wine, Locale.Get("mainWindow.importWinePath"));

    async Task ImportLinuxLaunchToolForGameAsync(string gameId, LinuxLaunchToolKind kind, string title)
    {
        if (DataContext is not MainWindowViewModel { CurrentScreen: LauncherSettingsSectionScreenViewModel screen })
            return;

        if (!StorageProvider.CanOpen)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        if (files.Count == 0)
            return;

        string? localPath = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
            return;

        screen.ImportCustomLinuxLaunchToolForGame(gameId, kind, localPath);
    }

    async void ApplySettingsGamePath(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.CurrentScreen is not LauncherSettingsSectionScreenViewModel screen)
            return;

        bool hasSelectionChanges = screen.IsGameSelectionChanged();
        if (!hasSelectionChanges)
        {
            Messages.Show("common.info", Locale.Get("common.ok"));
            return;
        }

        if (!screen.TryValidateGameSelection(out string? validationError))
        {
            Messages.Show("common.warning", validationError ?? Locale.Get("errors.noPathSelected"));
            return;
        }

        if (!Messages.ShowOptions("common.warning", Locale.Get("errors.gamePathChangePrompt")))
            return;

        bool hadPreviousActive = ActiveGameManager.IsConfigured;
        string? previousGameId = hadPreviousActive ? ActiveGameManager.Current.Id : null;
        string? previousGamePath = hadPreviousActive ? ActiveGameManager.Current.RootPath : null;
        string previousAseGamePath = Settings.AseGamePath;
        string previousAsaGamePath = Settings.AsaGamePath;
        bool previousPreAquatica = Settings.PreAquatica;
        screen.ApplyGameSelection();

        bool hasCurrentActive = ActiveGameManager.IsConfigured;
        bool activeChanged = hadPreviousActive != hasCurrentActive
            || (hadPreviousActive
                    && hasCurrentActive
                    && (!string.Equals(previousGameId, ActiveGameManager.Current.Id, StringComparison.OrdinalIgnoreCase)
                            || !string.Equals(previousGamePath, ActiveGameManager.Current.RootPath, StringComparison.OrdinalIgnoreCase)));

        if (!activeChanged)
        {
            viewModel.RefreshNavigation();
            Messages.Show("common.info", Locale.Get("common.ok"));
            return;
        }

        bool reloaded = await viewModel.ReloadAfterGameSwitchAsync();
        if (!reloaded)
        {
            Settings.AseGamePath = previousAseGamePath;
            Settings.AsaGamePath = previousAsaGamePath;
            Settings.PreAquatica = previousPreAquatica;
            if (!string.IsNullOrWhiteSpace(previousGameId) && !string.IsNullOrWhiteSpace(previousGamePath))
                ActiveGameManager.Configure(previousGameId, previousGamePath);
            Settings.Save();

            bool rollbackReloaded = hadPreviousActive && !string.IsNullOrWhiteSpace(previousGameId) && !string.IsNullOrWhiteSpace(previousGamePath)
              ? await viewModel.ReloadAfterGameSwitchAsync()
              : true;
            if (!rollbackReloaded)
            {
                Messages.Show("common.warning", viewModel.LastBootstrapErrorMessage ?? Locale.Get("errors.steamClientBootstrapFailed"));
                return;
            }

            screen.Activate();
            viewModel.RefreshNavigation();
            Messages.Show("common.warning", "Game switch failed; previous game configuration was restored.");
            return;
        }

        viewModel.RefreshNavigation();
        Messages.Show("common.info", Locale.Get("common.ok"));
    }

    async Task<string?> PickFolderPathAsync(string title)
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

    async void CleanDownloadCache(object? sender, RoutedEventArgs e)
    {
        if (!LauncherServices.TekSteamClient.IsLoaded)
        {
            ShowSteamClientUnavailable();
            return;
        }

        if (!Messages.ShowOptions("common.warning", Locale.Get("gameOptionsTab.cleanDownloadCachePrompt")))
            return;

        await LauncherSettingsWorkflow.CleanDownloadCacheAsync();

        Messages.Show("common.info", Locale.Get("gameOptionsTab.cleanDownloadCacheSuccess"));
    }

    void DeleteLauncherSettings(object? sender, RoutedEventArgs e)
    {
        if (!Messages.ShowOptions("common.warning", Locale.Get("errors.deleteLauncherSettingsPrompt")))
            return;

        Settings.Delete = true;
        LauncherServices.Lifetime.Shutdown();
    }

    void SaveLinuxLaunchPreset(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { CurrentScreen: LauncherSettingsSectionScreenViewModel screen })
            return;

        if (string.IsNullOrWhiteSpace(screen.LinuxLaunchPresetName))
            return;

        screen.SaveLinuxLaunchPreset();
    }

    void ApplyLinuxLaunchPreset(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: LauncherSettingsSectionScreenViewModel screen })
            screen.ApplySelectedLinuxPreset();
    }

    void DeleteLinuxLaunchPreset(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { CurrentScreen: LauncherSettingsSectionScreenViewModel screen })
            return;

        if (screen.SelectedLinuxLaunchPreset is null)
            return;

        if (!Messages.ShowOptions("common.warning", string.Format(Locale.Get("errors.dlcDeletePrompt"), screen.SelectedLinuxLaunchPreset.Name)))
            return;

        screen.DeleteSelectedLinuxPreset();
    }

    void SettingsCloseOnLaunchChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { CurrentScreen: LauncherSettingsSectionScreenViewModel screen } || sender is not CheckBox checkBox)
            return;

        bool enabled = checkBox.IsChecked == true;
        screen.CloseOnGameLaunch = enabled;
        if (!LauncherSettingsWorkflow.ShouldWarnCloseOnLaunch(enabled))
            return;

        if (Messages.ShowOptions("common.warning", Locale.Get("errors.launcherCloseWarning")))
            return;

        screen.CloseOnGameLaunch = false;
    }

    async void SettingsPreAquaticaChanged(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.CurrentScreen is not LauncherSettingsSectionScreenViewModel screen || sender is not CheckBox checkBox)
            return;

        screen.PreAquatica = checkBox.IsChecked == true;

        await viewModel.RefreshShellStatusAsync();

        viewModel.GetScreen<DlcSectionScreenViewModel>(LauncherSection.DLC)?.Activate();
        viewModel.GetScreen<GameOptionsSectionScreenViewModel>(LauncherSection.GameOptions)?.Activate();
    }

    async void DeleteDlc(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { CurrentScreen: DlcSectionScreenViewModel screen } || sender is not Control { DataContext: DlcRowViewModel row })
            return;

        if (!Messages.ShowOptions("common.warning", string.Format(Locale.Get("errors.dlcDeletePrompt"), row.Name)))
            return;

        await screen.DeleteAsync(row);
    }

    void ApplyGameOptionsCustomParameters(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: GameOptionsSectionScreenViewModel screen })
            screen.ApplyCustomLaunchParameters();
    }

    void FixGameBloom(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: GameOptionsSectionScreenViewModel screen })
            screen.FixBloom();
    }

    void UpdateBaseGame(object? sender, RoutedEventArgs e) => OpenGameUpdater(false);

    void ValidateBaseGame(object? sender, RoutedEventArgs e) => OpenGameUpdater(true);

    void OpenDlcInstallUpdater(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.CurrentScreen is not DlcSectionScreenViewModel screen || sender is not Control { DataContext: DlcRowViewModel row })
            return;

        if (!LauncherServices.TekSteamClient.IsLoaded)
        {
            ShowSteamClientUnavailable();
            return;
        }

        var window = new DlcUpdaterWindow(row.Dlc, false);
        screen.Activate();
        window.Closed += async (_, _) =>
        {
            screen.Activate();
            await viewModel.RefreshShellStatusAsync();
        };
        window.Show(this);
    }

    void OpenDlcValidator(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.CurrentScreen is not DlcSectionScreenViewModel screen || sender is not Control { DataContext: DlcRowViewModel row })
            return;

        if (!LauncherServices.TekSteamClient.IsLoaded)
        {
            ShowSteamClientUnavailable();
            return;
        }

        var window = new DlcUpdaterWindow(row.Dlc, true);
        screen.Activate();
        window.Closed += async (_, _) =>
        {
            screen.Activate();
            await viewModel.RefreshShellStatusAsync();
        };
        window.Show(this);
    }

    async void CopyInstalledModId(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ModRowViewModel row })
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        await clipboard.SetTextAsync(row.IdText);
        Messages.Show("common.info", Locale.Get("errors.modIdCopied"));
    }

    async void DeleteInstalledMod(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { CurrentScreen: ModsSectionScreenViewModel screen } || sender is not Control { DataContext: ModRowViewModel row })
            return;

        if (!Messages.ShowOptions("common.info", Locale.Get("errors.modDeletePrompt")))
            return;

        await screen.DeleteAsync(row);
    }

    async void LookupModInstallCandidate(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: ModsSectionScreenViewModel screen })
            await screen.LookupInstallCandidateAsync();
    }

    void InstallSelectedMod(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { CurrentScreen: ModsSectionScreenViewModel screen })
            return;

        if (!LauncherServices.TekSteamClient.IsLoaded)
        {
            ShowSteamClientUnavailable();
            return;
        }

        var details = screen.SelectedModDetails;
        var window = new ModUpdaterWindow(details, true);
        window.Closed += (_, _) => screen.RefreshAfterUpdaterClosed();
        window.Show(this);
    }

    async void LoadNextWorkshopModsPage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: ModsSectionScreenViewModel screen })
            await screen.LoadNextWorkshopPageAsync();
    }

    async void LoadPreviousWorkshopModsPage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: ModsSectionScreenViewModel screen })
            await screen.LoadPreviousWorkshopPageAsync();
    }

    void OpenInstalledModWorkshopPage(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ModRowViewModel row })
            OpenUrl(row.WorkshopUrl);
    }

    void OpenWorkshopResultPage(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: WorkshopModRowViewModel row })
            OpenUrl(row.WorkshopUrl);
    }

    void OpenSelectedWorkshopPage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: ModsSectionScreenViewModel screen } && screen.SelectedModDetails.Id != 0)
            OpenUrl($"steam://openurl/https://steamcommunity.com/sharedfiles/filedetails/?id={screen.SelectedModDetails.Id}");
    }

    async void ReloadWorkshopMods(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: ModsSectionScreenViewModel screen })
            await screen.ReloadWorkshopPageAsync();
    }

    async void RefreshServers(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: ServersSectionScreenViewModel screen })
            await screen.RefreshAsync();
    }

    async void SearchWorkshopMods(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: ModsSectionScreenViewModel screen })
            await screen.SearchWorkshopAsync();
    }

    void SelectWorkshopModResult(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: ModsSectionScreenViewModel screen } && sender is Control { DataContext: WorkshopModRowViewModel row })
            screen.SelectWorkshopCandidate(row);
    }

    void UpdateInstalledMod(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { CurrentScreen: ModsSectionScreenViewModel screen } || sender is not Control { DataContext: ModRowViewModel row })
            return;

        if (!LauncherServices.TekSteamClient.IsLoaded)
        {
            ShowSteamClientUnavailable();
            return;
        }

        var details = row.GetDetailsForUpdater();
        var window = new ModUpdaterWindow(details, false);
        window.Closed += (_, _) => screen.RefreshAfterUpdaterClosed();
        window.Show(this);
    }

    void ValidateInstalledMod(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { CurrentScreen: ModsSectionScreenViewModel screen } || sender is not Control { DataContext: ModRowViewModel row })
            return;

        if (!LauncherServices.TekSteamClient.IsLoaded)
        {
            ShowSteamClientUnavailable();
            return;
        }

        var details = row.GetDetailsForUpdater();
        var window = new ModUpdaterWindow(details, true);
        window.Closed += (_, _) => screen.RefreshAfterUpdaterClosed();
        window.Show(this);
    }

    async void UnlockGameSkins(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: GameOptionsSectionScreenViewModel screen })
            await screen.UnlockSkinsAsync();
    }

    void OpenGameUpdater(bool validate, bool navigateToGameOptions = false)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        if (navigateToGameOptions && viewModel.CurrentScreen is not GameOptionsSectionScreenViewModel)
            SelectSection(LauncherSection.GameOptions);

        if (viewModel.CurrentScreen is not GameOptionsSectionScreenViewModel screen)
            return;

        if (!LauncherServices.TekSteamClient.IsLoaded)
        {
            ShowSteamClientUnavailable();
            return;
        }

        if (!validate && Steam.App.CurrentUserStatus.GameStatus == Game.Status.OwnedAndInstalled && !Messages.ShowOptions("common.warning", Locale.Get("errors.updateSteamGameWarning")))
            return;

        string? prompt = GameUpdateWorkflow.GetSafetyPrompt(validate);
        if (!string.IsNullOrWhiteSpace(prompt) && !Messages.ShowOptions("common.warning", prompt))
            return;

        var window = new GameUpdaterWindow(validate);
        window.Closed += async (_, _) =>
        {
            screen.Activate();
            await viewModel.RefreshShellStatusAsync();
        };
        window.Show(this);
    }

    async void JoinSelectedServer(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: ServersSectionScreenViewModel screen } && sender is Control { DataContext: ServerRowViewModel row })
            await screen.JoinAsync(row);
    }

    async void ExpandServerDetails(object? sender, RoutedEventArgs e)
    {
        if (sender is Expander { DataContext: ServerRowViewModel row })
            await row.EnsureModDetailsLoadedAsync();
    }

    void OpenClusterDiscord(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: ServersSectionScreenViewModel } && sender is Control { DataContext: ServersClusterRowViewModel row } && !string.IsNullOrWhiteSpace(row.DiscordUrl))
            OpenUrl(row.DiscordUrl);
    }

    void OpenServerDiscord(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ServerRowViewModel row } && !string.IsNullOrWhiteSpace(row.DiscordUrl))
            OpenUrl(row.DiscordUrl);
    }

    void OpenServerModWorkshopPage(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ServerModRowViewModel row })
            OpenUrl(row.WorkshopUrl);
    }

    void InstallServerMod(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.CurrentScreen is not ServersSectionScreenViewModel screen || sender is not Control { DataContext: ServerModRowViewModel row })
            return;

        if (!LauncherServices.TekSteamClient.IsLoaded)
        {
            ShowSteamClientUnavailable();
            return;
        }

        var window = new ModUpdaterWindow(row.Details, false);
        window.Closed += async (_, _) =>
        {
            screen.RefreshSnapshot();
            await viewModel.RefreshShellStatusAsync();
        };
        window.Show(this);
    }

    void ValidateAllServerMods(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.CurrentScreen is not ServersSectionScreenViewModel screen || sender is not Control { DataContext: ServerRowViewModel row })
            return;

        if (!LauncherServices.TekSteamClient.IsLoaded)
        {
            ShowSteamClientUnavailable();
            return;
        }

        if (row.ModIds.Length == 0)
            return;

        var window = new ServerModsUpdaterWindow(row.ModIds);
        window.Closed += async (_, _) =>
        {
            screen.RefreshSnapshot();
            await viewModel.RefreshShellStatusAsync();
        };
        window.Show(this);
    }

    async void RefreshSelectedServersCluster(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: ServersSectionScreenViewModel screen })
            await screen.RefreshSelectedClusterAsync();
    }

    void NoticeActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: ShellNoticeViewModel notice })
            return;

        switch (notice.ActionKind)
        {
            case ShellNoticeActionKind.OpenLauncherReleasePage:
                OpenUrl("https://github.com/Dewn5228/ObeliskLauncher/releases");
                break;
            case ShellNoticeActionKind.OpenGameUpdater:
                OpenGameUpdater(false, navigateToGameOptions: true);
                break;
            case ShellNoticeActionKind.OpenDlcSection:
                SelectSection(LauncherSection.DLC);
                break;
        }
    }

    void SelectServersCluster(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: ServersSectionScreenViewModel screen } && sender is Control { DataContext: ServersClusterRowViewModel row })
            screen.SelectCluster(row);
    }

    void ToggleFavoriteServer(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentScreen: ServersSectionScreenViewModel screen } && sender is Control { DataContext: ServerRowViewModel row })
            screen.ToggleFavorite(row);
    }

    static void ShowSteamClientUnavailable() => Messages.Show("common.error", Locale.Get("errors.steamClientBootstrapFailed"));

    static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

}