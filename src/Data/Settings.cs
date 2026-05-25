using System.Linq;

namespace ObeliskLauncher.Data;

/// <summary>Manages global launcher settings and delegates per-game settings to per-game classes.</summary>
static class Settings
{
    const int LinuxPresetSchemaVersion = 2;
    static readonly List<string> s_customLinuxLaunchToolIds = new();
    static readonly List<LinuxLaunchPreset> s_linuxLaunchPresets = new();
    static readonly Dictionary<string, string> s_gamePaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets a value that indicates whether the launcher should close itself after launching the game.</summary>
    public static bool CloseOnGameLaunch { get; set; }
    /// <summary>Gets or sets a value that indicates whether settings file should be deleted rather than saved when shutting down the app.</summary>
    public static bool Delete { get; set; }
    public static IReadOnlyList<string> CustomLinuxLaunchToolIds => s_customLinuxLaunchToolIds;
    public static IReadOnlyList<LinuxLaunchPreset> LinuxLaunchPresets => s_linuxLaunchPresets;
    public static IReadOnlyDictionary<string, string> GamePaths => s_gamePaths;
    public static bool PreAquatica { get; set; }
    public static bool AsaForceEgsAuth { get; set; }
    public static string AsaCfApiWrapper { get; set; } = "apiw.nuclearist.ru";
    public static string AseGamePath
    {
        get => GetGamePath(GameCatalog.AseGameId);
        set => SetGamePath(GameCatalog.AseGameId, value);
    }

