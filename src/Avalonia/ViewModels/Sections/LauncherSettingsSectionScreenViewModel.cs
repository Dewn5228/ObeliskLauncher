namespace TEKLauncher.Avalonia.ViewModels;

public sealed class LauncherSettingsSectionScreenViewModel : LauncherSectionScreenViewModel
{
    string _gamePath;
    string _linuxLaunchPresetName = string.Empty;
    IReadOnlyList<LinuxLaunchPreset> _linuxLaunchPresets;
    IReadOnlyList<LinuxLaunchToolOption> _linuxLaunchTools;
    LinuxLaunchPreset? _selectedLinuxLaunchPreset;
    LinuxLaunchToolOption? _selectedLinuxLaunchTool;

    public LauncherSettingsSectionScreenViewModel()
      : base(LauncherSection.LauncherSettings)
    {
        _gamePath = Game.Path ?? string.Empty;
        _linuxLaunchTools = LinuxLaunchToolResolver.GetAvailableOptions(Settings.LinuxLaunchTool, Game.Path, Settings.CustomLinuxLaunchToolIds);
        _selectedLinuxLaunchTool = ResolveSelectedLinuxLaunchTool(_linuxLaunchTools);
        _linuxLaunchPresets = [.. Settings.LinuxLaunchPresets];
        _selectedLinuxLaunchPreset = _linuxLaunchPresets.Count > 0 ? _linuxLaunchPresets[0] : null;
    }

    public string GamePath
    {
        get => _gamePath;
        set => SetProperty(ref _gamePath, value);
    }

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

