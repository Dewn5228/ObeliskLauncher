using Avalonia.Media;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ObeliskLauncher.Avalonia.ViewModels;

public sealed class UpdaterStageViewModel : INotifyPropertyChanged
{
    IBrush _accentBrush = Brushes.Goldenrod;

    public UpdaterStageViewModel(string label)
    {
        Label = label;
    }

    public IBrush AccentBrush
    {
        get => _accentBrush;
        private set
        {
            if (ReferenceEquals(_accentBrush, value))
                return;

            _accentBrush = value;
            PropertyChanged?.Invoke(this, new(nameof(AccentBrush)));
        }
    }

    public string Label { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Finish(bool success) => AccentBrush = success ? Brushes.LimeGreen : Brushes.IndianRed;
}

enum UpdaterProgressMode
{
    None,
    Percentage,
    Binary,
    Numbers
}

internal abstract class SteamTaskUpdaterWindowViewModelBase : INotifyPropertyChanged, IDisposable
{
    static readonly Lock s_instancesLock = new();
    static readonly List<SteamTaskUpdaterWindowViewModelBase> s_instances = [];
    static int s_activeTaskCount;
    unsafe TEKSteamClient.AmItemDesc* _desc;
    bool _disposed;
    bool _isActionEnabled = true;
    bool _isProgressIndeterminate = true;
    UpdaterStageViewModel? _currentStage;
    UpdaterProgressMode _progressMode;
    double _progressMaximum = 1;
    string _speedText = string.Empty;
    DateTime _speedSampleAt;
    long _speedSampleBytes;
    double _speedBps;
    string _progressText = Locale.Get("customUI.waitingForJobData");
    double _progressValue;
    string _actionLabel = Locale.Get("common.pause");
    string? _spaceMessage;
    IBrush _statusBrush = Brushes.Goldenrod;
    string _statusText = string.Empty;
    string _errorTechnicalDetails = string.Empty;
    string _etaText = string.Empty;
    Thread? _taskThread;
    string _title;

    protected SteamTaskUpdaterWindowViewModelBase(string title, bool validate)
    {
        _title = title;
        ForceVerify = validate;
        lock (s_instancesLock)
            s_instances.Add(this);
    }

    public string ActionLabel
    {
        get => _actionLabel;
        protected set => SetProperty(ref _actionLabel, value);
    }

    public bool IsActionEnabled
    {
        get => _isActionEnabled;
        protected set => SetProperty(ref _isActionEnabled, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        protected set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public bool IsSteamTaskActive => _taskThread?.IsAlive == true;

    public static bool IsAnySteamTaskActive => Volatile.Read(ref s_activeTaskCount) > 0;

    public double ProgressMaximum
    {
        get => _progressMaximum;
        protected set => SetProperty(ref _progressMaximum, value);
    }

    public string ProgressText
    {
        get => _progressText;
        protected set
        {
            if (_progressText == value)
                return;

            _progressText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasProgressText));
        }
    }

    public bool HasProgressText => !string.IsNullOrWhiteSpace(ProgressText);

    public TextAlignment NumericTextAlignment => Locale.CurrentLanguage == "ar" ? TextAlignment.Right : TextAlignment.Left;

