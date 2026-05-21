namespace TEKLauncher.Avalonia.ViewModels;

public enum ShellNoticeActionKind
{
    None,
    OpenLauncherReleasePage,
    OpenGameUpdater,
    OpenDlcSection
}

public sealed class ShellNoticeViewModel
{
    public required ShellNoticeActionKind ActionKind { get; init; }

    public string? ActionLabel { get; init; }

    public bool HasAction => !string.IsNullOrWhiteSpace(ActionLabel) && ActionKind != ShellNoticeActionKind.None;

    public required string Message { get; init; }
}