using System.Runtime.CompilerServices;
using System.Linq;

namespace ObeliskLauncher.ARK;

/// <summary>Represents a DLC of the game.</summary>
class DLC
{
    static uint s_cachedAppId;
    static string? s_cachedRootPath;
    static DLC[] s_cachedList = [];

    /// <summary>Current status of the DLC.</summary>
    Status _status;
    readonly string _rootPath;
    /// <summary>Path to the Content folder of the DLC.</summary>
    readonly string _path;
    /// <summary>Path to the SeekFreeContent folder of the DLC.</summary>
    readonly string _sfcPath;
    /// <summary>Path to the .umap file of the DLC.</summary>
    readonly string _umapPath;
    public readonly uint AppId;
    /// <summary>ID of Steam depot that stores the DLC content.</summary>
    public readonly uint DepotId;
    /// <summary>Code of the map provided by the DLC.</summary>
    public readonly MapCode Code;
    /// <summary>List of all DLC supported by current active game.</summary>
    public static IReadOnlyList<DLC> List => GetList();
    /// <summary>Gets a value that indicates whether the DLC is installed.</summary>
    public bool IsInstalled
    {
        get
        {
            bool result = File.Exists(_umapPath);
            if (Code == MapCode.Genesis)
                result |= File.Exists(Path.Combine(_rootPath, "ShooterGame", "Content", "Maps", "Genesis2", "Gen2.umap"));
            if (!result && Code == MapCode.Mod && DepotId != 0)
                result = IsInstalledByManifest();
            if (!result)
                result = HasInstalledFiles();
            return result;
        }
    }

