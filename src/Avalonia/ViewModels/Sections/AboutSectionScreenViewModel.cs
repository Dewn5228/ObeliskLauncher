namespace TEKLauncher.Avalonia.ViewModels;

public sealed class AboutSectionScreenViewModel : LauncherSectionScreenViewModel
{
    public AboutSectionScreenViewModel()
      : base(LauncherSection.About)
    {
    }

    public string AppVersion => LauncherBootstrap.Version;

    public string DescriptionBody => Locale.Get("tabsDescriptions.about");

    public string KeyFeaturesHeader => Locale.Get("launcherSettingsTab.keyFeaturesHeader");

    public string KeyFeaturesText => Locale.Get("launcherSettingsTab.keyFeatures");

    public string LinksHeader => Locale.Get("common.links");

    public override void Activate() => RefreshLocale();

    public override void RefreshLocale()
    {
        OnPropertyChanged(nameof(DescriptionBody));
        OnPropertyChanged(nameof(KeyFeaturesHeader));
        OnPropertyChanged(nameof(KeyFeaturesText));
        OnPropertyChanged(nameof(LinksHeader));
    }
}