    public bool CommunismMode
    {
        get => Settings.CommunismMode;
        set
        {
            if (Settings.CommunismMode == value)
                return;

            Settings.CommunismMode = value;
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

    public bool LinuxLaunchToolVisible => OperatingSystem.IsLinux();

    public string LinuxLaunchToolNote => Locale.Get("launcherSettingsTab.linuxLaunchToolNote");

    public string LinuxCustomPrefixPath
    {
        get => Settings.LinuxCompatDataPath;
        set
        {
            string normalized = value.Trim();
            if (Settings.LinuxCompatDataPath == normalized)
                return;

            Settings.LinuxCompatDataPath = normalized;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public string LinuxExtraEnvironmentVariables
    {
        get => Settings.LinuxExtraEnvironmentVariables;
        set
        {
            string normalized = value.Trim();
            if (Settings.LinuxExtraEnvironmentVariables == normalized)
                return;

            Settings.LinuxExtraEnvironmentVariables = normalized;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public string LinuxLaunchWrappers
    {
        get => Settings.LinuxLaunchWrappers;
        set
        {
            string normalized = value.Trim();
            if (Settings.LinuxLaunchWrappers == normalized)
                return;

            Settings.LinuxLaunchWrappers = normalized;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool LinuxUseGameMode
    {
        get => Settings.LinuxUseGameMode;
        set
        {
            if (Settings.LinuxUseGameMode == value)
                return;

            Settings.LinuxUseGameMode = value;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool LinuxUseGamescope
    {
        get => Settings.LinuxUseGamescope;
        set
        {
            if (Settings.LinuxUseGamescope == value)
                return;

            Settings.LinuxUseGamescope = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LinuxGamescopeOptionsVisible));
            PersistSettings();
        }
    }

    public string LinuxGamescopeArguments
    {
        get => Settings.LinuxGamescopeArguments;
        set
        {
            string normalized = value.Trim();
            if (Settings.LinuxGamescopeArguments == normalized)
                return;

            Settings.LinuxGamescopeArguments = normalized;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool LinuxUseGamescopeFsr
    {
        get => Settings.LinuxUseGamescopeFsr;
        set
        {
            if (Settings.LinuxUseGamescopeFsr == value)
                return;

            Settings.LinuxUseGamescopeFsr = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LinuxGamescopeFsrOptionsVisible));
            PersistSettings();
        }
    }

    public string LinuxGamescopeSharpness
    {
        get => Settings.LinuxGamescopeSharpness;
        set
        {
            string normalized = value.Trim();
            if (Settings.LinuxGamescopeSharpness == normalized)
                return;

            Settings.LinuxGamescopeSharpness = normalized;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool LinuxUseMangoHud
    {
        get => Settings.LinuxUseMangoHud;
        set
        {
            if (Settings.LinuxUseMangoHud == value)
                return;

            Settings.LinuxUseMangoHud = value;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool LinuxUseVkBasalt
    {
        get => Settings.LinuxUseVkBasalt;
        set
        {
            if (Settings.LinuxUseVkBasalt == value)
                return;

            Settings.LinuxUseVkBasalt = value;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool LinuxUseWineFullscreenFsr
    {
        get => Settings.LinuxUseWineFullscreenFsr;
        set
        {
            if (Settings.LinuxUseWineFullscreenFsr == value)
                return;

            Settings.LinuxUseWineFullscreenFsr = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LinuxWineFsrOptionsVisible));
            PersistSettings();
        }
    }

    public string LinuxWineFullscreenFsrStrength
    {
        get => Settings.LinuxWineFullscreenFsrStrength;
        set
        {
            string normalized = value.Trim();
            if (Settings.LinuxWineFullscreenFsrStrength == normalized)
                return;

            Settings.LinuxWineFullscreenFsrStrength = normalized;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public bool LinuxGamescopeOptionsVisible => LinuxUseGamescope;

    public bool LinuxGamescopeFsrOptionsVisible => LinuxUseGamescope;

    public bool LinuxWineFsrOptionsVisible => LinuxUseWineFullscreenFsr;

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

    public LinuxLaunchToolOption? SelectedLinuxLaunchTool
    {
        get => _selectedLinuxLaunchTool;
        set
        {
            if (value is null || _selectedLinuxLaunchTool?.Id == value.Id)
                return;

            _selectedLinuxLaunchTool = value;
            Settings.RegisterCustomLinuxLaunchTool(value.Id);
            Settings.LinuxLaunchTool = value.Id;
            OnPropertyChanged();
            PersistSettings();
        }
    }

    public override void Activate()
    {
        GamePath = Game.Path ?? string.Empty;
        RefreshLinuxLaunchTools();
        RefreshLinuxLaunchPresets();
        OnPropertyChanged(nameof(CloseOnGameLaunch));
        OnPropertyChanged(nameof(CommunismMode));
        OnPropertyChanged(nameof(LinuxCustomPrefixPath));
        OnPropertyChanged(nameof(LinuxExtraEnvironmentVariables));
        OnPropertyChanged(nameof(LinuxGamescopeArguments));
        OnPropertyChanged(nameof(LinuxGamescopeFsrOptionsVisible));
        OnPropertyChanged(nameof(LinuxGamescopeOptionsVisible));
        OnPropertyChanged(nameof(LinuxGamescopeSharpness));
        OnPropertyChanged(nameof(LinuxLaunchPresetName));
        OnPropertyChanged(nameof(LinuxLaunchToolNote));
        OnPropertyChanged(nameof(LinuxLaunchToolVisible));
        OnPropertyChanged(nameof(LinuxLaunchWrappers));
        OnPropertyChanged(nameof(LinuxUseGameMode));
        OnPropertyChanged(nameof(LinuxUseGamescope));
        OnPropertyChanged(nameof(LinuxUseGamescopeFsr));
        OnPropertyChanged(nameof(LinuxUseMangoHud));
        OnPropertyChanged(nameof(LinuxUseVkBasalt));
        OnPropertyChanged(nameof(LinuxUseWineFullscreenFsr));
        OnPropertyChanged(nameof(LinuxWineFullscreenFsrStrength));
        OnPropertyChanged(nameof(LinuxWineFsrOptionsVisible));
        OnPropertyChanged(nameof(PreAquatica));
    }

    public void ApplySelectedLinuxPreset()
    {
        if (SelectedLinuxLaunchPreset is null)
            return;

        Settings.ApplyLinuxLaunchPreset(SelectedLinuxLaunchPreset);
        RefreshLinuxLaunchTools();
        RaiseLinuxLaunchConfigurationChanged();
        PersistSettings();
    }

    public void ImportCustomLinuxLaunchTool(LinuxLaunchToolKind kind, string executablePath)
    {
        string normalizedPath = Path.GetFullPath(executablePath);
        string toolId = $"{(kind == LinuxLaunchToolKind.Proton ? "proton" : "wine")}:{normalizedPath}";
        Settings.RegisterCustomLinuxLaunchTool(toolId);
        Settings.LinuxLaunchTool = toolId;
        RefreshLinuxLaunchTools();
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

    void RefreshLinuxLaunchTools()
    {
        LinuxLaunchTools = LinuxLaunchToolResolver.GetAvailableOptions(Settings.LinuxLaunchTool, Game.Path, Settings.CustomLinuxLaunchToolIds);
        _selectedLinuxLaunchTool = ResolveSelectedLinuxLaunchTool(LinuxLaunchTools);
        OnPropertyChanged(nameof(SelectedLinuxLaunchTool));
    }

    void RaiseLinuxLaunchConfigurationChanged()
    {
        OnPropertyChanged(nameof(LinuxCustomPrefixPath));
        OnPropertyChanged(nameof(LinuxExtraEnvironmentVariables));
        OnPropertyChanged(nameof(LinuxGamescopeArguments));
        OnPropertyChanged(nameof(LinuxGamescopeSharpness));
        OnPropertyChanged(nameof(LinuxLaunchWrappers));
        OnPropertyChanged(nameof(LinuxUseGameMode));
        OnPropertyChanged(nameof(LinuxUseGamescope));
        OnPropertyChanged(nameof(LinuxUseGamescopeFsr));
        OnPropertyChanged(nameof(LinuxUseMangoHud));
        OnPropertyChanged(nameof(LinuxUseVkBasalt));
        OnPropertyChanged(nameof(LinuxUseWineFullscreenFsr));
        OnPropertyChanged(nameof(LinuxWineFullscreenFsrStrength));
        OnPropertyChanged(nameof(LinuxGamescopeOptionsVisible));
        OnPropertyChanged(nameof(LinuxGamescopeFsrOptionsVisible));
        OnPropertyChanged(nameof(LinuxWineFsrOptionsVisible));
    }

    static void PersistSettings() => Settings.Save();

    void RefreshLinuxLaunchPresets(string? selectedPresetName = null)
    {
        LinuxLaunchPresets = [.. Settings.LinuxLaunchPresets];
        string targetName = selectedPresetName ?? SelectedLinuxLaunchPreset?.Name ?? LinuxLaunchPresetName.Trim();
        SelectedLinuxLaunchPreset = ResolveSelectedLinuxLaunchPreset(LinuxLaunchPresets, targetName);
    }

    static LinuxLaunchToolOption? ResolveSelectedLinuxLaunchTool(IReadOnlyList<LinuxLaunchToolOption> options)
    {
        string selectedId = LinuxLaunchToolResolver.NormalizeSelection(Settings.LinuxLaunchTool);
        foreach (LinuxLaunchToolOption option in options)
            if (option.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
                return option;

        return options.Count > 0 ? options[0] : null;
    }

    static LinuxLaunchPreset? ResolveSelectedLinuxLaunchPreset(IReadOnlyList<LinuxLaunchPreset> options, string? selectedName)
    {
        if (!string.IsNullOrWhiteSpace(selectedName))
            foreach (LinuxLaunchPreset option in options)
                if (option.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase))
                    return option;

        return options.Count > 0 ? options[0] : null;
    }
}