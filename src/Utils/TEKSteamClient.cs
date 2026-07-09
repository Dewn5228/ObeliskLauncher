using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;

namespace ObeliskLauncher.Utils;

/// <summary>Bindings for tek-steamclient library.</summary>
static partial class TEKSteamClient
{
    const string NativeLibraryName = "libtek-steamclient-2.dll";
    static readonly object s_librarySync = new();
    static IntPtr s_libraryHandle;
    static string? s_libraryPath;

    static TEKSteamClient() => NativeLibrary.SetDllImportResolver(typeof(TEKSteamClient).Assembly, ResolveLibrary);

    public static readonly string DllPath = Path.Combine(LauncherBootstrap.AppDataFolder,
        OperatingSystem.IsWindows() ? NativeLibraryName : "libtek-steamclient.so.2");
    public static LibCtx? Ctx = null;
    public static AppManager? AppMng = null;
    public static CmClient? Cm = null;

    public static void RegisterLibrary(string libraryPath, IntPtr libraryHandle)
    {
        lock (s_librarySync)
        {
            s_libraryPath = libraryPath;
            s_libraryHandle = libraryHandle;
        }
    }

    static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (string.Equals(libraryName, "steamclient64.dll", StringComparison.Ordinal))
            return Steam.Api.LibraryHandle;

        if (!IsTekSteamClientLibraryName(libraryName))
            return IntPtr.Zero;

        lock (s_librarySync)
        {
            if (s_libraryHandle != IntPtr.Zero)
                return s_libraryHandle;

            if (!string.IsNullOrWhiteSpace(s_libraryPath))
            {
                s_libraryHandle = NativeLibrary.Load(s_libraryPath);
                return s_libraryHandle;
            }
        }

