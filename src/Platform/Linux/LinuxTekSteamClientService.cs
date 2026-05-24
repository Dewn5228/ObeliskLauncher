namespace TEKLauncher.Platform;

sealed class LinuxTekSteamClientService : ITekSteamClientService
{
    public bool IsLoaded => Context is not null && AppManager is not null;

    public TEKSteamClient.LibCtx? Context { get; private set; }

    public TEKSteamClient.AppManager? AppManager { get; private set; }

    public TEKSteamClient.Error CheckForUpdates(int timeoutMs) => AppManager!.CheckForUpdates(timeoutMs);

    public TEKSteamClient.Error CancelJob(ref TEKSteamClient.AmItemDesc itemDesc) => AppManager!.CancelJob(ref itemDesc);

    public long EstimateDeltaDiskSpace(nint delta) => TEKSteamClient.DeltaEstimateDiskSpace(delta);

    public unsafe TEKSteamClient.AmItemDesc* GetItemDesc(TEKSteamClient.ItemId* id)
    {
        TEKSteamClient.AppManager? appManager = AppManager;
        if (appManager is null)
            return null;

        return appManager.GetItemDesc(id);
    }

    public void Initialize(TEKSteamClient.LibCtx ctx, TEKSteamClient.AppManager appManager)
    {
        Close();
        Context = ctx;
        AppManager = appManager;
    }

    public void LockItemDescs() => AppManager!.LockItemDescs();

    public void Close()
    {
        AppManager?.Close();
        Context?.Close();
        AppManager = null;
        Context = null;
    }

    public void PauseJob(ref TEKSteamClient.AmItemDesc itemDesc) => TEKSteamClient.AppManager.PauseJob(ref itemDesc);

    public unsafe TEKSteamClient.Error RunJob(in TEKSteamClient.ItemId itemId, ulong manifestId, bool forceVerify, TEKSteamClient.AmJobUpdFunc? updHandler, out TEKSteamClient.AmItemDesc* itemDesc)
      => AppManager!.RunJob(in itemId, manifestId, forceVerify, updHandler, out itemDesc);

    public void UnlockItemDescs() => AppManager!.UnlockItemDescs();
}