using System.Linq;

namespace ObeliskLauncher.UI;

readonly record struct GameOptionsActionResult(string Message, int Severity);

public readonly record struct GameOptionsLaunchParameterDefinition(string Parameter, string TitleCode, string DescriptionCode);

static class GameOptionsWorkflow
{
    static readonly GameOptionsLaunchParameterDefinition[] s_standardParameters =
    [
      new("-d3d10", "launchOptimization.useDirectX10", "launchOptimization.useDirectX10Desc"),
    new("-nosplash", "launchOptimization.noSplashScreen", "launchOptimization.noSplashScreenDesc"),
    new("-nomansky", "launchOptimization.disableSkyDetail", "launchOptimization.disableSkyDetailDesc"),
    new("-nomemorybias", "launchOptimization.noMemoryBias", "launchOptimization.noMemoryBiasDesc"),
    new("-lowmemory", "launchOptimization.lowMemoryMode", "launchOptimization.lowMemoryModeDesc"),
    new("-norhithread", "launchOptimization.noRHIThread", "launchOptimization.noRHIThreadDesc"),
    new("-novsync", "launchOptimization.disableVSync", "launchOptimization.disableVSyncDesc"),
    new("-preventhibernation", "launchOptimization.disableSPHibernation", "launchOptimization.disableSPHibernationDesc")
    ];

    public static bool HasPredefinedLaunchParameters => ActiveGameManager.Current.Id == GameCatalog.AseGameId;

    public static IReadOnlyList<GameOptionsLaunchParameterDefinition> StandardParameters
        => HasPredefinedLaunchParameters ? s_standardParameters : Array.Empty<GameOptionsLaunchParameterDefinition>();

    public static bool HasLaunchParameter(string parameter) => Game.LaunchParameters.Contains(parameter);

    public static string GetCustomLaunchParametersText()
        => HasPredefinedLaunchParameters
            ? string.Join(' ', Game.LaunchParameters.Where(parameter => !IsKnownPredefinedParameter(parameter)))
            : string.Join(' ', Game.LaunchParameters);

    public static void SetCustomLaunchParameters(string text)
    {
        List<string> preservedParameters = [];
        if (HasPredefinedLaunchParameters)
            foreach (string parameter in Game.LaunchParameters)
                if (IsKnownPredefinedParameter(parameter))
                    preservedParameters.Add(parameter);

        Game.LaunchParameters.Clear();
        foreach (string parameter in preservedParameters)
            if (!Game.LaunchParameters.Contains(parameter))
                Game.LaunchParameters.Add(parameter);

        if (string.IsNullOrWhiteSpace(text))
            return;

        foreach (string parameter in text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (!Game.LaunchParameters.Contains(parameter))
                Game.LaunchParameters.Add(parameter);
    }

    public static void SetLaunchParameter(string parameter, bool enabled)
    {
        if (!HasPredefinedLaunchParameters)
            return;

        if (enabled)
        {
            if (!Game.LaunchParameters.Contains(parameter))
                Game.LaunchParameters.Add(parameter);
        }
        else
            Game.LaunchParameters.Remove(parameter);
    }

    public static void EnforcePredefinedLaunchParameterPolicy()
    {
        if (!HasPredefinedLaunchParameters)
            return;
    }

    static bool IsKnownPredefinedParameter(string parameter)
        => Array.IndexOf(Game.StandardLaunchParameters, parameter) != -1;

    public static GameOptionsActionResult FixBloom()
    {
        if (Game.IsRunning)
            return new(Locale.Get("errors.fixBloomFail"), 2);

        string configDirectory = LauncherPlatform.Current.GetGameConfigDirectory(ActiveGameManager.Current.RootPath);
        Directory.CreateDirectory(configDirectory);
        string file = Path.Combine(configDirectory, "Scalability.ini");
        using var writer = new StreamWriter(file);
        for (int i = 0; i < 4; i++)
        {
            string entry = $"[PostProcessQuality@{i}]\r\nr.BloomQuality=1";
            if (i < 3)
                entry += "\r\n";
            writer.Write(entry);
        }

        return new(Locale.Get("errors.fixBloomSuccess"), 1);
    }

    public static async Task<GameOptionsActionResult> UnlockSkinsAsync()
    {
        if (Game.IsRunning)
            return new(Locale.Get("errors.unlockSkinsFail"), 2);

        string file = Path.Combine(LauncherBootstrap.AppDataFolder, "Dw_PlayerLocalData.arkprofile");
        bool success = await Downloader.DownloadFileAsync(file, new EventHandlers(),
          "https://nuclearist.ru/static/obelisklauncher/PlayerLocalData.arkprofile",
          "https://drive.google.com/uc?export=download&id=1YsuoGqf-XOvdg5oneuoPDOVeVN8uRkRF");

        if (!success)
            return new(Locale.Get("modsTab.downloadFailed"), 2);

        string profilesDirectory = LauncherPlatform.Current.GetGameProfilesDirectory(ActiveGameManager.Current.RootPath);
        Directory.CreateDirectory(profilesDirectory);
        File.Move(file, Path.Combine(profilesDirectory, "PlayerLocalData.arkprofile"), true);
        return new(Locale.Get("errors.unlockSkinsSuccess"), 1);
    }
}