    public static string AsaGamePath
    {
        get => GetGamePath(GameCatalog.AsaGameId);
        set => SetGamePath(GameCatalog.AsaGameId, value);
    }
    /// <summary>Loads settings from the file and assigns their values to appropriate classes.</summary>
    public static void Load()
    {
        string settingsFile = Path.Combine(LauncherBootstrap.AppDataFolder, "Settings.json");
        if (!File.Exists(settingsFile))
            return;
        using var stream = File.OpenRead(settingsFile);
        Json json;
        try { json = JsonSerializer.Deserialize<Json>(stream)!; }
        catch { return; }

        // Load global settings
        s_gamePaths.Clear();
        if (json.GamePaths is not null)
            foreach ((string gameId, string path) in json.GamePaths)
                SetGamePath(gameId, path);

        // Legacy path slots are mapped into dynamic game path storage.
        if (!string.IsNullOrWhiteSpace(json.AseGamePath))
            SetGamePath(GameCatalog.AseGameId, json.AseGamePath);
        if (!string.IsNullOrWhiteSpace(json.AsaGamePath))
            SetGamePath(GameCatalog.AsaGameId, json.AsaGamePath);

        if (!string.IsNullOrWhiteSpace(json.ActiveGameType) && !string.IsNullOrWhiteSpace(json.ActiveGamePath))
            SetGamePath(json.ActiveGameType, json.ActiveGamePath);

        if (!string.IsNullOrWhiteSpace(json.ActiveGameType) && !string.IsNullOrWhiteSpace(json.ActiveGamePath))
            ActiveGameManager.Configure(json.ActiveGameType, json.ActiveGamePath);
        CloseOnGameLaunch = json.CloseOnGameLaunch;
        Game.HighProcessPriority = json.HighProcessPriority;
        PreAquatica = json.PreAquatica;
        Game.RunAsAdmin = json.RunAsAdmin;
        Game.UseSpacewar = json.UseSpacewar;
        Game.Language = json.GameLanguage;
        AsaForceEgsAuth = json.AsaForceEgsAuth;
        if (json.AsaCfApiWrapper is not null)
        {
            AsaCfApiWrapper = NormalizeText(json.AsaCfApiWrapper);
            if (AsaCfApiWrapper == "https://83374.apiw.nuclearist.ru/")
                AsaCfApiWrapper = "apiw.nuclearist.ru";
        }
        else
        {
            AsaCfApiWrapper = "apiw.nuclearist.ru";
        }

        // Load custom tools and presets
        s_customLinuxLaunchToolIds.Clear();
        if (json.CustomLinuxLaunchTools is not null)
            foreach (string toolId in json.CustomLinuxLaunchTools)
                RegisterCustomLinuxLaunchTool(toolId);

        s_linuxLaunchPresets.Clear();
        if (json.LinuxLaunchPresets is not null)
            foreach (LinuxLaunchPreset preset in json.LinuxLaunchPresets)
                SaveLinuxLaunchPreset(NormalizeLinuxLaunchPreset(preset, json.LinuxLaunchPresetSchemaVersion ?? 1));

        // Load per-game settings
        var aseJson = new ArkSurvivalEvolvedSettings.Json(
            json.AseLinuxLaunchTool,
            json.AseLinuxCompatDataPath,
            json.AseLinuxExtraEnvironmentVariables,
            json.AseLinuxLaunchWrappers,
            json.AseLinuxUseGameMode,
            json.AseLinuxUseGamescope,
            json.AseLinuxGamescopeArguments,
            json.AseLinuxUseGamescopeFsr,
            json.AseLinuxGamescopeSharpness,
            json.AseLinuxUseMangoHud,
            json.AseLinuxUseVkBasalt,
            json.AseLinuxUseWineFullscreenFsr,
            json.AseLinuxWineFullscreenFsrStrength,
            json.LaunchParameters);
        ArkSurvivalEvolvedSettings.Load(aseJson);

        var asaJson = new ArkSurvivalAscendedSettings.Json(
            json.AsaLinuxLaunchTool,
            json.AsaLinuxCompatDataPath,
            json.AsaLinuxExtraEnvironmentVariables,
            json.AsaLinuxLaunchWrappers,
            json.AsaLinuxUseGameMode,
            json.AsaLinuxUseGamescope,
            json.AsaLinuxGamescopeArguments,
            json.AsaLinuxUseGamescopeFsr,
            json.AsaLinuxGamescopeSharpness,
            json.AsaLinuxUseMangoHud,
            json.AsaLinuxUseVkBasalt,
            json.AsaLinuxUseWineFullscreenFsr,
            json.AsaLinuxWineFullscreenFsrStrength);
        ArkSurvivalAscendedSettings.Load(asaJson);

        var capabilities = LauncherServices.GameLauncher.Capabilities;
        if (!capabilities.SupportsHighProcessPriority)
            Game.HighProcessPriority = false;
        if (!capabilities.SupportsRunAsAdmin)
            Game.RunAsAdmin = false;
        if (!capabilities.SupportsSpoofAppId)
            Game.UseSpacewar = false;

        if (!string.IsNullOrWhiteSpace(json.LauncherLanguage))
            Locale.CurrentLanguage = json.LauncherLanguage;
    }
    /// <summary>Saves settings into the JSON file.</summary>
    public static void Save()
    {
        string settingsFile = Path.Combine(LauncherBootstrap.AppDataFolder, "Settings.json");
        if (Delete)
        {
            if (File.Exists(settingsFile))
                File.Delete(settingsFile);
            return;
        }

        string? activeGameType = ActiveGameManager.IsConfigured ? ActiveGameManager.Current.Id : null;
        string? activeGamePath = ActiveGameManager.IsConfigured ? ActiveGameManager.Current.RootPath : null;

        // Get per-game settings from ASE
        ArkSurvivalEvolvedSettings.PopulateSaveJson(
            out var aseLinuxLaunchTool,
            out var aseLinuxCompatDataPath,
            out var aseLinuxExtraEnvironmentVariables,
            out var aseLinuxLaunchWrappers,
            out var aseLinuxUseGameMode,
            out var aseLinuxUseGamescope,
            out var aseLinuxGamescopeArguments,
            out var aseLinuxUseGamescopeFsr,
            out var aseLinuxGamescopeSharpness,
            out var aseLinuxUseMangoHud,
            out var aseLinuxUseVkBasalt,
            out var aseLinuxUseWineFullscreenFsr,
            out var aseLinuxWineFullscreenFsrStrength,
            out var launchParameters);

        // Get per-game settings from ASA
        ArkSurvivalAscendedSettings.PopulateSaveJson(
            out var asaLinuxLaunchTool,
            out var asaLinuxCompatDataPath,
            out var asaLinuxExtraEnvironmentVariables,
            out var asaLinuxLaunchWrappers,
            out var asaLinuxUseGameMode,
            out var asaLinuxUseGamescope,
            out var asaLinuxGamescopeArguments,
            out var asaLinuxUseGamescopeFsr,
            out var asaLinuxGamescopeSharpness,
            out var asaLinuxUseMangoHud,
            out var asaLinuxUseVkBasalt,
            out var asaLinuxUseWineFullscreenFsr,
            out var asaLinuxWineFullscreenFsrStrength);

        var json = new Json(
            CloseOnGameLaunch,
            Game.HighProcessPriority,
            PreAquatica,
            Game.RunAsAdmin,
            Game.UseSpacewar,
            Game.Language,
            AsaForceEgsAuth,
            AsaCfApiWrapper,
            null,
            null,
            new Dictionary<string, string>(s_gamePaths, StringComparer.OrdinalIgnoreCase),
            activeGameType,
            activeGamePath,
            aseLinuxLaunchTool,
            aseLinuxCompatDataPath,
            aseLinuxExtraEnvironmentVariables,
            aseLinuxLaunchWrappers,
            aseLinuxUseGameMode,
            aseLinuxUseGamescope,
            aseLinuxGamescopeArguments,
            aseLinuxUseGamescopeFsr,
            aseLinuxGamescopeSharpness,
            aseLinuxUseMangoHud,
            aseLinuxUseVkBasalt,
            aseLinuxUseWineFullscreenFsr,
            aseLinuxWineFullscreenFsrStrength,
            asaLinuxLaunchTool,
            asaLinuxCompatDataPath,
            asaLinuxExtraEnvironmentVariables,
            asaLinuxLaunchWrappers,
            asaLinuxUseGameMode,
            asaLinuxUseGamescope,
            asaLinuxGamescopeArguments,
            asaLinuxUseGamescopeFsr,
            asaLinuxGamescopeSharpness,
            asaLinuxUseMangoHud,
            asaLinuxUseVkBasalt,
            asaLinuxUseWineFullscreenFsr,
            asaLinuxWineFullscreenFsrStrength,
            Locale.CurrentLanguage,
            launchParameters,
            [.. s_customLinuxLaunchToolIds],
            [.. s_linuxLaunchPresets],
            LinuxPresetSchemaVersion);

        using var stream = File.Create(settingsFile);
        JsonSerializer.Serialize(stream, json, new JsonSerializerOptions() { WriteIndented = true });
    }

