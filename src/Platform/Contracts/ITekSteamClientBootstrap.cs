namespace ObeliskLauncher.Platform;

readonly record struct TekSteamClientBootstrapResult(bool Success, bool RestartRequired, string? ErrorMessage, string? DownloadName, string? DownloadUrl, string? WarningMessage);

interface ITekSteamClientBootstrap
{
    Task<TekSteamClientBootstrapResult> InitializeAsync(string gamePath);
}