        return IntPtr.Zero;
    }

    static bool IsTekSteamClientLibraryName(string libraryName)
        => string.Equals(libraryName, NativeLibraryName, StringComparison.Ordinal)
        || string.Equals(libraryName, "libtek-steamclient.so.2", StringComparison.Ordinal);

    #region Native Functions
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_version")]
    public static partial nint GetVersion();
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_load_locale", StringMarshalling = StringMarshalling.Utf16)]
    static partial void LoadLocaleNative(string path);

    public static void LoadLocale(string path)
    {
        if (OperatingSystem.IsLinux())
            return;

        LoadLocaleNative(path);
    }

    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_lib_init")]
    private static partial nint LibInit([MarshalAs(UnmanagedType.I1)] bool useFileCache, [MarshalAs(UnmanagedType.I1)] bool disableLwsLogs);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_lib_cleanup")]
    private static partial void LibCleanup(nint libCtx);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_err_get_msgs")]
    private static partial ErrorMessages GetErrorMsgs(in Error err);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_err_release_msgs")]
    private static partial void ReleaseMsgs(ref ErrorMessages errMsgs);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_s3c_sync_manifest", StringMarshalling = StringMarshalling.Utf8)]
    private static partial Error S3CSyncManifest(nint libCtx, string url, int timeoutMs);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_s3c_get_srv_for_mrc", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint S3CGetServerForManifestRequestCode(nint libCtx, uint appId, uint depotId);
    
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_cm_client_create")]
    private static partial nint CmClientCreate(nint libCtx, nint userData);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_cm_client_destroy")]
    private static partial void CmClientDestroy(nint client);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_cm_set_user_data")]
    private static unsafe partial void CmSetUserData(nint client, void* userData);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_cm_connect")]
    private static partial void CmConnect(nint client, nint connectionCb, CLong fetchTimeoutMs, nint disconnectionCb);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_cm_disconnect")]
    private static partial void CmDisconnect(nint client);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_cm_sign_in_anon")]
    private static partial void CmSignInAnon(nint client, nint cb, int timeoutMs);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_cm_ws_get_details")]
    private static partial void CmWsGetDetails(nint client, ref CmDataWs data, nint cb, int timeoutMs);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_cm_ws_query_items", StringMarshalling = StringMarshalling.Utf8)]
    private static partial void CmWsQueryItems(nint client, ref CmDataWs data, uint appId, int page, string? searchQuery, nint cb, int timeoutMs);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_cm_get_access_token")]
    private static partial void CmGetAccessToken(nint client, ref CmDataPics data, nint cb, int timeoutMs);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_cm_get_product_info")]
    private static partial void CmGetProductInfo(nint client, ref CmDataPics data, nint cb, int timeoutMs);

    [DllImport("ucrtbase", CallingConvention = CallingConvention.Cdecl, EntryPoint = "free")]
    private static extern unsafe void FreeWindows(void* ptr);

    private static unsafe void FreeNativeMemory(void* ptr)
    {
        if (ptr == null) return;
        if (OperatingSystem.IsWindows())
            FreeWindows(ptr);
        else
            Marshal.FreeHGlobal((nint)ptr);
    }

    private static VdfNode? FindVdfSection(VdfNode root, string sectionName)
    {
        if (root.Children.TryGetValue(sectionName, out var node))
            return node;
        foreach (var child in root.Children.Values)
            if (child.Children.TryGetValue(sectionName, out var inner))
                return inner;
        return null;
    }

    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_dd_estimate_disk_space")]
    public static partial long DeltaEstimateDiskSpace(nint delta);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_am_create", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint AmCreateUtf16(nint libCtx, string dir, out Error err);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_am_create", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint AmCreateUtf8(nint libCtx, string dir, out Error err);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_am_destroy")]
    private static partial void AmDestroy(nint am);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_am_set_ws_dir", StringMarshalling = StringMarshalling.Utf16)]
    private static partial Error AmSetWorkshopDirUtf16(nint am, string wsDir);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_am_set_ws_dir", StringMarshalling = StringMarshalling.Utf8)]
    private static partial Error AmSetWorkshopDirUtf8(nint am, string wsDir);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_am_get_item_desc")]
    private static unsafe partial AmItemDesc* AmGetItemDesc(nint am, ItemId* itemId);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_am_item_descs_lock")]
    private static partial void AmItemDescsLock(nint am);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_am_item_descs_unlock")]
    private static partial void AmItemDescsUnlock(nint am);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_am_check_for_upds")]
    private static partial Error AmCheckForUpdates(nint am, int timeoutMs);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_am_create_job")]
    private static unsafe partial Error AmCreateJob(nint am, ItemId* itemId, ulong manifestId, [MarshalAs(UnmanagedType.I1)] bool forceVerify, out AmItemDesc* itemDesc);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_am_run_job")]
    private static unsafe partial Error AmRunJob(nint am, ref AmItemDesc itemDesc, nint updHandler);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_am_pause_job")]
    private static partial void AmPauseJob(ref AmItemDesc itemDesc);
    [LibraryImport(NativeLibraryName, EntryPoint = "tek_sc_am_cancel_job")]
    private static partial Error AmCancelJob(nint am, ref AmItemDesc itemDesc);
    #endregion

    #region Native Types
    [StructLayout(LayoutKind.Sequential)]
    public struct Error
    {
        public int Type;
        public int Primary;
        public int Auxiliary;
        public int Extra;
        public nint Uri;
        public readonly bool Success => Primary == 0;
        public readonly string Message
        {
            get
            {
                var msgs = GetErrorMsgs(in this);
                var msg = $"An error has occurred\nType: ({Type}) {Marshal.PtrToStringUTF8(msgs.TypeStr)}\nPrimary message: ({Primary}) {Marshal.PtrToStringUTF8(msgs.Primary)}";
                if (msgs.Auxiliary != 0)
                    msg += $"\nAuxiliary message: ({Auxiliary}) {Marshal.PtrToStringUTF8(msgs.Auxiliary)}";
                if (msgs.Extra != 0)
                    msg += $"\n{Marshal.PtrToStringUTF8(msgs.Extra)}";
                if (Uri != 0)
                {
                    msg += $"\n{Marshal.PtrToStringUTF8(msgs.UriType)}: {Marshal.PtrToStringUTF8(Uri)}";
                }
                ReleaseMsgs(ref msgs);
                return msg;
            }
        }
        public readonly string AuxMessage
        {
            get
            {
                var msgs = GetErrorMsgs(in this);
                string msg;
                if (msgs.Auxiliary == 0)
                    msg = Marshal.PtrToStringUTF8(msgs.Primary)!;
                else
                    msg = $"({Auxiliary}) {Marshal.PtrToStringUTF8(msgs.Auxiliary)}";
                if (msgs.Extra != 0)
                    msg += $" ({Marshal.PtrToStringUTF8(msgs.Extra)})";
                ReleaseMsgs(ref msgs);
                return msg;
            }
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ErrorMessages
    {
        public int Type;
        public nint TypeStr;
        public nint Primary;
        public nint Auxiliary;
        public nint Extra;
        public nint UriType;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct ItemId
    {
        public uint AppId;
        public uint DepotId;
        public ulong WorkshopItemId;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct CmWsItemDetails
    {
        public ulong Id;
        public ulong ManifestId;
        public long LastUpdated;
        public nint Name;
        public nint PreviewUrl;
        public nint Children;
        public int NumChildren;
        public uint AppId;
        public Error Result;
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct CmDataWs
    {
        public CmWsItemDetails* Details;
        public int NumDetails;
        public int NumReturnedDetails;
        public int TotalItems;
        public Error Result;
        public nint Event;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct CmPicsEntry
    {
        public ulong AccessToken;
        public uint Id;
        public int DataSize;
        public nint Data;
        public Error Result;
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct CmDataPics
    {
        public CmPicsEntry* AppEntries;
        public CmPicsEntry* PackageEntries;
        public int NumAppEntries;
        public int NumPackageEntries;
        public CLong TimeoutMs;
        public Error Result;
        public nint Event;
    }
    [Flags]
    public enum AmItemStatus
    {
        Job = 1 << 0,
        UpdAvailable = 1 << 1
    }
    public enum AmJobStage
    {
        FetchingData,
        DwManifest,
        DwPatch,
        Verifying,
        Downloading,
        Pathcing,
        Installing,
        Deleting,
        Finalizing
    }
    public enum AmJobState
    {
        Stopped,
        Running,
        PausePending
    }
    public enum AmPatchStatus
    {
        Unknown,
        Unused,
        Used
    }
    [Flags]
    public enum AmUpdType
    {
        State = 1 << 0,
        Stage = 1 << 1,
        Progress = 1 << 2,
        DeltaCreated = 1 << 3
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct AmJobDesc
    {
        public volatile AmJobState State;
        public AmJobStage Stage;
        public long ProgressCurrent;
        public long ProgressTotal;
        public ulong SourceManifestId;
        public ulong TargetManifestId;
        public AmPatchStatus PatchStatus;
        public nint Delta;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct AmItemDesc
    {
        public unsafe AmItemDesc* Next;
        public ItemId Id;
        public AmItemStatus Status;
        public ulong CurrentManifestId;
        public ulong LatestManifestId;
        public AmJobDesc Job;
    }
    public unsafe delegate void CmCallbackFunc(nint client, void* data, void* userData);
    public delegate void AmJobUpdFunc(ref AmItemDesc desc, AmUpdType upd_mask);
    #endregion

    public class LibCtx : SafeHandle
    {
        public LibCtx() : base(0, true)
        {
            handle = LibInit(true, true);
            if (handle == 0)
                throw new Exception("Failed to initialize tek-steamclient library context");
        }
        public override bool IsInvalid => handle == 0;
        protected override bool ReleaseHandle()
        {
            LibCleanup(handle);
            return true;
        }

        public Error SyncS3Manifest(string url) => S3CSyncManifest(handle, url, 16000);

        public string? GetServerForManifestRequestCode(uint appId, uint depotId)
        {
            nint serverPtr = S3CGetServerForManifestRequestCode(handle, appId, depotId);
            return serverPtr == 0 ? null : Marshal.PtrToStringUTF8(serverPtr);
        }
    }
    public class CmClient : SafeHandle
    {
        public CmClient(LibCtx ctx) : base(0, true)
        {
            handle = CmClientCreate(ctx.DangerousGetHandle(), 0);
            if (handle == 0)
                throw new Exception("Failed to create a CM client");
        }
        public override bool IsInvalid => handle == 0;
        public void Disconnect() => CmDisconnect(handle);
        protected override bool ReleaseHandle()
        {
            CmClientDestroy(handle);
            return true;
        }
        private static unsafe void CbConn(nint client, void* data, void* userData)
        {
            ref var dataWs = ref Unsafe.AsRef<CmDataWs>(userData);
            dataWs.Result = *(Error*)data;
            GCHandle<AutoResetEvent>.FromIntPtr(dataWs.Event).Target.Set();
        }
        private static unsafe void CbConnPics(nint client, void* data, void* userData)
        {
            ref var dataPics = ref Unsafe.AsRef<CmDataPics>(userData);
            dataPics.Result = *(Error*)data;
            GCHandle<AutoResetEvent>.FromIntPtr(dataPics.Event).Target.Set();
        }
        private static unsafe void CbDisconn(nint client, void* data, void* userData) { }
        private static unsafe void CbSignedIn(nint client, void* data, void* userData)
        {
            ref var dataWs = ref Unsafe.AsRef<CmDataWs>(userData);
            dataWs.Result = *(Error*)data;
            GCHandle<AutoResetEvent>.FromIntPtr(dataWs.Event).Target.Set();
        }
        private static unsafe void CbSignedInPics(nint client, void* data, void* userData)
        {
            ref var dataPics = ref Unsafe.AsRef<CmDataPics>(userData);
            dataPics.Result = *(Error*)data;
            GCHandle<AutoResetEvent>.FromIntPtr(dataPics.Event).Target.Set();
        }
        private static unsafe void CbMd(nint client, void* data, void* userData) => GCHandle<AutoResetEvent>.FromIntPtr(Unsafe.AsRef<CmDataWs>(data).Event).Target.Set();
        private static unsafe void CbPics(nint client, void* data, void* userData) => GCHandle<AutoResetEvent>.FromIntPtr(Unsafe.AsRef<CmDataPics>(data).Event).Target.Set();

        public unsafe Mod.ModDetails[] GetModDetails(params ulong[] ids)
        {
            var entries = new CmWsItemDetails[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                entries[i].Id = ids[i];
            fixed (CmWsItemDetails* entriesPtr = entries)
            {
                using var ev = new GCHandle<AutoResetEvent>(new(false));
                CmDataWs data = new()
                {
                    Details = entriesPtr,
                    NumDetails = entries.Length,
                    Event = GCHandle<AutoResetEvent>.ToIntPtr(ev)
                };
                CmSetUserData(handle, &data);
                for (; ; )
                {
                    CmWsGetDetails(handle, ref data, Marshal.GetFunctionPointerForDelegate(CbMd), 10000);
                    ev.Target.WaitOne();
                    if (!data.Result.Success)
                    {
                        if (data.Result.Auxiliary != 30)
                            return [];
                        CmSignInAnon(handle, Marshal.GetFunctionPointerForDelegate(CbSignedIn), 10000);
                        ev.Target.WaitOne();
                        if (!data.Result.Success)
                        {
                            if (data.Result.Auxiliary != 29)
                                return [];
                            CmConnect(handle, Marshal.GetFunctionPointerForDelegate(CbConn), new CLong(10000), Marshal.GetFunctionPointerForDelegate(CbDisconn));
                            ev.Target.WaitOne();
                            if (!data.Result.Success)
                                return [];
                            continue;
                        }
                        continue;
                    }
                    break;
                }
            }
            var result = new Mod.ModDetails[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                var item = entries[i];
                result[i] = new(item.AppId, item.Result.Success ? 1 : item.Result.Auxiliary == 9 ? 2 : 0, DateTimeOffset.FromUnixTimeSeconds(item.LastUpdated).Ticks, item.Id, item.ManifestId, Marshal.PtrToStringUTF8(item.Name)!, Marshal.PtrToStringUTF8(item.PreviewUrl)!);
            }
            return result;
        }
        public unsafe Mod.ModDetails[] QueryMods(uint page, string? search, out uint total)
        {
            total = 0;
            var entries = stackalloc CmWsItemDetails[20];
            using var ev = new GCHandle<AutoResetEvent>(new(false));
            CmDataWs data = new()
            {
                Details = entries,
                NumDetails = 20,
                Event = GCHandle<AutoResetEvent>.ToIntPtr(ev)
            };
            CmSetUserData(handle, &data);
            for (; ; )
            {
                CmWsQueryItems(handle, ref data, ActiveGameManager.Current.SteamAppId, (int)page, search, Marshal.GetFunctionPointerForDelegate(CbMd), 10000);
                ev.Target.WaitOne();
                if (!data.Result.Success)
                {
                    if (data.Result.Auxiliary != 30)
                        return [];
                    CmSignInAnon(handle, Marshal.GetFunctionPointerForDelegate(CbSignedIn), 10000);
                    ev.Target.WaitOne();
                    if (!data.Result.Success)
                    {
                        if (data.Result.Auxiliary != 29)
                            return [];
                        CmConnect(handle, Marshal.GetFunctionPointerForDelegate(CbConn), new CLong(10000), Marshal.GetFunctionPointerForDelegate(CbDisconn));
                        ev.Target.WaitOne();
                        if (!data.Result.Success)
                            return [];
                        continue;
                    }
                    continue;
                }
                break;
            }
            var result = new Mod.ModDetails[data.NumReturnedDetails];
            for (int i = 0; i < result.Length; i++)
            {
                var item = entries[i];
                result[i] = new(item.AppId, item.Result.Success ? 1 : item.Result.Auxiliary == 9 ? 2 : 0, DateTimeOffset.FromUnixTimeSeconds(item.LastUpdated).Ticks, item.Id, item.ManifestId, Marshal.PtrToStringUTF8(item.Name)!, Marshal.PtrToStringUTF8(item.PreviewUrl)!);
            }
            total = (uint)data.TotalItems;
            return result;
        }
        public unsafe Dictionary<uint, ulong> GetAccessTokens(uint[] appIds)
        {
            var entries = new CmPicsEntry[appIds.Length];
            for (int i = 0; i < appIds.Length; i++)
                entries[i].Id = appIds[i];

            fixed (CmPicsEntry* entriesPtr = entries)
            {
                using var ev = new GCHandle<AutoResetEvent>(new(false));
                CmDataPics data = new()
                {
                    AppEntries = entriesPtr,
                    NumAppEntries = entries.Length,
                    Event = GCHandle<AutoResetEvent>.ToIntPtr(ev)
                };
                CmSetUserData(handle, &data);
                for (; ; )
                {
                    CmGetAccessToken(handle, ref data, Marshal.GetFunctionPointerForDelegate(CbPics), 10000);
                    ev.Target.WaitOne();
                    if (!data.Result.Success)
                    {
                        if (data.Result.Auxiliary != 30)
                            return [];
                        CmSignInAnon(handle, Marshal.GetFunctionPointerForDelegate(CbSignedInPics), 10000);
                        ev.Target.WaitOne();
                        if (!data.Result.Success)
                        {
                            if (data.Result.Auxiliary != 29)
                                return [];
                            CmConnect(handle, Marshal.GetFunctionPointerForDelegate(CbConnPics), new CLong(10000), Marshal.GetFunctionPointerForDelegate(CbDisconn));
                            ev.Target.WaitOne();
                            if (!data.Result.Success)
                                return [];
                            continue;
                        }
                        continue;
                    }
                    break;
                }
            }

            var result = new Dictionary<uint, ulong>();
            foreach (var entry in entries)
            {
                if (entry.Result.Success && entry.AccessToken != 0)
                    result[entry.Id] = entry.AccessToken;
            }
            return result;
        }
        public unsafe List<CmPicsEntry> GetProductInfo(uint[] appIds, Dictionary<uint, ulong> tokens)
        {
            var entries = new CmPicsEntry[appIds.Length];
            for (int i = 0; i < appIds.Length; i++)
            {
                entries[i].Id = appIds[i];
                if (tokens.TryGetValue(appIds[i], out ulong token))
                    entries[i].AccessToken = token;
            }

            fixed (CmPicsEntry* entriesPtr = entries)
            {
                using var ev = new GCHandle<AutoResetEvent>(new(false));
                CmDataPics data = new()
                {
                    AppEntries = entriesPtr,
                    NumAppEntries = entries.Length,
                    TimeoutMs = new CLong(15000), // HTTP download timeout
                    Event = GCHandle<AutoResetEvent>.ToIntPtr(ev)
                };
                CmSetUserData(handle, &data);
                for (; ; )
                {
                    CmGetProductInfo(handle, ref data, Marshal.GetFunctionPointerForDelegate(CbPics), 10000);
                    ev.Target.WaitOne();
                    if (!data.Result.Success)
                    {
                        if (data.Result.Auxiliary != 30)
                            return [];
                        CmSignInAnon(handle, Marshal.GetFunctionPointerForDelegate(CbSignedInPics), 10000);
                        ev.Target.WaitOne();
                        if (!data.Result.Success)
                        {
                            if (data.Result.Auxiliary != 29)
                                return [];
                            CmConnect(handle, Marshal.GetFunctionPointerForDelegate(CbConnPics), new CLong(10000), Marshal.GetFunctionPointerForDelegate(CbDisconn));
                            ev.Target.WaitOne();
                            if (!data.Result.Success)
                                return [];
                            continue;
                        }
                        continue;
                    }
                    break;
                }
            }

            return entries.ToList();
        }

        public unsafe List<(uint AppId, string Name, bool HasDepot)> GetDlcCatalog(uint parentAppId)
        {
            var tokens = GetAccessTokens(new[] { parentAppId });
            var info = GetProductInfo(new[] { parentAppId }, tokens);
            if (info.Count == 0 || !info[0].Result.Success || info[0].Data == 0)
            {
                LauncherLog.Warning("GetDlcCatalog: no product info for app {AppId}", parentAppId);
                if (info.Count > 0)
                    LauncherLog.Warning("GetDlcCatalog: entry Result.Success={Success}, Data={Data}, DataSize={Size}",
                        info[0].Result.Success, info[0].Data, info[0].DataSize);
                return new();
            }

            LauncherLog.Debug("GetDlcCatalog: product info text size={Size}, preview={Text}",
                info[0].DataSize,
                Marshal.PtrToStringUTF8(info[0].Data, Math.Min(info[0].DataSize, 200)));

            string parentVdf = Marshal.PtrToStringUTF8(info[0].Data, info[0].DataSize);
            FreeNativeMemory((void*)info[0].Data);

            var root = VdfParser.Parse(parentVdf);

            var extended = FindVdfSection(root, "extended");
            if (extended is null)
                LauncherLog.Warning("GetDlcCatalog: 'extended' section not found in parsed VDF");
            string? dlcListText = extended?["listofdlc"]?.Value;
            LauncherLog.Debug("GetDlcCatalog: extended.listofdlc = '{List}'", dlcListText ?? "(null)");
            if (string.IsNullOrWhiteSpace(dlcListText))
                return new();

            var dlcAppIds = new List<uint>();
            foreach (string part in dlcListText.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (uint.TryParse(part, out uint dlcAppId) && dlcAppId != 0)
                    dlcAppIds.Add(dlcAppId);

            LauncherLog.Debug("GetDlcCatalog: parsed DLC app IDs: [{Ids}]", string.Join(", ", dlcAppIds));
            if (dlcAppIds.Count == 0)
                return new();

            var dlcTokens = GetAccessTokens(dlcAppIds.ToArray());
            var dlcInfos = GetProductInfo(dlcAppIds.ToArray(), dlcTokens);

            var result = new List<(uint AppId, string Name, bool HasDepot)>();
            foreach (var entry in dlcInfos)
            {
                string dlcName = $"App {entry.Id}";
                bool hasDepot = false;

                if (entry.Result.Success && entry.Data != 0)
                {
                    string dlcVdf = Marshal.PtrToStringUTF8(entry.Data, entry.DataSize);
                    FreeNativeMemory((void*)entry.Data);

                    var dlcRoot = VdfParser.Parse(dlcVdf);
                    var common = FindVdfSection(dlcRoot, "common");
                    if (common?["name"]?.Value is string name)
                        dlcName = name;

                    var depots = FindVdfSection(dlcRoot, "depots");
                    if (depots is not null)
                    {
                        foreach (var depot in depots.Children.Values)
                        {
                            string? sizeStr = depot["manifests"]?["public"]?["size"]?.Value;
                            if (ulong.TryParse(sizeStr, out ulong size) && size >= 10)
                            {
                                hasDepot = true;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    LauncherLog.Warning("GetDlcCatalog: DLC {AppId} failed - Success={Success}, Data={Data}",
                        entry.Id, entry.Result.Success, entry.Data);
                }

                LauncherLog.Debug("GetDlcCatalog: DLC {AppId} -> name='{Name}', hasDepot={HasDepot}",
                    entry.Id, dlcName, hasDepot);
                result.Add((entry.Id, dlcName, hasDepot));
            }

            LauncherLog.Debug("GetDlcCatalog: returning {Count} DLC entries", result.Count);
            return result;
        }
    }
    public class AppManager : SafeHandle
    {
        readonly Error _err;
        public AppManager(LibCtx ctx, string path) : base(0, true)
        {
            handle = OperatingSystem.IsLinux()
                ? AmCreateUtf8(ctx.DangerousGetHandle(), path, out _err)
                : AmCreateUtf16(ctx.DangerousGetHandle(), path, out _err);
        }
        public override bool IsInvalid => handle == 0;
        protected override bool ReleaseHandle()
        {
            AmDestroy(handle);
            return true;
        }

        public Error CreationError => _err;
        public Error SetWorkshopDir(string path) => OperatingSystem.IsLinux()
            ? AmSetWorkshopDirUtf8(handle, path)
            : AmSetWorkshopDirUtf16(handle, path);
        public void LockItemDescs() => AmItemDescsLock(handle);
        public void UnlockItemDescs() => AmItemDescsUnlock(handle);
        public unsafe AmItemDesc* GetItemDesc(ItemId* id) => AmGetItemDesc(handle, id);
        public Error CheckForUpdates(int timeoutMs) => AmCheckForUpdates(handle, timeoutMs);
        public unsafe Error RunJob(in ItemId itemId, ulong manifestId, bool forceVerify, AmJobUpdFunc? updHandler, out AmItemDesc* itemDesc)
        {
            fixed (ItemId* itemIdPtr = &itemId)
            {
                var desc = AmGetItemDesc(handle, itemIdPtr);
                if (desc == null || !desc->Status.HasFlag(AmItemStatus.Job))
                {
                    var res = AmCreateJob(handle, itemIdPtr, manifestId, forceVerify, out desc);
                    if (!res.Success)
                    {
                        itemDesc = null;
                        return res;
                    }
                }
                itemDesc = desc;
                return AmRunJob(handle, ref Unsafe.AsRef<AmItemDesc>(desc), updHandler is null ? 0 : Marshal.GetFunctionPointerForDelegate(updHandler));
            }
        }
        public static void PauseJob(ref AmItemDesc itemDesc) => AmPauseJob(ref itemDesc);
        public Error CancelJob(ref AmItemDesc itemDesc) => AmCancelJob(handle, ref itemDesc);
    }
    public class Exception(string message) : System.Exception(message) { }
}
