using System.Collections.Concurrent;
using System.Linq;
using ObeliskLauncher.Data;

namespace ObeliskLauncher.Servers;

/// <summary>Represents an ARK server.</summary>
public class Server
{
    /// <summary>Server query endpoint.</summary>
    readonly IPEndPoint _endpoint;
    /// <summary>List of official map names that may be returned by server queries.</summary>
    static readonly string[] s_mapNames = { "TheIsland", "TheCenter", "ScorchedEarth", "Ragnarok", "Aberration", "Extinction", "Valguero_P", "Genesis", "CrystalIsles", "Gen2", "LostIsland", "Abyss" };
    /// <summary>Stores cached server/cluster information objects by their URLs.</summary>
    static readonly ConcurrentDictionary<string, Info> s_infoCache = new();
    /// <summary>Gets a value that indicates whether server's mode is PvE.</summary>
    public bool IsPvE { get; private set; }
    public int NumPlayers { get; private set; }
    public int MaxNumPlayers { get; private set; }
    /// <summary>Gets IDs of the mods that the server is running.</summary>
    public ulong[] ModIds { get; private set; } = Array.Empty<ulong>();
    /// <summary>Gets cluster ID of the server.</summary>
    public string? ClusterId { get; private set; }
    /// <summary>Gets a launch parameter for joining the server directly.</summary>
    public string Address => $"{_endpoint.Address}:{_endpoint.Port}";
    /// <summary>Gets map name to be displayed in the GUI.</summary>
    public string DisplayMapName { get; private set; } = string.Empty;
    /// <summary>Gets server display name for cluster.</summary>
    public string DisplayName => Info?.ServerName switch
    {
        null => Name,
        "" => DisplayMapName,
        _ => Info.ServerName
    };
    /// <summary>Gets name of the server in Steam network.</summary>
    public string Name { get; private set; } = string.Empty;
    /// <summary>Gets version of the server.</summary>
    public string? Version { get; private set; }
    /// <summary>Gets code of the map that the server is running.</summary>
    public MapCode Map { get; private set; }
    /// <summary>Gets extra server/cluster information that may be included by its owner.</summary>
    public Info? Info { get; private set; }
    /// <summary>Initializes a new server object with specified endpoint.</summary>
    /// <param name="endpoint">IP endpoint for querying the server.</param>
    public Server(IPEndPoint endpoint) => _endpoint = endpoint;
    /// <summary>Adds the server to Steam favorites list.</summary>
    public void AddFavorite()
    {
        LauncherServices.ServerBrowser.AddFavorite(_endpoint);
        lock (Cluster.Favorites.Servers)
        {
            if (!Cluster.Favorites.Servers.Contains(this))
                Cluster.Favorites.Servers.Add(this);
        }
        LauncherServices.ServerUi.OnClusterServerCountChanged(Cluster.Favorites);
        LauncherServices.ServerUi.OnServerAdded(Cluster.Favorites, this);
    }
    /// <summary>Removes the server from Steam favorites list.</summary>
    public void RemoveFavorite()
    {
        LauncherServices.ServerBrowser.RemoveFavorite(_endpoint);
        lock (Cluster.Favorites.Servers)
            Cluster.Favorites.Servers.Remove(this);
        LauncherServices.ServerUi.OnClusterServerCountChanged(Cluster.Favorites);
        LauncherServices.ServerUi.OnServerRemoved(Cluster.Favorites, this);
    }
    /// <summary>Loads server information using Steam Server Queries.</summary>
    /// <returns><see langword="true"/> if all query requests succeeded and the server is using TEK Wrapper; otherwise <see langword="false"/>.</returns>
    public bool Query()
    {
        bool Fail(string reason)
        {
            LauncherLog.Debug("Server query failed for {Address}: {Reason}", Address, reason);
            return false;
        }

        try
        {
        Span<byte> request = stackalloc byte[]
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0x54, 0x53, 0x6F, 0x75, 0x72, 0x63, 0x65, 0x20, 0x45, 0x6E,
            0x67, 0x69, 0x6E, 0x65, 0x20, 0x51, 0x75, 0x65, 0x72, 0x79, 0x00, 0x00, 0x00, 0x00, 0x00
        };
        byte[]? buffer = UdpClient.Transact(_endpoint, request[..25]);
        if (buffer is null)
            return Fail("A2S_INFO returned no response.");
        if (buffer.Length == 9) //Sometimes the server may return challenge number which needs to be sent back
        {
            new Span<byte>(buffer, 5, 4).CopyTo(request[^4..]);
            buffer = UdpClient.Transact(_endpoint, request);
            if (buffer is null)
                return Fail("A2S_INFO challenge retry returned no response.");
        }
        int nullIndex = Array.IndexOf(buffer, (byte)0, 6);
        if (nullIndex < 7)
            return Fail("A2S_INFO payload did not contain a valid name field.");
        Name = Encoding.ASCII.GetString(buffer, 6, nullIndex - 6);
        int startIndex = Name.LastIndexOf(" - (v");
        if (startIndex >= 0)
        {
            Version = Name[(startIndex + 5)..^1];
            Name = Name[..startIndex];
        }
        startIndex = nullIndex + 1;
        nullIndex = Array.IndexOf(buffer, (byte)0, startIndex);
        string map = Encoding.ASCII.GetString(buffer, startIndex, nullIndex - startIndex);
        Map = (MapCode)Array.IndexOf(s_mapNames, map);
        if (Map == (MapCode)(-1))
            Map = MapCode.Mod;
        DisplayMapName = Map switch
        {
            MapCode.TheIsland => "The Island",
            MapCode.Genesis2 => "Genesis 2",
            MapCode.Mod => map,
            _ => ResolveDlcMapDisplayName(map)
        };
        startIndex = nullIndex + 1;
        nullIndex = Array.IndexOf(buffer, (byte)0, startIndex);
        string gameDir = Encoding.ASCII.GetString(buffer, startIndex, nullIndex - startIndex);
        if (!ActiveGameManager.Current.AcceptedServerGameDirs.Contains(gameDir, StringComparer.OrdinalIgnoreCase))
            return Fail("A2S_INFO reported a different gamedir.");
        MaxNumPlayers = buffer[Array.IndexOf(buffer, (byte)0, nullIndex + 1) + 4];
        //A2S_RULES
        request = request[..9];
        request[4] = 0x56;
        BitConverter.TryWriteBytes(request[5..], 0xFFFFFFFF);
        buffer = UdpClient.Transact(_endpoint, request);
        if (buffer is null)
            return Fail("A2S_RULES challenge request returned no response.");
        buffer[4] = 0x56;
        buffer = UdpClient.Transact(_endpoint, buffer);
        if (buffer is null)
            return Fail("A2S_RULES request returned no response.");
        if (buffer[4] != 0x45)
            return Fail($"A2S_RULES returned unexpected header 0x{buffer[4]:X2}.");
        int numRules = BitConverter.ToInt16(buffer, 5);
        startIndex = 7;
        bool tekWrapperInstalled = false;
        string? infoFileUrl = null;
        for (int i = 0; i < numRules; i++)
        {
            nullIndex = Array.IndexOf(buffer, (byte)0, startIndex);
            switch (Encoding.ASCII.GetString(buffer, startIndex, nullIndex - startIndex))
            {
                case "ClusterId_s":
                    startIndex = nullIndex + 1;
                    nullIndex = Array.IndexOf(buffer, (byte)0, startIndex);
                    ClusterId = Encoding.ASCII.GetString(buffer, startIndex, nullIndex - startIndex);
                    startIndex = nullIndex + 1;
                    break;
                case "NUMOPENPUBCONN":
                    startIndex = nullIndex + 1;
                    nullIndex = Array.IndexOf(buffer, (byte)0, startIndex);
                    NumPlayers = MaxNumPlayers - (int.TryParse(Encoding.ASCII.GetString(buffer, startIndex, nullIndex - startIndex), out int num) ? num : MaxNumPlayers);
                    startIndex = nullIndex + 1;
                    break;
                case "SEARCHKEYWORDS_s":
                    startIndex = nullIndex + 1;
                    nullIndex = Array.IndexOf(buffer, (byte)0, startIndex);
                    string[] values = Encoding.ASCII.GetString(buffer, startIndex, nullIndex - startIndex).Split();
                    if (values.Length < 3 || values[0] != "TEKWrapper")
                        return Fail(values.Length < 3 ? "SEARCHKEYWORDS_s did not contain enough TEKWrapper fields." : $"SEARCHKEYWORDS_s started with '{values[0]}'.");
                    tekWrapperInstalled = true;
                    if (values.Length > 3) //Mod list for some reason is comma-and-whitespace-separated instead of comma-separated
                    {
                        ModIds = new ulong[values.Length - 2];
                        for (int j = 1; j < values.Length - 1; j++)
                            ModIds[j - 1] = ulong.Parse(values[j].TrimEnd(','));
                    }
                    else
                    {
                        if (values[1] != "0")
                        {
                            string[] ids = values[1].Split(',');
                            ModIds = new ulong[ids.Length];
                            for (int j = 0; j < ids.Length; j++)
                                if (!ulong.TryParse(ids[j], out ModIds[j]))
                                    return Fail($"Failed to parse mod id '{ids[j]}'.");
                        }
                        if (values[2] != "0")
                            infoFileUrl = values[2];
                    }
                    startIndex = nullIndex + 1;
                    break;
                case "SERVERUSESBATTLEYE_b":
                    startIndex = nullIndex + 1;
                    nullIndex = Array.IndexOf(buffer, (byte)0, startIndex);
                    if (Encoding.ASCII.GetString(buffer, startIndex, nullIndex - startIndex) != "false")
                        return Fail("SERVERUSESBATTLEYE_b was not false.");
                    startIndex = nullIndex + 1;
                    break;
                case "SESSIONISPVE_i":
                    startIndex = nullIndex + 1;
                    nullIndex = Array.IndexOf(buffer, (byte)0, startIndex);
                    IsPvE = Encoding.ASCII.GetString(buffer, startIndex, nullIndex - startIndex) == "1";
                    startIndex = nullIndex + 1;
                    break;
                default:
                    startIndex = Array.IndexOf(buffer, (byte)0, ++nullIndex) + 1;
                    break;
            }
        }
        if (!tekWrapperInstalled)
            return Fail("Server did not advertise TEKWrapper.");
        //Attempt to load info file
        if (infoFileUrl is null)
        {
            return true;
        }
        if (s_infoCache.TryGetValue(infoFileUrl, out var info))
        {
            Info = info;
            return true;
        }
        info = Downloader.DownloadJsonAsync<Info>(infoFileUrl).Result;
        if (info is null || !(info.Discord?.StartsWith("https://discord.gg/") ?? true) || (info.ServerDescription?.Other?.Length ?? 0) > 6 || (info.ClusterDescription?.Other?.Length ?? 0) > 6)
        {
            return true;
        }
        s_infoCache[infoFileUrl] = info;
        Info = info;
        return true;
        }
        catch (Exception ex)
        {
            LauncherLog.Warning("Server query error for {Address}: {Error}", Address, ex.Message);
            return false;
        }
    }

    string ResolveDlcMapDisplayName(string fallback)
    {
        try
        {
            return DLC.Get(Map).Name;
        }
        catch (Exception ex)
        {
            LauncherLog.Warning("Falling back to raw map name for {Address}. ActiveGame={GameId}, MapCode={MapCode}, Reason={Reason}",
                Address,
                ActiveGameManager.Current.Id,
                Map,
                ex.Message);
            return fallback;
        }
    }

    public override bool Equals(object? obj) => obj is Server other && _endpoint.Equals(other._endpoint);
    public override int GetHashCode() => _endpoint.GetHashCode();
}