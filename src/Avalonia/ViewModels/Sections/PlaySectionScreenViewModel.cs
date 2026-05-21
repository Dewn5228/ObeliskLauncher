namespace TEKLauncher.Avalonia.ViewModels;

public sealed class PlaySectionScreenViewModel : LauncherSectionScreenViewModel
{
    readonly string[] _launcherLanguages;
    readonly int[] _launcherLanguageMap;

    static readonly string[] s_gameLanguages =
    [
      "Català",
    "Cesky",
    "Dansk",
    "Deutsch",
    "English",
    "Espanol",
    "Euskara",
    "Suomi",
    "Francais",
    "Magyar",
    "Italiano",
    "日本語",
    "ქართული",
    "한국어",
    "Nederlands",
    "Polski",
    "Português/Brasil",
    "Русский",
    "Svenska",
    "ภาษาไทย",
    "Türkçe",
    "Українська",
    "中文",
    "中文 (简体)",
    "中文 (台湾)"
    ];

    static readonly string[] s_launcherLanguages =
    [
      "English",
    "Español",
    "Français",
    "Nederlands",
    "Português",
    "Ελληνικά",
    "Русский",
    "简体中文"
    ];

    public PlaySectionScreenViewModel()
      : base(LauncherSection.Play)
    {
        (_launcherLanguages, _launcherLanguageMap) = BuildLauncherLanguageOptions();
    }

    public string CurrentGamePath => Game.Path ?? LocManager.GetString(LocCode.NoPathSelected);

    public string? HeroImagePath => UI.CommunismModeWorkflow.GetPlayImagePath();

    public bool HasHeroImage => HeroImagePath is not null;

    public int GameLanguageIndex
    {
        get => Game.Language;
        set => Game.Language = value;
    }

    public IReadOnlyList<string> GameLanguages => s_gameLanguages;

    public int LauncherLanguageIndex
    {
        get
        {
            int currentIndex = LocManager.CurrentLanguageIndex;
            int uiIndex = Array.IndexOf(_launcherLanguageMap, currentIndex);
            return uiIndex >= 0 ? uiIndex : 0;
        }
        set
        {
            if (value < 0 || value >= _launcherLanguageMap.Length)
                return;

            LocManager.SetCurrentLanguage(_launcherLanguageMap[value]);
        }
    }

    public IReadOnlyList<string> LauncherLanguages => _launcherLanguages;

    public bool LauncherLanguageSelectionEnabled => _launcherLanguages.Length > 1;

    public bool LauncherLanguageAvailabilityNoteVisible => !LauncherLanguageSelectionEnabled;

    public string LauncherLanguageAvailabilityNote => "Additional launcher translations are unavailable in this build because the localization files are missing.";

    public string LaunchBehaviorNote => OperatingSystem.IsLinux()
      ? "Linux launches ARK through Steam/Proton. Windows-only elevation behavior is not used on this platform."
      : string.Empty;

    public bool LaunchBehaviorNoteVisible => !RunAsAdminVisible;

    public bool RunAsAdmin
    {
        get => Game.RunAsAdmin;
        set => Game.RunAsAdmin = value;
    }

    public bool RunAsAdminVisible => Game.CanRunAsAdmin;

    public bool UseSpacewar
    {
        get => Game.UseSpacewar;
        set => Game.UseSpacewar = value;
    }

    public bool UseSpacewarVisible => Game.CanUseSpacewar && Steam.App.CurrentUserStatus.GameStatus != Game.Status.NotOwned;

    public Task LaunchAsync() => Task.Run(() => Game.Launch(null));

    public override void Activate()
    {
        OnPropertyChanged(nameof(CurrentGamePath));
        OnPropertyChanged(nameof(GameLanguageIndex));
        OnPropertyChanged(nameof(HeroImagePath));
        OnPropertyChanged(nameof(HasHeroImage));
        OnPropertyChanged(nameof(LaunchBehaviorNote));
        OnPropertyChanged(nameof(LaunchBehaviorNoteVisible));
        OnPropertyChanged(nameof(LauncherLanguageAvailabilityNote));
        OnPropertyChanged(nameof(LauncherLanguageAvailabilityNoteVisible));
        OnPropertyChanged(nameof(LauncherLanguageIndex));
        OnPropertyChanged(nameof(LauncherLanguageSelectionEnabled));
        OnPropertyChanged(nameof(LauncherLanguages));
        OnPropertyChanged(nameof(RunAsAdmin));
        OnPropertyChanged(nameof(RunAsAdminVisible));
        OnPropertyChanged(nameof(UseSpacewar));
        OnPropertyChanged(nameof(UseSpacewarVisible));
    }

    static (string[] Labels, int[] IndexMap) BuildLauncherLanguageOptions()
    {
        var labels = new List<string>(s_launcherLanguages.Length);
        var indexMap = new List<int>(s_launcherLanguages.Length);
        for (int i = 0; i < s_launcherLanguages.Length; i++)
        {
            if (!LocManager.IsLanguageAvailable(i))
                continue;

            labels.Add(s_launcherLanguages[i]);
            indexMap.Add(i);
        }

        if (labels.Count == 0)
        {
            labels.Add(s_launcherLanguages[0]);
            indexMap.Add(0);
        }

        return ([.. labels], [.. indexMap]);
    }
}