namespace TEKLauncher.Platform;

interface ILauncherPlatform
{
    string AppDataFolder { get; }
    string GetGameConfigDirectory(string gamePath);
    string GetGameProfilesDirectory(string gamePath);
    long GetDiskFreeSpace(string directory);
    string? GetLastLaunchedVersion();
    void SetLastLaunchedVersion(string version);
    int? GetSteamProcessId();
    string? GetSteamInstallPath();
    string? GetSteamClientDllPath();
    bool TryLoadModule(string modulePath);
}