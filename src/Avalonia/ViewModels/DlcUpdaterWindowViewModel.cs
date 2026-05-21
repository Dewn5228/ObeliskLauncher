namespace TEKLauncher.Avalonia.ViewModels;

internal sealed class DlcUpdaterWindowViewModel : SteamTaskUpdaterWindowViewModelBase
{
    readonly DLC _dlc;
    readonly int _lastStatus;

    internal DlcUpdaterWindowViewModel(DLC dlc, bool validate)
      : base(string.Format(Locale.Get("gameOptionsTab.steamUpdater"), dlc.Name), validate)
    {
        _dlc = dlc;
        _lastStatus = (int)dlc.CurrentStatus;
        dlc.CurrentStatus = DLC.Status.Updating;
    }

    protected override string ThreadName => $"DLCUpdater-{_dlc.Code}";

    protected override TEKSteamClient.ItemId CreateItemId() => new() { AppId = 346110, DepotId = _dlc.DepotId, WorkshopItemId = 0 };

    protected override ulong GetManifestId() => Settings.PreAquatica ? GetPreAquaticaManifestId() : 0;

    protected override unsafe void HandleSuccessfulResult(TEKSteamClient.Error result)
    {
        NewStatus = Settings.PreAquatica && CurrentDesc->CurrentManifestId != GetPreAquaticaManifestId()
          ? (int)DLC.Status.UpdateAvailable
          : (int)DLC.Status.Installed;
        SetStatus(result.Primary == 85
          ? string.Format(Locale.Get("common.alreadyUpToDate"), _dlc.Name)
          : Locale.Get("updateFinished"), global::Avalonia.Media.Brushes.LimeGreen);
    }

    protected override bool TryCloseCore()
    {
        _dlc.CurrentStatus = NewStatus == -1 ? (DLC.Status)_lastStatus : (DLC.Status)NewStatus;
        return true;
    }

    unsafe ulong GetPreAquaticaManifestId() => _dlc.DepotId switch
    {
        346114 => 5573587184752106093,
        375351 => 8265777340034981821,
        375354 => 7952753366101555648,
        375357 => 1447242805278740772,
        473851 => 2551727096735353757,
        473854 => 847717640995143866,
        473857 => 1054814513659387220,
        1318685 => 8189621638927588129,
        1691801 => 3147973472387347535,
        1887561 => 580528532335699271,
        _ => 0
    };
}