    unsafe bool IsInstalledByManifest()
    {
        try
        {
            var itemId = new TEKSteamClient.ItemId { AppId = ActiveGameManager.Current.SteamAppId, DepotId = DepotId, WorkshopItemId = 0 };
            var desc = LauncherServices.TekSteamClient.GetItemDesc(&itemId);
            if (desc is not null && desc->CurrentManifestId != 0)
                return true;

            if (AppId != 0 && AppId != itemId.AppId)
            {
                itemId.AppId = AppId;
                desc = LauncherServices.TekSteamClient.GetItemDesc(&itemId);
                if (desc is not null && desc->CurrentManifestId != 0)
                    return true;
            }
        }
        catch (Exception ex)
        {
            LauncherLog.Warning("DLC manifest query failed for AppId={AppId}, DepotId={DepotId}. Error={Error}",
                ActiveGameManager.Current.SteamAppId, DepotId, ex.Message);
        }

        try
        {
            string depotKey = DepotId.ToString(CultureInfo.InvariantCulture);
            foreach (uint appId in GetManifestAppIdCandidates())
            {
                string appManifestPath = Path.GetFullPath(Path.Combine(_rootPath, "..", "..", $"appmanifest_{appId}.acf"));
                if (!File.Exists(appManifestPath))
                    continue;

                string content = File.ReadAllText(appManifestPath);
                var root = VdfParser.Parse(content);
                if (root["AppState"]?["InstalledDepots"]?[depotKey] is not null)
                    return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            LauncherLog.Warning("DLC manifest file parse failed for DepotId={DepotId}. Error={Error}", DepotId, ex.Message);
            return false;
        }
    }

    IEnumerable<uint> GetManifestAppIdCandidates()
    {
        uint primary = ActiveGameManager.Current.SteamAppId;
        yield return primary;
        if (AppId != 0 && AppId != primary)
            yield return AppId;
    }

    bool HasInstalledFiles()
    {
        try
        {
            if (Directory.Exists(_path))
            {
                if (Directory.EnumerateFiles(_path, "*.umap", SearchOption.AllDirectories).Any())
                    return true;
                if (Directory.EnumerateFiles(_path, "*.pak", SearchOption.AllDirectories).Any())
                    return true;
            }

            if (Directory.Exists(_sfcPath) && Directory.EnumerateFiles(_sfcPath, "*.pak", SearchOption.AllDirectories).Any())
                return true;
        }
        catch
        {
        }

        return false;
    }

    public void SyncInstalledStatus()
    {
        if (_status is Status.Updating or Status.Deleting)
            return;

        bool isInstalled = IsInstalled;
        if (!isInstalled)
            CurrentStatus = Status.NotInstalled;
        else if (_status == Status.NotInstalled)
            CurrentStatus = Status.Installed;
    }

    /// <summary>Gets the display name of the DLC.</summary>
    public string Name { get; }
    public event Action<DLC>? StatusChanged;
    /// <summary>Gets or sets current status of the DLC.</summary>
    public Status CurrentStatus
    {
        get => _status;
        set
        {
            _status = value;
            StatusChanged?.Invoke(this);
        }
    }

    /// <summary>Initializes a new DLC object based on its primary parameters.</summary>
    /// <param name="name">Display name of the DLC.</param>
    /// <param name="appId">Steam app ID of the DLC.</param>
    /// <param name="depotId">ID of Steam depot that stores the DLC content.</param>
    /// <param name="isMod"><see langword="true"/> if the DLC folders are located in Mods directory rather than Maps; otherwise, <see langword="false"/>.</param>
    /// <param name="has_P"><see langword="true"/> if the name of DLC's .umap file is suffixed with "_P"; otherwise, <see langword="false"/>.</param>
    DLC(string rootPath, GameDlcInfo info)
    {
        _rootPath = rootPath;
        string contentDirectory = info.IsModContent ? "Mods" : "Maps";
        string folderName = string.IsNullOrWhiteSpace(info.FolderNameOverride) ? info.Name.Replace(" ", string.Empty) : info.FolderNameOverride;
        _path = Path.Combine(rootPath, "ShooterGame", "Content", contentDirectory, folderName);
        _sfcPath = Path.Combine(rootPath, "ShooterGame", "SeekFreeContent", contentDirectory, folderName);
        string mapFileName = string.IsNullOrWhiteSpace(info.MapFileNameOverride) ? info.Code.ToString() : info.MapFileNameOverride;
        if (info.HasPPostfix)
            mapFileName += "_P";
        _umapPath = Path.Combine(_path, $"{mapFileName}.umap");
        AppId = info.AppId;
        DepotId = info.DepotId;
        Name = info.Name;
        Code = info.Code;
        _status = IsInstalled ? Status.Installed : Status.NotInstalled;
    }
    /// <summary>Uninstalls the DLC.</summary>
    public unsafe void Delete()
    {
        CurrentStatus = Status.Deleting;
        var itemId = new TEKSteamClient.ItemId { AppId = ActiveGameManager.Current.SteamAppId, DepotId = DepotId, WorkshopItemId = 0 };
        TEKSteamClient.AmItemDesc* desc = null;
        foreach (uint candidateAppId in GetManifestAppIdCandidates())
        {
            var candidateItemId = new TEKSteamClient.ItemId { AppId = candidateAppId, DepotId = DepotId, WorkshopItemId = 0 };
            var candidateDesc = LauncherServices.TekSteamClient.GetItemDesc(&candidateItemId);
            if (candidateDesc is null)
                continue;

            if (desc is null)
            {
                desc = candidateDesc;
                itemId = candidateItemId;
            }

            if (candidateDesc->CurrentManifestId != 0)
            {
                desc = candidateDesc;
                itemId = candidateItemId;
                break;
            }
        }
        var prevStatus = _status;
        CurrentStatus = Status.Deleting;
        if (desc == null)
            DeleteInstalledFiles();
        else
        {
            if (desc->Status.HasFlag(TEKSteamClient.AmItemStatus.Job))
            {
                if (!LauncherServices.TekSteamClient.CancelJob(ref Unsafe.AsRef<TEKSteamClient.AmItemDesc>(desc)).Success)
                {
                    LauncherLog.Warning("DLC delete: failed to cancel active job. Name={Name}, DepotId={DepotId}", Name, DepotId);
                    CurrentStatus = prevStatus;
                    return;
                }
            }

            if (desc->CurrentManifestId == 0)
                DeleteInstalledFiles();
            else
            {
                if (!LauncherServices.TekSteamClient.RunJob(in itemId, ulong.MaxValue, false, null, out desc).Success)
                {
                    LauncherLog.Warning("DLC delete: Steam uninstall job failed. Name={Name}, AppId={AppId}, DepotId={DepotId}", Name, itemId.AppId, DepotId);
                    CurrentStatus = prevStatus;
                    return;
                }

                if (IsInstalled)
                    LauncherLog.Warning("DLC delete: install detection still true after uninstall job, deleting leftover files. Name={Name}, DepotId={DepotId}", Name, DepotId);
                DeleteInstalledFiles();
            }
        }

        CurrentStatus = Status.NotInstalled;
    }

    void DeleteInstalledFiles()
    {
        TryDeleteDirectory(_path, "Content");
        TryDeleteDirectory(_sfcPath, "SeekFreeContent");
        if (Code == MapCode.Genesis)
        {
            string gen2Folder = Path.Combine(_rootPath, "ShooterGame", "Content", "Maps", "Genesis2");
            TryDeleteDirectory(gen2Folder, "Genesis2Content");
            gen2Folder = Path.Combine(_rootPath, "ShooterGame", "SeekFreeContent", "Maps", "Genesis2");
            TryDeleteDirectory(gen2Folder, "Genesis2SeekFree");
        }
    }

    void TryDeleteDirectory(string path, string kind)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            LauncherLog.Warning("DLC delete: failed to delete directory. Name={Name}, Kind={Kind}, Path={Path}, Error={Error}", Name, kind, path, ex.Message);
            throw;
        }
    }

    /// <summary>Retrieves a DLC by its map code.</summary>
    /// <param name="code">Code of the map whose DLC needs to be retrieved.</param>
    /// <returns>A DLC that provides the specified map.</returns>
    public static DLC Get(MapCode code)
    {
        if (code == MapCode.Genesis2)
            code = MapCode.Genesis;
        foreach (var dlc in List)
            if (dlc.Code == code)
                return dlc;
        throw new InvalidOperationException($"DLC for map code '{code}' is not available for active game '{ActiveGameManager.Current.Id}'.");
    }

    static DLC[] GetList()
    {
        IGameContext context = ActiveGameManager.Current;
        uint appId = context.SteamAppId;
        string rootPath = context.RootPath;
        if (s_cachedAppId == appId && string.Equals(s_cachedRootPath, rootPath, StringComparison.OrdinalIgnoreCase) && s_cachedList.Length > 0)
            return s_cachedList;

        s_cachedAppId = appId;
        s_cachedRootPath = rootPath;
        s_cachedList = BuildList(rootPath, context.DlcCatalog);
        return s_cachedList;
    }

    static DLC[] BuildList(string rootPath, IReadOnlyList<GameDlcInfo> catalog)
    {
        var items = new List<DLC>(catalog.Count);
        foreach (GameDlcInfo dlc in catalog)
            items.Add(new(rootPath, dlc));

        return [.. items];
    }
    /// <summary>Defines DLC status codes.</summary>
    public enum Status
    {
        NotInstalled,
        Installed,
        CheckingForUpdates,
        UpdateAvailable,
        Updating,
        Deleting
    }
}