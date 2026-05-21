using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using TEKLauncher.Platform;
using TEKLauncher.Servers;

namespace TEKLauncher.Steam;

static class LinuxServerBrowser
{
    static readonly object s_sync = new();
    static IntPtr s_libraryHandle;
    static IntPtr s_steamClient;
    static IntPtr s_steamMatchmaking;
    static IntPtr s_steamMatchmakingServers;
    static IntPtr s_steamUtils;
    static int s_pipe;
    static int s_user;
    static CreateSteamPipe s_createSteamPipe = null!;
    static BReleaseSteamPipe s_bReleaseSteamPipe = null!;
    static ConnectToGlobalUser s_connectToGlobalUser = null!;
    static ReleaseUser s_releaseUser = null!;
    static GetISteamGenericInterface s_getISteamGenericInterface = null!;
    static BShutdownIfAllPipesClosed s_bShutdownIfAllPipesClosed = null!;
    static AddFavoriteGame s_addFavoriteGame = null!;
    static RemoveFavoriteGame s_removeFavoriteGame = null!;
    static RequestInternetServerList s_requestInternetServerList = null!;
    static RequestLANServerList s_requestLANServerList = null!;
    static RequestFavoritesServerList s_requestFavoritesServerList = null!;
    static ReleaseRequest s_releaseRequest = null!;
    static GetServerDetails s_getServerDetails = null!;
    static CancelQuery s_cancelQuery = null!;
    static GetServerCount s_getServerCount = null!;
    static RunFrame s_runFrame = null!;
    static bool s_initializationAttempted;

    delegate IntPtr CreateInterface([MarshalAs(UnmanagedType.LPStr)] string version, int size);
    delegate int CreateSteamPipe(IntPtr pThis);
    delegate bool BReleaseSteamPipe(IntPtr pThis, int hSteamPipe);
    delegate int ConnectToGlobalUser(IntPtr pThis, int hSteamPipe);
    delegate void ReleaseUser(IntPtr pThis, int hSteamPipe, int hUser);
    delegate IntPtr GetISteamGenericInterface(IntPtr pThis, int hSteamUser, int hSteamPipe, [MarshalAs(UnmanagedType.LPStr)] string version);
    delegate bool BShutdownIfAllPipesClosed(IntPtr pThis);
    delegate int AddFavoriteGame(IntPtr pThis, uint appId, uint ip, ushort connPort, ushort queryPort, uint flags, uint lastPlayed);
    delegate bool RemoveFavoriteGame(IntPtr pThis, uint appId, uint ip, ushort connPort, ushort queryPort, uint flags);
    delegate IntPtr RequestInternetServerList(IntPtr pThis, uint appId, in MatchMakingKeyValuePair_t[] filters, uint filterCount, IntPtr requestServersResponse);
    delegate IntPtr RequestLANServerList(IntPtr pThis, uint appId, IntPtr requestServersResponse);
    delegate IntPtr RequestFavoritesServerList(IntPtr pThis, uint appId, in MatchMakingKeyValuePair_t[] filters, uint filterCount, IntPtr requestServersResponse);
    delegate void ReleaseRequest(IntPtr pThis, IntPtr request);
    delegate IntPtr GetServerDetails(IntPtr pThis, IntPtr request, int index);
    delegate void CancelQuery(IntPtr pThis, IntPtr request);
    delegate int GetServerCount(IntPtr pThis, IntPtr request);
    delegate void RunFrame(IntPtr pThis);

