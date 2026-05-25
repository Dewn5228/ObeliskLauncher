namespace ObeliskLauncher.Platform;

interface ITekSteamClientService
{
    bool IsLoaded { get; }
    TEKSteamClient.LibCtx? Context { get; }
    TEKSteamClient.AppManager? AppManager { get; }
    TEKSteamClient.Error CheckForUpdates(int timeoutMs);
    TEKSteamClient.Error CancelJob(ref TEKSteamClient.AmItemDesc itemDesc);
    long EstimateDeltaDiskSpace(nint delta);
    unsafe TEKSteamClient.AmItemDesc* GetItemDesc(TEKSteamClient.ItemId* id);
    void Initialize(TEKSteamClient.LibCtx ctx, TEKSteamClient.AppManager appManager);
    void LockItemDescs();
    void Close();
    void PauseJob(ref TEKSteamClient.AmItemDesc itemDesc);
    unsafe TEKSteamClient.Error RunJob(in TEKSteamClient.ItemId itemId, ulong manifestId, bool forceVerify, TEKSteamClient.AmJobUpdFunc? updHandler, out TEKSteamClient.AmItemDesc* itemDesc);
    void UnlockItemDescs();
}