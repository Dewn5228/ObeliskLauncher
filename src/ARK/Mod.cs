using System.Runtime.CompilerServices;

namespace ObeliskLauncher.ARK;

/// <summary>Represents a mod of the game.</summary>
class Mod
{
    /// <summary>Current status of the mod.</summary>
    Status _status;
    /// <summary>Steam published file ID of the mod.</summary>
    public readonly ulong Id;
    /// <summary>Path to the folder that contains compressed files of the mod.</summary>
    public readonly string CompressedFolderPath;
    /// <summary>Path to the .mod file of the mod.</summary>
    public readonly string ModFilePath;
    /// <summary>Path to the folder that contains uncompressed files of the mod used by the game.</summary>
    public readonly string ModsFolderPath;
    /// <summary>Internal name of the mod extracted from its mod.info file.</summary>
    public readonly string Name;
    /// <summary>List of all mods recognized by the launcher.</summary>
    public static readonly List<Mod> List = new();
    public static event Action? ListInitialized;
    public static event Action? DetailsUpdated;
    public static event Action? UpdatesAvailable;
    /// <summary>Gets or sets current status of the mod.</summary>
    public Status CurrentStatus
    {
        get => _status;
        set
        {
            _status = value;
            StatusChanged?.Invoke(this);
        }
    }
    /// <summary>Gets or sets Steam workshop details of the mod.</summary>
    public ModDetails Details { get; set; }
    public event Action<Mod>? StatusChanged;
    /// <summary>Path to the directory that stores compressed file fodlers for all mods.</summary>
    public static string? CompressedModsDirectory { get; private set; }
    /// <summary>Initializes a new mod object with specified ID.</summary>
    /// <param name="id">Steam published file ID of the mod.</param>
    public Mod(ulong id)
    {
        IGameContext game = ActiveGameManager.Current;
        Id = id;
        CompressedFolderPath = Path.Combine(CompressedModsDirectory!, id.ToString());
        string modInfoFile = Path.Combine(CompressedFolderPath, "mod.info");
        if (File.Exists(modInfoFile))
        {
            using var stream = File.OpenRead(modInfoFile);
            Span<byte> buffer = stackalloc byte[4];
            stream.ReadExactly(buffer);
            int stringSize = BitConverter.ToInt32(buffer);
            if (stringSize == 0)
                Name = string.Empty;
            byte[] stringBuffer = new byte[--stringSize];
            stream.ReadExactly(stringBuffer);
            Name = Encoding.UTF8.GetString(stringBuffer);
        }
        else
            Name = string.Empty;
        ModsFolderPath = Path.Combine(game.RootPath, "ShooterGame", "Content", "Mods", Id.ToString());
        ModFilePath = string.Concat(ModsFolderPath, ".mod");
    }
    /// <summary>Uninstalls the mod.</summary>
    public unsafe void Delete()
    {
        IGameContext game = ActiveGameManager.Current;
        var itemId = new TEKSteamClient.ItemId { AppId = game.SteamAppId, DepotId = game.WorkshopDepotId, WorkshopItemId = Id };
        var desc = LauncherServices.TekSteamClient.GetItemDesc(&itemId);
        var prevStatus = _status;
        CurrentStatus = Status.Deleting;
        if (Directory.Exists(ModsFolderPath))
            Directory.Delete(ModsFolderPath, true);
        if (File.Exists(ModFilePath))
            File.Delete(ModFilePath);
        if (desc != null)
        {
            if (desc->Status.HasFlag(TEKSteamClient.AmItemStatus.Job))
            {
                if (!LauncherServices.TekSteamClient.CancelJob(ref Unsafe.AsRef<TEKSteamClient.AmItemDesc>(desc)).Success)
                {
                    CurrentStatus = prevStatus;
                    return;
                }
            }
            if (desc->CurrentManifestId != 0)
            {
                if (!LauncherServices.TekSteamClient.RunJob(in itemId, ulong.MaxValue, false, null, out desc).Success)
                {
                    CurrentStatus = prevStatus;
                    return;
                }
            }
        }
        if (Directory.Exists(CompressedFolderPath))
            Directory.Delete(CompressedFolderPath, true);
        lock (List)
            List.Remove(this);
    }
    /// <summary>Finds all installed mods, gets their details, checks for updates and populates the <see cref="List"/>.</summary>
    public static unsafe void InitializeList()
    {
        IGameContext game = ActiveGameManager.Current;
        lock (List)
            List.Clear();

        //Set up Mods directory
        CompressedModsDirectory = game.WorkshopDir;
        if (!Directory.Exists(CompressedModsDirectory))
        {
            string workshopDirectory = Path.GetFullPath(Path.Combine(game.RootPath, "..", "..", "workshop", "content", game.SteamAppId.ToString()));
            if (Directory.Exists(workshopDirectory))
                Directory.CreateSymbolicLink(CompressedModsDirectory, workshopDirectory);
            else
            {
                Directory.CreateDirectory(CompressedModsDirectory);
                return;
            }
        }
        //Find all mods in there
        lock (List)
        {
            foreach (string modFolder in Directory.EnumerateDirectories(CompressedModsDirectory))
                if (ulong.TryParse(Path.GetFileName(modFolder), out ulong id))
                    List.Add(new(id));
            if (List.Count == 0)
                return;
        }
        ListInitialized?.Invoke();
        //Try to get mod details from Steam
        ulong[] ids;
        lock (List)
        {
            ids = new ulong[List.Count];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = List[i].Id;
        }
        var details = Platform.LauncherServices.TekSteamClient.Cm?.GetModDetails(ids) ?? [];
        bool updatesAvailable = false;
        lock (List)
        {
            foreach (var item in details)
                if (item.Status == 1)
                    List.Find(m => m.Id == item.Id)!.Details = item;
            //Check for updates
            if (LauncherServices.TekSteamClient.IsLoaded)
                foreach (var mod in List)
                {
                    var id = new TEKSteamClient.ItemId { AppId = game.SteamAppId, DepotId = game.WorkshopDepotId, WorkshopItemId = mod.Id };
                    var desc = LauncherServices.TekSteamClient.GetItemDesc(&id);
                    if (desc != null && desc->Status.HasFlag(TEKSteamClient.AmItemStatus.UpdAvailable))
                    {
                        updatesAvailable = true;
                        mod.CurrentStatus = Status.UpdateAvailable;
                    }
                }
        }
        DetailsUpdated?.Invoke();
        if (updatesAvailable)
            UpdatesAvailable?.Invoke();
    }
    /// <summary>Defines mod status codes.</summary>
    public enum Status
    {
        Installed,
        UpdateAvailable,
        Updating,
        Deleting
    }
    /// <summary>Represents Steam published file details of a mod.</summary>
    public readonly record struct ModDetails(uint AppId, int Status, long LastUpdated, ulong Id, ulong ManifestId, string Name, string PreviewUrl);
}