using System.Diagnostics;
using System.Text;
using TEKLauncher.Servers;

namespace TEKLauncher.ARK;

/// <summary>Manages game files and parameters.</summary>
static class Game
{
    static GameLaunchCapabilities LaunchCapabilities => LauncherServices.GameLauncher.Capabilities;

    /// <summary>List of codes of all cultures supported by the game.</summary>
    public static readonly string[] CultureCodes = { "ca", "cs", "da", "de", "en", "es", "eu", "fi", "fr", "hu", "it", "ja", "ka", "ko", "nl", "pl", "pt_BR", "ru", "sv", "th", "tr", "uk", "zh", "zh-Hans-CN", "zh-TW" };
    /// <summary>List of standard launch parameters.</summary>
    public static readonly string[] StandardLaunchParameters = { "-d3d10", "-nosplash", "-nomansky", "-nomemorybias", "-lowmemory", "-norhithread", "-novsync", "-preventhibernation" };
    /// <summary>List of active launch parameters.</summary>
    public static readonly List<string> LaunchParameters = new();
    /// <summary>Gets a value that indicates whether DirectX is installed.</summary>
    public static bool DirectXInstalled => LauncherServices.GameLauncher.DirectXInstalled;
    /// <summary>Gets or sets a value that indicates whether TEK Injector should set game process base priority to high.</summary>
    public static bool HighProcessPriority { get; set; }
    public static bool CanUseHighProcessPriority => LaunchCapabilities.SupportsHighProcessPriority;
    /// <summary>Gets a value that indicates whether game files are corrupted.</summary>
    public static bool IsCorrupted => !File.Exists(ExePath) || !File.Exists(System.IO.Path.Combine(Path!, "Engine", "Binaries", "ThirdParty", "Steamworks", "Steamv132", "Win64", "steam_api64.dll"));
    /// <summary>Gets a value that indicates whether the game is running.</summary>
    public static bool IsRunning => Process.GetProcessesByName("ShooterGame").Length > 0;
    /// <summary>Gets or sets a value that indicates whether the game should be executed with administrator privileges.</summary>
    public static bool RunAsAdmin { get; set; }
    public static bool CanRunAsAdmin => LaunchCapabilities.SupportsRunAsAdmin;
    /// <summary>Gets or sets a value that indicates whether game's Steam app ID should be set to 480.</summary>
    public static bool UseSpacewar { get; set; }
    public static bool CanUseSpacewar => LaunchCapabilities.SupportsSpoofAppId;
    /// <summary>Gets or sets the index of the game localization to use.</summary>
    public static int Language { get; set; } = 4;
    /// <summary>Gets or sets path to ShooterGame.exe.</summary>
    public static string ExePath { get; private set; } = null!;
    /// <summary>Gets or sets path to the root game folder.</summary>
    public static string? Path { get; set; }

    public static void Initialize()
    {
        ExePath = System.IO.Path.Combine(Path!, "ShooterGame", "Binaries", "Win64", "ShooterGame.exe");
    }

