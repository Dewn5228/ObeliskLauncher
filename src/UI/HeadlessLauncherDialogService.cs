using System.Runtime.InteropServices;

namespace ObeliskLauncher.UI;

sealed class HeadlessLauncherDialogService : ILauncherDialogService
{
    const uint MbIconInformation = 0x40;
    const uint MbIconError = 0x10;

    public void Show(string type, string message) => ShowNative(type, message);

    public bool ShowOptions(string type, string message)
    {
        ShowNative(type, message);
        return false;
    }

    public void ShowDownloadErr(string name, string url)
    {
        string message = $"The launcher was unable to download {name}. Open the download page and place the file into the launcher data folder before restarting the launcher.";
        ShowNative("common.error", $"{message}{Environment.NewLine}{url}");
    }

    static void ShowNative(string type, string message)
    {
        string title = Locale.Get(type);
        LauncherLog.Warning("Headless dialog [{Title}] {Message}", title, message);

        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine($"{title}: {message}");
            return;
        }

        uint icon = type.Contains("error", StringComparison.OrdinalIgnoreCase) ? MbIconError : MbIconInformation;
        MessageBoxW(IntPtr.Zero, message, title, icon);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
