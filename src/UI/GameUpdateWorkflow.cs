using System.Linq;
using System.Runtime.CompilerServices;

namespace TEKLauncher.UI;

static class GameUpdateWorkflow
{
    public const ulong PreAquaticaManifestId = 8075379529797638112;

    public static unsafe string? GetSafetyPrompt(bool validate)
    {
        IGameContext game = ActiveGameManager.Current;
        var itemId = new TEKSteamClient.ItemId { AppId = game.SteamAppId, DepotId = game.MainDepotId, WorkshopItemId = 0 };
        var desc = LauncherServices.TekSteamClient.GetItemDesc((TEKSteamClient.ItemId*)Unsafe.AsPointer(ref itemId));
        if (desc is null)
            return null;

        if (Settings.PreAquatica && game.Id == GameCatalog.AseGameId)
        {
            if (desc->CurrentManifestId > 0 && desc->CurrentManifestId != PreAquaticaManifestId && DLC.List.Any(dlc => dlc.IsInstalled))
            {
                return "It appears that you are trying to downgrade to Pre-Aquatica version.\nDoing so directly is unsafe and will break game files, and there are 2 ways you can avoid that:\n1. Completely reinstalling the game, by deleting its entire folder while launcher is closed, and then using Validate option\n2. Uninstalling all DLC, disabling Pre-Aquatica mode, then using Validate option here to recover missing files,\n  then finally downgrade via enabling Pre-Aquatica mode back and using Update option here.\nOr do you want to accept the risk and proceed to downgrade right now anyway?";
            }
        }
        else if (game.Id == GameCatalog.AseGameId && desc->CurrentManifestId == PreAquaticaManifestId && validate)
        {
            return "It appears that you are trying to update from Pre-Aquatica version via validation.\nThis is unsafe and will likely break your game files, you should use Update option instead.\nOr do you want to accept the risk and proceed to validation now anyway?";
        }

        return null;
    }
}