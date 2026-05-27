using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ObeliskLauncher.Servers;

namespace ObeliskLauncher.Avalonia.ViewModels;

public sealed class LauncherNavNodeViewModel
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required LauncherSection Section { get; init; }

    public string? GameId { get; init; }

    public bool IsSelected { get; init; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(GameId) ? Title : $"{GameId} / {Title}";
}

public sealed class LauncherNavGroupViewModel : INotifyPropertyChanged
{
    public required string Title { get; init; }

    public required bool IsVisible { get; init; }

    public required IReadOnlyList<LauncherNavNodeViewModel> Children { get; init; }

    public int ChildCount => Children.Count;

    bool _isExpanded = true;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;

            _isExpanded = value;
            PropertyChanged?.Invoke(this, new(nameof(IsExpanded)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    string _defaultSubtitle;
    readonly string? _startupMessage;
    public MainWindowViewModel()
      : this(false, null)
    {
    }

    public MainWindowViewModel(bool beginInstallation = false, string? startupMessage = null)
    {
        _startupMessage = startupMessage;
        _defaultSubtitle = _startupMessage ?? (beginInstallation
  ? Locale.Get("status.preparingInstallation")
  : Locale.Get("status.manageArkInstalls"));
        _subtitle = _defaultSubtitle;

        _beginInstallation = beginInstallation;
        _screens = new()
        {
            [LauncherSection.Play] = new PlaySectionScreenViewModel(),
            [LauncherSection.Servers] = new ServersSectionScreenViewModel(),
            [LauncherSection.GameOptions] = new GameOptionsSectionScreenViewModel(),
            [LauncherSection.DLC] = new DlcSectionScreenViewModel(),
            [LauncherSection.Mods] = new ModsSectionScreenViewModel(),
            [LauncherSection.GlobalSettings] = new GlobalLauncherSettingsSectionScreenViewModel(),
            [LauncherSection.AseSettings] = new AseLauncherSettingsSectionScreenViewModel(),
            [LauncherSection.AsaSettings] = new AsaLauncherSettingsSectionScreenViewModel(),
            [LauncherSection.About] = new AboutSectionScreenViewModel()
        };
        _selectedSection = LauncherSection.Play;
        _currentScreen = _screens[_selectedSection];
        _currentScreen.Activate();
        RefreshNavigationGroups();
        if (TryGetFirstNavigationNode(out LauncherNavNodeViewModel? node) && node is not null)
            SelectSectionInternal(node.Section, node.Key);
    }

    readonly bool _beginInstallation;
    readonly Dictionary<LauncherSection, LauncherSectionScreenViewModel> _screens;
    IReadOnlyList<LauncherNavGroupViewModel> _navigationGroups = [];
    ShellNoticeViewModel[] _baseNotices = [];
    bool _canRefreshShellStatus;
    bool _bootstrapSuccess;
    bool _bootstrapRestartRequired;
    bool _launcherUpdateAvailable;
    bool _gameUpdateAvailable;
    bool _dlcUpdatesAvailable;
    string? _catalogWarningMessage;
    string? _bootstrapWarningMessage;
    string? _bootstrapDownloadName;
    string? _bootstrapErrorMessage;
    LauncherSectionScreenViewModel _currentScreen;
    string? _downloadFailureName;
    string? _downloadFailureUrl;
    bool _isBusy;
    string _gameVersion = Locale.Get("common.loading");
    string _gameVersionColor = "#FFFFFF";
    ShellNoticeViewModel[] _notices = [];
    LauncherSection _selectedSection;
    string _selectedNodeKey = string.Empty;
    bool _shouldRestartAfterInitialization;
    bool _shouldStartInstallationAfterInitialization;
    string _subtitle;
    Task<bool>? _reloadAfterSwitchTask;
    Task<string?>? _navigationSwitchTask;

    public string Title => "Obelisk Launcher";

    public string Subtitle
    {
        get => _subtitle;
        private set => SetProperty(ref _subtitle, value);
    }

    public string GameVersion
    {
        get => _gameVersion;
        private set => SetProperty(ref _gameVersion, value);
    }

    public string GameVersionColor
    {
        get => _gameVersionColor;
        private set => SetProperty(ref _gameVersionColor, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? DownloadFailureName => _downloadFailureName;

    public string? DownloadFailureUrl => _downloadFailureUrl;

    public string? LastBootstrapErrorMessage => _bootstrapErrorMessage;

    public ShellNoticeViewModel[] Notices
    {
        get => _notices;
        private set => SetProperty(ref _notices, value);
    }

    public bool ShouldRestartAfterInitialization => _shouldRestartAfterInitialization;

    public bool ShouldStartInstallationAfterInitialization => _shouldStartInstallationAfterInitialization;

    public LauncherSectionScreenViewModel CurrentScreen
    {
        get => _currentScreen;
        private set => SetProperty(ref _currentScreen, value);
    }

    public IReadOnlyList<LauncherNavGroupViewModel> NavigationGroups
    {
        get => _navigationGroups;
        private set => SetProperty(ref _navigationGroups, value);
    }

    public string SelectedSectionDescription
    {
        get
        {
            LauncherNavNodeViewModel? node = FindNodeByKey(_selectedNodeKey);
            return node is not null ? node.Description : Locale.Get(LauncherShellNavigation.GetInfo(_selectedSection).Description);
        }
    }

    public string SelectedSectionTitle
    {
        get
        {
            LauncherNavNodeViewModel? node = FindNodeByKey(_selectedNodeKey);
            return node is not null ? node.DisplayTitle : Locale.Get(LauncherShellNavigation.GetInfo(_selectedSection).TitleCode);
        }
    }

    public LauncherSection SelectedSection
    {
        get => _selectedSection;
        private set
        {
            if (_selectedSection == value)
                return;

            _selectedSection = value;
            CurrentScreen = _screens[value];
            CurrentScreen.Activate();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSectionTitle));
            OnPropertyChanged(nameof(SelectedSectionDescription));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshLocale()
    {
        _defaultSubtitle = _startupMessage ?? (_beginInstallation
          ? Locale.Get("status.preparingInstallation")
          : Locale.Get("status.manageArkInstalls"));

        RefreshNavigationGroups();

        RebuildNotices();

        CurrentScreen.RefreshLocale();
        OnPropertyChanged(nameof(SelectedSectionTitle));
        OnPropertyChanged(nameof(SelectedSectionDescription));
    }

    public async Task InitializeAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var startup = await LauncherShellStartup.InitializeAsync(_beginInstallation);
            ApplyGameVersionStatus(startup.GameVersionText, startup.GameVersionTone);

            _downloadFailureName = startup.BootstrapResult.DownloadName;
            _downloadFailureUrl = startup.BootstrapResult.DownloadUrl;
            _shouldRestartAfterInitialization = startup.BootstrapResult.RestartRequired;
            _shouldStartInstallationAfterInitialization = startup.BeginInstallation
              && startup.BootstrapResult.Success
              && !startup.BootstrapResult.RestartRequired
              && startup.BootstrapResult.DownloadName is null;

            _launcherUpdateAvailable = startup.LauncherUpdateAvailable;
            _bootstrapSuccess = startup.BootstrapResult.Success;
            _bootstrapRestartRequired = startup.BootstrapResult.RestartRequired;
            _bootstrapWarningMessage = startup.BootstrapResult.WarningMessage;
            _bootstrapDownloadName = startup.BootstrapResult.DownloadName;
            _bootstrapErrorMessage = startup.BootstrapResult.ErrorMessage;
            _gameUpdateAvailable = startup.GameUpdateAvailable;
            _dlcUpdatesAvailable = startup.DlcUpdatesAvailable;
            _catalogWarningMessage = startup.CatalogWarning;
            RebuildNotices();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public bool TrySelectSection(LauncherSection section, out string? warningMessage)
    {
        warningMessage = null;
        if (LauncherShellNavigation.RequiresSpacewarWarning(section) && Steam.App.CurrentUserStatus.GameStatus == Game.Status.OwnedAndInstalled && !Game.UseSpacewar)
        {
            warningMessage = Locale.Get("modsTab.modsOnSteamWarning");
            return false;
        }

        string selectedNodeKey = ResolveNodeKeyForSection(section) ?? _selectedNodeKey;
        SelectSectionInternal(section, selectedNodeKey);
        RefreshNavigationGroups();
        OnPropertyChanged(nameof(SelectedSectionTitle));
        OnPropertyChanged(nameof(SelectedSectionDescription));
        return true;
    }

    public Task<string?> TrySelectNavigationNodeAsync(LauncherNavNodeViewModel node)
    {
        Task<string?>? inFlight = _navigationSwitchTask;
        if (inFlight is not null && !inFlight.IsCompleted)
        {
            LauncherLog.Debug("TrySelectNavigationNodeAsync: reusing in-flight navigation switch task.");
            return inFlight;
        }

        Task<string?> task = TrySelectNavigationNodeCoreAsync(node);
        _navigationSwitchTask = task;
        return task;
    }

    async Task<string?> TrySelectNavigationNodeCoreAsync(LauncherNavNodeViewModel node)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(node.GameId))
            {
                string targetPath = Settings.GetGamePath(node.GameId!);
                if (string.IsNullOrWhiteSpace(targetPath))
                    return string.Format(Locale.Get("errors.gamePathNotConfigured"), node.GameId);

                bool switchingGame = !ActiveGameManager.IsConfigured || !string.Equals(ActiveGameManager.Current.Id, node.GameId, StringComparison.OrdinalIgnoreCase);
                if (switchingGame)
                {
                    string? previousGameId = ActiveGameManager.IsConfigured ? ActiveGameManager.Current.Id : null;
                    string? previousGamePath = ActiveGameManager.IsConfigured ? ActiveGameManager.Current.RootPath : null;
                    bool previousPreAquatica = Settings.PreAquatica;

                    ActiveGameManager.Configure(node.GameId!, targetPath);
                    Settings.Save();

                    if (_bootstrapSuccess && LauncherServices.TekSteamClient.IsLoaded)
                    {
                        bool reloaded = await ReloadAfterGameSwitchAsync();
                        if (!reloaded)
                        {
                            if (string.IsNullOrWhiteSpace(_bootstrapErrorMessage))
                            {
                                LauncherLog.Warning("TrySelectNavigationNodeAsync: first game switch reload failed with no bootstrap error; retrying once. TargetGameId={TargetGameId}", node.GameId);
                                await Task.Delay(500);
                                reloaded = await ReloadAfterGameSwitchAsync();
                            }

                            if (reloaded)
                            {
                                LauncherLog.Information("TrySelectNavigationNodeAsync: game switch reload recovered on retry. TargetGameId={TargetGameId}", node.GameId);
                                Steam.App.UpdateUserStatus();
                            }
                            else
                            {
                                LauncherLog.Warning(
                                    "TrySelectNavigationNodeAsync: game switch reload failed. TargetGameId={TargetGameId}, TargetPath={TargetPath}, PreviousGameId={PreviousGameId}, PreviousPath={PreviousPath}, BootstrapError={BootstrapError}",
                                    node.GameId,
                                    targetPath,
                                    previousGameId ?? "<none>",
                                    previousGamePath ?? "<none>",
                                    _bootstrapErrorMessage ?? "<none>");

                                if (!string.IsNullOrWhiteSpace(previousGameId) && !string.IsNullOrWhiteSpace(previousGamePath))
                                {
                                    ActiveGameManager.Configure(previousGameId, previousGamePath);
                                    Settings.PreAquatica = previousPreAquatica;
                                    Settings.Save();
                                    await ReloadAfterGameSwitchAsync();
                                }

                                RefreshNavigationGroups();
                                return _bootstrapErrorMessage ?? "Game switch is still in progress. Please try again in a moment.";
                            }
                        }
                    }
                }
            }

            if (LauncherShellNavigation.RequiresSpacewarWarning(node.Section) && Steam.App.CurrentUserStatus.GameStatus == Game.Status.OwnedAndInstalled && !Game.UseSpacewar)
                return Locale.Get("modsTab.modsOnSteamWarning");

            SelectSectionInternal(node.Section, node.Key);
            RefreshNavigationGroups();
            OnPropertyChanged(nameof(SelectedSectionTitle));
            OnPropertyChanged(nameof(SelectedSectionDescription));
            return null;
        }
        finally
        {
            if (_navigationSwitchTask?.IsCompleted == true)
                _navigationSwitchTask = null;
        }
    }

    public void RefreshNavigation()
    {
        RefreshNavigationGroups();
        if (FindNodeByKey(_selectedNodeKey) is null && TryGetFirstNavigationNode(out LauncherNavNodeViewModel? node) && node is not null)
            SelectSectionInternal(node.Section, node.Key);

        OnPropertyChanged(nameof(SelectedSectionTitle));
        OnPropertyChanged(nameof(SelectedSectionDescription));
    }

    public async Task RefreshShellStatusAsync()
    {
        if (IsBusy || !_canRefreshShellStatus || !LauncherServices.TekSteamClient.IsLoaded)
            return;

        var status = await LauncherShellStartup.GetStatusSummaryAsync(_beginInstallation);
        ApplyGameVersionStatus(status.GameVersionText, status.GameVersionTone);
        _gameUpdateAvailable = status.GameUpdateAvailable;
        _dlcUpdatesAvailable = status.DlcUpdatesAvailable;
        _catalogWarningMessage = status.CatalogWarning;
        ApplyNotices(BuildDynamicNotices(_gameUpdateAvailable, _dlcUpdatesAvailable));
    }

    public Task<bool> ReloadAfterGameSwitchAsync()
    {
        Task<bool>? inFlight = _reloadAfterSwitchTask;
        if (inFlight is not null && !inFlight.IsCompleted)
        {
            LauncherLog.Debug("ReloadAfterGameSwitchAsync: reusing in-flight reload task.");
            return inFlight;
        }

        Task<bool> task = ReloadAfterGameSwitchCoreAsync();
        _reloadAfterSwitchTask = task;
        return task;
    }

    async Task<bool> ReloadAfterGameSwitchCoreAsync()
    {
        bool acquiredBusy = !IsBusy;
        if (acquiredBusy)
            IsBusy = true;
        else
            LauncherLog.Debug("ReloadAfterGameSwitchCoreAsync: proceeding while shell is already busy.");

        try
        {
            LauncherServices.ServerBrowser.Shutdown();
            LauncherServices.TekSteamClient.Close();
            Steam.CM.Client.Disconnect();
            Steam.App.UpdateUserStatus();
            await Task.Delay(250);

            TekSteamClientBootstrapResult bootstrapResult = default;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                bootstrapResult = await LauncherServices.TekSteamClientBootstrap.InitializeAsync(ActiveGameManager.Current.RootPath);
                if (bootstrapResult.Success && !bootstrapResult.RestartRequired && bootstrapResult.DownloadName is null)
                    break;

                LauncherLog.Warning(
                    "ReloadAfterGameSwitchAsync: tek-steamclient bootstrap attempt {Attempt} failed. Success={Success}, RestartRequired={RestartRequired}, DownloadName={DownloadName}, Error={Error}",
                    attempt,
                    bootstrapResult.Success,
                    bootstrapResult.RestartRequired,
                    bootstrapResult.DownloadName ?? "<none>",
                    bootstrapResult.ErrorMessage ?? "<none>");
                if (attempt < 3)
                    await Task.Delay(250 * attempt);
            }

            _bootstrapSuccess = bootstrapResult.Success;
            _bootstrapRestartRequired = bootstrapResult.RestartRequired;
            _bootstrapWarningMessage = bootstrapResult.WarningMessage;
            _bootstrapDownloadName = bootstrapResult.DownloadName;
            _bootstrapErrorMessage = bootstrapResult.ErrorMessage;

            if (!bootstrapResult.Success || bootstrapResult.RestartRequired || bootstrapResult.DownloadName is not null)
            {
                LauncherLog.Warning(
                    "ReloadAfterGameSwitchCoreAsync: bootstrap returned non-ready state. Success={Success}, RestartRequired={RestartRequired}, DownloadName={DownloadName}, Error={Error}",
                    bootstrapResult.Success,
                    bootstrapResult.RestartRequired,
                    bootstrapResult.DownloadName ?? "<none>",
                    bootstrapResult.ErrorMessage ?? "<none>");
                RebuildNotices();
                return false;
            }

            await Task.Run(Mod.InitializeList);
            await Task.Run(Cluster.ReloadLists);

            var status = await LauncherShellStartup.GetStatusSummaryAsync(_beginInstallation);
            ApplyGameVersionStatus(status.GameVersionText, status.GameVersionTone);
            _gameUpdateAvailable = status.GameUpdateAvailable;
            _dlcUpdatesAvailable = status.DlcUpdatesAvailable;
            _catalogWarningMessage = status.CatalogWarning;

            foreach (var screen in _screens.Values)
                screen.Activate();

            RebuildNotices();
            return true;
        }
        finally
        {
            if (acquiredBusy)
                IsBusy = false;
            if (_reloadAfterSwitchTask?.IsCompleted == true)
                _reloadAfterSwitchTask = null;
        }
    }