    public static LinuxLaunchPreset CreateLinuxLaunchPreset(string name, string? gameId = null) => new()
    {
        Name = name.Trim(),
        LaunchTool = LinuxLaunchToolResolver.NormalizeSelection(GetLinuxLaunchTool(gameId)),
        PrefixPath = GetLinuxCompatDataPath(gameId),
        ExtraEnvironmentVariables = GetLinuxExtraEnvironmentVariables(gameId),
        LaunchWrappers = GetLinuxLaunchWrappers(gameId),
        UseGameMode = GetLinuxUseGameMode(gameId),
        UseGamescope = GetLinuxUseGamescope(gameId),
        GamescopeArguments = GetLinuxGamescopeArguments(gameId),
        UseGamescopeFsr = GetLinuxUseGamescopeFsr(gameId),
        GamescopeSharpness = GetLinuxGamescopeSharpness(gameId),
        UseMangoHud = GetLinuxUseMangoHud(gameId),
        UseVkBasalt = GetLinuxUseVkBasalt(gameId),
        UseWineFullscreenFsr = GetLinuxUseWineFullscreenFsr(gameId),
        WineFullscreenFsrStrength = GetLinuxWineFullscreenFsrStrength(gameId)
    };

    public static bool DeleteLinuxLaunchPreset(string name)
    {
        int index = s_linuxLaunchPresets.FindIndex(preset => preset.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return false;

        s_linuxLaunchPresets.RemoveAt(index);
        return true;
    }

    public static void RegisterCustomLinuxLaunchTool(string? toolId)
    {
        string normalizedToolId = LinuxLaunchToolResolver.NormalizeSelection(toolId);
        if (normalizedToolId.Equals(LinuxLaunchToolResolver.AutomaticId, StringComparison.OrdinalIgnoreCase))
            return;

        if (s_customLinuxLaunchToolIds.FindIndex(existingToolId => existingToolId.Equals(normalizedToolId, StringComparison.OrdinalIgnoreCase)) < 0)
            s_customLinuxLaunchToolIds.Add(normalizedToolId);
    }

    public static void SaveLinuxLaunchPreset(LinuxLaunchPreset preset)
    {
        if (string.IsNullOrWhiteSpace(preset.Name))
            return;

        preset.Name = preset.Name.Trim();
        preset.LaunchTool = LinuxLaunchToolResolver.NormalizeSelection(preset.LaunchTool);
        RegisterCustomLinuxLaunchTool(preset.LaunchTool);
        preset.PrefixPath = NormalizeDirectoryPath(preset.PrefixPath);
        preset.ExtraEnvironmentVariables = NormalizeText(preset.ExtraEnvironmentVariables);
        preset.LaunchWrappers = NormalizeMultilineText(preset.LaunchWrappers);
        preset.GamescopeArguments = NormalizeText(preset.GamescopeArguments);
        preset.GamescopeSharpness = NormalizeText(preset.GamescopeSharpness);
        preset.WineFullscreenFsrStrength = NormalizeText(preset.WineFullscreenFsrStrength);

        int existingIndex = s_linuxLaunchPresets.FindIndex(item => item.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
            s_linuxLaunchPresets[existingIndex] = preset;
        else
            s_linuxLaunchPresets.Add(preset);

        s_linuxLaunchPresets.Sort((left, right) => StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name));
    }

    internal static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;

    internal static string NormalizeMultilineText(string? value)
      => string.Join('\n', (value ?? string.Empty)
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(static line => !string.IsNullOrWhiteSpace(line)));

    internal static string NormalizeDirectoryPath(string? value)
    {
        string normalized = NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        try
        {
            return Path.GetFullPath(normalized);
        }
        catch
        {
            return string.Empty;
        }
    }

    static LinuxLaunchPreset NormalizeLinuxLaunchPreset(LinuxLaunchPreset preset, int schemaVersion)
    {
        if (schemaVersion < LinuxPresetSchemaVersion)
        {
            preset.PrefixPath = NormalizeDirectoryPath(preset.PrefixPath);
            preset.LaunchWrappers = NormalizeMultilineText(preset.LaunchWrappers);
        }

        return preset;
    }

    static bool IsAsaGameId(string? gameId)
            => string.Equals(gameId, GameCatalog.AsaGameId, StringComparison.OrdinalIgnoreCase);

    public static string GetGamePath(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            return string.Empty;

        return s_gamePaths.TryGetValue(gameId, out string? gamePath) ? gamePath : string.Empty;
    }

    public static void SetGamePath(string gameId, string? path)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            return;

        string normalized = NormalizeDirectoryPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
            s_gamePaths.Remove(gameId);
        else
            s_gamePaths[gameId] = normalized;
    }