    public string ErrorTechnicalDetails
    {
        get => _errorTechnicalDetails;
        private set
        {
            if (_errorTechnicalDetails == value) return;
            _errorTechnicalDetails = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasErrorTechnicalDetails));
        }
    }

    public bool HasErrorTechnicalDetails => !string.IsNullOrWhiteSpace(_errorTechnicalDetails);

    public string SpeedText
    {
        get => _speedText;
        protected set
        {
            if (_speedText == value)
                return;

            _speedText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSpeedText));
        }
    }

    public bool HasSpeedText => !string.IsNullOrWhiteSpace(SpeedText);

    public string EtaText
    {
        get => _etaText;
        private set
        {
            if (_etaText == value)
                return;
            _etaText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasEtaText));
        }
    }

    public bool HasEtaText => !string.IsNullOrWhiteSpace(EtaText);

    public double ProgressValue
    {
        get => _progressValue;
        protected set => SetProperty(ref _progressValue, value);
    }

    public ObservableCollection<UpdaterStageViewModel> Stages { get; } = [];

    public IBrush StatusBrush
    {
        get => _statusBrush;
        protected set => SetProperty(ref _statusBrush, value);
    }

    public string StatusText
    {
        get => _statusText;
        protected set => SetProperty(ref _statusText, value);
    }

    public string Title => _title;

    protected bool ForceVerify { get; set; }

    protected int NewStatus { get; set; } = -1;

    protected unsafe TEKSteamClient.AmItemDesc* CurrentDesc => _desc;

    protected string? SpaceMessage
    {
        get => _spaceMessage;
        set => _spaceMessage = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        lock (s_instancesLock)
            s_instances.Remove(this);
    }

    public static void PauseAllActiveTasks()
    {
        SteamTaskUpdaterWindowViewModelBase[] instances;
        lock (s_instancesLock)
            instances = [.. s_instances];

        foreach (var instance in instances)
            instance.PauseForShutdown();
    }

    public unsafe void PauseOrRetry()
    {
        if (ActionLabel == Locale.Get("common.pause"))
        {
            if (_desc is null)
                return;

            IsActionEnabled = false;
            LauncherServices.TekSteamClient.PauseJob(ref Unsafe.AsRef<TEKSteamClient.AmItemDesc>(_desc));
            return;
        }

        if (Game.IsRunning)
        {
            SetStatus(Locale.Get("errors.updateFailGameRunning"), Brushes.IndianRed);
            return;
        }

        StartTask();
    }

    public void Start()
    {
        if (Game.IsRunning)
        {
            SetRetryState();
            SetStatus(Locale.Get("errors.updateFailGameRunning"), Brushes.IndianRed);
            return;
        }

        StartTask();
    }

    public bool TryClose()
    {
        if (IsSteamTaskActive)
        {
            Messages.Show("common.warning", Locale.Get("status.pauseRequired"));
            return false;
        }

        return TryCloseCore();
    }

    protected void CompleteSuccessfully(TEKSteamClient.Error result)
    {
        ProgressMaximum = 1;
        ProgressValue = 1;
        IsProgressIndeterminate = false;
        ProgressText = "100%";
        ClearSpeed();
        HandleSuccessfulResult(result);
        FinishCurrentStage(true);
        IsActionEnabled = false;
    }

    protected void FinishCurrentStage(bool success) => _currentStage?.Finish(success);

    protected virtual void OnTaskStarting()
    {
        SetPauseState();
        ErrorTechnicalDetails = string.Empty;
        SetStatus(Locale.Get("modsTab.fetchingData"), Brushes.Goldenrod);
    }

    protected void PostFailureResult(TEKSteamClient.Error result)
    {
        PostToUi(() =>
        {
            ProgressMaximum = 1;
            ProgressValue = 1;
            IsProgressIndeterminate = false;
            string originalDiagnostic = result.Message;
            string userMessage = Locale.CurrentLanguage == "ar"
                ? (result.Type == 4 && result.Primary == 77 && result.Auxiliary == 28
                    ? Locale.Get("customUI.downloadTimeout")
                    : result.Primary == 77
                        ? Locale.Get("customUI.steamChunkFailed")
                        : string.Format(Locale.Get("customUI.downloadErrorGeneric"), result.Type, result.Primary, result.Auxiliary))
                : originalDiagnostic;
            ErrorTechnicalDetails = string.Equals(userMessage, originalDiagnostic, StringComparison.Ordinal)
                ? string.Empty
                : originalDiagnostic;
            SetStatus(userMessage, Brushes.IndianRed);
            ClearSpeed();
            if (result.Uri != 0)
                Marshal.FreeHGlobal(result.Uri);
            FinishCurrentStage(false);
            SetRetryState();
        });
    }

    protected void PostPausedResult()
    {
        PostToUi(() =>
        {
            if (_spaceMessage is null)
            {
                ProgressMaximum = 1;
                ProgressValue = 1;
                IsProgressIndeterminate = false;
                ClearSpeed();
                SetStatus(Locale.Get("status.operationPaused"), Brushes.LimeGreen);
            }
            else
            {
                ResetProgress(true);
                SetStatus(_spaceMessage, Brushes.IndianRed);
                _spaceMessage = null;
            }

            FinishCurrentStage(false);
            SetRetryState();
        });
    }

    protected void ResetProgress(bool indeterminate)
    {
        ProgressValue = 0;
        ProgressMaximum = 1;
        IsProgressIndeterminate = indeterminate;
        ProgressText = indeterminate ? GetIndeterminateProgressText() : GetDeterminateProgressText(0, 0);
        ClearSpeed();
    }

    void ClearSpeed()
    {
        _speedSampleAt = default;
        _speedSampleBytes = 0;
        _speedBps = 0;
        SpeedText = string.Empty;
        EtaText = string.Empty;
    }

    protected void ResetTaskVisuals()
    {
        _currentStage = null;
        Stages.Clear();
        ResetProgress(true);
    }

    protected unsafe TEKSteamClient.Error RunJob(in TEKSteamClient.ItemId itemId, ulong manifestId) => LauncherServices.TekSteamClient.RunJob(in itemId, manifestId, ForceVerify, UpdHandler, out _desc);

    protected void SetPauseState()
    {
        ActionLabel = Locale.Get("common.pause");
        IsActionEnabled = true;
    }

    protected void SetRetryState()
    {
        ActionLabel = Locale.Get("common.retry");
        IsActionEnabled = true;
    }

    protected void SetStatus(string statusText, IBrush brush)
    {
        StatusText = statusText;
        StatusBrush = brush;
    }

    protected void SetTitle(string title)
    {
        if (_title == title)
            return;

        _title = title;
        OnPropertyChanged(nameof(Title));
    }

    protected virtual unsafe void RunTaskCore()
    {
        var itemId = CreateItemId();
        TEKSteamClient.Error result = RunJob(in itemId, GetManifestId());
        if (result.Success || result.Primary == 85)
        {
            PostToUi(() => CompleteSuccessfully(result));
            return;
        }

        if (result.Primary == 68)
        {
            PostPausedResult();
            return;
        }

        if (_desc->Job.Stage == TEKSteamClient.AmJobStage.Pathcing && ((result.Type == 3 && result.Primary == 6 && (result.Auxiliary == 2 || result.Auxiliary == 38)) || (result.Type == 1 && result.Primary == 78 && result.Auxiliary == 48)))
        {
            if (result.Uri != 0)
                Marshal.FreeHGlobal(result.Uri);

            result = LauncherServices.TekSteamClient.CancelJob(ref Unsafe.AsRef<TEKSteamClient.AmItemDesc>(_desc));
            if (result.Success)
            {
                PostToUi(ResetTaskVisuals);
                ForceVerify = true;
                RunTaskCore();
                return;
            }
        }

        PostFailureResult(result);
    }

    unsafe void RunTaskProcedure()
    {
        try
        {
            RunTaskCore();
        }
        finally
        {
            Interlocked.Decrement(ref s_activeTaskCount);
        }
    }

    void AddStage(string code)
    {
        var stage = new UpdaterStageViewModel(Locale.Get(code));
        _currentStage?.Finish(true);
        _currentStage = stage;
        Stages.Add(stage);
    }

    unsafe void UpdHandler(ref TEKSteamClient.AmItemDesc desc, TEKSteamClient.AmUpdType updateMask)
    {
        if (updateMask.HasFlag(TEKSteamClient.AmUpdType.DeltaCreated))
        {
            long diskFreeSpace = LauncherPlatform.Current.GetDiskFreeSpace(Path.Combine(ActiveGameManager.Current.RootPath, "tek-sc-data")) + 20971520;
            long requiredSpace = LauncherServices.TekSteamClient.EstimateDeltaDiskSpace(desc.Job.Delta);
            if (diskFreeSpace < requiredSpace)
            {
                _spaceMessage = $"{Locale.Get("modsTab.notEnoughSpace")} "
                    + $"{Locale.Get("gameOptionsTab.requiredDiskSpace")}: {Locale.BytesToString(requiredSpace)}. "
                    + $"{Locale.Get("gameOptionsTab.freeDiskSpace")}: {Locale.BytesToString(diskFreeSpace)}.";
                LauncherServices.TekSteamClient.PauseJob(ref desc);
            }
        }

        if (updateMask.HasFlag(TEKSteamClient.AmUpdType.Stage))
        {
            var stage = desc.Job.Stage;
            PostToUi(() =>
            {
                switch (stage)
                {
                    case TEKSteamClient.AmJobStage.FetchingData:
                        _progressMode = UpdaterProgressMode.None;
                        ResetProgress(true);
                        AddStage("modsTab.fetchingData");
                        break;
                    case TEKSteamClient.AmJobStage.DwManifest:
                        _progressMode = UpdaterProgressMode.Binary;
                        ResetProgress(false);
                        AddStage("modsTab.downloadingManifest");
                        break;
                    case TEKSteamClient.AmJobStage.DwPatch:
                        _progressMode = UpdaterProgressMode.Binary;
                        ResetProgress(false);
                        AddStage("modsTab.downloadingPatch");
                        break;
                    case TEKSteamClient.AmJobStage.Verifying:
                        _progressMode = UpdaterProgressMode.Percentage;
                        ResetProgress(false);
                        AddStage("modsTab.validating");
                        break;
                    case TEKSteamClient.AmJobStage.Downloading:
                        _progressMode = UpdaterProgressMode.Binary;
                        ResetProgress(false);
                        AddStage("modsTab.downloadingFiles");
                        break;
                    case TEKSteamClient.AmJobStage.Pathcing:
                        _progressMode = UpdaterProgressMode.Percentage;
                        ResetProgress(false);
                        AddStage("modsTab.patching");
                        break;
                    case TEKSteamClient.AmJobStage.Installing:
                        _progressMode = UpdaterProgressMode.Numbers;
                        ResetProgress(false);
                        AddStage("modsTab.installingFiles");
                        break;
                    case TEKSteamClient.AmJobStage.Deleting:
                        _progressMode = UpdaterProgressMode.Numbers;
                        ResetProgress(false);
                        AddStage("common.deleting");
                        break;
                    case TEKSteamClient.AmJobStage.Finalizing:
                        _progressMode = UpdaterProgressMode.None;
                        ResetProgress(true);
                        AddStage("modsTab.finalizing");
                        break;
                }
                SetStatus(_currentStage?.Label ?? Locale.Get("common.na"), Brushes.Goldenrod);
            });
        }

        if (updateMask.HasFlag(TEKSteamClient.AmUpdType.Progress))
        {
            long current = desc.Job.Stage switch
            {
                TEKSteamClient.AmJobStage.Verifying or TEKSteamClient.AmJobStage.Downloading or TEKSteamClient.AmJobStage.Installing => Interlocked.Read(ref desc.Job.ProgressCurrent),
                _ => desc.Job.ProgressCurrent
            };

            long total = desc.Job.ProgressTotal;
            var mode = _progressMode;
            PostToUi(() =>
            {
                IsProgressIndeterminate = total <= 0;
                ProgressMaximum = total <= 0 ? 1 : total;
                ProgressValue = total <= 0 ? 0 : Math.Clamp(current, 0, total);
                ProgressText = total <= 0 ? GetIndeterminateProgressText() : GetDeterminateProgressText(current, total);
                UpdateSpeed(mode, current, total);
            });
        }
    }

    // Byte amounts in download UI stay in Latin order with standard MB/GB
    // abbreviations. The underlying byte counts and game data are untouched.
    static string DownloadBytes(long bytes)
    {
        string number = Locale.BytesToString(bytes, out _);
        string unit = bytes >= 1073741824 ? "GB"
                    : bytes >= 1048576 ? "MB" : "KB";
        return number + "\u00A0" + unit;
    }

    string GetDeterminateProgressText(long current, long total) => _progressMode switch
    {
        UpdaterProgressMode.Binary => Locale.CurrentLanguage == "ar"
            ? $"{DownloadBytes(current)} / {DownloadBytes(total)}"
            : $"{Locale.BytesToString(current)} / {Locale.BytesToString(total)}",
        UpdaterProgressMode.Numbers => Locale.CurrentLanguage == "ar" ? FormattableString.Invariant($"{current:N0} / {total:N0}") : $"{current:N0} / {total:N0}",
        _ => Locale.CurrentLanguage == "ar" ? FormattableString.Invariant($"{Math.Clamp(total == 0 ? 0 : current * 100.0 / total, 0, 100):0.#}%") : $"{Math.Clamp(total == 0 ? 0 : current * 100.0 / total, 0, 100):0.#}%"
    };

    string GetIndeterminateProgressText() => _progressMode switch
    {
        UpdaterProgressMode.Binary => Locale.Get("customUI.preparingDownloadSize"),
        UpdaterProgressMode.Numbers => Locale.Get("customUI.preparingFileList"),
        UpdaterProgressMode.Percentage => Locale.Get("customUI.preparingProgressData"),
        _ => Locale.Get("customUI.waitingForJobData")
    };

    void UpdateSpeed(UpdaterProgressMode mode, long current, long total)
    {
        if (mode != UpdaterProgressMode.Binary || total <= 0 || current < 0)
        {
            if (!string.IsNullOrEmpty(SpeedText))
                SpeedText = string.Empty;
            EtaText = string.Empty;
            return;
        }

        var now = DateTime.UtcNow;
        if (_speedSampleAt == default)
        {
            _speedSampleAt = now;
            _speedSampleBytes = current;
            return;
        }

        if (current < _speedSampleBytes)
        {
            _speedSampleAt = now;
            _speedSampleBytes = current;
            _speedBps = 0;
            SpeedText = string.Empty;
            EtaText = string.Empty;
            return;
        }

        double elapsed = (now - _speedSampleAt).TotalSeconds;
        if (elapsed < 0.5)
            return;

        long delta = current - _speedSampleBytes;
        double instant = delta / elapsed;
        _speedBps = _speedBps <= 0 ? instant : _speedBps * 0.7 + instant * 0.3;
        _speedSampleAt = now;
        _speedSampleBytes = current;

        if (_speedBps < 1 || current >= total)
        {
            SpeedText = _speedBps >= 1
                ? Locale.CurrentLanguage == "ar"
                    ? $"{DownloadBytes((long)_speedBps)}/s"
                    : $"{Locale.BytesToString((long)_speedBps)}/{Locale.Get("customUI.perSecond")}"
                : string.Empty;
            EtaText = string.Empty;
            return;
        }

        long remaining = total - current;
        long etaSeconds = (long)Math.Ceiling(remaining / _speedBps);
        if (Locale.CurrentLanguage == "ar")
        {
            SpeedText = $"{DownloadBytes((long)_speedBps)}/s";
            EtaText = $"{Locale.Get("customUI.remainingTime")}: {Locale.SecondsToString(etaSeconds)}";
        }
        else
        {
            EtaText = string.Empty;
            SpeedText = $"{Locale.BytesToString((long)_speedBps)}/{Locale.Get("customUI.perSecond")} \u2022 {Locale.Get("customUI.remainingTime")} {Locale.SecondsToString(etaSeconds)}";
        }
    }

    void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }

    void StartTask()
    {
        _currentStage = null;
        Stages.Clear();
        ResetProgress(true);
        OnTaskStarting();
        Interlocked.Increment(ref s_activeTaskCount);
        _taskThread = new Thread(RunTaskProcedure)
        {
            IsBackground = true,
            Name = ThreadName
        };
        _taskThread.Start();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new(propertyName));

    protected abstract TEKSteamClient.ItemId CreateItemId();

    protected abstract ulong GetManifestId();

    protected abstract void HandleSuccessfulResult(TEKSteamClient.Error result);

    protected abstract string ThreadName { get; }

    protected abstract bool TryCloseCore();

    unsafe void PauseForShutdown()
    {
        if (_taskThread?.IsAlive == true && _desc is not null)
            LauncherServices.TekSteamClient.PauseJob(ref Unsafe.AsRef<TEKSteamClient.AmItemDesc>(_desc));
    }

    protected static void PostToUi(Action action) => Dispatcher.UIThread.Post(action);
}