    public T? GetScreen<T>(LauncherSection section)
      where T : LauncherSectionScreenViewModel => _screens.TryGetValue(section, out LauncherSectionScreenViewModel? screen) ? screen as T : null;

    (ShellNoticeViewModel[] Notices, bool CanRefreshShellStatus) BuildBaseNotices()
    {
        var notices = new List<ShellNoticeViewModel>();

        if (_launcherUpdateAvailable)
            notices.Add(new ShellNoticeViewModel
            {
                ActionKind = ShellNoticeActionKind.OpenLauncherReleasePage,
                ActionLabel = Locale.Get("common.download"),
                Message = "An update to the launcher is available."
            });

        if (_bootstrapWarningMessage is not null)
            notices.Add(new ShellNoticeViewModel
            {
                ActionKind = ShellNoticeActionKind.None,
                Message = _bootstrapWarningMessage
            });

        if (!string.IsNullOrWhiteSpace(Steam.App.StartupNotice))
            notices.Add(new ShellNoticeViewModel
            {
                ActionKind = ShellNoticeActionKind.None,
                Message = Steam.App.StartupNotice
            });

        if (!string.IsNullOrWhiteSpace(GameCatalog.StartupNotice))
            notices.Add(new ShellNoticeViewModel
            {
                ActionKind = ShellNoticeActionKind.None,
                Message = GameCatalog.StartupNotice
            });

        if (_bootstrapDownloadName is not null)
        {
            notices.Add(new ShellNoticeViewModel
            {
                ActionKind = ShellNoticeActionKind.None,
                Message = Locale.Get("errors.downloadFailed") + _bootstrapDownloadName
            });
            return (notices.ToArray(), false);
        }

        if (_bootstrapRestartRequired)
        {
            notices.Add(new ShellNoticeViewModel
            {
                ActionKind = ShellNoticeActionKind.None,
                Message = Locale.Get("status.restartRequiredForSteamClient")
            });
            return (notices.ToArray(), false);
        }

        if (!_bootstrapSuccess)
        {
            notices.Add(new ShellNoticeViewModel
            {
                ActionKind = ShellNoticeActionKind.None,
                Message = _bootstrapErrorMessage ?? Locale.Get("errors.steamClientBootstrapFailed")
            });
            return (notices.ToArray(), false);
        }

        return (notices.ToArray(), true);
    }

