namespace ObeliskLauncher.UI;

/// <summary>Manages message windows.</summary>
class Messages
{
    /// <summary>Displays a message window with OK button.</summary>
    /// <param name="type">Type of the message as well as name of the icon and title.</param>
    /// <param name="message">Message text displayed in the window.</param>
    public static void Show(string type, string message) => LauncherDialogService.Current.Show(type, message);
    /// <summary>Displays a message window with two options (Yes and No) and returns the result of user's choice.</summary>
    /// <param name="type">Type of the message as well as name of the icon and title.</param>
    /// <param name="message">Message text displayed in the window.</param>
    public static bool ShowOptions(string type, string message) => LauncherDialogService.Current.ShowOptions(type, message);
    public static void ShowDownloadErr(string name, string url) => LauncherDialogService.Current.ShowDownloadErr(name, url);
}