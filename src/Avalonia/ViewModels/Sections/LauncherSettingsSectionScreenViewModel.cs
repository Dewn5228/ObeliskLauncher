namespace ObeliskLauncher.Avalonia.ViewModels;

public enum LauncherSettingsPageKind
{
    Global,
    Ase,
    Asa
}

public abstract class LauncherSettingsSectionScreenViewModel : LauncherSectionScreenViewModel
{
    const string AseScopeId = GameCatalog.AseGameId;
    const string AsaScopeId = GameCatalog.AsaGameId;

    string _aseGamePath;
    string _asaGamePath;
    string _linuxLaunchPresetName = string.Empty;
    IReadOnlyList<LinuxLaunchPreset> _linuxLaunchPresets;
    IReadOnlyList<LinuxLaunchToolOption> _linuxLaunchTools;
    LinuxLaunchPreset? _selectedLinuxLaunchPreset;
    readonly LauncherSettingsPageKind _pageKind;

    protected LauncherSettingsSectionScreenViewModel(LauncherSection section, LauncherSettingsPageKind pageKind)
        : base(section)
    {
        _pageKind = pageKind;
        _aseGamePath = Settings.AseGamePath;
        _asaGamePath = Settings.AsaGamePath;
        _linuxLaunchTools = LinuxLaunchToolResolver.GetAvailableOptions(Settings.GetLinuxLaunchTool(GetLinuxLaunchToolScopeIdForPage()), GetConfiguredRootPathOrFallback(), Settings.CustomLinuxLaunchToolIds);
        _linuxLaunchPresets = [.. Settings.LinuxLaunchPresets];
        _selectedLinuxLaunchPreset = _linuxLaunchPresets.Count > 0 ? _linuxLaunchPresets[0] : null;
    }

    public bool IsGlobalPage => _pageKind == LauncherSettingsPageKind.Global;

    public bool IsAsePage => _pageKind == LauncherSettingsPageKind.Ase;

    public bool IsAsaPage => _pageKind == LauncherSettingsPageKind.Asa;

    public bool IsScopedPage => _pageKind is LauncherSettingsPageKind.Ase or LauncherSettingsPageKind.Asa;

    public bool LinuxLaunchPresetsVisible => IsGlobalPage && OperatingSystem.IsLinux();

    public string PageTitle => _pageKind switch
    {
        LauncherSettingsPageKind.Global => "Global",
        LauncherSettingsPageKind.Ase => "ASE",
        LauncherSettingsPageKind.Asa => "ASA",
        _ => "Settings"
    };



