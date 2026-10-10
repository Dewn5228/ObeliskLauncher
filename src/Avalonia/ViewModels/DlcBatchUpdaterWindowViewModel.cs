using Avalonia.Media;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ObeliskLauncher.Avalonia.ViewModels;

internal sealed class DlcBatchUpdaterWindowViewModel : SteamTaskUpdaterWindowViewModelBase
{
    readonly DLC[] _dlcs;

    internal DlcBatchUpdaterWindowViewModel(IReadOnlyList<DLC> dlcs)
      : base(string.Format(Locale.Get("gameOptionsTab.steamUpdater"), Locale.Get("dlcTab.tabTitle")), false)
    {
        _dlcs = [.. dlcs];
    }

    protected override string ThreadName => "DLCBatchUpdater";

    protected override TEKSteamClient.ItemId CreateItemId() => default;

    protected override ulong GetManifestId() => 0;

    protected override void HandleSuccessfulResult(TEKSteamClient.Error result)
    {
    }

    protected override unsafe void RunTaskCore()
    {
        if (_dlcs.Length == 0)
        {
            PostToUi(() =>
            {
                ResetTaskVisuals();
                SetStatus(Locale.Get("common.na"), Brushes.IndianRed);
                SetRetryState();
            });
            return;
        }

        for (int index = 0; index < _dlcs.Length; index++)
        {
            DLC dlc = _dlcs[index];
            dlc.CurrentStatus = DLC.Status.Updating;
            int captured = index;
            PostToUi(() =>
            {
                SetTitle(string.Format(Locale.Get("gameOptionsTab.steamUpdater"), $"{dlc.Name} [{captured + 1}/{_dlcs.Length}]"));
                ResetTaskVisuals();
                OnTaskStarting();
            });

            var itemId = new TEKSteamClient.ItemId { AppId = ActiveGameManager.Current.SteamAppId, DepotId = dlc.DepotId, WorkshopItemId = 0 };
            TEKSteamClient.Error result = RunJob(in itemId, GetDlcManifestId(dlc));

            if (!result.Success && result.Primary == 8 && dlc.AppId != 0 && dlc.AppId != itemId.AppId)
            {
                var fallback = new TEKSteamClient.ItemId { AppId = dlc.AppId, DepotId = dlc.DepotId, WorkshopItemId = 0 };
                result = RunJob(in fallback, GetDlcManifestId(dlc));
            }

            if (result.Success || result.Primary == 85)
            {
                PostToUi(() =>
                {
                    ApplyDlcSuccess(dlc, result);
                    ProgressMaximum = 1;
                    ProgressValue = 1;
                    IsProgressIndeterminate = false;
                    ProgressText = "100%";
                    SetStatus(result.Primary == 85
                        ? string.Format(Locale.Get("common.alreadyUpToDate"), dlc.Name)
                        : string.Format(Locale.Get("updateFinished")) + $" — {dlc.Name}", Brushes.LimeGreen);
                    FinishCurrentStage(true);
                });
                continue;
            }

            if (result.Primary == 68)
            {
                dlc.CurrentStatus = DLC.Status.UpdateAvailable;
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
                    itemId = new TEKSteamClient.ItemId { AppId = ActiveGameManager.Current.SteamAppId, DepotId = dlc.DepotId, WorkshopItemId = 0 };
                    result = RunJob(in itemId, GetDlcManifestId(dlc));
                    if (result.Success || result.Primary == 85)
                    {
                        PostToUi(() =>
                        {
                            ApplyDlcSuccess(dlc, result);
                            ProgressMaximum = 1;
                            ProgressValue = 1;
                            IsProgressIndeterminate = false;
                            ProgressText = "100%";
                            SetStatus(Locale.Get("updateFinished") + $" — {dlc.Name}", Brushes.LimeGreen);
                            FinishCurrentStage(true);
                        });
                        continue;
                    }
                }
            }

            dlc.CurrentStatus = DLC.Status.UpdateAvailable;
            PostFailureResult(result);
            return;
        }

        PostToUi(() => IsActionEnabled = false);
    }

    protected override bool TryCloseCore() => true;

    unsafe void ApplyDlcSuccess(DLC dlc, TEKSteamClient.Error result)
    {
        bool preAquatica = Settings.PreAquatica && ActiveGameManager.Current.Id == GameCatalog.AseGameId;
        dlc.CurrentStatus = preAquatica && CurrentDesc->CurrentManifestId != GetDlcManifestId(dlc)
            ? DLC.Status.UpdateAvailable
            : DLC.Status.Installed;
    }

    static ulong GetDlcManifestId(DLC dlc)
    {
        if (!Settings.PreAquatica || ActiveGameManager.Current.Id != GameCatalog.AseGameId)
            return 0;
        return dlc.DepotId switch
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
}
