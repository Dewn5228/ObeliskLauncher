using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Reflection;

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
