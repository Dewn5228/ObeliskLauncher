using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ObeliskLauncher.Avalonia.ViewModels;

internal sealed class DlcUpdaterWindowViewModel : SteamTaskUpdaterWindowViewModelBase
{
    const int NoManifestPrimaryCode = 8;
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

    protected override TEKSteamClient.ItemId CreateItemId() => new() { AppId = ActiveGameManager.Current.SteamAppId, DepotId = _dlc.DepotId, WorkshopItemId = 0 };

    TEKSteamClient.ItemId CreateFallbackItemId() => new() { AppId = _dlc.AppId, DepotId = _dlc.DepotId, WorkshopItemId = 0 };

    protected override ulong GetManifestId()
      => Settings.PreAquatica && ActiveGameManager.Current.Id == GameCatalog.AseGameId
        ? GetPreAquaticaManifestId()
        : 0;

    protected override unsafe void HandleSuccessfulResult(TEKSteamClient.Error result)
    {
        NewStatus = Settings.PreAquatica
          && ActiveGameManager.Current.Id == GameCatalog.AseGameId
          && CurrentDesc->CurrentManifestId != GetPreAquaticaManifestId()
          ? (int)DLC.Status.UpdateAvailable
          : (int)DLC.Status.Installed;
        SetStatus(result.Primary == 85
          ? string.Format(Locale.Get("common.alreadyUpToDate"), _dlc.Name)
          : Locale.Get("updateFinished"), global::Avalonia.Media.Brushes.LimeGreen);
    }

    protected override unsafe void RunTaskCore()
    {
        TEKSteamClient.ItemId itemId = CreateItemId();
        TEKSteamClient.Error result = RunJob(in itemId, GetManifestId());

        if (!result.Success
          && result.Primary == NoManifestPrimaryCode
          && _dlc.AppId != 0
          && _dlc.AppId != itemId.AppId)
        {
            var fallbackItemId = CreateFallbackItemId();
            if (fallbackItemId.AppId != itemId.AppId)
                result = RunJob(in fallbackItemId, GetManifestId());
        }

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

        if (CurrentDesc->Job.Stage == TEKSteamClient.AmJobStage.Pathcing && ((result.Type == 3 && result.Primary == 6 && (result.Auxiliary == 2 || result.Auxiliary == 38)) || (result.Type == 1 && result.Primary == 78 && result.Auxiliary == 48)))
        {
            if (result.Uri != 0)
                Marshal.FreeHGlobal(result.Uri);

            result = LauncherServices.TekSteamClient.CancelJob(ref Unsafe.AsRef<TEKSteamClient.AmItemDesc>(CurrentDesc));
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