namespace TEKLauncher.UI;

public readonly record struct DlcActionResult(string Message, int Severity);

static class DlcWorkflow
{
    public static string GetStatusText(DLC.Status status) => Locale.Get(status switch
    {
        DLC.Status.NotInstalled => "common.notInstalled",
        DLC.Status.Installed => "common.installed",
        DLC.Status.CheckingForUpdates => "common.checkingForUpdates",
        DLC.Status.UpdateAvailable => "common.updateAvailable",
        DLC.Status.Updating => "common.updating",
        DLC.Status.Deleting => "common.deleting",
        _ => "common.na"
    });

    public static string GetStatusColor(DLC.Status status) => status switch
    {
        DLC.Status.NotInstalled => "#D4573B",
        DLC.Status.Installed => "#0AA63E",
        _ => "#D49B38"
    };

    public static async Task<DlcActionResult> DeleteAsync(DLC dlc)
    {
        if (Game.IsRunning)
            return new(Locale.Get("errors.updateFailGameRunning"), 2);

        await Task.Run(dlc.Delete);
        return new($"{dlc.Name}: {GetStatusText(dlc.CurrentStatus)}", dlc.CurrentStatus == DLC.Status.NotInstalled ? 1 : 2);
    }

    public static DlcActionResult GetInstallNotYetMigratedMessage() => new("DLC install/update still depends on the old updater window and has not been ported to Avalonia yet.", 0);

    public static DlcActionResult GetValidateNotYetMigratedMessage() => new("DLC validation still depends on the old updater window and has not been ported to Avalonia yet.", 0);
}