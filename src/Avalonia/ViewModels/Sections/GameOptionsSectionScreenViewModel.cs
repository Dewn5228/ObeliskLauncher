using System.ComponentModel;
using System.Linq;

namespace ObeliskLauncher.Avalonia.ViewModels;

public sealed class GameOptionsLaunchParameterViewModel : INotifyPropertyChanged
{
    readonly GameOptionsLaunchParameterDefinition _definition;
    bool _isEnabled;

    public GameOptionsLaunchParameterViewModel(GameOptionsLaunchParameterDefinition definition)
    {
        _definition = definition;
        Refresh();
    }

    public string Description => Locale.Get(_definition.DescriptionCode);

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            GameOptionsWorkflow.SetLaunchParameter(_definition.Parameter, value);
            PropertyChanged?.Invoke(this, new(nameof(IsEnabled)));
        }
    }

    public string Title => Locale.Get(_definition.TitleCode);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh()
    {
        _isEnabled = GameOptionsWorkflow.HasLaunchParameter(_definition.Parameter);
        PropertyChanged?.Invoke(this, new(nameof(IsEnabled)));
        PropertyChanged?.Invoke(this, new(nameof(Title)));
        PropertyChanged?.Invoke(this, new(nameof(Description)));
    }
}

public sealed class GameOptionsSectionScreenViewModel : LauncherSectionScreenViewModel
{
    string _customLaunchParameters = string.Empty;
    bool _highProcessPriority;
    bool _isBusy;
    string _statusColor = "#D49B38";
    string _statusMessage = string.Empty;

    public GameOptionsSectionScreenViewModel()
      : base(LauncherSection.GameOptions)
    {
        LaunchParameters = [.. GameOptionsWorkflow.StandardParameters.Select(definition => new GameOptionsLaunchParameterViewModel(definition))];
        Activate();
    }

    public string CurrentGamePath => ActiveGameManager.Current.RootPath;

    public bool DirectXStatusVisible => Game.CanRunAsAdmin || Game.CanUseHighProcessPriority;

    public string CustomLaunchParameters
    {
        get => _customLaunchParameters;
        set => SetProperty(ref _customLaunchParameters, value);
    }

    public bool DirectXInstalled => Game.DirectXInstalled;

    public bool DirectXInstalledVisible => DirectXStatusVisible && DirectXInstalled;

    public bool DirectXMissing => !DirectXInstalled;

    public bool DirectXMissingVisible => DirectXStatusVisible && DirectXMissing;

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HighProcessPriority
    {
        get => HighProcessPriorityVisible && _highProcessPriority;
        set
        {
            if (_highProcessPriority == value)
                return;

            _highProcessPriority = value;
            Game.HighProcessPriority = value;
            OnPropertyChanged();
        }
    }

    public string HighProcessPriorityDescription => Locale.Get("playTab.highProcessPriorityDesc");

    public bool HighProcessPriorityVisible => Game.CanUseHighProcessPriority;

    public bool FixBloomAndSkinsVisible => ActiveGameManager.Current.Id != GameCatalog.AsaGameId;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;

            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanRunMaintenance));
        }
    }

    public bool CanRunMaintenance => !IsBusy;

    public bool PredefinedLaunchParametersVisible => GameOptionsWorkflow.HasPredefinedLaunchParameters;

    public GameOptionsLaunchParameterViewModel[] LaunchParameters { get; }

    public string MaintenanceNote => OperatingSystem.IsLinux()
      ? Locale.Get("gameOptionsTab.maintenanceNoteLinux")
      : Locale.Get("gameOptionsTab.maintenanceNoteWindows");

    public string StatusColor
    {
        get => _statusColor;
        private set => SetProperty(ref _statusColor, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
                return;

            _statusMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatus));
        }
    }

    public override void Activate()
    {
        GameOptionsWorkflow.EnforcePredefinedLaunchParameterPolicy();
        CustomLaunchParameters = GameOptionsWorkflow.GetCustomLaunchParametersText();
        _highProcessPriority = Game.HighProcessPriority;
        OnPropertyChanged(nameof(HighProcessPriority));
        OnPropertyChanged(nameof(CurrentGamePath));
        OnPropertyChanged(nameof(DirectXInstalled));
        OnPropertyChanged(nameof(DirectXInstalledVisible));
        OnPropertyChanged(nameof(DirectXMissing));
        OnPropertyChanged(nameof(DirectXMissingVisible));
        OnPropertyChanged(nameof(DirectXStatusVisible));
        OnPropertyChanged(nameof(PredefinedLaunchParametersVisible));
        foreach (var parameter in LaunchParameters)
            parameter.Refresh();
        OnPropertyChanged(nameof(HighProcessPriorityVisible));
        OnPropertyChanged(nameof(FixBloomAndSkinsVisible));
    }

    public void ApplyCustomLaunchParameters()
    {
        GameOptionsWorkflow.SetCustomLaunchParameters(CustomLaunchParameters);
        SetStatus("Launch parameters updated.", 1);
        Activate();
    }

    public void FixBloom()
    {
        var result = GameOptionsWorkflow.FixBloom();
        SetStatus(result.Message, result.Severity);
    }

    public async Task UnlockSkinsAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        SetStatus(Locale.Get("downloads.downloading"), 0);
        try
        {
            var result = await GameOptionsWorkflow.UnlockSkinsAsync();
            SetStatus(result.Message, result.Severity);
        }
        finally
        {
            IsBusy = false;
        }
    }

    void SetStatus(string message, int severity)
    {
        StatusMessage = message;
        StatusColor = severity switch
        {
            1 => "#0AA63E",
            2 => "#D4573B",
            _ => "#D49B38"
        };
    }
}