    ShellNoticeViewModel[] BuildDynamicNotices(bool gameUpdateAvailable, bool dlcUpdatesAvailable)
    {
        var notices = new List<ShellNoticeViewModel>();
        if (!string.IsNullOrWhiteSpace(_catalogWarningMessage))
            notices.Add(new ShellNoticeViewModel
            {
                ActionKind = ShellNoticeActionKind.None,
                Message = _catalogWarningMessage
            });

        if (gameUpdateAvailable)
            notices.Add(new ShellNoticeViewModel
            {
                ActionKind = ShellNoticeActionKind.OpenGameUpdater,
                ActionLabel = Locale.Get("common.update"),
                Message = Locale.Get("gameUpdateAvailable")
            });

        if (dlcUpdatesAvailable)
            notices.Add(new ShellNoticeViewModel
            {
                ActionKind = ShellNoticeActionKind.OpenDlcSection,
                ActionLabel = Locale.Get("common.update"),
                Message = Locale.Get("dlcTab.dlcUpdatesAvailable")
            });

        return notices.ToArray();
    }

    void ApplyNotices(ShellNoticeViewModel[] dynamicNotices)
    {
        ShellNoticeViewModel[] notices = [.. _baseNotices, .. dynamicNotices];
        Notices = notices;
        Subtitle = notices.Length > 0 ? notices[0].Message : _defaultSubtitle;
    }

