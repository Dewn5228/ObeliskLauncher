using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TEKLauncher.Avalonia.ViewModels;

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
            [LauncherSection.LauncherSettings] = new LauncherSettingsSectionScreenViewModel(),
            [LauncherSection.About] = new AboutSectionScreenViewModel()
        };
        _selectedSection = LauncherSection.Play;
        _currentScreen = _screens[_selectedSection];
        _currentScreen.Activate();
    }

    readonly bool _beginInstallation;
    readonly Dictionary<LauncherSection, LauncherSectionScreenViewModel> _screens;
    ShellNoticeViewModel[] _baseNotices = [];
    bool _canRefreshShellStatus;
    bool _bootstrapSuccess;
    bool _bootstrapRestartRequired;
    bool _launcherUpdateAvailable;
    bool _gameUpdateAvailable;
    bool _dlcUpdatesAvailable;
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
    bool _shouldRestartAfterInitialization;
    bool _shouldStartInstallationAfterInitialization;
    string _subtitle;

    public string Title => "TEK Launcher";

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

    public LauncherSectionInfo[] Sections => LauncherShellNavigation.Sections;

    public string SelectedSectionDescription => Locale.Get(LauncherShellNavigation.GetInfo(_selectedSection).Description);

    public string SelectedSectionTitle => Locale.Get(LauncherShellNavigation.GetInfo(_selectedSection).TitleCode);

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

        SelectedSection = section;
        return true;
    }

    public async Task RefreshShellStatusAsync()
    {
        if (IsBusy || !_canRefreshShellStatus || !LauncherServices.TekSteamClient.IsLoaded)
            return;

        var status = await LauncherShellStartup.GetStatusSummaryAsync(_beginInstallation);
        ApplyGameVersionStatus(status.GameVersionText, status.GameVersionTone);
        _gameUpdateAvailable = status.GameUpdateAvailable;
        _dlcUpdatesAvailable = status.DlcUpdatesAvailable;
        ApplyNotices(BuildDynamicNotices(_gameUpdateAvailable, _dlcUpdatesAvailable));
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
                ActionLabel = Locale.Get("common.update"),
                Message = Locale.Get("status.whatsNew")
            });

        if (_bootstrapWarningMessage is not null)
            notices.Add(new ShellNoticeViewModel
            {
                ActionKind = ShellNoticeActionKind.None,
                Message = _bootstrapWarningMessage
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