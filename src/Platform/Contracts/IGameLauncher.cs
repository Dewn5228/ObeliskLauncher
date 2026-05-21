namespace TEKLauncher.Platform;

readonly record struct GameLaunchCapabilities(bool SupportsHighProcessPriority, bool SupportsRunAsAdmin, bool SupportsSpoofAppId);

readonly record struct GameLaunchRequest(string ExecutablePath, IReadOnlyList<string> Arguments, bool HighProcessPriority, bool RunAsAdmin, byte[] RuntimeSettings);

readonly record struct GameLaunchResult(bool Success, string? ErrorMessage);

interface IGameLauncher
{
    GameLaunchCapabilities Capabilities { get; }
    bool DirectXInstalled { get; }
    GameLaunchResult Launch(in GameLaunchRequest request);
}