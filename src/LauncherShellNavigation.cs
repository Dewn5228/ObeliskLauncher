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

public readonly record struct LauncherSectionInfo(LauncherSection Section, LocCode TitleCode, string Description);

static class LauncherShellNavigation
{
    public static readonly LauncherSectionInfo[] Sections =
    [
      new(LauncherSection.Play, LocCode.PlayTab, "Launch the game and review session-ready status."),
    new(LauncherSection.Servers, LocCode.ServersTab, "Browse servers, favorites, and cluster state."),
    new(LauncherSection.GameOptions, LocCode.GameOptionsTab, "Install, update, validate, and manage the game build."),
    new(LauncherSection.DLC, LocCode.DLCTab, "Review DLC installation state and available updates."),
    new(LauncherSection.Mods, LocCode.ModsTab, "Manage workshop mods, previews, and installed content."),
    new(LauncherSection.LauncherSettings, LocCode.LauncherSettingsTab, "Change launcher paths, behavior, and cleanup options."),
    new(LauncherSection.About, LocCode.AboutTab, "View project information, versioning, and credits.")
    ];

    public static LauncherSectionInfo GetInfo(LauncherSection section) => Sections[(int)section];

    public static LauncherSection SectionFromIndex(int index) => Sections[index].Section;

    public static int IndexOf(LauncherSection section) => (int)section;

    public static bool RequiresSpacewarWarning(LauncherSection section) => Game.CanUseSpacewar && section == LauncherSection.Mods;
}