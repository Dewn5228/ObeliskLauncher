namespace TEKLauncher;

public enum LauncherSection
{
    Play,
    Servers,
    GameOptions,
    DLC,
    Mods,
    LauncherSettings,
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
    new(LauncherSection.LauncherSettings, "tabs.launcherSettings", "tabsDescriptions.launcherSettings"),
    new(LauncherSection.About, "tabs.about", "tabsDescriptions.about")
    ];

    public static LauncherSectionInfo GetInfo(LauncherSection section) => Sections[(int)section];

    public static LauncherSection SectionFromIndex(int index) => Sections[index].Section;

    public static int IndexOf(LauncherSection section) => (int)section;

    public static bool RequiresSpacewarWarning(LauncherSection section) => Game.CanUseSpacewar && section == LauncherSection.Mods;
}