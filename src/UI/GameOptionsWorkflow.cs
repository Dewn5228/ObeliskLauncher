namespace TEKLauncher.UI;

readonly record struct GameOptionsActionResult(string Message, int Severity);

public readonly record struct GameOptionsLaunchParameterDefinition(string Parameter, LocCode TitleCode, LocCode DescriptionCode);

static class GameOptionsWorkflow
{
    static readonly GameOptionsLaunchParameterDefinition[] s_standardParameters =
    [
      new("-d3d10", LocCode.UseDirectX10, LocCode.UseDirectX10Desc),
    new("-nosplash", LocCode.NoSplashScreen, LocCode.NoSplashScreenDesc),
    new("-nomansky", LocCode.DisableSkyDetail, LocCode.DisableSkyDetailDesc),
    new("-nomemorybias", LocCode.NoMemoryBias, LocCode.NoMemoryBiasDesc),
    new("-lowmemory", LocCode.LowMemoryMode, LocCode.LowMemoryModeDesc),
    new("-norhithread", LocCode.NoRHIThread, LocCode.NoRHIThreadDesc),
    new("-novsync", LocCode.DisableVSync, LocCode.DisableVSyncDesc),
    new("-preventhibernation", LocCode.DisableSPHibernation, LocCode.DisableSPHibernationDesc)
    ];

    public static IReadOnlyList<GameOptionsLaunchParameterDefinition> StandardParameters => s_standardParameters;

    public static bool HasLaunchParameter(string parameter) => Game.LaunchParameters.Contains(parameter);

    public static string GetCustomLaunchParametersText() => string.Join(' ', Game.LaunchParameters.FindAll(parameter => Array.IndexOf(Game.StandardLaunchParameters, parameter) == -1));

    public static void SetCustomLaunchParameters(string text)
    {
        Game.LaunchParameters.RemoveAll(parameter => Array.IndexOf(Game.StandardLaunchParameters, parameter) == -1);
        if (string.IsNullOrWhiteSpace(text))
            return;

        foreach (string parameter in text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (!Game.LaunchParameters.Contains(parameter))
                Game.LaunchParameters.Add(parameter);
    }

    public static void SetLaunchParameter(string parameter, bool enabled)
    {
        if (enabled)
        {
            if (!Game.LaunchParameters.Contains(parameter))
                Game.LaunchParameters.Add(parameter);
        }
        else
            Game.LaunchParameters.Remove(parameter);
    }

    public static GameOptionsActionResult FixBloom()
    {
        if (Game.IsRunning)
            return new(LocManager.GetString(LocCode.FixBloomFail), 2);

        if (string.IsNullOrWhiteSpace(Game.Path))
            return new(LocManager.GetString(LocCode.NoPathSelected), 2);

        string configDirectory = LauncherPlatform.Current.GetGameConfigDirectory(Game.Path);
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

        return new(LocManager.GetString(LocCode.FixBloomSuccess), 1);
    }

    public static async Task<GameOptionsActionResult> UnlockSkinsAsync()
    {
        if (Game.IsRunning)
            return new(LocManager.GetString(LocCode.UnlockSkinsFail), 2);

        if (string.IsNullOrWhiteSpace(Game.Path))
            return new(LocManager.GetString(LocCode.NoPathSelected), 2);

        string file = Path.Combine(LauncherBootstrap.AppDataFolder, "Dw_PlayerLocalData.arkprofile");
        bool success = await Downloader.DownloadFileAsync(file, new EventHandlers(),
          "https://nuclearist.ru/static/teklauncher/PlayerLocalData.arkprofile",
          "https://drive.google.com/uc?export=download&id=1YsuoGqf-XOvdg5oneuoPDOVeVN8uRkRF");

        if (!success)
            return new(LocManager.GetString(LocCode.DownloadFailed), 2);

        string profilesDirectory = LauncherPlatform.Current.GetGameProfilesDirectory(Game.Path);
        Directory.CreateDirectory(profilesDirectory);
        File.Move(file, Path.Combine(profilesDirectory, "PlayerLocalData.arkprofile"), true);
        return new(LocManager.GetString(LocCode.UnlockSkinsSuccess), 1);
    }
}