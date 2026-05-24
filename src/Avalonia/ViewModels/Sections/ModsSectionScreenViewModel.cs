using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TEKLauncher.Avalonia.ViewModels;

public sealed class ModRowViewModel : INotifyPropertyChanged
{
    readonly Mod _mod;

    internal ModRowViewModel(Mod mod)
    {
        _mod = mod;
    }

    public bool CanDelete => _mod.CurrentStatus is Mod.Status.Installed or Mod.Status.UpdateAvailable;

    public bool CanUpdate => _mod.CurrentStatus == Mod.Status.UpdateAvailable;

    public bool CanValidate => _mod.CurrentStatus is Mod.Status.Installed or Mod.Status.UpdateAvailable;

    public string DetailText => ModWorkflow.GetDetailText(_mod);

    public string DisplayName => ModWorkflow.Describe(_mod);

    public string IdText => _mod.Id.ToString();

    internal Mod Mod => _mod;

    public string StatusColor => ModWorkflow.GetStatusColor(_mod.CurrentStatus);

    public string StatusText => ModWorkflow.GetStatusText(_mod.CurrentStatus);

    public string WorkshopUrl => $"steam://openurl/https://steamcommunity.com/sharedfiles/filedetails/?id={_mod.Id}";

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task<ModActionResult> DeleteAsync()
    {
        var result = await ModWorkflow.DeleteAsync(_mod);
        Refresh();
        return result;
    }

    internal Mod.ModDetails GetDetailsForUpdater() => _mod.Details.Status == 0 ? new() { Id = _mod.Id, Name = _mod.Name } : _mod.Details;

    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new(nameof(CanDelete)));
        PropertyChanged?.Invoke(this, new(nameof(CanUpdate)));
        PropertyChanged?.Invoke(this, new(nameof(CanValidate)));
        PropertyChanged?.Invoke(this, new(nameof(DetailText)));
        PropertyChanged?.Invoke(this, new(nameof(DisplayName)));
        PropertyChanged?.Invoke(this, new(nameof(IdText)));
        PropertyChanged?.Invoke(this, new(nameof(StatusColor)));
        PropertyChanged?.Invoke(this, new(nameof(StatusText)));
        PropertyChanged?.Invoke(this, new(nameof(WorkshopUrl)));
    }
}

public sealed class WorkshopModRowViewModel
{
    readonly Mod.ModDetails _details;

    internal WorkshopModRowViewModel(Mod.ModDetails details)
    {
        _details = details;
    }

    public string DetailText => _details.Id.ToString();

    public string DisplayName => _details.Name;

    internal Mod.ModDetails Details => _details;

    public bool IsInstalled
    {
        get
        {
            lock (Mod.List)
                return Mod.List.Any(mod => mod.Id == _details.Id);
        }
    }

    public string WorkshopUrl => $"steam://openurl/https://steamcommunity.com/sharedfiles/filedetails/?id={_details.Id}";
}

public sealed class ModsSectionScreenViewModel : LauncherSectionScreenViewModel
{
    bool _canInstallSelected;
    bool _canOpenSelectedWorkshop;
    ulong _candidateId;
    string _candidateSubtitle = string.Empty;
    string _candidateTitle = string.Empty;
    bool _hasStatus;
    bool _hasWorkshopStatus;
    bool _isBusy;
    string _installIdInput = string.Empty;
    string _statusColor = "#D49B38";
    string _statusMessage = string.Empty;
    string _workshopPageText = string.Empty;
    string _workshopQuery = string.Empty;
    string _workshopStatusText = string.Empty;
    Mod.ModDetails _selectedModDetails;
    uint _currentWorkshopPage;
    uint _totalWorkshopPages;
    bool _workshopInitialized;

    public ModsSectionScreenViewModel()
      : base(LauncherSection.Mods)
    {
        Rows = [];
        WorkshopRows = [];
        Activate();
    }

    public bool CanInstallSelected
    {
        get => _canInstallSelected;
        private set => SetProperty(ref _canInstallSelected, value);
    }

    public bool CanOpenSelectedWorkshop
    {
        get => _canOpenSelectedWorkshop;
        private set => SetProperty(ref _canOpenSelectedWorkshop, value);
    }

    public bool HasCandidate => !string.IsNullOrWhiteSpace(CandidateTitle);

    public bool HasNoMods => Rows.Count == 0;

