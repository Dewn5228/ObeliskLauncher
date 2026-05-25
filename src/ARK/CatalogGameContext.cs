namespace ObeliskLauncher.ARK;

sealed class CatalogGameContext(string gameId, string rootPath) : IGameContext
{
    readonly GameCatalogEntry _catalog = GameCatalog.GetByGameId(gameId);

    public uint SteamAppId => _catalog.SteamAppId;

    public uint RuntimeAppId => _catalog.RuntimeAppId;

    public uint MainDepotId => _catalog.MainDepotId;

    public uint WorkshopDepotId => _catalog.WorkshopDepotId;

    public string ServerGameDir => _catalog.ServerGameDir;

    public IReadOnlyList<string> AcceptedServerGameDirs => _catalog.AcceptedServerGameDirs;

    public string Id => _catalog.Id;

    public string DisplayName => _catalog.DisplayName;

    public string SteamFolderName => _catalog.SteamFolderName;

    public string ExeFileName => _catalog.ExeFileName;

    public string RootPath { get; } = Path.GetFullPath(rootPath);

    public string ExePath => _catalog.BuildExePath(RootPath);

    public string WorkshopDir => _catalog.BuildWorkshopPath(RootPath);

    public IReadOnlyList<GameDlcInfo> DlcCatalog => _catalog.DlcCatalog;

    public IReadOnlyDictionary<uint, string> RuntimeDlcDisplayNames => _catalog.RuntimeDlcDisplayNames;

    public IReadOnlyDictionary<uint, ulong> PreAquaticaManifestOverrides => _catalog.PreAquaticaManifestOverrides;

    public bool SupportsSpacewarSpoof => _catalog.SupportsSpacewarSpoof;

    public bool IsWindowsBuild => _catalog.IsWindowsBuild;
}
