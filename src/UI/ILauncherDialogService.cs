namespace ObeliskLauncher.UI;

interface ILauncherDialogService
{
    void Show(string type, string message);

    bool ShowOptions(string type, string message);

    void ShowDownloadErr(string name, string url);
}