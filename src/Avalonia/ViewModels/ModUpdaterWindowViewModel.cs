namespace TEKLauncher.Avalonia.ViewModels;

internal sealed class ModUpdaterWindowViewModel : SteamTaskUpdaterWindowViewModelBase
{
    readonly Mod.ModDetails _details;
    readonly int _lastStatus;

    internal ModUpdaterWindowViewModel(Mod.ModDetails details, bool validate)
      : base(string.Format(Locale.Get("gameOptionsTab.steamUpdater"), details.Name), validate)
    {
        _details = details;

        lock (Mod.List)
        {
            var mod = Mod.List.Find(item => item.Id == details.Id);
            if (mod is not null)
            {
                _lastStatus = (int)mod.CurrentStatus;
                mod.CurrentStatus = Mod.Status.Updating;
            }
            else
                _lastStatus = -1;
        }
    }

    void EnsureModPresent()
    {
        lock (Mod.List)
        {
            var mod = Mod.List.Find(item => item.Id == _details.Id);
            if (mod is null)
            {
                mod = new(_details.Id);
                Mod.List.Add(mod);
            }

            mod.Details = _details;
            mod.CurrentStatus = Mod.Status.Installed;
        }
    }

    protected override string ThreadName => $"ModUpdater-{_details.Id}";

    protected override TEKSteamClient.ItemId CreateItemId() => new() { AppId = 346110, DepotId = 346110, WorkshopItemId = _details.Id };

    protected override ulong GetManifestId() => 0;

    protected override void HandleSuccessfulResult(TEKSteamClient.Error result)
    {
        NewStatus = (int)Mod.Status.Installed;
        EnsureModPresent();
        SetStatus(result.Primary == 85
          ? string.Format(Locale.Get("common.alreadyUpToDate"), _details.Name)
          : string.Format(Locale.Get("modsTab.modInstallSuccess"), _details.Name), global::Avalonia.Media.Brushes.LimeGreen);
    }

    protected override bool TryCloseCore()
    {
        lock (Mod.List)
        {
            var mod = Mod.List.Find(item => item.Id == _details.Id);
            if (mod is not null && _lastStatus >= 0)
                mod.CurrentStatus = NewStatus == -1 ? (Mod.Status)_lastStatus : (Mod.Status)NewStatus;
        }

        return true;
    }
}