namespace TEKLauncher.Data;

/// <summary>Manages launcher settings.</summary>
static class Settings
{
    static readonly List<string> s_customLinuxLaunchToolIds = new();
    static readonly List<LinuxLaunchPreset> s_linuxLaunchPresets = new();

    /// <summary>Gets or sets a value that indicates whether the launcher should close itself after launching the game.</summary>
    public static bool CloseOnGameLaunch { get; set; }
    /// <summary>Gets or sets a value that indicates whether communism mode is enabled.</summary>
    public static bool CommunismMode { get; set; }
    /// <summary>Gets or sets a value that indicates whether settings file should be deleted rather than saved when shutting down the app.</summary>
    public static bool Delete { get; set; }
    public static IReadOnlyList<string> CustomLinuxLaunchToolIds => s_customLinuxLaunchToolIds;
    public static string LinuxCompatDataPath { get; set; } = string.Empty;
    public static string LinuxExtraEnvironmentVariables { get; set; } = string.Empty;
    public static string LinuxLaunchTool { get; set; } = LinuxLaunchToolResolver.AutomaticId;
    public static string LinuxLaunchWrappers { get; set; } = string.Empty;
    public static IReadOnlyList<LinuxLaunchPreset> LinuxLaunchPresets => s_linuxLaunchPresets;
    public static string LinuxGamescopeArguments { get; set; } = string.Empty;
    public static string LinuxGamescopeSharpness { get; set; } = string.Empty;
    public static bool LinuxUseGameMode { get; set; }
    public static bool LinuxUseGamescope { get; set; }
    public static bool LinuxUseGamescopeFsr { get; set; }
    public static bool LinuxUseMangoHud { get; set; }
    public static bool LinuxUseVkBasalt { get; set; }
    public static bool LinuxUseWineFullscreenFsr { get; set; }
    public static string LinuxWineFullscreenFsrStrength { get; set; } = string.Empty;
    public static bool PreAquatica { get; set; }
    /// <summary>Loads settings from the file and assigns their values to appropriate properties in static classes.</summary>
    public static void Load()
    {
        string settingsFile = Path.Combine(LauncherBootstrap.AppDataFolder, "Settings.json");
        if (!File.Exists(settingsFile))
            return;
        using var stream = File.OpenRead(settingsFile);
        Json json;
        try { json = JsonSerializer.Deserialize<Json>(stream)!; }
        catch { return; }
        CloseOnGameLaunch = json.CloseOnGameLaunch;
        CommunismMode = json.CommunismMode;
        Game.HighProcessPriority = json.HighProcessPriority;
        PreAquatica = json.PreAquatica;
        Game.RunAsAdmin = json.RunAsAdmin;
        Game.UseSpacewar = json.UseSpacewar;
        Game.Language = json.GameLanguage;
        Game.Path = json.ARKPath;
        s_customLinuxLaunchToolIds.Clear();
        if (json.CustomLinuxLaunchTools is not null)
            foreach (string toolId in json.CustomLinuxLaunchTools)
                RegisterCustomLinuxLaunchTool(toolId);
        LinuxCompatDataPath = NormalizeText(json.LinuxCompatDataPath);
        LinuxExtraEnvironmentVariables = NormalizeText(json.LinuxExtraEnvironmentVariables);
        LinuxLaunchTool = LinuxLaunchToolResolver.NormalizeSelection(json.LinuxLaunchTool);
        LinuxLaunchWrappers = NormalizeText(json.LinuxLaunchWrappers);
        LinuxGamescopeArguments = NormalizeText(json.LinuxGamescopeArguments);
        LinuxGamescopeSharpness = NormalizeText(json.LinuxGamescopeSharpness);
        LinuxUseGameMode = json.LinuxUseGameMode;
        LinuxUseGamescope = json.LinuxUseGamescope;
        LinuxUseGamescopeFsr = json.LinuxUseGamescopeFsr;
        LinuxUseMangoHud = json.LinuxUseMangoHud;
        LinuxUseVkBasalt = json.LinuxUseVkBasalt;
        LinuxUseWineFullscreenFsr = json.LinuxUseWineFullscreenFsr;
        LinuxWineFullscreenFsrStrength = NormalizeText(json.LinuxWineFullscreenFsrStrength);
        s_linuxLaunchPresets.Clear();
        if (json.LinuxLaunchPresets is not null)
            foreach (LinuxLaunchPreset preset in json.LinuxLaunchPresets)
                SaveLinuxLaunchPreset(preset);
        if (json.LaunchParameters is not null)
            foreach (string parameter in json.LaunchParameters)
                if (!Game.LaunchParameters.Contains(parameter))
                    Game.LaunchParameters.Add(parameter);

        var capabilities = LauncherServices.GameLauncher.Capabilities;
        if (!capabilities.SupportsHighProcessPriority)
            Game.HighProcessPriority = false;
        if (!capabilities.SupportsRunAsAdmin)
            Game.RunAsAdmin = false;
        if (!capabilities.SupportsSpoofAppId)
            Game.UseSpacewar = false;

        LocManager.CurrentLanguage = json.LauncherLanguage;
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
        var json = new Json(
            CloseOnGameLaunch,
            CommunismMode,
            Game.HighProcessPriority,
            PreAquatica,
            Game.RunAsAdmin,
            Game.UseSpacewar,
            Game.Language,
            Game.Path,
            LinuxLaunchTool,
            LocManager.CurrentLanguage,
            Game.LaunchParameters,
            [.. s_customLinuxLaunchToolIds],
            LinuxCompatDataPath,
            LinuxExtraEnvironmentVariables,
            LinuxLaunchWrappers,
            LinuxUseGameMode,
            LinuxUseGamescope,
            LinuxGamescopeArguments,
            LinuxUseGamescopeFsr,
            LinuxGamescopeSharpness,
            LinuxUseMangoHud,
            LinuxUseVkBasalt,
            LinuxUseWineFullscreenFsr,
            LinuxWineFullscreenFsrStrength,
            [.. s_linuxLaunchPresets]);
        using var stream = File.Create(settingsFile);
        JsonSerializer.Serialize(stream, json, new JsonSerializerOptions() { WriteIndented = true });
    }

