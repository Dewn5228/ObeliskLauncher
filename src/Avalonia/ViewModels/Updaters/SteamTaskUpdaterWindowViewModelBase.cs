using Avalonia.Media;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace TEKLauncher.Avalonia.ViewModels;

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
    string _progressText = "Waiting for job data...";
    double _progressValue;
    string _actionLabel = Locale.Get("common.pause");
    string? _spaceMessage;
    IBrush _statusBrush = Brushes.Goldenrod;
    string _statusText = string.Empty;
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
        HandleSuccessfulResult(result);
        FinishCurrentStage(true);
        IsActionEnabled = false;
    }

    protected void FinishCurrentStage(bool success) => _currentStage?.Finish(success);

    protected virtual void OnTaskStarting()
    {
        SetPauseState();
        SetStatus(Locale.Get("modsTab.fetchingData"), Brushes.Goldenrod);
    }

    protected void PostFailureResult(TEKSteamClient.Error result)
    {
        PostToUi(() =>
        {
            ProgressMaximum = 1;
            ProgressValue = 1;
            IsProgressIndeterminate = false;
            SetStatus(result.Message, Brushes.IndianRed);
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
            PostToUi(() =>
            {
                IsProgressIndeterminate = total <= 0;
                ProgressMaximum = total <= 0 ? 1 : total;
                ProgressValue = total <= 0 ? 0 : Math.Clamp(current, 0, total);
                ProgressText = total <= 0 ? GetIndeterminateProgressText() : GetDeterminateProgressText(current, total);
            });
        }
    }

    string GetDeterminateProgressText(long current, long total) => _progressMode switch
    {
        UpdaterProgressMode.Binary => $"{Locale.BytesToString(current)} / {Locale.BytesToString(total)}",
        UpdaterProgressMode.Numbers => $"{current:N0} / {total:N0}",
        _ => $"{Math.Clamp(total == 0 ? 0 : current * 100.0 / total, 0, 100):0.#}%"
    };

    string GetIndeterminateProgressText() => _progressMode switch
    {
        UpdaterProgressMode.Binary => "Preparing download size...",
        UpdaterProgressMode.Numbers => "Preparing file list...",
        UpdaterProgressMode.Percentage => "Preparing progress data...",
        _ => "Waiting for job data..."
    };

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