    void RebuildNotices()
    {
        (_baseNotices, _canRefreshShellStatus) = BuildBaseNotices();
        ApplyNotices(BuildDynamicNotices(_gameUpdateAvailable, _dlcUpdatesAvailable));
    }

    void RefreshNavigationGroups()
    {
        static IReadOnlyList<LauncherSection> CreateGameSections(string? gameId)
        {
            var sections = new List<LauncherSection> { LauncherSection.Play, LauncherSection.GameOptions, LauncherSection.DLC };
            bool isAsa = gameId == GameCatalog.AsaGameId;
            if (!isAsa)
                sections.Insert(1, LauncherSection.Servers);
            if (!isAsa)
                sections.Add(LauncherSection.Mods);
            sections.Insert(1, gameId == GameCatalog.AseGameId ? LauncherSection.AseSettings : LauncherSection.AsaSettings);
            return sections;
        }

        static LauncherNavNodeViewModel BuildNode(LauncherSection section, string? gameId, string selectedNodeKey)
        {
            LauncherSectionInfo info = LauncherShellNavigation.GetInfo(section);
            string key = $"{(string.IsNullOrWhiteSpace(gameId) ? "GLOBAL" : gameId)}:{section}";
            return new()
            {
                Key = key,
                Title = Locale.Get(info.TitleCode),
                Description = Locale.Get(info.Description),
                Section = section,
                GameId = gameId,
                IsSelected = key == selectedNodeKey
            };
        }

        var groups = new List<LauncherNavGroupViewModel>();
        foreach (GameCatalogEntry game in GameCatalog.AllGames.OrderBy(static game => game.Id, StringComparer.OrdinalIgnoreCase))
        {
            bool isVisible = !string.IsNullOrWhiteSpace(Settings.GetGamePath(game.Id));
            groups.Add(new LauncherNavGroupViewModel
            {
                Title = game.Id,
                IsVisible = isVisible,
                IsExpanded = true,
                Children = [.. CreateGameSections(game.Id).Select(section => BuildNode(section, game.Id, _selectedNodeKey))]
            });
        }

        groups.Add(new LauncherNavGroupViewModel
        {
            Title = "Global",
            IsVisible = true,
            IsExpanded = true,
            Children =
            [
                BuildNode(LauncherSection.GlobalSettings, null, _selectedNodeKey),
                BuildNode(LauncherSection.About, null, _selectedNodeKey)
            ]
        });

        NavigationGroups = groups;
    }