    public static void ApplyLinuxLaunchPreset(LinuxLaunchPreset preset)
    {
        LinuxLaunchTool = LinuxLaunchToolResolver.NormalizeSelection(preset.LaunchTool);
        RegisterCustomLinuxLaunchTool(LinuxLaunchTool);
        LinuxCompatDataPath = NormalizeText(preset.PrefixPath);
        LinuxExtraEnvironmentVariables = NormalizeText(preset.ExtraEnvironmentVariables);
        LinuxLaunchWrappers = NormalizeText(preset.LaunchWrappers);
        LinuxUseGameMode = preset.UseGameMode;
        LinuxUseGamescope = preset.UseGamescope;
        LinuxGamescopeArguments = NormalizeText(preset.GamescopeArguments);
        LinuxUseGamescopeFsr = preset.UseGamescopeFsr;
        LinuxGamescopeSharpness = NormalizeText(preset.GamescopeSharpness);
        LinuxUseMangoHud = preset.UseMangoHud;
        LinuxUseVkBasalt = preset.UseVkBasalt;
        LinuxUseWineFullscreenFsr = preset.UseWineFullscreenFsr;
        LinuxWineFullscreenFsrStrength = NormalizeText(preset.WineFullscreenFsrStrength);
    }

    public static LinuxLaunchPreset CreateLinuxLaunchPreset(string name) => new()
    {
        Name = name.Trim(),
        LaunchTool = LinuxLaunchToolResolver.NormalizeSelection(LinuxLaunchTool),
        PrefixPath = LinuxCompatDataPath,
        ExtraEnvironmentVariables = LinuxExtraEnvironmentVariables,
        LaunchWrappers = LinuxLaunchWrappers,
        UseGameMode = LinuxUseGameMode,
        UseGamescope = LinuxUseGamescope,
        GamescopeArguments = LinuxGamescopeArguments,
        UseGamescopeFsr = LinuxUseGamescopeFsr,
        GamescopeSharpness = LinuxGamescopeSharpness,
        UseMangoHud = LinuxUseMangoHud,
        UseVkBasalt = LinuxUseVkBasalt,
        UseWineFullscreenFsr = LinuxUseWineFullscreenFsr,
        WineFullscreenFsrStrength = LinuxWineFullscreenFsrStrength
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
        preset.PrefixPath = NormalizeText(preset.PrefixPath);
        preset.ExtraEnvironmentVariables = NormalizeText(preset.ExtraEnvironmentVariables);
        preset.LaunchWrappers = NormalizeText(preset.LaunchWrappers);
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

    static string NormalizeText(string? value) => value?.Trim() ?? string.Empty;

    /// <summary>Represents settings' JSON object.</summary>
    readonly record struct Json(bool CloseOnGameLaunch, bool CommunismMode, bool HighProcessPriority, bool PreAquatica, bool RunAsAdmin, bool UseSpacewar, int GameLanguage, string? ARKPath, string? LinuxLaunchTool, string? LauncherLanguage, List<string>? LaunchParameters, List<string>? CustomLinuxLaunchTools, string? LinuxCompatDataPath, string? LinuxExtraEnvironmentVariables, string? LinuxLaunchWrappers, bool LinuxUseGameMode, bool LinuxUseGamescope, string? LinuxGamescopeArguments, bool LinuxUseGamescopeFsr, string? LinuxGamescopeSharpness, bool LinuxUseMangoHud, bool LinuxUseVkBasalt, bool LinuxUseWineFullscreenFsr, string? LinuxWineFullscreenFsrStrength, List<LinuxLaunchPreset>? LinuxLaunchPresets);
}