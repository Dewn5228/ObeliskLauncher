using System.Runtime.CompilerServices;

namespace TEKLauncher.UI;

static class LauncherSettingsWorkflow
{
    public static async Task<bool> CleanDownloadCacheAsync()
    {
        if (!LauncherServices.TekSteamClient.IsLoaded)
            return false;

        await Task.Run(delegate
        {
            LauncherServices.TekSteamClient.LockItemDescs();
            unsafe
            {
                var desc = LauncherServices.TekSteamClient.GetItemDesc(null);
                while (desc != null)
                {
                    if (desc->Status.HasFlag(TEKSteamClient.AmItemStatus.Job))
                        LauncherServices.TekSteamClient.CancelJob(ref Unsafe.AsRef<TEKSteamClient.AmItemDesc>(desc));
                    desc = desc->Next;
                }
            }
            LauncherServices.TekSteamClient.UnlockItemDescs();
        });

        return true;
    }

    public static string GetGamePathChangePromptCode(string newPath) => File.Exists(Path.Combine(newPath, "ShooterGame", "Binaries", "Win64", "ShooterGame.exe"))
      ? "errors.gamePathChangePrompt"
      : "errors.gamePathChangeFilesMissing";

    public static bool ShouldWarnCloseOnLaunch(bool enabled) => enabled && (Steam.App.CurrentUserStatus.GameStatus != Game.Status.OwnedAndInstalled || Game.CanUseSpacewar && Game.UseSpacewar);
}