    static string ResolveScopedGameId(string? gameId = null)
    {
        if (!string.IsNullOrWhiteSpace(gameId))
            return gameId;

        if (ActiveGameManager.IsConfigured)
            return ActiveGameManager.Current.Id;

        foreach (GameCatalogEntry game in GameCatalog.AllGames)
            if (!string.IsNullOrWhiteSpace(GetGamePath(game.Id)))
                return game.Id;

        return GameCatalog.DefaultGameId;
    }

    /// <summary>
    /// Converts a Steam App ID to the corresponding game ID string.
    /// </summary>
    public static string GetGameIdBySteamAppId(string steamAppId)
    {
        if (uint.TryParse(steamAppId, out uint parsedSteamAppId) && GameCatalog.TryGetGameIdBySteamAppId(parsedSteamAppId, out string gameId))
            return gameId;

        return ResolveScopedGameId(null);
    }

    public static string GetLinuxCompatDataPath(string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        return IsAsaGameId(resolvedGameId)
            ? ArkSurvivalAscendedSettings.GetLinuxCompatDataPath()
            : ArkSurvivalEvolvedSettings.GetLinuxCompatDataPath();
    }

    public static void SetLinuxCompatDataPath(string value, string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        if (IsAsaGameId(resolvedGameId))
            ArkSurvivalAscendedSettings.SetLinuxCompatDataPath(value);
        else
            ArkSurvivalEvolvedSettings.SetLinuxCompatDataPath(value);
    }

    public static string GetLinuxExtraEnvironmentVariables(string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        return IsAsaGameId(resolvedGameId)
            ? ArkSurvivalAscendedSettings.GetLinuxExtraEnvironmentVariables()
            : ArkSurvivalEvolvedSettings.GetLinuxExtraEnvironmentVariables();
    }

    public static void SetLinuxExtraEnvironmentVariables(string value, string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        if (IsAsaGameId(resolvedGameId))
            ArkSurvivalAscendedSettings.SetLinuxExtraEnvironmentVariables(value);
        else
            ArkSurvivalEvolvedSettings.SetLinuxExtraEnvironmentVariables(value);
    }

