namespace TEKLauncher.UI;

public readonly record struct DlcActionResult(string Message, int Severity);

static class DlcWorkflow
{
    public static string GetStatusText(DLC.Status status) => LocManager.GetString(status switch
    {
        DLC.Status.NotInstalled => LocCode.NotInstalled,
        DLC.Status.Installed => LocCode.Installed,
        DLC.Status.CheckingForUpdates => LocCode.CheckingForUpdates,
        DLC.Status.UpdateAvailable => LocCode.UpdateAvailable,
        DLC.Status.Updating => LocCode.Updating,
        DLC.Status.Deleting => LocCode.Deleting,
        _ => LocCode.NA
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
            return new(LocManager.GetString(LocCode.UpdateFailGameRunning), 2);

        await Task.Run(dlc.Delete);
        return new($"{dlc.Name}: {GetStatusText(dlc.CurrentStatus)}", dlc.CurrentStatus == DLC.Status.NotInstalled ? 1 : 2);
    }

    public static DlcActionResult GetInstallNotYetMigratedMessage() => new("DLC install/update still depends on the old updater window and has not been ported to Avalonia yet.", 0);

    public static DlcActionResult GetValidateNotYetMigratedMessage() => new("DLC validation still depends on the old updater window and has not been ported to Avalonia yet.", 0);
}