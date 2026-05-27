namespace ObeliskLauncher;

public enum LauncherSection
{
    Play,
    Servers,
    GameOptions,
    DLC,
    Mods,
    GlobalSettings,
    AseSettings,
    AsaSettings,
    About
}

public readonly record struct LauncherSectionInfo(LauncherSection Section, string TitleCode, string Description);

static class LauncherShellNavigation
{
    public static readonly LauncherSectionInfo[] Sections =
    [
        new(LauncherSection.Play, "tabs.play", "tabsDescriptions.play"),
        new(LauncherSection.Servers, "tabs.servers", "tabsDescriptions.servers"),
        new(LauncherSection.GameOptions, "tabs.gameOptions", "tabsDescriptions.gameOptions"),
        new(LauncherSection.DLC, "tabs.dlc", "tabsDescriptions.dlc"),
        new(LauncherSection.Mods, "tabs.mods", "tabsDescriptions.mods"),
        new(LauncherSection.GlobalSettings, "tabs.launcherSettings", "tabsDescriptions.launcherSettings"),
        new(LauncherSection.AseSettings, "tabs.launcherSettings", "tabsDescriptions.launcherSettings"),
        new(LauncherSection.AsaSettings, "tabs.launcherSettings", "tabsDescriptions.launcherSettings"),
        new(LauncherSection.About, "tabs.about", "tabsDescriptions.about")
    ];

    public static LauncherSectionInfo GetInfo(LauncherSection section) => Sections[(int)section];

    public static bool RequiresSpacewarWarning(LauncherSection section) => Game.CanUseSpacewar && section == LauncherSection.Mods;
}