    public static string GetLinuxLaunchTool(string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        return IsAsaGameId(resolvedGameId)
            ? ArkSurvivalAscendedSettings.GetLinuxLaunchTool()
            : ArkSurvivalEvolvedSettings.GetLinuxLaunchTool();
    }

    public static void SetLinuxLaunchTool(string value, string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        if (IsAsaGameId(resolvedGameId))
            ArkSurvivalAscendedSettings.SetLinuxLaunchTool(value);
        else
            ArkSurvivalEvolvedSettings.SetLinuxLaunchTool(value);
    }

    public static string GetLinuxLaunchWrappers(string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        return IsAsaGameId(resolvedGameId)
            ? ArkSurvivalAscendedSettings.GetLinuxLaunchWrappers()
            : ArkSurvivalEvolvedSettings.GetLinuxLaunchWrappers();
    }

    public static void SetLinuxLaunchWrappers(string value, string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        if (IsAsaGameId(resolvedGameId))
            ArkSurvivalAscendedSettings.SetLinuxLaunchWrappers(value);
        else
            ArkSurvivalEvolvedSettings.SetLinuxLaunchWrappers(value);
    }

    public static string GetLinuxGamescopeArguments(string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        return IsAsaGameId(resolvedGameId)
            ? ArkSurvivalAscendedSettings.GetLinuxGamescopeArguments()
            : ArkSurvivalEvolvedSettings.GetLinuxGamescopeArguments();
    }

    public static void SetLinuxGamescopeArguments(string value, string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        if (IsAsaGameId(resolvedGameId))
            ArkSurvivalAscendedSettings.SetLinuxGamescopeArguments(value);
        else
            ArkSurvivalEvolvedSettings.SetLinuxGamescopeArguments(value);
    }

    public static string GetLinuxGamescopeSharpness(string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        return IsAsaGameId(resolvedGameId)
            ? ArkSurvivalAscendedSettings.GetLinuxGamescopeSharpness()
            : ArkSurvivalEvolvedSettings.GetLinuxGamescopeSharpness();
    }

    public static void SetLinuxGamescopeSharpness(string value, string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        if (IsAsaGameId(resolvedGameId))
            ArkSurvivalAscendedSettings.SetLinuxGamescopeSharpness(value);
        else
            ArkSurvivalEvolvedSettings.SetLinuxGamescopeSharpness(value);
    }

    public static bool GetLinuxUseGameMode(string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        return IsAsaGameId(resolvedGameId)
            ? ArkSurvivalAscendedSettings.GetLinuxUseGameMode()
            : ArkSurvivalEvolvedSettings.GetLinuxUseGameMode();
    }

    public static void SetLinuxUseGameMode(bool value, string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        if (IsAsaGameId(resolvedGameId))
            ArkSurvivalAscendedSettings.SetLinuxUseGameMode(value);
        else
            ArkSurvivalEvolvedSettings.SetLinuxUseGameMode(value);
    }

    public static bool GetLinuxUseGamescope(string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        return IsAsaGameId(resolvedGameId)
            ? ArkSurvivalAscendedSettings.GetLinuxUseGamescope()
            : ArkSurvivalEvolvedSettings.GetLinuxUseGamescope();
    }

    public static void SetLinuxUseGamescope(bool value, string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        if (IsAsaGameId(resolvedGameId))
            ArkSurvivalAscendedSettings.SetLinuxUseGamescope(value);
        else
            ArkSurvivalEvolvedSettings.SetLinuxUseGamescope(value);
    }

    public static bool GetLinuxUseGamescopeFsr(string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        return IsAsaGameId(resolvedGameId)
            ? ArkSurvivalAscendedSettings.GetLinuxUseGamescopeFsr()
            : ArkSurvivalEvolvedSettings.GetLinuxUseGamescopeFsr();
    }

    public static void SetLinuxUseGamescopeFsr(bool value, string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        if (IsAsaGameId(resolvedGameId))
            ArkSurvivalAscendedSettings.SetLinuxUseGamescopeFsr(value);
        else
            ArkSurvivalEvolvedSettings.SetLinuxUseGamescopeFsr(value);
    }

    public static bool GetLinuxUseMangoHud(string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        return IsAsaGameId(resolvedGameId)
            ? ArkSurvivalAscendedSettings.GetLinuxUseMangoHud()
            : ArkSurvivalEvolvedSettings.GetLinuxUseMangoHud();
    }

