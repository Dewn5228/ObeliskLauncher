using System.Runtime.CompilerServices;

namespace ObeliskLauncher.UI;

static class LauncherSettingsWorkflow
{
    static readonly string[] s_supportedExeRelativePaths =
    [
        "ShooterGame/Binaries/Win64/ShooterGame.exe",
        "ShooterGame/Binaries/Win64/ArkAscended.exe",
        "ShooterGame/Binaries/Win64/ArkAscended_BE.exe"
    ];

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

    public static string GetGamePathChangePromptCode(string newPath) => HasSupportedExecutable(newPath)
        ? "errors.gamePathChangePrompt"
        : "errors.gamePathChangeFilesMissing";

    static bool HasSupportedExecutable(string rootPath)
    {
        foreach (string relativePath in s_supportedExeRelativePaths)
            if (File.Exists(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar))))
                return true;

        return false;
    }

    public static bool ShouldWarnCloseOnLaunch(bool enabled) => enabled && (Steam.App.CurrentUserStatus.GameStatus != Game.Status.OwnedAndInstalled || Game.CanUseSpacewar && Game.UseSpacewar);
}