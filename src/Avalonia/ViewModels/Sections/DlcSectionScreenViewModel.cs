using System.ComponentModel;
using System.Linq;

namespace TEKLauncher.Avalonia.ViewModels;

public sealed class DlcRowViewModel : INotifyPropertyChanged
{
    readonly DLC _dlc;

    internal DlcRowViewModel(DLC dlc)
    {
        _dlc = dlc;
    }

    public bool CanDelete => _dlc.CurrentStatus is DLC.Status.Installed or DLC.Status.UpdateAvailable;

    public bool CanInstall => (int)_dlc.CurrentStatus % 3 == 0;

    public bool CanValidate => _dlc.CurrentStatus is DLC.Status.Installed or DLC.Status.UpdateAvailable;

    internal DLC Dlc => _dlc;

    public string Name => _dlc.Name;

    public string StatusColor => DlcWorkflow.GetStatusColor(_dlc.CurrentStatus);

    public string StatusText => DlcWorkflow.GetStatusText(_dlc.CurrentStatus);

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task<DlcActionResult> DeleteAsync()
    {
        var result = await DlcWorkflow.DeleteAsync(_dlc);
        Refresh();
        return result;
    }

    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new(nameof(CanDelete)));
        PropertyChanged?.Invoke(this, new(nameof(CanInstall)));
        PropertyChanged?.Invoke(this, new(nameof(CanValidate)));
        PropertyChanged?.Invoke(this, new(nameof(Name)));
        PropertyChanged?.Invoke(this, new(nameof(StatusColor)));
        PropertyChanged?.Invoke(this, new(nameof(StatusText)));
    }
}

public sealed class DlcSectionScreenViewModel : LauncherSectionScreenViewModel
{
    bool _isBusy;
    string _statusColor = "#D49B38";
    string _statusMessage = string.Empty;

    public DlcSectionScreenViewModel()
      : base(LauncherSection.DLC)
    {
        Rows = [.. DLC.List.Select(dlc => new DlcRowViewModel(dlc))];
        Activate();
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;

            _isBusy = value;
            OnPropertyChanged();
        }
    }

    public string SectionNote => Locale.Get("dlcTab.sectionNote");

    public DlcRowViewModel[] Rows { get; }

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
        foreach (var row in Rows)
            row.Refresh();
    }

    public async Task DeleteAsync(DlcRowViewModel row)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            SetStatus(await row.DeleteAsync());
        }
        finally
        {
            IsBusy = false;
        }
    }

    void SetStatus(DlcActionResult result)
    {
        StatusMessage = result.Message;
        StatusColor = result.Severity switch
        {
            1 => "#0AA63E",
            2 => "#D4573B",
            _ => "#D49B38"
        };
    }
}