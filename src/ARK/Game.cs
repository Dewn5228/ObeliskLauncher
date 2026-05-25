using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using ObeliskLauncher.Data;
using ObeliskLauncher.Servers;

namespace ObeliskLauncher.ARK;

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
    static readonly string[] s_asaExeFallbackRelativePaths =
    [
        "ShooterGame/Binaries/Win64/ArkAscended.exe",
        "ShooterGame/Binaries/Win64/ArkAscended_BE.exe"
    ];

    /// <summary>Gets a value that indicates whether game files are corrupted.</summary>
    public static bool IsCorrupted
    {
        get
        {
            if (!File.Exists(ExePath))
                return true;

            // ASE historically ships Steamworks files under Steamv132; ASA may not.
            if (ActiveGameManager.Current.Id == GameCatalog.AseGameId)
                return !File.Exists(System.IO.Path.Combine(Path, "Engine", "Binaries", "ThirdParty", "Steamworks", "Steamv132", "Win64", "steam_api64.dll"));

            return false;
        }
    }
    /// <summary>Gets a value that indicates whether the game is running.</summary>
    public static bool IsRunning => Process.GetProcessesByName("ShooterGame").Length > 0;
    /// <summary>Gets or sets a value that indicates whether the game should be executed with administrator privileges.</summary>
    public static bool RunAsAdmin { get; set; }
    public static bool CanRunAsAdmin => LaunchCapabilities.SupportsRunAsAdmin;
    /// <summary>Gets or sets a value that indicates whether game's Steam app ID should be set to 480.</summary>
    public static bool UseSpacewar
    {
        get => _useSpacewar;
        set => _useSpacewar = value && CanUseSpacewar;
    }
    public static bool CanUseSpacewar => LaunchCapabilities.SupportsSpoofAppId && ActiveGameManager.Current.SupportsSpacewarSpoof;
    public static uint SteamAppId => ActiveGameManager.Current.SteamAppId;
    public static string SteamAppIdString => SteamAppId.ToString();
    public static uint MainDepotId => ActiveGameManager.Current.MainDepotId;
    public static uint WorkshopDepotId => ActiveGameManager.Current.WorkshopDepotId;
    public static string SteamFolderName => ActiveGameManager.Current.SteamFolderName;
    public static string ActiveGameId => ActiveGameManager.Current.Id;
    public static string ActiveGameName => ActiveGameManager.Current.DisplayName;
    /// <summary>Gets or sets the index of the game localization to use.</summary>
    public static int Language { get; set; } = 4;
    /// <summary>Gets path to game executable for active game context.</summary>
    public static string ExePath => ResolveExecutablePath();
    /// <summary>Gets path to the root game folder for active game context.</summary>
    public static string Path => ActiveGameManager.Current.RootPath;

    static bool _useSpacewar;

    /// <summary>Executes the game and optionally initiates connection to a server.</summary>
    /// <param name="server">Server to join, <see langword="null"/> if no server needs to be joined.</param>
    public static void Launch(Server? server)
    {
        LauncherLog.Information("Launch requested. GameId={GameId}, GameName={GameName}, RootPath={RootPath}, ExePath={ExePath}, Server={Server}",
            ActiveGameManager.Current.Id,
            ActiveGameManager.Current.DisplayName,
            Path,
            ExePath,
            server?.Address ?? "none");

        var launchLog = new StringBuilder();
        launchLog.AppendLine($"UTC: {DateTimeOffset.UtcNow:O}");
        launchLog.AppendLine($"Game path: {Path}");
        launchLog.AppendLine($"Executable path: {ExePath}");
        launchLog.AppendLine($"Steam running: {Steam.App.IsRunning}");
        launchLog.AppendLine($"DirectX installed: {DirectXInstalled}");
        launchLog.AppendLine($"Is corrupted: {IsCorrupted}");

        if (IsCorrupted)
        {
            LauncherLog.Warning("Launch blocked: files are considered corrupted. GameId={GameId}, ExePath={ExePath}", ActiveGameManager.Current.Id, ExePath);
            WriteLaunchAttemptLog(launchLog, "Launch blocked: game files look corrupted.");
            Messages.Show("common.error", Locale.Get("errors.launchFailCorrupted"));
        }
        else if (!Steam.App.IsRunning)
        {
            LauncherLog.Warning("Launch blocked: Steam is not running");
            WriteLaunchAttemptLog(launchLog, "Launch blocked: Steam is not running.");
            Messages.Show("common.error", Locale.Get("errors.launchFailSteamNotRunning"));
        }
        else if (!DirectXInstalled)
        {
            LauncherLog.Warning("Launch blocked: DirectX/runtime requirements are missing");
            WriteLaunchAttemptLog(launchLog, "Launch blocked: DirectX/runtime requirements are missing.");
            Messages.Show("common.error", string.Format(Locale.Get("errors.launchFailDirectXNotInstalled"), Locale.Get("errors.installDirectX")));
        }
        else if (server is not null && server.Map > MapCode.TheIsland && server.Map < MapCode.Mod && !DLC.Get(server.Map).IsInstalled)
        {
            LauncherLog.Warning("Launch blocked: required DLC is missing for server join. Map={Map}", server.Map);
            WriteLaunchAttemptLog(launchLog, $"Launch blocked: DLC '{server.Map}' is missing for server join.");
            Messages.Show("common.warning", Locale.Get("errors.joinFailDLCMissing"));
        }
        else
        {
            Steam.App.UpdateUserStatus();
            launchLog.AppendLine($"Steam ID: {Steam.App.CurrentUserStatus.SteamId64}");
            launchLog.AppendLine($"Game status: {Steam.App.CurrentUserStatus.GameStatus}");
            if (Steam.App.CurrentUserStatus.SteamId64 == 0)
            {
                LauncherLog.Warning("Launch blocked: Steam user not logged in or unresolved");
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

            uint spoofAppId = GetEffectiveSpoofAppId();
            bool? forceEgsAuth = null;
            string? cfApiWrapper = null;
            if (ActiveGameManager.Current.Id == GameCatalog.AsaGameId)
            {
                if (Settings.AsaForceEgsAuth)
                    forceEgsAuth = true;

                if (!string.IsNullOrWhiteSpace(Settings.AsaCfApiWrapper))
                    cfApiWrapper = Settings.AsaCfApiWrapper;
            }

            string? workshopDirPath = ActiveGameManager.Current.RuntimeAppId == 346110 ? ActiveGameManager.Current.WorkshopDir : null;
            string? workshopAmPath = ActiveGameManager.Current.RuntimeAppId == 346110 ? Path : null;
            var settings = new TekGameRuntimeSettings("steam", ActiveGameManager.Current.RuntimeAppId, spoofAppId, new Dictionary<uint, string>(ActiveGameManager.Current.RuntimeDlcDisplayNames), [.. instDlc], workshopDirPath, workshopAmPath, forceEgsAuth, cfApiWrapper);

            var data = JsonSerializer.SerializeToUtf8Bytes(settings, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            bool useHighProcessPriority = CanUseHighProcessPriority && HighProcessPriority;
            bool useRunAsAdmin = CanRunAsAdmin && RunAsAdmin;
            LauncherLog.Debug(
                "Preparing launch payload. RuntimeAppId={RuntimeAppId}, SpoofAppId={SpoofAppId}, InstalledDlcCount={InstalledDlcCount}, Args={Args}, HighPriority={HighPriority}, RunAsAdmin={RunAsAdmin}",
                ActiveGameManager.Current.RuntimeAppId,
                spoofAppId,
                instDlc.Count,
                string.Join(' ', args),
                useHighProcessPriority,
                useRunAsAdmin);
            launchLog.AppendLine($"High priority: {useHighProcessPriority}");
            launchLog.AppendLine($"Run as admin: {useRunAsAdmin}");
            launchLog.AppendLine($"Use Spacewar requested: {UseSpacewar}");
            launchLog.AppendLine($"Use Spacewar effective: {spoofAppId == 480}");
            if (forceEgsAuth is true)
                launchLog.AppendLine("ASA runtime option: force_egs_auth=true");
            if (!string.IsNullOrWhiteSpace(cfApiWrapper))
                launchLog.AppendLine($"ASA runtime option: cf_api_wrapper={cfApiWrapper}");
            launchLog.AppendLine($"Arguments: {string.Join(' ', args)}");
            var launchResult = LauncherServices.GameLauncher.Launch(new(ExePath, args, useHighProcessPriority, useRunAsAdmin, data));
            if (!launchResult.Success)
            {
                LauncherLog.Error("Platform launcher failed. Error={Error}", launchResult.ErrorMessage ?? "unknown");
                WriteLaunchAttemptLog(launchLog, $"Launch failed in platform launcher: {launchResult.ErrorMessage}");
                Messages.Show("Error", launchResult.ErrorMessage!);
                return;
            }

            LauncherLog.Information("Launch submitted successfully. GameId={GameId}, Server={Server}", ActiveGameManager.Current.Id, server?.Address ?? "none");
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

    static uint GetEffectiveSpoofAppId() => CanUseSpacewar && UseSpacewar ? 480u : 0;

    static string ResolveExecutablePath()
    {
        string configuredPath = ActiveGameManager.Current.ExePath;
        if (File.Exists(configuredPath))
            return configuredPath;

        if (!string.Equals(ActiveGameManager.Current.Id, GameCatalog.AsaGameId, StringComparison.OrdinalIgnoreCase))
            return configuredPath;

        string rootPath = ActiveGameManager.Current.RootPath;
        foreach (string relativePath in s_asaExeFallbackRelativePaths)
        {
            string candidate = System.IO.Path.Combine(rootPath, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return candidate;
        }

        return configuredPath;
    }

    /// <summary>Game ownership status used to initialize ARK Shellcode.</summary>
    public enum Status
    {
        NotOwned,
        Owned,
        OwnedAndInstalled
    }

    readonly record struct TekGameRuntimeSettings(string Store, uint AppId, uint SpoofAppId, Dictionary<uint, string> Dlc, uint[] InstalledDlc, string? WorkshopDirPath, string? WorkshopAmPath, bool? ForceEgsAuth, string? CfApiWrapper);
}