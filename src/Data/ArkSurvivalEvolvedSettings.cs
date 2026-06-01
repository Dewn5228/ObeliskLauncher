using System.Text.Json.Serialization;

namespace ObeliskLauncher.Data;

/// <summary>Manages Ark Survival Evolved (ASE) specific settings.</summary>
static class ArkSurvivalEvolvedSettings
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
  static readonly List<string> s_launchParameters = new();

  public static IReadOnlyList<string> LaunchParameters => s_launchParameters;

  internal static void Load(Json json)
  {
    s_linuxCompatDataPath = Settings.NormalizeDirectoryPath(json.AseLinuxCompatDataPath);
    s_linuxExtraEnvironmentVariables = Settings.NormalizeText(json.AseLinuxExtraEnvironmentVariables);
    s_linuxLaunchTool = LinuxLaunchToolResolver.NormalizeSelection(json.AseLinuxLaunchTool);
    s_linuxLaunchWrappers = Settings.NormalizeMultilineText(json.AseLinuxLaunchWrappers);
    s_linuxGamescopeArguments = Settings.NormalizeText(json.AseLinuxGamescopeArguments);
    s_linuxGamescopeSharpness = Settings.NormalizeText(json.AseLinuxGamescopeSharpness);
    s_linuxUseGameMode = json.AseLinuxUseGameMode ?? false;
    s_linuxUseGamescope = json.AseLinuxUseGamescope ?? false;
    s_linuxUseGamescopeFsr = json.AseLinuxUseGamescopeFsr ?? false;
    s_linuxUseMangoHud = json.AseLinuxUseMangoHud ?? false;
    s_linuxUseVkBasalt = json.AseLinuxUseVkBasalt ?? false;
    s_linuxUseWineFullscreenFsr = json.AseLinuxUseWineFullscreenFsr ?? false;
    s_linuxWineFullscreenFsrStrength = Settings.NormalizeText(json.AseLinuxWineFullscreenFsrStrength);

    ValidateLinuxLaunchConfiguration();

    // Load ASE launch parameters
    s_launchParameters.Clear();
    if (json.LaunchParameters is not null)
    {
      foreach (string parameter in json.LaunchParameters)
      {
        if (!s_launchParameters.Contains(parameter))
          s_launchParameters.Add(parameter);
      }
    }

    // Populate Game.LaunchParameters for compatibility during transition
    Game.LaunchParameters.Clear();
    foreach (string parameter in s_launchParameters)
    {
      if (!Game.LaunchParameters.Contains(parameter))
        Game.LaunchParameters.Add(parameter);
    }
  }

  internal static void PopulateSaveJson(out string? launchTool, out string? compatDataPath,
      out string? extraEnvironmentVariables, out string? launchWrappers, out bool? useGameMode,
      out bool? useGamescope, out string? gamescopeArguments, out bool? useGamescopeFsr,
      out string? gamescopeSharpness, out bool? useMangoHud, out bool? useVkBasalt,
      out bool? useWineFullscreenFsr, out string? wineFullscreenFsrStrength, out List<string>? launchParameters)
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
    launchParameters = Game.LaunchParameters.Count > 0 ? [.. Game.LaunchParameters] : null;
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

  public static void SetLaunchParameters(IEnumerable<string> launchParameters)
  {
    s_launchParameters.Clear();
    foreach (string parameter in launchParameters)
    {
      if (!s_launchParameters.Contains(parameter))
        s_launchParameters.Add(parameter);
    }

    Game.LaunchParameters.Clear();
    foreach (string parameter in s_launchParameters)
    {
      if (!Game.LaunchParameters.Contains(parameter))
        Game.LaunchParameters.Add(parameter);
    }
  }

  static void ValidateLinuxLaunchConfiguration()
  {
    s_linuxLaunchTool = LinuxLaunchToolResolver.NormalizeSelection(s_linuxLaunchTool);
    s_linuxCompatDataPath = Settings.NormalizeDirectoryPath(s_linuxCompatDataPath);
    s_linuxLaunchWrappers = Settings.NormalizeMultilineText(s_linuxLaunchWrappers);
  }

  /// <summary>Represents ASE-specific settings' JSON object.</summary>
  internal readonly record struct Json(
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
      List<string>? LaunchParameters);
}
