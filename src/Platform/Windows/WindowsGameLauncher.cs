using System.Runtime.InteropServices;

namespace ObeliskLauncher.Platform;

sealed class WindowsGameLauncher : IGameLauncher
{
    GameRuntimeAssets? _assets;

    public GameLaunchCapabilities Capabilities { get; } = new(true, true, true);

    static string InjectorPath => Path.Combine(LauncherBootstrap.AppDataFolder, "libtek-injector.dll");

    static string RuntimePath => Path.Combine(LauncherBootstrap.AppDataFolder, "libtek-game-runtime.dll");

    public bool DirectXInstalled
    {
        get
        {
            string system32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
            return File.Exists(Path.Combine(system32, "d3d11.dll"))
                && File.Exists(Path.Combine(system32, "xinput1_3.dll"))
                && File.Exists(Path.Combine(system32, "xapofx1_5.dll"))
                && File.Exists(Path.Combine(system32, "x3daudio1_7.dll"));
        }
    }

    public unsafe GameLaunchResult Launch(in GameLaunchRequest request)
    {
        EnsureRuntimeLibraries();

        var argv = new nint[request.Arguments.Count];
        for (int i = 0; i < argv.Length; i++)
            argv[i] = Marshal.StringToHGlobalUni(request.Arguments[i]);

        var argvNative = Marshal.AllocHGlobal(argv.Length * Marshal.SizeOf<nint>());
        Marshal.Copy(argv, 0, argvNative, argv.Length);

        TEKInjector.InjFlags flags = 0;
        if (request.HighProcessPriority)
            flags |= TEKInjector.InjFlags.HighPrio;
        if (request.RunAsAdmin)
            flags |= TEKInjector.InjFlags.RunAsAdmin;

        fixed (byte* dataPtr = request.RuntimeSettings)
        {
            string currentDir = Path.GetDirectoryName(Path.GetFullPath(request.ExecutablePath)) ?? ".";

            var injArgs = new TEKInjector.Args
            {
                ExePath = Marshal.StringToHGlobalUni(request.ExecutablePath),
                CurrentDir = Marshal.StringToHGlobalUni(currentDir),
                DllPath = Marshal.StringToHGlobalUni(RuntimePath),
                Type = TEKInjector.LoadType.Pipe,
                Argc = argv.Length,
                Argv = argvNative,
                Flags = flags,
                DataSize = (uint)request.RuntimeSettings.Length,
                Data = (nint)dataPtr
            };

            TEKInjector.RunGame(ref injArgs);

            foreach (var ptr in argv)
                Marshal.FreeHGlobal(ptr);
            Marshal.FreeHGlobal(argvNative);
            Marshal.FreeHGlobal(injArgs.ExePath);
            Marshal.FreeHGlobal(injArgs.CurrentDir);
            Marshal.FreeHGlobal(injArgs.DllPath);

            if (injArgs.Result == TEKInjector.Res.Ok)
                return new(true, null);

            var msg = injArgs.Result switch
            {
                TEKInjector.Res.GetTokenInfo => "Failed to get process token information",
                TEKInjector.Res.OpenToken => "Failed to open current process token",
                TEKInjector.Res.DuplicateToken => "Failed to duplicate process token",
                TEKInjector.Res.SetTokenInfo => "Failed to set token information",
                TEKInjector.Res.CreateProcess => "Failed to create game process",
                TEKInjector.Res.MemAlloc => "Failed to allocate memory in game process",
                TEKInjector.Res.MemWrite => "Failed to write to game process memory",
                TEKInjector.Res.SecDesc => "Failed to setup security descriptor for the pipe",
                TEKInjector.Res.CreateMapping => "Failed to create file mapping",
                TEKInjector.Res.MapView => "Failed to map view of the file mapping",
                TEKInjector.Res.CreateThread => "Failed to create injection thread",
                TEKInjector.Res.ThreadWait => "Failed to wait for injection thread to finish",
                TEKInjector.Res.DllLoad => "TEK Game Runtime failed to load",
                TEKInjector.Res.ResumeThread => "Failed to resume game's main thread",
                _ => $"Unknown result code {(int)injArgs.Result}"
            };
            if (injArgs.Win32Error != 0)
                msg += $": ({injArgs.Win32Error}) {Marshal.GetPInvokeErrorMessage((int)injArgs.Win32Error)}";
            return new(false, msg);
        }
    }

    void EnsureRuntimeLibraries()
    {
        if (_assets is not null)
            return;

        var result = GameRuntimeBootstrap.Acquire();
        if (result.Assets is not { } assets)
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to acquire TEK Game Runtime binaries.");

        File.Copy(assets.InjectorExecutablePath, InjectorPath, overwrite: true);
        File.Copy(assets.RuntimeLibraryPath, RuntimePath, overwrite: true);

        NativeLibrary.Load(InjectorPath);
        _assets = assets;
    }
}