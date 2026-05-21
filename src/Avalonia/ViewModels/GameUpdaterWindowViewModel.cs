namespace TEKLauncher.Avalonia.ViewModels;

internal sealed class GameUpdaterWindowViewModel : SteamTaskUpdaterWindowViewModelBase
{
    internal GameUpdaterWindowViewModel(bool validate)
      : base(string.Format(Locale.Get("gameOptionsTab.steamUpdater"), "ARK"), validate)
    {
    }

    protected override string ThreadName => "GameUpdater";

    protected override TEKSteamClient.ItemId CreateItemId() => new() { AppId = 346110, DepotId = 346111, WorkshopItemId = 0 };

    protected override ulong GetManifestId() => Settings.PreAquatica ? GameUpdateWorkflow.PreAquaticaManifestId : 0;

    protected override unsafe void HandleSuccessfulResult(TEKSteamClient.Error result)
    {
        SetStatus(result.Primary == 85
          ? string.Format(Locale.Get("common.alreadyUpToDate"), "ARK")
          : Locale.Get("updateFinished"), global::Avalonia.Media.Brushes.LimeGreen);
    }

    protected override bool TryCloseCore() => true;
}