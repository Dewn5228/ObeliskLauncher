namespace TEKLauncher.ARK;

interface IGameContext
{
    uint SteamAppId { get; }
    uint RuntimeAppId { get; }
    uint MainDepotId { get; }
    uint WorkshopDepotId { get; }
    string ServerGameDir { get; }
    IReadOnlyList<string> AcceptedServerGameDirs { get; }
    string Id { get; }
    string DisplayName { get; }
    string SteamFolderName { get; }
    string ExeFileName { get; }
    string RootPath { get; }
    string ExePath { get; }
    string WorkshopDir { get; }
    IReadOnlyList<GameDlcInfo> DlcCatalog { get; }
    IReadOnlyDictionary<uint, string> RuntimeDlcDisplayNames { get; }
    IReadOnlyDictionary<uint, ulong> PreAquaticaManifestOverrides { get; }
    bool SupportsSpacewarSpoof { get; }
    bool IsWindowsBuild { get; }
}