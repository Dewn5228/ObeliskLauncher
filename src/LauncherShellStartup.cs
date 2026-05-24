using System.Runtime.CompilerServices;
using System.Linq;

namespace TEKLauncher;

enum LauncherShellTone
{
    Neutral,
    Success,
    Warning,
    Error
}

readonly record struct LauncherShellStatusSummary(
  string GameVersionText,
  LauncherShellTone GameVersionTone,
  bool GameUpdateAvailable,
    bool DlcUpdatesAvailable,
    string? CatalogWarning);

readonly record struct LauncherShellStartupResult(
  string GameVersionText,
  LauncherShellTone GameVersionTone,
  bool LauncherUpdateAvailable,
  bool GameUpdateAvailable,
  bool DlcUpdatesAvailable,
    string? CatalogWarning,
  bool BeginInstallation,
  TekSteamClientBootstrapResult BootstrapResult);

static class LauncherShellStartup
{
    public static async Task<LauncherShellStartupResult> InitializeAsync(bool beginInstallation)
    {
        IGameContext game = ActiveGameManager.Current;
        string gameVersionText = Locale.Get(Game.IsCorrupted ? "?None?" : "common.na");
        var gameVersionTone = Game.IsCorrupted ? LauncherShellTone.Error : LauncherShellTone.Neutral;

        bool launcherUpdateAvailable = await CheckLauncherUpdateAsync();

        var bootstrapResult = await LauncherServices.TekSteamClientBootstrap.InitializeAsync(game.RootPath);
        if (!bootstrapResult.Success || bootstrapResult.RestartRequired || bootstrapResult.DownloadName is not null)
            return new(gameVersionText, gameVersionTone, launcherUpdateAvailable, false, false, null, beginInstallation, bootstrapResult);

        var statusSummary = await GetStatusSummaryAsync(beginInstallation);
        gameVersionText = statusSummary.GameVersionText;
        gameVersionTone = statusSummary.GameVersionTone;

        return new(gameVersionText, gameVersionTone, launcherUpdateAvailable, statusSummary.GameUpdateAvailable, statusSummary.DlcUpdatesAvailable, statusSummary.CatalogWarning, beginInstallation, bootstrapResult);
    }

    public static async Task<LauncherShellStatusSummary> GetStatusSummaryAsync(bool beginInstallation)
    {
        IGameContext game = ActiveGameManager.Current;
        string gameVersionText = Locale.Get(Game.IsCorrupted ? "?None?" : "common.na");
        var gameVersionTone = Game.IsCorrupted ? LauncherShellTone.Error : LauncherShellTone.Neutral;
        bool gameUpdateAvailable = false;
        bool dlcUpdatesAvailable = false;
        string? catalogWarning = null;

        if (!beginInstallation && await Task.Run(() => LauncherServices.TekSteamClient.CheckForUpdates(20000).Success))
            unsafe
            {
                static TEKSteamClient.AmItemDesc* GetDesc(uint appId, uint depotId)
                {
                    if (!LauncherServices.TekSteamClient.IsLoaded)
                        return null;

                    var itemId = new TEKSteamClient.ItemId { AppId = appId, DepotId = depotId, WorkshopItemId = 0 };
                    try
                    {
                        return LauncherServices.TekSteamClient.GetItemDesc((TEKSteamClient.ItemId*)Unsafe.AsPointer(ref itemId));
                    }
                    catch (NullReferenceException)
                    {
                        return null;
                    }
                }

                var desc = GetDesc(game.SteamAppId, game.MainDepotId);
                if (desc != null && desc->CurrentManifestId != 0)
                {
                    gameUpdateAvailable = IsGameUpdateAvailable(desc);

                    gameVersionText = Locale.Get(gameUpdateAvailable ? "common.outdated" : "common.latest");
                    gameVersionTone = gameUpdateAvailable ? LauncherShellTone.Warning : LauncherShellTone.Success;
                }

                foreach (var dlc in ARK.DLC.List)
                {
                    if (dlc.CurrentStatus != ARK.DLC.Status.Installed)
                        continue;

                    desc = GetDesc(game.SteamAppId, dlc.DepotId);
                    if (desc == null)
                        continue;

                    bool updateAvailable = IsDlcUpdateAvailable(desc, dlc.DepotId);

                    dlc.CurrentStatus = updateAvailable ? ARK.DLC.Status.UpdateAvailable : ARK.DLC.Status.Installed;
                }

                dlcUpdatesAvailable = ARK.DLC.List.Any(d => d.CurrentStatus == ARK.DLC.Status.UpdateAvailable);
            }

        return new(gameVersionText, gameVersionTone, gameUpdateAvailable, dlcUpdatesAvailable, catalogWarning);
    }

    static async Task<bool> CheckLauncherUpdateAsync()
    {
        string? versionString = await Downloader.DownloadStringAsync(
          "https://teknology-hub.com/software/tek-launcher/version",
          "https://de.teknology-hub.com/software/tek-launcher/version");

        if (versionString is null)
        {
            var release = await Downloader.DownloadJsonAsync<Release>("https://api.github.com/repos/Dewn5228/TEKLauncher/releases/latest");
            versionString = release.TagName?[1..];
        }

        return Version.TryParse(versionString, out var onlineVersion)
          && Version.TryParse(LauncherBootstrap.Version, out var currentVersion)
          && onlineVersion > currentVersion;
    }

    static unsafe bool IsGameUpdateAvailable(TEKSteamClient.AmItemDesc* desc)
            => Settings.PreAquatica && ActiveGameManager.Current.Id == GameCatalog.AseGameId
        ? desc->CurrentManifestId != UI.GameUpdateWorkflow.PreAquaticaManifestId
        : desc->Status.HasFlag(TEKSteamClient.AmItemStatus.UpdAvailable);

    static unsafe bool IsDlcUpdateAvailable(TEKSteamClient.AmItemDesc* desc, uint depotId)
    {
        if (!Settings.PreAquatica || ActiveGameManager.Current.Id != GameCatalog.AseGameId)
            return desc->Status.HasFlag(TEKSteamClient.AmItemStatus.UpdAvailable);

        if (!ActiveGameManager.Current.PreAquaticaManifestOverrides.TryGetValue(depotId, out ulong expectedManifestId))
            return false;

        return desc->CurrentManifestId != expectedManifestId;
    }

    readonly record struct Release
    {
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        public string? TagName { get; init; }
    }
}