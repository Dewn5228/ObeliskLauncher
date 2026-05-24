using System.Text.Json.Serialization;

namespace TEKLauncher.Data;

/// <summary>Manages Ark Survival Ascended (ASA) specific settings.</summary>
static class ArkSurvivalAscendedSettings
{
    static string s_linuxCompatDataPath = string.Empty;
    static string s_linuxExtraEnvironmentVariables = string.Empty;
    static string s_linuxLaunchTool = LinuxLaunchToolResolver.AutomaticId;
    static string s_linuxLaunchWrappers = string.Empty;
    static string s_linuxGamescopeArguments = string.Empty;
    static string s_linuxGamescopeSharpness = string.Empty;
    static bool s_linuxUseGameMode;
    static bool s_linuxUseGamescope;
    static bool s_linuxUseGamescopeFsr;
    static bool s_linuxUseMangoHud;
    static bool s_linuxUseVkBasalt;
    static bool s_linuxUseWineFullscreenFsr;
    static string s_linuxWineFullscreenFsrStrength = string.Empty;

    internal static void Load(Json json)
    {
        s_linuxCompatDataPath = Settings.NormalizeDirectoryPath(json.AsaLinuxCompatDataPath);
        s_linuxExtraEnvironmentVariables = Settings.NormalizeText(json.AsaLinuxExtraEnvironmentVariables);
        s_linuxLaunchTool = LinuxLaunchToolResolver.NormalizeSelection(json.AsaLinuxLaunchTool);
        s_linuxLaunchWrappers = Settings.NormalizeMultilineText(json.AsaLinuxLaunchWrappers);
        s_linuxGamescopeArguments = Settings.NormalizeText(json.AsaLinuxGamescopeArguments);
        s_linuxGamescopeSharpness = Settings.NormalizeText(json.AsaLinuxGamescopeSharpness);
        s_linuxUseGameMode = json.AsaLinuxUseGameMode ?? false;
        s_linuxUseGamescope = json.AsaLinuxUseGamescope ?? false;
        s_linuxUseGamescopeFsr = json.AsaLinuxUseGamescopeFsr ?? false;
        s_linuxUseMangoHud = json.AsaLinuxUseMangoHud ?? false;
        s_linuxUseVkBasalt = json.AsaLinuxUseVkBasalt ?? false;
        s_linuxUseWineFullscreenFsr = json.AsaLinuxUseWineFullscreenFsr ?? false;
        s_linuxWineFullscreenFsrStrength = Settings.NormalizeText(json.AsaLinuxWineFullscreenFsrStrength);

        ValidateLinuxLaunchConfiguration();
    }

    internal static void PopulateSaveJson(out string? launchTool, out string? compatDataPath,
        out string? extraEnvironmentVariables, out string? launchWrappers, out bool? useGameMode,
        out bool? useGamescope, out string? gamescopeArguments, out bool? useGamescopeFsr,
        out string? gamescopeSharpness, out bool? useMangoHud, out bool? useVkBasalt,
        out bool? useWineFullscreenFsr, out string? wineFullscreenFsrStrength)
    {
        launchTool = s_linuxLaunchTool;
        compatDataPath = s_linuxCompatDataPath;
        extraEnvironmentVariables = s_linuxExtraEnvironmentVariables;
        launchWrappers = s_linuxLaunchWrappers;
        useGameMode = s_linuxUseGameMode ? true : null;
        useGamescope = s_linuxUseGamescope ? true : null;
        gamescopeArguments = s_linuxGamescopeArguments;
        useGamescopeFsr = s_linuxUseGamescopeFsr ? true : null;
        gamescopeSharpness = s_linuxGamescopeSharpness;
        useMangoHud = s_linuxUseMangoHud ? true : null;
        useVkBasalt = s_linuxUseVkBasalt ? true : null;
        useWineFullscreenFsr = s_linuxUseWineFullscreenFsr ? true : null;
        wineFullscreenFsrStrength = s_linuxWineFullscreenFsrStrength;
    }

    public static string GetLinuxCompatDataPath()
      => s_linuxCompatDataPath;

    public static void SetLinuxCompatDataPath(string value)
      => s_linuxCompatDataPath = Settings.NormalizeDirectoryPath(value);

    public static string GetLinuxExtraEnvironmentVariables()
      => s_linuxExtraEnvironmentVariables;

    public static void SetLinuxExtraEnvironmentVariables(string value)
      => s_linuxExtraEnvironmentVariables = Settings.NormalizeText(value);

    public static string GetLinuxLaunchTool()
      => s_linuxLaunchTool;

    public static void SetLinuxLaunchTool(string value)
      => s_linuxLaunchTool = LinuxLaunchToolResolver.NormalizeSelection(value);

    public static string GetLinuxLaunchWrappers()
      => s_linuxLaunchWrappers;

    public static void SetLinuxLaunchWrappers(string value)
      => s_linuxLaunchWrappers = Settings.NormalizeMultilineText(value);

    public static string GetLinuxGamescopeArguments()
      => s_linuxGamescopeArguments;

    public static void SetLinuxGamescopeArguments(string value)
      => s_linuxGamescopeArguments = Settings.NormalizeText(value);

    public static string GetLinuxGamescopeSharpness()
      => s_linuxGamescopeSharpness;

    public static void SetLinuxGamescopeSharpness(string value)
      => s_linuxGamescopeSharpness = Settings.NormalizeText(value);

    public static bool GetLinuxUseGameMode()
      => s_linuxUseGameMode;

    public static void SetLinuxUseGameMode(bool value)
      => s_linuxUseGameMode = value;

    public static bool GetLinuxUseGamescope()
      => s_linuxUseGamescope;

    public static void SetLinuxUseGamescope(bool value)
      => s_linuxUseGamescope = value;

    public static bool GetLinuxUseGamescopeFsr()
      => s_linuxUseGamescopeFsr;

    public static void SetLinuxUseGamescopeFsr(bool value)
      => s_linuxUseGamescopeFsr = value;

    public static bool GetLinuxUseMangoHud()
      => s_linuxUseMangoHud;

    public static void SetLinuxUseMangoHud(bool value)
      => s_linuxUseMangoHud = value;

    public static bool GetLinuxUseVkBasalt()
      => s_linuxUseVkBasalt;

    public static void SetLinuxUseVkBasalt(bool value)
      => s_linuxUseVkBasalt = value;

    public static bool GetLinuxUseWineFullscreenFsr()
      => s_linuxUseWineFullscreenFsr;

    public static void SetLinuxUseWineFullscreenFsr(bool value)
      => s_linuxUseWineFullscreenFsr = value;

    public static string GetLinuxWineFullscreenFsrStrength()
      => s_linuxWineFullscreenFsrStrength;

    public static void SetLinuxWineFullscreenFsrStrength(string value)
      => s_linuxWineFullscreenFsrStrength = Settings.NormalizeText(value);

    static void ValidateLinuxLaunchConfiguration()
    {
        s_linuxLaunchTool = LinuxLaunchToolResolver.NormalizeSelection(s_linuxLaunchTool);
        s_linuxCompatDataPath = Settings.NormalizeDirectoryPath(s_linuxCompatDataPath);
        s_linuxLaunchWrappers = Settings.NormalizeMultilineText(s_linuxLaunchWrappers);
    }

    /// <summary>Represents ASA-specific settings' JSON object.</summary>
    internal readonly record struct Json(
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
        string? AsaLinuxWineFullscreenFsrStrength);
}
