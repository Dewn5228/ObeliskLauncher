using Microsoft.Win32;

namespace ObeliskLauncher.Platform;

sealed class WindowsLauncherPlatform : ILauncherPlatform
{
    const string LauncherRegistryKey = @"SOFTWARE\ObeliskLauncher";
    const string SteamRegistryKey = @"SOFTWARE\WOW6432Node\Valve\Steam";
    const string SteamActiveProcessKey = @"SOFTWARE\Valve\Steam\ActiveProcess";

    public string AppDataFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Obelisk Launcher");

    public string GetGameConfigDirectory(string gamePath) => Path.Combine(gamePath, "ShooterGame", "Saved", "Config", "WindowsNoEditor");

    public string GetGameProfilesDirectory(string gamePath) => Path.Combine(gamePath, "ShooterGame", "Saved", "LocalProfiles");

    public long GetDiskFreeSpace(string directory) => WinAPI.GetDiskFreeSpace(directory);

    public string? GetLastLaunchedVersion() => (string?)Registry.LocalMachine.OpenSubKey(LauncherRegistryKey)?.GetValue("LastLaunchedVersion");

    public void SetLastLaunchedVersion(string version) => Registry.LocalMachine.CreateSubKey(LauncherRegistryKey).SetValue("LastLaunchedVersion", version);

    public int? GetSteamProcessId() => (int?)Registry.LocalMachine.OpenSubKey(SteamRegistryKey)?.GetValue("SteamPID");

    public string? GetSteamInstallPath() => (string?)Registry.LocalMachine.OpenSubKey(SteamRegistryKey)?.GetValue("InstallPath");

    public string? GetSteamClientDllPath() => (string?)Registry.CurrentUser.OpenSubKey(SteamActiveProcessKey)?.GetValue("SteamClientDll64");

    public bool TryLoadModule(string modulePath) => WinAPI.LoadLibraryExW(modulePath, IntPtr.Zero, 0x8) != IntPtr.Zero;
}