    public static void AddFavorite(IPEndPoint endpoint)
    {
        if (!EnsureInitialized())
            throw new InvalidOperationException("Linux Steam server browser is not initialized.");

        Span<byte> buffer = stackalloc byte[4];
        endpoint.Address.TryWriteBytes(buffer, out _);
        s_addFavoriteGame(s_steamMatchmaking, 346110, (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer)), (ushort)endpoint.Port, (ushort)endpoint.Port, 0x1, (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public static Server[]? GetServers(ServerBrowserListType type, string? clusterId = null)
    {
        if (!EnsureInitialized())
            return null;

        MatchMakingKeyValuePair_t[]? filters = type == ServerBrowserListType.LAN ? null :
        [
          new() { m_szKey = "gamedir", m_szValue = "ark_survival_evolved" },
      new() { m_szKey = "gamedataand", m_szValue = clusterId is null ? "SERVERUSESBATTLEYE_b:false,TEKWrapper:1" : $"SERVERUSESBATTLEYE_b:false,TEKWrapper:1,CLUSTERID_s:{clusterId}" }
        ];

        IntPtr request = type switch
        {
            ServerBrowserListType.LAN => s_requestLANServerList(s_steamMatchmakingServers, 346110, IntPtr.Zero),
            ServerBrowserListType.Favorites => s_requestFavoritesServerList(s_steamMatchmakingServers, 346110, in filters!, 2, IntPtr.Zero),
            _ => s_requestInternetServerList(s_steamMatchmakingServers, 346110, in filters!, 2, IntPtr.Zero)
        };

        try
        {
            for (int remaining = type == ServerBrowserListType.Online ? 250 : 20; remaining > 0 && s_getServerCount(s_steamMatchmakingServers, request) == 0; remaining--)
            {
                s_runFrame(s_steamUtils);
                Thread.Sleep(20);
            }

            s_cancelQuery(s_steamMatchmakingServers, request);
            int numServers = Math.Min(s_getServerCount(s_steamMatchmakingServers, request), 300);
            var result = new Server[numServers];
            for (int i = 0; i < numServers; i++)
            {
                var address = Marshal.PtrToStructure<Servernetadr_t>(s_getServerDetails(s_steamMatchmakingServers, request, i));
                result[i] = new(new(unchecked((uint)IPAddress.NetworkToHostOrder(address.m_unIP)), address.m_usQueryPort));
            }

            return result;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (request != IntPtr.Zero)
                s_releaseRequest(s_steamMatchmakingServers, request);
        }
    }

    public static void RemoveFavorite(IPEndPoint endpoint)
    {
        if (!EnsureInitialized())
            throw new InvalidOperationException("Linux Steam server browser is not initialized.");

        Span<byte> buffer = stackalloc byte[4];
        endpoint.Address.TryWriteBytes(buffer, out _);
        s_removeFavoriteGame(s_steamMatchmaking, 346110, (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer)), (ushort)endpoint.Port, (ushort)endpoint.Port, 0x1);
    }

    public static void Shutdown()
    {
        lock (s_sync)
        {
            if (s_steamClient == IntPtr.Zero)
                return;

            s_releaseUser(s_steamClient, s_pipe, s_user);
            s_bReleaseSteamPipe(s_steamClient, s_pipe);
            s_bShutdownIfAllPipesClosed(s_steamClient);
            s_steamClient = IntPtr.Zero;
            s_steamMatchmaking = IntPtr.Zero;
            s_steamMatchmakingServers = IntPtr.Zero;
            s_steamUtils = IntPtr.Zero;
        }
    }

    static bool EnsureInitialized()
    {
        lock (s_sync)
        {
            if (s_steamClient != IntPtr.Zero)
                return true;

            if (s_initializationAttempted)
                return false;

            s_initializationAttempted = true;
            return TryInitialize();
        }
    }

    static bool TryInitialize()
    {
        if (!App.IsRunning)
        {
            return false;
        }

        string? libraryPath = LauncherPlatform.Current.GetSteamClientDllPath();
        if (string.IsNullOrWhiteSpace(libraryPath) || !File.Exists(libraryPath))
        {
            return false;
        }

        try
        {
            s_libraryHandle = NativeLibrary.Load(libraryPath);
            IntPtr createInterfaceExport = NativeLibrary.GetExport(s_libraryHandle, "CreateInterface");
            var createInterface = Marshal.GetDelegateForFunctionPointer<CreateInterface>(createInterfaceExport);
            s_steamClient = createInterface("SteamClient023", 0);
            if (s_steamClient == IntPtr.Zero)
            {
                return false;
            }

            IntPtr steamClientVtable = Marshal.ReadIntPtr(s_steamClient);
            s_createSteamPipe = GetVirtualFunction<CreateSteamPipe>(steamClientVtable, 0);
            s_bReleaseSteamPipe = GetVirtualFunction<BReleaseSteamPipe>(steamClientVtable, 1);
            s_connectToGlobalUser = GetVirtualFunction<ConnectToGlobalUser>(steamClientVtable, 2);
            s_releaseUser = GetVirtualFunction<ReleaseUser>(steamClientVtable, 4);
            s_getISteamGenericInterface = GetVirtualFunction<GetISteamGenericInterface>(steamClientVtable, 12);
            s_bShutdownIfAllPipesClosed = GetVirtualFunction<BShutdownIfAllPipesClosed>(steamClientVtable, 23);

            s_pipe = s_createSteamPipe(s_steamClient);
            s_user = s_connectToGlobalUser(s_steamClient, s_pipe);
            s_steamMatchmaking = s_getISteamGenericInterface(s_steamClient, s_user, s_pipe, "SteamMatchMaking009");
            s_steamMatchmakingServers = s_getISteamGenericInterface(s_steamClient, s_user, s_pipe, "SteamMatchMakingServers002");
            s_steamUtils = s_getISteamGenericInterface(s_steamClient, s_user, s_pipe, "SteamUtils010");
            if (s_steamMatchmaking == IntPtr.Zero || s_steamMatchmakingServers == IntPtr.Zero || s_steamUtils == IntPtr.Zero)
            {
                Shutdown();
                return false;
            }

            IntPtr matchmakingVtable = Marshal.ReadIntPtr(s_steamMatchmaking);
            s_addFavoriteGame = GetVirtualFunction<AddFavoriteGame>(matchmakingVtable, 2);
            s_removeFavoriteGame = GetVirtualFunction<RemoveFavoriteGame>(matchmakingVtable, 3);

            IntPtr matchmakingServersVtable = Marshal.ReadIntPtr(s_steamMatchmakingServers);
            s_requestInternetServerList = GetVirtualFunction<RequestInternetServerList>(matchmakingServersVtable, 0);
            s_requestLANServerList = GetVirtualFunction<RequestLANServerList>(matchmakingServersVtable, 1);
            s_requestFavoritesServerList = GetVirtualFunction<RequestFavoritesServerList>(matchmakingServersVtable, 3);
            s_releaseRequest = GetVirtualFunction<ReleaseRequest>(matchmakingServersVtable, 6);
            s_getServerDetails = GetVirtualFunction<GetServerDetails>(matchmakingServersVtable, 7);
            s_cancelQuery = GetVirtualFunction<CancelQuery>(matchmakingServersVtable, 8);
            s_getServerCount = GetVirtualFunction<GetServerCount>(matchmakingServersVtable, 11);

            IntPtr utilsVtable = Marshal.ReadIntPtr(s_steamUtils);
            s_runFrame = GetVirtualFunction<RunFrame>(utilsVtable, 14);

            return true;
        }
        catch (Exception)
        {
            Shutdown();
            return false;
        }
    }

    static TDelegate GetVirtualFunction<TDelegate>(IntPtr vtable, int slot)
      where TDelegate : Delegate => Marshal.GetDelegateForFunctionPointer<TDelegate>(Marshal.ReadIntPtr(vtable, slot * IntPtr.Size));

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct MatchMakingKeyValuePair_t
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string m_szKey;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string m_szValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Servernetadr_t
    {
        public ushort m_usConnectionPort;
        public ushort m_usQueryPort;
        public int m_unIP;
    }
}