    /// <summary>Executes the game and optionally initiates connection to a server.</summary>
    /// <param name="server">Server to join, <see langword="null"/> if no server needs to be joined.</param>
    public static void Launch(Server? server)
    {
        var launchLog = new StringBuilder();
        launchLog.AppendLine($"UTC: {DateTimeOffset.UtcNow:O}");
        launchLog.AppendLine($"Game path: {Path ?? "<null>"}");
        launchLog.AppendLine($"Executable path: {ExePath}");
        launchLog.AppendLine($"Steam running: {Steam.App.IsRunning}");
        launchLog.AppendLine($"DirectX installed: {DirectXInstalled}");
        launchLog.AppendLine($"Is corrupted: {IsCorrupted}");

        if (IsCorrupted)
        {
            WriteLaunchAttemptLog(launchLog, "Launch blocked: game files look corrupted.");
            Messages.Show("common.error", Locale.Get("errors.launchFailCorrupted"));
        }
        else if (!Steam.App.IsRunning)
        {
            WriteLaunchAttemptLog(launchLog, "Launch blocked: Steam is not running.");
            Messages.Show("common.error", Locale.Get("errors.launchFailSteamNotRunning"));
        }
        else if (!DirectXInstalled)
        {
            WriteLaunchAttemptLog(launchLog, "Launch blocked: DirectX/runtime requirements are missing.");
            Messages.Show("common.error", string.Format(Locale.Get("errors.launchFailDirectXNotInstalled"), Locale.Get("errors.installDirectX")));
        }
        else if (server is not null && server.Map > MapCode.TheIsland && server.Map < MapCode.Mod && !DLC.Get(server.Map).IsInstalled)
        {
            WriteLaunchAttemptLog(launchLog, $"Launch blocked: DLC '{server.Map}' is missing for server join.");
            Messages.Show("common.warning", Locale.Get("errors.joinFailDlcMissing"));
        }
        else
        {
            Steam.App.UpdateUserStatus();
            launchLog.AppendLine($"Steam ID: {Steam.App.CurrentUserStatus.SteamId64}");
            launchLog.AppendLine($"Game status: {Steam.App.CurrentUserStatus.GameStatus}");
            if (Steam.App.CurrentUserStatus.SteamId64 == 0)
            {
                WriteLaunchAttemptLog(launchLog, "Launch blocked: Steam user is not logged in or could not be resolved.");
                Messages.Show("common.error", Locale.Get("errors.launchFailNotLoggedIntoSteam"));
                return;
            }

            var args = new List<string>(LaunchParameters)
            {
                $"-culture={CultureCodes[Language]}"
            };
            if (server is not null)
            {
                args.Add("+connect");
                args.Add(server.Address);
            }

            var instDlc = new List<uint>();
            foreach (var dlc in DLC.List)
                if (dlc.IsInstalled)
                    instDlc.Add(dlc.AppId);

            var settings = new TekGameRuntimeSettings("steam", 346110, UseSpacewar ? 480u : 0, new Dictionary<uint, string>
            {
                [473850] = "The Center – ARK Expansion Map",
                [508150] = "Primitive+ – ARK Total Conversion",
                [512540] = "ARK: Scorched Earth – Expansion Pack",
                [642250] = "Ragnarok – ARK Expansion Map",
                [696680] = "ARK: Survival Evolved Season Pass",
                [708770] = "ARK: Aberration – Expansion Pack",
                [887380] = "ARK: Extinction – Expansion Pack",
                [1100810] = "Valguero – ARK Expansion Map",
                [1113410] = "ARK: Genesis Season Pass",
                [1270830] = "Crystal Isles – ARK Expansion Map",
                [1691800] = "Lost Island – ARK Expansion Map",
                [1887560] = "Fjordur – ARK Expansion Map",
                [3537070] = "Aquatica – ARK Expansion Map"
            }, [.. instDlc], System.IO.Path.Combine(Path!, "Mods"), Path!);

            uint spoofAppId = CanUseSpacewar && UseSpacewar ? 480u : 0;
            settings = settings with { SpoofAppId = spoofAppId };

            var data = JsonSerializer.SerializeToUtf8Bytes(settings, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
            bool useHighProcessPriority = CanUseHighProcessPriority && HighProcessPriority;
            bool useRunAsAdmin = CanRunAsAdmin && RunAsAdmin;
            launchLog.AppendLine($"High priority: {useHighProcessPriority}");
            launchLog.AppendLine($"Run as admin: {useRunAsAdmin}");
            launchLog.AppendLine($"Use Spacewar: {spoofAppId == 480}");
            launchLog.AppendLine($"Arguments: {string.Join(' ', args)}");
            var launchResult = LauncherServices.GameLauncher.Launch(new(ExePath, args, useHighProcessPriority, useRunAsAdmin, data));
            if (!launchResult.Success)
            {
                WriteLaunchAttemptLog(launchLog, $"Launch failed in platform launcher: {launchResult.ErrorMessage}");
                Messages.Show("Error", launchResult.ErrorMessage!);
                return;
            }

            WriteLaunchAttemptLog(launchLog, "Launch request submitted successfully.");
            if (Settings.CloseOnGameLaunch)
                LauncherServices.Lifetime.Shutdown();
        }
    }

    static void WriteLaunchAttemptLog(StringBuilder builder, string outcome)
    {
        try
        {
            builder.AppendLine($"Outcome: {outcome}");
            string path = System.IO.Path.Combine(LauncherPlatform.Current.AppDataFolder, "last-launch-attempt.txt");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, builder.ToString());
        }
        catch
        {
        }
    }

    /// <summary>Game ownership status used to initialize ARK Shellcode.</summary>
    public enum Status
    {
        NotOwned,
        Owned,
        OwnedAndInstalled
    }

    readonly record struct TekGameRuntimeSettings(string Store, uint AppId, uint SpoofAppId, Dictionary<uint, string> Dlc, uint[] InstalledDlc, string WorkshopDirPath, string WorkshopAmPath);
}