    public string AseGamePath
    {
        get => _aseGamePath;
        set
        {
            if (_aseGamePath == value)
                return;

            _aseGamePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreAquaticaVisible));
        }
    }

    public string AsaGamePath
    {
        get => _asaGamePath;
        set => SetProperty(ref _asaGamePath, value);
    }

    public bool PreAquaticaVisible => IsGlobalPage && !string.IsNullOrWhiteSpace(AseGamePath);

    public bool CloseOnGameLaunch
    {
        get => Settings.CloseOnGameLaunch;
        set
        {
            if (Settings.CloseOnGameLaunch == value)
                return;

            Settings.CloseOnGameLaunch = value;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool PreAquatica
    {
        get => Settings.PreAquatica;
        set
        {
            if (Settings.PreAquatica == value)
                return;

            Settings.PreAquatica = value;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool LinuxLaunchToolVisible => IsScopedPage && OperatingSystem.IsLinux();

    public string LinuxLaunchToolNote => Locale.Get("launcherSettingsTab.linuxLaunchToolNote");

    public LinuxLaunchToolOption? SelectedAseLinuxLaunchTool
    {
        get => ResolveSelectedLinuxLaunchTool(LinuxLaunchTools, AseScopeId);
        set
        {
            if (value is null)
                return;

            string currentTool = LinuxLaunchToolResolver.NormalizeSelection(Settings.GetLinuxLaunchTool(AseScopeId));
            if (currentTool == value.Id)
                return;

            Settings.RegisterCustomLinuxLaunchTool(value.Id);
            Settings.SetLinuxLaunchTool(value.Id, AseScopeId);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedAseLinuxLaunchToolIndex));
            PersistSettings();
        }
    }

    public int SelectedAseLinuxLaunchToolIndex
    {
        get => ResolveSelectedLinuxLaunchToolIndex(LinuxLaunchTools, AseScopeId);
        set => SetLinuxLaunchToolByIndex(value, AseScopeId, nameof(SelectedAseLinuxLaunchToolIndex), nameof(SelectedAseLinuxLaunchTool));
    }

    public LinuxLaunchToolOption? SelectedAsaLinuxLaunchTool
    {
        get => ResolveSelectedLinuxLaunchTool(LinuxLaunchTools, AsaScopeId);
        set
        {
            if (value is null)
                return;

            string currentTool = LinuxLaunchToolResolver.NormalizeSelection(Settings.GetLinuxLaunchTool(AsaScopeId));
            if (currentTool == value.Id)
                return;

            Settings.RegisterCustomLinuxLaunchTool(value.Id);
            Settings.SetLinuxLaunchTool(value.Id, AsaScopeId);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedAsaLinuxLaunchToolIndex));
            PersistSettings();
        }
    }

    public int SelectedAsaLinuxLaunchToolIndex
    {
        get => ResolveSelectedLinuxLaunchToolIndex(LinuxLaunchTools, AsaScopeId);
        set => SetLinuxLaunchToolByIndex(value, AsaScopeId, nameof(SelectedAsaLinuxLaunchToolIndex), nameof(SelectedAsaLinuxLaunchTool));
    }

    public string AseLinuxCustomPrefixPath
    {
        get => Settings.GetLinuxCompatDataPath(AseScopeId);
        set
        {
            string normalized = value.Trim();
            if (Settings.GetLinuxCompatDataPath(AseScopeId) == normalized)
                return;

            Settings.SetLinuxCompatDataPath(normalized, AseScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public string AsaLinuxCustomPrefixPath
    {
        get => Settings.GetLinuxCompatDataPath(AsaScopeId);
        set
        {
            string normalized = value.Trim();
            if (Settings.GetLinuxCompatDataPath(AsaScopeId) == normalized)
                return;

            Settings.SetLinuxCompatDataPath(normalized, AsaScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public string AseLinuxExtraEnvironmentVariables
    {
        get => Settings.GetLinuxExtraEnvironmentVariables(AseScopeId);
        set
        {
            string normalized = value.Trim();
            if (Settings.GetLinuxExtraEnvironmentVariables(AseScopeId) == normalized)
                return;

            Settings.SetLinuxExtraEnvironmentVariables(normalized, AseScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public string AsaLinuxExtraEnvironmentVariables
    {
        get => Settings.GetLinuxExtraEnvironmentVariables(AsaScopeId);
        set
        {
            string normalized = value.Trim();
            if (Settings.GetLinuxExtraEnvironmentVariables(AsaScopeId) == normalized)
                return;

            Settings.SetLinuxExtraEnvironmentVariables(normalized, AsaScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public string AseLinuxLaunchWrappers
    {
        get => Settings.GetLinuxLaunchWrappers(AseScopeId);
        set
        {
            string normalized = value.Trim();
            if (Settings.GetLinuxLaunchWrappers(AseScopeId) == normalized)
                return;

            Settings.SetLinuxLaunchWrappers(normalized, AseScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public string AsaLinuxLaunchWrappers
    {
        get => Settings.GetLinuxLaunchWrappers(AsaScopeId);
        set
        {
            string normalized = value.Trim();
            if (Settings.GetLinuxLaunchWrappers(AsaScopeId) == normalized)
                return;

            Settings.SetLinuxLaunchWrappers(normalized, AsaScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool AseLinuxUseGameMode
    {
        get => Settings.GetLinuxUseGameMode(AseScopeId);
        set
        {
            if (Settings.GetLinuxUseGameMode(AseScopeId) == value)
                return;

            Settings.SetLinuxUseGameMode(value, AseScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool AsaLinuxUseGameMode
    {
        get => Settings.GetLinuxUseGameMode(AsaScopeId);
        set
        {
            if (Settings.GetLinuxUseGameMode(AsaScopeId) == value)
                return;

            Settings.SetLinuxUseGameMode(value, AsaScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool AseLinuxUseGamescope
    {
        get => Settings.GetLinuxUseGamescope(AseScopeId);
        set
        {
            if (Settings.GetLinuxUseGamescope(AseScopeId) == value)
                return;

            Settings.SetLinuxUseGamescope(value, AseScopeId);
            OnPropertyChanged();
            OnPropertyChanged(nameof(AseLinuxGamescopeOptionsVisible));
            PersistSettings();
        }
    }

    public bool AsaLinuxUseGamescope
    {
        get => Settings.GetLinuxUseGamescope(AsaScopeId);
        set
        {
            if (Settings.GetLinuxUseGamescope(AsaScopeId) == value)
                return;

            Settings.SetLinuxUseGamescope(value, AsaScopeId);
            OnPropertyChanged();
            OnPropertyChanged(nameof(AsaLinuxGamescopeOptionsVisible));
            PersistSettings();
        }
    }

    public string AseLinuxGamescopeArguments
    {
        get => Settings.GetLinuxGamescopeArguments(AseScopeId);
        set
        {
            string normalized = value.Trim();
            if (Settings.GetLinuxGamescopeArguments(AseScopeId) == normalized)
                return;

            Settings.SetLinuxGamescopeArguments(normalized, AseScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public string AsaLinuxGamescopeArguments
    {
        get => Settings.GetLinuxGamescopeArguments(AsaScopeId);
        set
        {
            string normalized = value.Trim();
            if (Settings.GetLinuxGamescopeArguments(AsaScopeId) == normalized)
                return;

            Settings.SetLinuxGamescopeArguments(normalized, AsaScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool AseLinuxUseGamescopeFsr
    {
        get => Settings.GetLinuxUseGamescopeFsr(AseScopeId);
        set
        {
            if (Settings.GetLinuxUseGamescopeFsr(AseScopeId) == value)
                return;

            Settings.SetLinuxUseGamescopeFsr(value, AseScopeId);
            OnPropertyChanged();
            OnPropertyChanged(nameof(AseLinuxGamescopeFsrOptionsVisible));
            PersistSettings();
        }
    }

    public bool AsaLinuxUseGamescopeFsr
    {
        get => Settings.GetLinuxUseGamescopeFsr(AsaScopeId);
        set
        {
            if (Settings.GetLinuxUseGamescopeFsr(AsaScopeId) == value)
                return;

            Settings.SetLinuxUseGamescopeFsr(value, AsaScopeId);
            OnPropertyChanged();
            OnPropertyChanged(nameof(AsaLinuxGamescopeFsrOptionsVisible));
            PersistSettings();
        }
    }

    public string AseLinuxGamescopeSharpness
    {
        get => Settings.GetLinuxGamescopeSharpness(AseScopeId);
        set
        {
            string normalized = value.Trim();
            if (Settings.GetLinuxGamescopeSharpness(AseScopeId) == normalized)
                return;

            Settings.SetLinuxGamescopeSharpness(normalized, AseScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public string AsaLinuxGamescopeSharpness
    {
        get => Settings.GetLinuxGamescopeSharpness(AsaScopeId);
        set
        {
            string normalized = value.Trim();
            if (Settings.GetLinuxGamescopeSharpness(AsaScopeId) == normalized)
                return;

            Settings.SetLinuxGamescopeSharpness(normalized, AsaScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool AseLinuxUseMangoHud
    {
        get => Settings.GetLinuxUseMangoHud(AseScopeId);
        set
        {
            if (Settings.GetLinuxUseMangoHud(AseScopeId) == value)
                return;

            Settings.SetLinuxUseMangoHud(value, AseScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool AsaLinuxUseMangoHud
    {
        get => Settings.GetLinuxUseMangoHud(AsaScopeId);
        set
        {
            if (Settings.GetLinuxUseMangoHud(AsaScopeId) == value)
                return;

            Settings.SetLinuxUseMangoHud(value, AsaScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool AseLinuxUseVkBasalt
    {
        get => Settings.GetLinuxUseVkBasalt(AseScopeId);
        set
        {
            if (Settings.GetLinuxUseVkBasalt(AseScopeId) == value)
                return;

            Settings.SetLinuxUseVkBasalt(value, AseScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool AsaLinuxUseVkBasalt
    {
        get => Settings.GetLinuxUseVkBasalt(AsaScopeId);
        set
        {
            if (Settings.GetLinuxUseVkBasalt(AsaScopeId) == value)
                return;

            Settings.SetLinuxUseVkBasalt(value, AsaScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool AseLinuxUseWineFullscreenFsr
    {
        get => Settings.GetLinuxUseWineFullscreenFsr(AseScopeId);
        set
        {
            if (Settings.GetLinuxUseWineFullscreenFsr(AseScopeId) == value)
                return;

            Settings.SetLinuxUseWineFullscreenFsr(value, AseScopeId);
            OnPropertyChanged();
            OnPropertyChanged(nameof(AseLinuxWineFsrOptionsVisible));
            PersistSettings();
        }
    }

    public bool AsaLinuxUseWineFullscreenFsr
    {
        get => Settings.GetLinuxUseWineFullscreenFsr(AsaScopeId);
        set
        {
            if (Settings.GetLinuxUseWineFullscreenFsr(AsaScopeId) == value)
                return;

            Settings.SetLinuxUseWineFullscreenFsr(value, AsaScopeId);
            OnPropertyChanged();
            OnPropertyChanged(nameof(AsaLinuxWineFsrOptionsVisible));
            PersistSettings();
        }
    }

    public string AseLinuxWineFullscreenFsrStrength
    {
        get => Settings.GetLinuxWineFullscreenFsrStrength(AseScopeId);
        set
        {
            string normalized = value.Trim();
            if (Settings.GetLinuxWineFullscreenFsrStrength(AseScopeId) == normalized)
                return;

            Settings.SetLinuxWineFullscreenFsrStrength(normalized, AseScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public string AsaLinuxWineFullscreenFsrStrength
    {
        get => Settings.GetLinuxWineFullscreenFsrStrength(AsaScopeId);
        set
        {
            string normalized = value.Trim();
            if (Settings.GetLinuxWineFullscreenFsrStrength(AsaScopeId) == normalized)
                return;

            Settings.SetLinuxWineFullscreenFsrStrength(normalized, AsaScopeId);
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool AseLinuxGamescopeOptionsVisible => IsAsePage && AseLinuxUseGamescope;

    public bool AsaLinuxGamescopeOptionsVisible => IsAsaPage && AsaLinuxUseGamescope;

    public bool AseLinuxGamescopeFsrOptionsVisible => IsAsePage && AseLinuxUseGamescope;

    public bool AsaLinuxGamescopeFsrOptionsVisible => IsAsaPage && AsaLinuxUseGamescope;

    public bool AseLinuxWineFsrOptionsVisible => IsAsePage && AseLinuxUseWineFullscreenFsr;

    public bool AsaLinuxWineFsrOptionsVisible => IsAsaPage && AsaLinuxUseWineFullscreenFsr;



    public string LinuxLaunchPresetName
    {
        get => _linuxLaunchPresetName;
        set => SetProperty(ref _linuxLaunchPresetName, value);
    }

    public IReadOnlyList<LinuxLaunchPreset> LinuxLaunchPresets
    {
        get => _linuxLaunchPresets;
        private set => SetProperty(ref _linuxLaunchPresets, value);
    }

    public LinuxLaunchPreset? SelectedLinuxLaunchPreset
    {
        get => _selectedLinuxLaunchPreset;
        set
        {
            if (_selectedLinuxLaunchPreset == value)
                return;

            _selectedLinuxLaunchPreset = value;
            if (value is not null)
                LinuxLaunchPresetName = value.Name;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanApplyLinuxLaunchPreset));
            OnPropertyChanged(nameof(CanDeleteLinuxLaunchPreset));
        }
    }

    public bool CanApplyLinuxLaunchPreset => SelectedLinuxLaunchPreset is not null;

    public bool CanDeleteLinuxLaunchPreset => SelectedLinuxLaunchPreset is not null;

    public IReadOnlyList<LinuxLaunchToolOption> LinuxLaunchTools
    {
        get => _linuxLaunchTools;
        private set => SetProperty(ref _linuxLaunchTools, value);
    }



    public override void Activate()
    {
        AseGamePath = Settings.AseGamePath;
        AsaGamePath = Settings.AsaGamePath;
        RefreshLinuxLaunchTools();
        RefreshLinuxLaunchPresets();
        OnPropertyChanged(nameof(AseGamePath));
        OnPropertyChanged(nameof(AsaGamePath));
        OnPropertyChanged(nameof(SelectedAseLinuxLaunchTool));
        OnPropertyChanged(nameof(SelectedAsaLinuxLaunchTool));
        OnPropertyChanged(nameof(AseLinuxCustomPrefixPath));
        OnPropertyChanged(nameof(AsaLinuxCustomPrefixPath));
        OnPropertyChanged(nameof(AseLinuxExtraEnvironmentVariables));
        OnPropertyChanged(nameof(AsaLinuxExtraEnvironmentVariables));
        OnPropertyChanged(nameof(AseLinuxLaunchWrappers));
        OnPropertyChanged(nameof(AsaLinuxLaunchWrappers));
        OnPropertyChanged(nameof(AseLinuxUseGameMode));
        OnPropertyChanged(nameof(AsaLinuxUseGameMode));
        OnPropertyChanged(nameof(AseLinuxUseGamescope));
        OnPropertyChanged(nameof(AsaLinuxUseGamescope));
        OnPropertyChanged(nameof(AseLinuxGamescopeArguments));
        OnPropertyChanged(nameof(AsaLinuxGamescopeArguments));
        OnPropertyChanged(nameof(AseLinuxUseGamescopeFsr));
        OnPropertyChanged(nameof(AsaLinuxUseGamescopeFsr));
        OnPropertyChanged(nameof(AseLinuxGamescopeSharpness));
        OnPropertyChanged(nameof(AsaLinuxGamescopeSharpness));
        OnPropertyChanged(nameof(AseLinuxUseMangoHud));
        OnPropertyChanged(nameof(AsaLinuxUseMangoHud));
        OnPropertyChanged(nameof(AseLinuxUseVkBasalt));
        OnPropertyChanged(nameof(AsaLinuxUseVkBasalt));
        OnPropertyChanged(nameof(AseLinuxUseWineFullscreenFsr));
        OnPropertyChanged(nameof(AsaLinuxUseWineFullscreenFsr));
        OnPropertyChanged(nameof(AseLinuxWineFullscreenFsrStrength));
        OnPropertyChanged(nameof(AsaLinuxWineFullscreenFsrStrength));
        OnPropertyChanged(nameof(AseLinuxGamescopeOptionsVisible));
        OnPropertyChanged(nameof(AsaLinuxGamescopeOptionsVisible));
        OnPropertyChanged(nameof(AseLinuxGamescopeFsrOptionsVisible));
        OnPropertyChanged(nameof(AsaLinuxGamescopeFsrOptionsVisible));
        OnPropertyChanged(nameof(AseLinuxWineFsrOptionsVisible));
        OnPropertyChanged(nameof(AsaLinuxWineFsrOptionsVisible));
        OnPropertyChanged(nameof(CloseOnGameLaunch));
        OnPropertyChanged(nameof(IsGlobalPage));
        OnPropertyChanged(nameof(IsAsePage));
        OnPropertyChanged(nameof(IsAsaPage));
        OnPropertyChanged(nameof(IsScopedPage));
        OnPropertyChanged(nameof(LinuxLaunchPresetsVisible));
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(LinuxLaunchPresetName));
        OnPropertyChanged(nameof(LinuxLaunchToolNote));
        OnPropertyChanged(nameof(LinuxLaunchToolVisible));
        OnPropertyChanged(nameof(PreAquatica));
        OnPropertyChanged(nameof(PreAquaticaVisible));
    }

    public override void RefreshLocale()
    {
        OnPropertyChanged(nameof(LinuxLaunchToolNote));
        OnPropertyChanged(nameof(LinuxLaunchPresetName));
    }





    public void ImportCustomLinuxLaunchToolForGame(string gameId, LinuxLaunchToolKind kind, string executablePath)
    {
        string normalizedPath = Path.GetFullPath(executablePath);
        string toolId = $"{(kind == LinuxLaunchToolKind.Proton ? "proton" : "wine")}:{normalizedPath}";
        Settings.RegisterCustomLinuxLaunchTool(toolId);
        Settings.SetLinuxLaunchTool(toolId, gameId);
        RefreshLinuxLaunchTools();
        OnPropertyChanged(nameof(SelectedAseLinuxLaunchTool));
        OnPropertyChanged(nameof(SelectedAsaLinuxLaunchTool));
        PersistSettings();
    }

    public void SaveLinuxLaunchPreset()
    {
        string presetName = LinuxLaunchPresetName.Trim();
        if (string.IsNullOrWhiteSpace(presetName))
            return;

        Settings.SaveLinuxLaunchPreset(Settings.CreateLinuxLaunchPreset(presetName));
        RefreshLinuxLaunchPresets(presetName);
        PersistSettings();
    }

    public void ApplySelectedLinuxPreset()
    {
        if (SelectedLinuxLaunchPreset is null)
            return;

        LinuxLaunchPreset preset = SelectedLinuxLaunchPreset;
        foreach (string gameId in new[] { AseScopeId, AsaScopeId })
        {
            Settings.SetLinuxLaunchTool(preset.LaunchTool, gameId);
            Settings.RegisterCustomLinuxLaunchTool(preset.LaunchTool);
            Settings.SetLinuxCompatDataPath(preset.PrefixPath ?? string.Empty, gameId);
            Settings.SetLinuxExtraEnvironmentVariables(preset.ExtraEnvironmentVariables ?? string.Empty, gameId);
            Settings.SetLinuxLaunchWrappers(preset.LaunchWrappers ?? string.Empty, gameId);
            Settings.SetLinuxUseGameMode(preset.UseGameMode, gameId);
            Settings.SetLinuxUseGamescope(preset.UseGamescope, gameId);
            Settings.SetLinuxGamescopeArguments(preset.GamescopeArguments ?? string.Empty, gameId);
            Settings.SetLinuxUseGamescopeFsr(preset.UseGamescopeFsr, gameId);
            Settings.SetLinuxGamescopeSharpness(preset.GamescopeSharpness ?? string.Empty, gameId);
            Settings.SetLinuxUseMangoHud(preset.UseMangoHud, gameId);
            Settings.SetLinuxUseVkBasalt(preset.UseVkBasalt, gameId);
            Settings.SetLinuxUseWineFullscreenFsr(preset.UseWineFullscreenFsr, gameId);
            Settings.SetLinuxWineFullscreenFsrStrength(preset.WineFullscreenFsrStrength ?? string.Empty, gameId);
        }
        RefreshLinuxLaunchTools();
        PersistSettings();
        OnPropertyChanged(nameof(SelectedAseLinuxLaunchTool));
        OnPropertyChanged(nameof(SelectedAsaLinuxLaunchTool));
        OnPropertyChanged(nameof(AseLinuxCustomPrefixPath));
        OnPropertyChanged(nameof(AsaLinuxCustomPrefixPath));
        OnPropertyChanged(nameof(AseLinuxExtraEnvironmentVariables));
        OnPropertyChanged(nameof(AsaLinuxExtraEnvironmentVariables));
        OnPropertyChanged(nameof(AseLinuxGamescopeArguments));
        OnPropertyChanged(nameof(AsaLinuxGamescopeArguments));
        OnPropertyChanged(nameof(AseLinuxLaunchWrappers));
        OnPropertyChanged(nameof(AsaLinuxLaunchWrappers));
        OnPropertyChanged(nameof(AseLinuxUseGameMode));
        OnPropertyChanged(nameof(AsaLinuxUseGameMode));
        OnPropertyChanged(nameof(AseLinuxUseGamescope));
        OnPropertyChanged(nameof(AsaLinuxUseGamescope));
        OnPropertyChanged(nameof(AseLinuxUseGamescopeFsr));
        OnPropertyChanged(nameof(AsaLinuxUseGamescopeFsr));
        OnPropertyChanged(nameof(AseLinuxGamescopeSharpness));
        OnPropertyChanged(nameof(AsaLinuxGamescopeSharpness));
        OnPropertyChanged(nameof(AseLinuxUseMangoHud));
        OnPropertyChanged(nameof(AsaLinuxUseMangoHud));
        OnPropertyChanged(nameof(AseLinuxUseVkBasalt));
        OnPropertyChanged(nameof(AsaLinuxUseVkBasalt));
        OnPropertyChanged(nameof(AseLinuxUseWineFullscreenFsr));
        OnPropertyChanged(nameof(AsaLinuxUseWineFullscreenFsr));
        OnPropertyChanged(nameof(AseLinuxWineFullscreenFsrStrength));
        OnPropertyChanged(nameof(AsaLinuxWineFullscreenFsrStrength));
    }

    public bool DeleteSelectedLinuxPreset()
    {
        if (SelectedLinuxLaunchPreset is null)
            return false;

        string presetName = SelectedLinuxLaunchPreset.Name;
        bool deleted = Settings.DeleteLinuxLaunchPreset(presetName);
        if (deleted)
        {
            RefreshLinuxLaunchPresets();
            LinuxLaunchPresetName = string.Empty;
            OnPropertyChanged(nameof(LinuxLaunchPresetName));
            PersistSettings();
        }

        return deleted;
    }

    public bool TryValidateGameSelection(out string? errorMessage)
    {
        errorMessage = null;

        string normalizedAsePath = NormalizeDirectoryPath(AseGamePath);
        string normalizedAsaPath = NormalizeDirectoryPath(AsaGamePath);
        if (string.IsNullOrWhiteSpace(normalizedAsePath) && string.IsNullOrWhiteSpace(normalizedAsaPath))
        {
            errorMessage = Locale.Get("errors.noPathSelected");
            return false;
        }

        return true;
    }

    public void SaveGamePathDrafts()
    {
        ApplyGamePathSettings();
        PersistSettings();
    }

    public void ApplyGameSelection()
    {
        ApplyGamePathSettings();

        if (ActiveGameManager.IsConfigured)
        {
            string activeGameId = ActiveGameManager.Current.Id;
            string activePath = Settings.GetGamePath(activeGameId);

            if (!string.IsNullOrWhiteSpace(activePath))
                ActiveGameManager.Configure(activeGameId, activePath);
            else if (!string.IsNullOrWhiteSpace(Settings.GetGamePath(AseScopeId)))
                ActiveGameManager.Configure(AseScopeId, Settings.GetGamePath(AseScopeId));
            else if (!string.IsNullOrWhiteSpace(Settings.GetGamePath(AsaScopeId)))
                ActiveGameManager.Configure(AsaScopeId, Settings.GetGamePath(AsaScopeId));
        }
        else if (!string.IsNullOrWhiteSpace(Settings.GetGamePath(AseScopeId)))
            ActiveGameManager.Configure(AseScopeId, Settings.GetGamePath(AseScopeId));
        else if (!string.IsNullOrWhiteSpace(Settings.GetGamePath(AsaScopeId)))
            ActiveGameManager.Configure(AsaScopeId, Settings.GetGamePath(AsaScopeId));

        PersistSettings();
    }

    public bool IsGameSelectionChanged()
    {
        string normalizedAsePath = NormalizeDirectoryPath(AseGamePath);
        string normalizedAsaPath = NormalizeDirectoryPath(AsaGamePath);

        bool draftChanged = !string.Equals(normalizedAsePath, Settings.AseGamePath, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(normalizedAsaPath, Settings.AsaGamePath, StringComparison.OrdinalIgnoreCase);

        if (!ActiveGameManager.IsConfigured)
            return draftChanged || !string.IsNullOrWhiteSpace(normalizedAsePath) || !string.IsNullOrWhiteSpace(normalizedAsaPath);

        string expectedActivePath = ActiveGameManager.Current.Id == AsaScopeId ? normalizedAsaPath : normalizedAsePath;
        bool activePathChanged = !string.IsNullOrWhiteSpace(expectedActivePath)
            && !string.Equals(expectedActivePath, ActiveGameManager.Current.RootPath, StringComparison.OrdinalIgnoreCase);

        return draftChanged || activePathChanged;
    }

    void ApplyGamePathSettings()
    {
        Settings.AseGamePath = NormalizeDirectoryPath(AseGamePath);
        Settings.AsaGamePath = NormalizeDirectoryPath(AsaGamePath);
        if (Settings.PreAquatica && string.IsNullOrWhiteSpace(Settings.AseGamePath))
            Settings.PreAquatica = false;
    }

    void RefreshLinuxLaunchTools()
    {
        LinuxLaunchTools = LinuxLaunchToolResolver.GetAvailableOptions(Settings.GetLinuxLaunchTool(GetLinuxLaunchToolScopeIdForPage()), GetConfiguredRootPathOrFallback(), Settings.CustomLinuxLaunchToolIds);
        LauncherLog.Debug("Linux launch tools refreshed. PageKind={PageKind}, ScopeGameId={ScopeGameId}, OptionCount={OptionCount}, CurrentTool={CurrentTool}",
            _pageKind,
            GetLinuxLaunchToolScopeIdForPage(),
            LinuxLaunchTools.Count,
            Settings.GetLinuxLaunchTool(GetLinuxLaunchToolScopeIdForPage()));
        OnPropertyChanged(nameof(SelectedAseLinuxLaunchToolIndex));
        OnPropertyChanged(nameof(SelectedAsaLinuxLaunchToolIndex));
        OnPropertyChanged(nameof(SelectedAseLinuxLaunchTool));
        OnPropertyChanged(nameof(SelectedAsaLinuxLaunchTool));
        OnPropertyChanged(nameof(PreAquaticaVisible));
    }

    string GetLinuxLaunchToolScopeIdForPage()
        => _pageKind == LauncherSettingsPageKind.Asa ? AsaScopeId : AseScopeId;

    static void PersistSettings() => Settings.Save();

    void RefreshLinuxLaunchPresets(string? selectedPresetName = null)
    {
        LinuxLaunchPresets = [.. Settings.LinuxLaunchPresets];
        string targetName = selectedPresetName ?? SelectedLinuxLaunchPreset?.Name ?? LinuxLaunchPresetName.Trim();
        SelectedLinuxLaunchPreset = ResolveSelectedLinuxLaunchPreset(LinuxLaunchPresets, targetName);
    }

    static LinuxLaunchToolOption? ResolveSelectedLinuxLaunchTool(IReadOnlyList<LinuxLaunchToolOption> options, string gameId)
    {
        string selectedId = LinuxLaunchToolResolver.NormalizeSelection(Settings.GetLinuxLaunchTool(gameId));
        foreach (LinuxLaunchToolOption option in options)
            if (option.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
                return option;

        return options.Count > 0 ? options[0] : null;
    }

    static int ResolveSelectedLinuxLaunchToolIndex(IReadOnlyList<LinuxLaunchToolOption> options, string gameId)
    {
        LinuxLaunchToolOption? selected = ResolveSelectedLinuxLaunchTool(options, gameId);
        if (selected is null)
            return -1;

        for (int i = 0; i < options.Count; i++)
            if (ReferenceEquals(options[i], selected) || options[i].Id.Equals(selected.Id, StringComparison.OrdinalIgnoreCase))
                return i;

        return -1;
    }

    void SetLinuxLaunchToolByIndex(int index, string gameId, string indexPropertyName, string selectedToolPropertyName)
    {
        LauncherLog.Debug("Linux tool selection request. PageKind={PageKind}, GameId={GameId}, RequestedIndex={RequestedIndex}, OptionCount={OptionCount}",
            _pageKind,
            gameId,
            index,
            LinuxLaunchTools.Count);

        if (index < 0 || index >= LinuxLaunchTools.Count)
        {
            LauncherLog.Warning("Linux tool selection ignored because index is out of range. GameId={GameId}, RequestedIndex={RequestedIndex}, OptionCount={OptionCount}",
                gameId,
                index,
                LinuxLaunchTools.Count);
            return;
        }

        LinuxLaunchToolOption selectedTool = LinuxLaunchTools[index];
        string currentTool = LinuxLaunchToolResolver.NormalizeSelection(Settings.GetLinuxLaunchTool(gameId));
        if (currentTool.Equals(selectedTool.Id, StringComparison.OrdinalIgnoreCase))
        {
            LauncherLog.Debug("Linux tool selection ignored because selected tool already active. GameId={GameId}, ToolId={ToolId}",
                gameId,
                selectedTool.Id);
            return;
        }

        Settings.RegisterCustomLinuxLaunchTool(selectedTool.Id);
        Settings.SetLinuxLaunchTool(selectedTool.Id, gameId);
        LauncherLog.Information("Linux tool selection applied. GameId={GameId}, PreviousToolId={PreviousToolId}, NewToolId={NewToolId}",
            gameId,
            currentTool,
            selectedTool.Id);
        OnPropertyChanged(indexPropertyName);
        OnPropertyChanged(selectedToolPropertyName);
        PersistSettings();
    }

    static LinuxLaunchPreset? ResolveSelectedLinuxLaunchPreset(IReadOnlyList<LinuxLaunchPreset> options, string? selectedName)
    {
        if (!string.IsNullOrWhiteSpace(selectedName))
            foreach (LinuxLaunchPreset option in options)
                if (option.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase))
                    return option;

        return options.Count > 0 ? options[0] : null;
    }

    static string NormalizeDirectoryPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        try
        {
            return Path.GetFullPath(value.Trim());
        }
        catch
        {
            return string.Empty;
        }
    }

    static string GetConfiguredRootPathOrFallback()
    {
        if (ActiveGameManager.IsConfigured)
            return ActiveGameManager.Current.RootPath;

        if (!string.IsNullOrWhiteSpace(Settings.AseGamePath))
            return Settings.AseGamePath;

        if (!string.IsNullOrWhiteSpace(Settings.AsaGamePath))
            return Settings.AsaGamePath;

        return string.Empty;
    }
}