    void SelectSectionInternal(LauncherSection section, string selectedNodeKey)
    {
        _selectedNodeKey = selectedNodeKey;
        if (_selectedSection == section)
        {
            _screens[section].Activate();
            OnPropertyChanged(nameof(SelectedSectionTitle));
            OnPropertyChanged(nameof(SelectedSectionDescription));
            return;
        }

        SelectedSection = section;
    }

    LauncherNavNodeViewModel? FindNodeByKey(string key)
    {
        foreach (LauncherNavGroupViewModel group in NavigationGroups)
        {
            if (!group.IsVisible)
                continue;

            foreach (LauncherNavNodeViewModel node in group.Children)
                if (node.Key == key)
                    return node;
        }

        return null;
    }

    bool TryGetFirstNavigationNode(out LauncherNavNodeViewModel? node)
    {
        foreach (LauncherNavGroupViewModel group in NavigationGroups)
        {
            if (!group.IsVisible || group.Children.Count == 0)
                continue;

            node = group.Children[0];
            return true;
        }

        node = null;
        return false;
    }

    string? ResolveNodeKeyForSection(LauncherSection section)
    {
        if (section is LauncherSection.GlobalSettings or LauncherSection.About)
        {
            string globalKey = $"GLOBAL:{section}";
            return FindNodeByKey(globalKey) is null ? null : globalKey;
        }

        IEnumerable<string> gameOrder = ActiveGameManager.IsConfigured
            ? [ActiveGameManager.Current.Id, .. GameCatalog.AllGames.Select(static game => game.Id)]
            : [.. GameCatalog.AllGames.Select(static game => game.Id)];

        foreach (string gameId in gameOrder.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string key = $"{gameId}:{section}";
            if (FindNodeByKey(key) is not null)
                return key;
        }

        return null;
    }

    void ApplyGameVersionStatus(string text, LauncherShellTone tone)
    {
        GameVersion = text;
        GameVersionColor = tone switch
        {
            LauncherShellTone.Success => "#0AA63E",
            LauncherShellTone.Warning => "Yellow",
            LauncherShellTone.Error => "#9E2313",
            _ => "#FFFFFF"
        };
    }

    void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new(propertyName));

    void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }
}