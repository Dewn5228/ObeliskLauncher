namespace TEKLauncher.Data;

public sealed class LinuxLaunchPreset
{
    public string Name { get; set; } = string.Empty;

    public string LaunchTool { get; set; } = Platform.LinuxLaunchToolResolver.AutomaticId;

    public string? PrefixPath { get; set; }

    public string? ExtraEnvironmentVariables { get; set; }

    public string? LaunchWrappers { get; set; }

    public bool UseGameMode { get; set; }

    public bool UseGamescope { get; set; }

    public string? GamescopeArguments { get; set; }

    public bool UseGamescopeFsr { get; set; }

    public string? GamescopeSharpness { get; set; }

    public bool UseMangoHud { get; set; }

    public bool UseVkBasalt { get; set; }

    public bool UseWineFullscreenFsr { get; set; }

    public string? WineFullscreenFsrStrength { get; set; }
}