    public static void SetLinuxUseMangoHud(bool value, string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        if (IsAsaGameId(resolvedGameId))
            ArkSurvivalAscendedSettings.SetLinuxUseMangoHud(value);
        else
            ArkSurvivalEvolvedSettings.SetLinuxUseMangoHud(value);
    }

    public static bool GetLinuxUseVkBasalt(string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        return IsAsaGameId(resolvedGameId)
            ? ArkSurvivalAscendedSettings.GetLinuxUseVkBasalt()
            : ArkSurvivalEvolvedSettings.GetLinuxUseVkBasalt();
    }

    public static void SetLinuxUseVkBasalt(bool value, string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        if (IsAsaGameId(resolvedGameId))
            ArkSurvivalAscendedSettings.SetLinuxUseVkBasalt(value);
        else
            ArkSurvivalEvolvedSettings.SetLinuxUseVkBasalt(value);
    }

    public static bool GetLinuxUseWineFullscreenFsr(string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        return IsAsaGameId(resolvedGameId)
            ? ArkSurvivalAscendedSettings.GetLinuxUseWineFullscreenFsr()
            : ArkSurvivalEvolvedSettings.GetLinuxUseWineFullscreenFsr();
    }

    public static void SetLinuxUseWineFullscreenFsr(bool value, string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        if (IsAsaGameId(resolvedGameId))
            ArkSurvivalAscendedSettings.SetLinuxUseWineFullscreenFsr(value);
        else
            ArkSurvivalEvolvedSettings.SetLinuxUseWineFullscreenFsr(value);
    }

    public static string GetLinuxWineFullscreenFsrStrength(string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        return IsAsaGameId(resolvedGameId)
            ? ArkSurvivalAscendedSettings.GetLinuxWineFullscreenFsrStrength()
            : ArkSurvivalEvolvedSettings.GetLinuxWineFullscreenFsrStrength();
    }

    public static void SetLinuxWineFullscreenFsrStrength(string value, string? gameId = null)
    {
        string resolvedGameId = ResolveScopedGameId(gameId);
        if (IsAsaGameId(resolvedGameId))
            ArkSurvivalAscendedSettings.SetLinuxWineFullscreenFsrStrength(value);
        else
            ArkSurvivalEvolvedSettings.SetLinuxWineFullscreenFsrStrength(value);
    }

    /// <summary>Represents global settings' JSON object.</summary>
    readonly record struct Json(
        bool CloseOnGameLaunch,
        bool HighProcessPriority,
        bool PreAquatica,
        bool RunAsAdmin,
        bool UseSpacewar,
        int GameLanguage,
        bool AsaForceEgsAuth,
        string? AsaCfApiWrapper,
        string? AseGamePath,
        string? AsaGamePath,
        Dictionary<string, string>? GamePaths,
        string? ActiveGameType,
        string? ActiveGamePath,
        string? AseLinuxLaunchTool,
        string? AseLinuxCompatDataPath,
        string? AseLinuxExtraEnvironmentVariables,
        string? AseLinuxLaunchWrappers,
        bool? AseLinuxUseGameMode,
        bool? AseLinuxUseGamescope,
        string? AseLinuxGamescopeArguments,
        bool? AseLinuxUseGamescopeFsr,
        string? AseLinuxGamescopeSharpness,
        bool? AseLinuxUseMangoHud,
        bool? AseLinuxUseVkBasalt,
        bool? AseLinuxUseWineFullscreenFsr,
        string? AseLinuxWineFullscreenFsrStrength,
        string? AsaLinuxLaunchTool,
        string? AsaLinuxCompatDataPath,
        string? AsaLinuxExtraEnvironmentVariables,
        string? AsaLinuxLaunchWrappers,
        bool? AsaLinuxUseGameMode,
        bool? AsaLinuxUseGamescope,
        string? AsaLinuxGamescopeArguments,
        bool? AsaLinuxUseGamescopeFsr,
        string? AsaLinuxGamescopeSharpness,
        bool? AsaLinuxUseMangoHud,
        bool? AsaLinuxUseVkBasalt,
        bool? AsaLinuxUseWineFullscreenFsr,
        string? AsaLinuxWineFullscreenFsrStrength,
        string? LauncherLanguage,
        List<string>? LaunchParameters,
        List<string>? CustomLinuxLaunchTools,
        List<LinuxLaunchPreset>? LinuxLaunchPresets,
        int? LinuxLaunchPresetSchemaVersion);
}