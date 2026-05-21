namespace TEKLauncher.Avalonia.ViewModels;

public sealed class AboutSectionScreenViewModel : LauncherSectionScreenViewModel
{
    public AboutSectionScreenViewModel()
      : base(LauncherSection.About)
    {
    }

    public string AppVersion => LauncherBootstrap.Version;

    public string DescriptionBody => LocManager.GetString(LocCode.AboutTabDescription);

    public string KeyFeaturesHeader => LocManager.GetString(LocCode.KeyFeaturesHeader);

    public string KeyFeaturesText => LocManager.GetString(LocCode.KeyFeatures);

    public string LinksHeader => LocManager.GetString(LocCode.Links);
}