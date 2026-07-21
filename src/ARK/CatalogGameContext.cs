namespace ObeliskLauncher.ARK;

sealed class CatalogGameContext(string gameId, string rootPath) : IGameContext
{
    GameCatalogEntry Catalog => GameCatalog.GetByGameId(gameId);

    public uint SteamAppId => Catalog.SteamAppId;

    public uint RuntimeAppId => Catalog.RuntimeAppId;

    public uint MainDepotId => Catalog.MainDepotId;

    public uint WorkshopDepotId => Catalog.WorkshopDepotId;

    public string ServerGameDir => Catalog.ServerGameDir;

    public IReadOnlyList<string> AcceptedServerGameDirs => Catalog.AcceptedServerGameDirs;

    public string Id => Catalog.Id;

    public string DisplayName => Catalog.DisplayName;

    public string SteamFolderName => Catalog.SteamFolderName;

    public string ExeFileName => Catalog.ExeFileName;

    public string RootPath { get; } = Path.GetFullPath(rootPath);

    public string ExePath => Catalog.BuildExePath(RootPath);

    public string WorkshopDir => Catalog.BuildWorkshopPath(RootPath);

    public IReadOnlyList<GameDlcInfo> DlcCatalog => Catalog.DlcCatalog;

    public IReadOnlyDictionary<uint, string> RuntimeDlcDisplayNames => Catalog.RuntimeDlcDisplayNames;

    public IReadOnlyDictionary<uint, ulong> PreAquaticaManifestOverrides => Catalog.PreAquaticaManifestOverrides;

    public bool SupportsSpacewarSpoof => Catalog.SupportsSpacewarSpoof;

    public bool IsWindowsBuild => Catalog.IsWindowsBuild;
}
