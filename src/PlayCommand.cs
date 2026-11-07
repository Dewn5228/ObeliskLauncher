using System.Globalization;
using System.Linq;
using ObeliskLauncher.Data;
using ObeliskLauncher.Steam;
using ObeliskLauncher.UI;
using ObeliskLauncher.Utils;

namespace ObeliskLauncher;

static class PlayCommand
{
    const string PlayFlag = "--play";
    const string GameOption = "--game";

    public static bool TryRun(string[] args, out int exitCode)
    {
        if (!ContainsPlayFlag(args))
        {
            exitCode = 0;
            return false;
        }

        exitCode = Run(args);
        return true;
    }

    internal static bool ContainsPlayFlag(string[] args)
        => args.Any(static arg => string.Equals(arg, PlayFlag, StringComparison.OrdinalIgnoreCase));

    internal static string? GetGameOption(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], GameOption, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];

        return null;
    }

    static int Run(string[] args)
    {
        LauncherLog.Initialize();
        LauncherLog.Information("Headless play invoked. Args={Args}", args);
        LauncherBootstrap.HeadlessMode = true;
        LauncherDialogService.UseHeadless();

        try
        {
            Directory.CreateDirectory(LauncherBootstrap.AppDataFolder);
            Settings.Load();
            Locale.Init(CultureInfo.CurrentUICulture.Name);
        }
        catch (Exception ex)
        {
            LauncherLog.Error(ex, "Headless play failed during bootstrap");
            Messages.Show("common.error", ex.Message);
            return 1;
        }

        if (!IPC.Initialize())
        {
            LauncherLog.Warning("Headless play aborted: another launcher instance is running");
            Messages.Show("common.error", Locale.Get("errors.anotherInstanceRunning"));
            return 3;
        }

        try
        {
            if (!App.Initialize())
            {
                LauncherLog.Warning("Headless play aborted: Steam initialization failed");
                Messages.Show("common.error", Locale.Get("errors.steamMissing"));
                return 4;
            }

            if (!TryResolveTarget(args, out string gameId, out string gamePath, out string displayName))
            {
                LauncherLog.Warning("Headless play aborted: no usable game target. RequestedGame={Game}", GetGameOption(args) ?? "(active)");
                Messages.Show("common.warning", string.Format(Locale.Get("errors.gamePathNotConfigured"), displayName));
                return 2;
            }

            ActiveGameManager.Configure(gameId, gamePath);
            LauncherLog.Information("Headless play target resolved. GameId={GameId}, GamePath={GamePath}", gameId, gamePath);

            try
            {
                var bootstrapResult = LauncherServices.TekSteamClientBootstrap.InitializeAsync(gamePath).GetAwaiter().GetResult();
                LauncherLog.Information("Headless TekSteamClient bootstrap finished. Success={Success}, RestartRequired={RestartRequired}, DownloadName={DownloadName}, Error={Error}",
                    bootstrapResult.Success,
                    bootstrapResult.RestartRequired,
                    bootstrapResult.DownloadName ?? "none",
                    bootstrapResult.ErrorMessage ?? "none");
            }
            catch (Exception ex)
            {
                LauncherLog.Warning(ex, "Headless TekSteamClient bootstrap failed; DLC manifest detection will be unavailable");
            }

            bool launched = Game.Launch(null);
            LauncherLog.Information("Headless play finished. Launched={Launched}", launched);
            return launched ? 0 : 1;
        }
        catch (Exception ex)
        {
            LauncherLog.Error(ex, "Headless play failed");
            Messages.Show("common.error", ex.Message);
            return 1;
        }
        finally
        {
            IPC.Dispose();
        }
    }

    static bool TryResolveTarget(string[] args, out string gameId, out string gamePath, out string displayName)
    {
        gameId = string.Empty;
        gamePath = string.Empty;
        displayName = string.Empty;

        string? requestedGameId = GetGameOption(args);
        if (!string.IsNullOrWhiteSpace(requestedGameId))
        {
            if (!TryGetCatalogGame(requestedGameId, out GameCatalogEntry? requested) || requested is null)
            {
                displayName = requestedGameId;
                return false;
            }

            gameId = requested.Id;
            displayName = requested.DisplayName;
            gamePath = Settings.GetGamePath(requested.Id);
            return !string.IsNullOrWhiteSpace(gamePath) && Directory.Exists(gamePath);
        }

        if (ActiveGameManager.IsConfigured)
        {
            gameId = ActiveGameManager.Current.Id;
            displayName = ActiveGameManager.Current.DisplayName;
            gamePath = ActiveGameManager.Current.RootPath;
            return !string.IsNullOrWhiteSpace(gamePath) && Directory.Exists(gamePath);
        }

        foreach (GameCatalogEntry game in GameCatalog.AllGames)
        {
            string path = Settings.GetGamePath(game.Id);
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                continue;

            gameId = game.Id;
            displayName = game.DisplayName;
            gamePath = path;
            return true;
        }

        displayName = GameCatalog.DefaultGameId;
        return false;
    }

    static bool TryGetCatalogGame(string gameId, out GameCatalogEntry? entry)
    {
        entry = GameCatalog.AllGames.FirstOrDefault(
            game => game.Id.Equals(gameId, StringComparison.OrdinalIgnoreCase));
        return entry is not null;
    }
}
