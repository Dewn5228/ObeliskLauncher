using Avalonia.Media;
using System.Linq;

namespace TEKLauncher.Avalonia.ViewModels;

internal sealed class ServerModsUpdaterWindowViewModel : SteamTaskUpdaterWindowViewModelBase
{
    readonly ulong[] _modIds;

    internal ServerModsUpdaterWindowViewModel(ulong[] modIds)
      : base(string.Format(LocManager.GetString(LocCode.SteamUpdater), LocManager.GetString(LocCode.Mods)), true)
    {
        _modIds = modIds;
    }

    protected override string ThreadName => "ServerModsValidator";

    protected override TEKSteamClient.ItemId CreateItemId() => default;

    protected override ulong GetManifestId() => 0;

    protected override void HandleSuccessfulResult(TEKSteamClient.Error result)
    {
    }

    protected override unsafe void RunTaskCore()
    {
        if (_modIds.Length == 0)
        {
            PostToUi(() =>
            {
                ResetTaskVisuals();
                SetStatus(LocManager.GetString(LocCode.NA), Brushes.IndianRed);
                SetRetryState();
            });
            return;
        }

        PostToUi(() =>
        {
            ResetTaskVisuals();
            SetStatus(LocManager.GetString(LocCode.RequestingModDetails), Brushes.Goldenrod);
        });

        var details = ResolveDetails();
        if (details.Length == 0)
        {
            PostToUi(() =>
            {
                ResetTaskVisuals();
                SetStatus(LocManager.GetString(LocCode.RequestingModDetailsFail), Brushes.IndianRed);
                SetRetryState();
            });
            return;
        }

        for (int index = 0; index < details.Length; index++)
        {
            Mod.ModDetails detail = details[index];
            PostToUi(() =>
            {
                SetTitle(string.Format(LocManager.GetString(LocCode.SteamUpdater), $"{detail.Name} [{index + 1}/{details.Length}]"));
                ResetTaskVisuals();
                OnTaskStarting();
            });

            var itemId = new TEKSteamClient.ItemId
            {
                AppId = 346110,
                DepotId = 346110,
                WorkshopItemId = detail.Id
            };

            TEKSteamClient.Error result = RunJob(in itemId, 0);
            if (result.Success || result.Primary == 85)
            {
                PostToUi(() =>
                {
                    EnsureModPresent(detail);
                    ProgressMaximum = 1;
                    ProgressValue = 1;
                    IsProgressIndeterminate = false;
                    ProgressText = "100%";
                    SetStatus(result.Primary == 85
              ? string.Format(LocManager.GetString(LocCode.AlreadyUpToDate), detail.Name)
              : string.Format(LocManager.GetString(LocCode.ModInstallSuccess), detail.Name), Brushes.LimeGreen);
                    FinishCurrentStage(true);
                });
                continue;
            }

            if (result.Primary == 68)
            {
                PostPausedResult();
                return;
            }

            PostFailureResult(result);
            return;
        }

        PostToUi(() => IsActionEnabled = false);
    }

    protected override bool TryCloseCore() => true;

    void EnsureModPresent(Mod.ModDetails detail)
    {
        lock (Mod.List)
        {
            var mod = Mod.List.Find(item => item.Id == detail.Id);
            if (mod is null)
            {
                mod = new(detail.Id);
                Mod.List.Add(mod);
            }

            mod.Details = detail;
            mod.CurrentStatus = Mod.Status.Installed;
        }
    }

    Mod.ModDetails[] ResolveDetails()
    {
        Mod.ModDetails[] details = Steam.CM.Client.GetModDetails(_modIds);
        if (details.Length == 0)
            return [];

        var detailsById = details.ToDictionary(item => item.Id);
        Mod.ModDetails[] resolved = new Mod.ModDetails[_modIds.Length];
        for (int i = 0; i < _modIds.Length; i++)
            resolved[i] = detailsById.TryGetValue(_modIds[i], out Mod.ModDetails detail) ? detail : new Mod.ModDetails { Id = _modIds[i], Name = _modIds[i].ToString() };
        return resolved;
    }
}