    public bool HasStatus
    {
        get => _hasStatus;
        private set => SetProperty(ref _hasStatus, value);
    }

    public bool HasWorkshopResults => WorkshopRows.Count > 0;

    public bool HasWorkshopStatus
    {
        get => _hasWorkshopStatus;
        private set => SetProperty(ref _hasWorkshopStatus, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string InstallIdInput
    {
        get => _installIdInput;
        set => SetProperty(ref _installIdInput, value);
    }

    public string CandidateSubtitle
    {
        get => _candidateSubtitle;
        private set
        {
            if (SetCandidateField(ref _candidateSubtitle, value))
                OnPropertyChanged(nameof(HasCandidate));
        }
    }

    public string CandidateTitle
    {
        get => _candidateTitle;
        private set
        {
            if (SetCandidateField(ref _candidateTitle, value))
                OnPropertyChanged(nameof(HasCandidate));
        }
    }

    public bool CanLoadNextWorkshopPage => _currentWorkshopPage > 0 && _currentWorkshopPage < _totalWorkshopPages;

    public bool CanLoadPreviousWorkshopPage => _currentWorkshopPage > 1;

    public ObservableCollection<ModRowViewModel> Rows { get; }

    public ObservableCollection<WorkshopModRowViewModel> WorkshopRows { get; }

    public string StatusColor
    {
        get => _statusColor;
        private set => SetProperty(ref _statusColor, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string WorkshopPageText
    {
        get => _workshopPageText;
        private set => SetProperty(ref _workshopPageText, value);
    }

    public string WorkshopQuery
    {
        get => _workshopQuery;
        set => SetProperty(ref _workshopQuery, value);
    }

    public string WorkshopStatusText
    {
        get => _workshopStatusText;
        private set => SetProperty(ref _workshopStatusText, value);
    }

    public override void Activate()
    {
        RefreshRows();
        RefreshCandidateState();
        if (!_workshopInitialized)
        {
            _workshopInitialized = true;
            _ = LoadWorkshopPageAsync(1);
        }
    }

    public async Task DeleteAsync(ModRowViewModel row)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            SetStatus(await row.DeleteAsync());
            RefreshRows();
            RefreshCandidateState();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LookupInstallCandidateAsync()
    {
        if (IsBusy)
            return;

        if (!ulong.TryParse(InstallIdInput.Trim(), out ulong id))
        {
            ClearCandidate();
            SetStatus(new(Locale.Get("modsTab.modWithThisIdDoesntExist"), 2));
            return;
        }

        IsBusy = true;
        try
        {
            var response = await Task.Run(() => Steam.CM.Client.GetModDetails(id));
            var details = response.Length == 0 ? default : response[0];
            ApplyCandidate(details, id);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadNextWorkshopPageAsync()
    {
        if (CanLoadNextWorkshopPage)
            await LoadWorkshopPageAsync(_currentWorkshopPage + 1);
    }

    public async Task LoadPreviousWorkshopPageAsync()
    {
        if (CanLoadPreviousWorkshopPage)
            await LoadWorkshopPageAsync(_currentWorkshopPage - 1);
    }

    public async Task ReloadWorkshopPageAsync()
    {
        uint page = _currentWorkshopPage == 0 ? 1 : _currentWorkshopPage;
        await LoadWorkshopPageAsync(page);
    }

    public async Task SearchWorkshopAsync()
    {
        await LoadWorkshopPageAsync(1);
    }

    public void SelectWorkshopCandidate(WorkshopModRowViewModel row)
    {
        InstallIdInput = row.Details.Id.ToString();
        ApplyCandidate(row.Details, row.Details.Id);
    }

    public void RefreshAfterUpdaterClosed()
    {
        RefreshRows();
        RefreshCandidateState();
        RefreshWorkshopRows();
    }

    internal Mod.ModDetails SelectedModDetails => _selectedModDetails;

    bool IsAlreadyInstalled(ulong id)
    {
        lock (Mod.List)
            return Mod.List.Any(mod => mod.Id == id);
    }

    void ApplyCandidate(Mod.ModDetails details, ulong requestedId)
    {
        _candidateId = requestedId;
        _selectedModDetails = default;

        if (details.Status != 1)
        {
            ClearCandidate();
            SetStatus(new(Locale.Get(details.Status == 0 ? "modsTab.failedToLoadPreview" : "modsTab.modWithThisIdDoesntExist"), 2));
            CanOpenSelectedWorkshop = requestedId > 0;
            return;
        }

        if (details.AppId != ActiveGameManager.Current.SteamAppId)
        {
            ClearCandidate();
            SetStatus(new(Locale.Get("modsTab.notAnARKMod"), 2));
            CanOpenSelectedWorkshop = true;
            return;
        }

        _selectedModDetails = details;
        CandidateTitle = details.Name;
        CandidateSubtitle = $"{details.Id}";
        CanOpenSelectedWorkshop = true;
        CanInstallSelected = !IsAlreadyInstalled(details.Id);
        SetStatus(new(CanInstallSelected ? string.Format(Locale.Get("modsTab.readyToInstall"), details.Name) : string.Format(Locale.Get("modsTab.alreadyInstalled"), details.Name), CanInstallSelected ? 1 : 0));
    }

    async Task LoadWorkshopPageAsync(uint page)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var result = await Task.Run(() => QueryWorkshop(page, WorkshopQuery));
            ApplyWorkshopPage(page, result.Total, result.Details);
        }
        finally
        {
            IsBusy = false;
        }
    }

    static (uint Total, Mod.ModDetails[] Details) QueryWorkshop(uint page, string? search)
    {
        var details = Steam.CM.Client.QueryMods(page, search, out uint total);
        return (total, details);
    }

    void ApplyWorkshopPage(uint page, uint totalItems, Mod.ModDetails[] details)
    {
        WorkshopRows.Clear();

        if (details.Length == 0)
        {
            _currentWorkshopPage = 0;
            _totalWorkshopPages = 0;
            WorkshopPageText = string.Empty;
            WorkshopStatusText = Locale.Get("modsTab.failedToLoadWorkshopModList");
            HasWorkshopStatus = true;
            OnPropertyChanged(nameof(HasWorkshopResults));
            OnPropertyChanged(nameof(CanLoadNextWorkshopPage));
            OnPropertyChanged(nameof(CanLoadPreviousWorkshopPage));
            return;
        }

        _currentWorkshopPage = page;
        _totalWorkshopPages = totalItems / 20 + (totalItems % 20 == 0 ? 0u : 1u);
        WorkshopPageText = string.Format(Locale.Get("modsTab.workshopPage"), _currentWorkshopPage, _totalWorkshopPages);
        WorkshopStatusText = string.Empty;
        HasWorkshopStatus = false;

        foreach (var detail in details)
            WorkshopRows.Add(new WorkshopModRowViewModel(detail));

        RefreshWorkshopRows();
        OnPropertyChanged(nameof(HasWorkshopResults));
        OnPropertyChanged(nameof(CanLoadNextWorkshopPage));
        OnPropertyChanged(nameof(CanLoadPreviousWorkshopPage));
    }

    void ClearCandidate()
    {
        _selectedModDetails = default;
        CandidateTitle = string.Empty;
        CandidateSubtitle = string.Empty;
        CanInstallSelected = false;
    }

    bool SetCandidateField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    void RefreshCandidateState()
    {
        if (_candidateId == 0)
        {
            CanOpenSelectedWorkshop = false;
            CanInstallSelected = false;
            return;
        }

        CanOpenSelectedWorkshop = true;
        if (_selectedModDetails.Status == 1)
            CanInstallSelected = !IsAlreadyInstalled(_selectedModDetails.Id);
    }

    void RefreshWorkshopRows()
    {
        foreach (var row in WorkshopRows)
            _ = row.IsInstalled;

        OnPropertyChanged(nameof(HasWorkshopResults));
    }

    void RefreshRows()
    {
        Rows.Clear();
        lock (Mod.List)
            foreach (var mod in Mod.List.OrderBy(mod => ModWorkflow.Describe(mod), StringComparer.OrdinalIgnoreCase))
                Rows.Add(new ModRowViewModel(mod));

        OnPropertyChanged(nameof(HasNoMods));
    }

    void SetStatus(ModActionResult result)
    {
        StatusMessage = result.Message;
        HasStatus = !string.IsNullOrWhiteSpace(result.Message);
        StatusColor = result.Severity switch
        {
            1 => "#0AA63E",
            2 => "#D4573B",
            _ => "#D49B38"
        };
    }
}