using System.Runtime.CompilerServices;

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
  bool DlcUpdatesAvailable);

readonly record struct LauncherShellStartupResult(
  string GameVersionText,
  LauncherShellTone GameVersionTone,
  bool LauncherUpdateAvailable,
  bool GameUpdateAvailable,
  bool DlcUpdatesAvailable,
  bool BeginInstallation,
  TekSteamClientBootstrapResult BootstrapResult);

static class LauncherShellStartup
{
    static readonly Dictionary<uint, ulong> s_preAquaticaDlcManifests = new()
    {
        [346114] = 5573587184752106093,
        [375351] = 8265777340034981821,
        [375354] = 7952753366101555648,
        [375357] = 1447242805278740772,
        [473851] = 2551727096735353757,
        [473854] = 847717640995143866,
        [473857] = 1054814513659387220,
        [1318685] = 8189621638927588129,
        [1691801] = 3147973472387347535,
        [1887561] = 580528532335699271
    };

    public static async Task<LauncherShellStartupResult> InitializeAsync(bool beginInstallation)
    {
        string gameVersionText = Locale.Get(Game.IsCorrupted ? "?None?" : "common.na");
        var gameVersionTone = Game.IsCorrupted ? LauncherShellTone.Error : LauncherShellTone.Neutral;

        bool launcherUpdateAvailable = await CheckLauncherUpdateAsync();

        var bootstrapResult = await LauncherServices.TekSteamClientBootstrap.InitializeAsync(Game.Path!);
        if (!bootstrapResult.Success || bootstrapResult.RestartRequired || bootstrapResult.DownloadName is not null)
            return new(gameVersionText, gameVersionTone, launcherUpdateAvailable, false, false, beginInstallation, bootstrapResult);

        var statusSummary = await GetStatusSummaryAsync(beginInstallation);
        gameVersionText = statusSummary.GameVersionText;
        gameVersionTone = statusSummary.GameVersionTone;

        return new(gameVersionText, gameVersionTone, launcherUpdateAvailable, statusSummary.GameUpdateAvailable, statusSummary.DlcUpdatesAvailable, beginInstallation, bootstrapResult);
    }

    public static async Task<LauncherShellStatusSummary> GetStatusSummaryAsync(bool beginInstallation)
    {
        string gameVersionText = Locale.Get(Game.IsCorrupted ? "?None?" : "common.na");
        var gameVersionTone = Game.IsCorrupted ? LauncherShellTone.Error : LauncherShellTone.Neutral;
        bool gameUpdateAvailable = false;
        bool dlcUpdatesAvailable = false;

        if (!beginInstallation && await Task.Run(() => LauncherServices.TekSteamClient.CheckForUpdates(20000).Success))
            unsafe
            {
                static TEKSteamClient.AmItemDesc* GetDesc(uint depotId)
                {
                    var itemId = new TEKSteamClient.ItemId { AppId = 346110, DepotId = depotId, WorkshopItemId = 0 };
                    return LauncherServices.TekSteamClient.GetItemDesc((TEKSteamClient.ItemId*)Unsafe.AsPointer(ref itemId));
                }

                var desc = GetDesc(346111);
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

                    desc = GetDesc(dlc.DepotId);
                    if (desc == null)
                        continue;

                    bool updateAvailable = IsDlcUpdateAvailable(desc, dlc.DepotId);

                    dlc.CurrentStatus = updateAvailable ? ARK.DLC.Status.UpdateAvailable : ARK.DLC.Status.Installed;
                }

                dlcUpdatesAvailable = Array.Exists(ARK.DLC.List, d => d.CurrentStatus == ARK.DLC.Status.UpdateAvailable);
            }

        return new(gameVersionText, gameVersionTone, gameUpdateAvailable, dlcUpdatesAvailable);
    }

    static async Task<bool> CheckLauncherUpdateAsync()
    {
        string? versionString = await Downloader.DownloadStringAsync(
          "https://teknology-hub.com/software/tek-launcher/version",
          "https://de.teknology-hub.com/software/tek-launcher/version");

        if (versionString is null)
        {
            var release = await Downloader.DownloadJsonAsync<Release>("https://api.github.com/repos/Nuclearistt/TEKLauncher/releases/latest");
            versionString = release.TagName?[1..];
        }

        return Version.TryParse(versionString, out var onlineVersion)
          && Version.TryParse(LauncherBootstrap.Version, out var currentVersion)
          && onlineVersion > currentVersion;
    }

    static unsafe bool IsGameUpdateAvailable(TEKSteamClient.AmItemDesc* desc)
      => Settings.PreAquatica
        ? desc->CurrentManifestId != UI.GameUpdateWorkflow.PreAquaticaManifestId
        : desc->Status.HasFlag(TEKSteamClient.AmItemStatus.UpdAvailable);

    static unsafe bool IsDlcUpdateAvailable(TEKSteamClient.AmItemDesc* desc, uint depotId)
    {
        if (!Settings.PreAquatica)
            return desc->Status.HasFlag(TEKSteamClient.AmItemStatus.UpdAvailable);

        if (!s_preAquaticaDlcManifests.TryGetValue(depotId, out ulong expectedManifestId))
            return false;

        return desc->CurrentManifestId != expectedManifestId;
    }

    readonly record struct Release
    {
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        public string? TagName { get; init; }
    }
}