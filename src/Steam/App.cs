using System.Diagnostics;
using ObeliskLauncher.ARK;
using ObeliskLauncher.Steam.CM;
using ObeliskLauncher.Utils;

namespace ObeliskLauncher.Steam;

/// <summary>Manages Steam app's files and configs.</summary>
static class App
{
    /// <summary>Steam installation path.</summary>
    static string s_path = null!;
    /// <summary>Gets a value that indicates whether Steam app is running.</summary>
    public static bool IsRunning
    {
        get
        {
            int? pid = LauncherPlatform.Current.GetSteamProcessId();
            if (!pid.HasValue || pid.Value == 0)
                return false;
            Process? steamProcess;
            try { steamProcess = Process.GetProcessById(pid.Value); }
            catch (ArgumentException) { steamProcess = null; }
            steamProcess?.Dispose();
            return steamProcess is not null;
        }
    }
    /// <summary>Gets or sets Steam's game installation path if there is one.</summary>
    public static string? GamePath { get; private set; }
    /// <summary>Gets or sets current Steam user status.</summary>
    public static UserStatus CurrentUserStatus { get; private set; }
    /// <summary>Optional startup notice shown when compatibility fallbacks are applied.</summary>
    public static string? StartupNotice { get; private set; }
    /// <summary>Retrieves primary data from Steam config files.</summary>
    public static bool Initialize()
    {
        string? path = LauncherPlatform.Current.GetSteamInstallPath();
        if (path is null)
            return false;
        s_path = path;
        StartupNotice = null;
        GamePath = null;
        string configFile = Path.Combine(path, "config", "config.vdf");
        if (File.Exists(configFile))
        {
            using var reader = new StreamReader(configFile);
            var vdf = VdfParser.Parse(reader.ReadToEnd())["Software"]?["Valve"]?["Steam"];
            if (vdf is not null)
            {
                if (uint.TryParse(vdf["CurrentCellID"]?.Value, out uint cellId))
                    CM.Client.CellId = cellId;
                var cmList = vdf["CMWebSocket"];
                if (cmList?.Children is not null)
                {
                    var urls = new Uri[cmList.Children.Count];
                    int i = 0;
                    foreach (string key in cmList.Children.Keys)
                        urls[i++] = new($"wss://{key}/cmsocket/");
                    WebSocketConnection.ServerList = new(urls);
                }
            }
        }
        UpdateUserStatus();
        return true;
    }
    public static void UpdateUserStatus()
    {
        bool hasConfiguredGame = ActiveGameManager.IsConfigured;
        GameCatalogEntry fallbackGame = GameCatalog.GetByGameId(GameCatalog.DefaultGameId);
        uint appId = hasConfiguredGame ? ActiveGameManager.Current.SteamAppId : fallbackGame.SteamAppId;
        string steamFolderName = hasConfiguredGame ? ActiveGameManager.Current.SteamFolderName : fallbackGame.SteamFolderName;
        string? configuredRootPath = hasConfiguredGame ? ActiveGameManager.Current.RootPath : null;

        try
        {
            string appIdText = appId.ToString();
            Environment.SetEnvironmentVariable("SteamAppId", appIdText);
            Environment.SetEnvironmentVariable("SteamGameId", appIdText);
            LauncherLog.Debug("Steam identity environment set. SteamAppId={SteamAppId}, ActiveGameConfigured={ActiveGameConfigured}", appIdText, hasConfiguredGame);
        }
        catch (Exception ex)
        {
            LauncherLog.Warning("Failed to set Steam identity environment variables: {Error}", ex.Message);
        }

        ulong steamId64 = 0;
        string loginUsersFile = Path.Combine(s_path, "config", "loginusers.vdf");
        if (File.Exists(loginUsersFile))
        {
            using var reader = new StreamReader(loginUsersFile);
            var root = VdfParser.Parse(reader.ReadToEnd());
            var users = root["users"];
            foreach (var (key, user) in users?.Children ?? [])
                if (user["MostRecent"]?.Value == "1")
                {
                    _ = ulong.TryParse(key, out steamId64);
                    break;
                }
        }
        if (steamId64 == 0)
        {
            CurrentUserStatus = new(0, Game.Status.NotOwned);
            return;
        }
        Game.Status status = Game.Status.NotOwned;
        string configFile = Path.Combine(s_path, "userdata", steamId64.ToString(), "config", "localconfig.vdf");
        if (File.Exists(configFile))
        {
            using var reader = new StreamReader(configFile);
            var vdf = VdfParser.Parse(reader.ReadToEnd())["apptickets"]?[appId.ToString()];
            if (vdf is not null)
                status = Game.Status.Owned;
        }
        if (status == Game.Status.Owned)
        {
            GamePath = TryGetGamePath(appId, steamFolderName);
            if (configuredRootPath is not null && GamePath == configuredRootPath)
                status = Game.Status.OwnedAndInstalled;
            else if (!hasConfiguredGame && !string.IsNullOrWhiteSpace(GamePath))
            {
                ActiveGameManager.Configure(GameCatalog.DefaultGameId, GamePath);
                status = Game.Status.OwnedAndInstalled;
                StartupNotice = $"Legacy settings detected: defaulted active game to {GameCatalog.DefaultGameId} using detected install path.";
            }
            else if (!hasConfiguredGame)
                StartupNotice = "Legacy settings detected: defaulted Steam status checks to ASE until game setup is completed.";
        }
        CurrentUserStatus = new(steamId64, status);
    }

    public static string? TryGetGamePath(uint appId, string steamFolderName)
    {
        string libraryFoldersFile = Path.Combine(s_path, "config", "libraryfolders.vdf");
        if (!File.Exists(libraryFoldersFile))
            return null;

        using var reader = new StreamReader(libraryFoldersFile);
        var vdf = VdfParser.Parse(reader.ReadToEnd());
        if (vdf.Children.Count == 0)
            return null;

        string appIdString = appId.ToString();
        foreach (var (_, library) in vdf.Children)
        {
            string? libraryPath = library["path"]?.Value;
            var apps = library["apps"]?.Children;
            if (libraryPath is null || apps is null)
                continue;

            if (apps.ContainsKey(appIdString))
                return Path.Combine(NormalizeVdfPath(libraryPath), "steamapps", "common", steamFolderName);
        }

        return null;
    }

    static string NormalizeVdfPath(string path) => path.Replace("\\\\", "\\").Replace('\\', Path.DirectorySeparatorChar);

    /// <summary>Contains user's Steam ID in 64-bit format and their game ownership status.</summary>
    public readonly record struct UserStatus(ulong SteamId64, Game.Status GameStatus);
}