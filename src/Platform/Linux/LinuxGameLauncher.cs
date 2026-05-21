using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using TEKLauncher.Data;
using TEKLauncher.Steam;
using TEKLauncher.Utils;

namespace TEKLauncher.Platform;

sealed class LinuxGameLauncher : IGameLauncher
{
    const string ArkAppId = "346110";

    public GameLaunchCapabilities Capabilities { get; } = new(false, false, true);

    public bool DirectXInstalled => true;

    public GameLaunchResult Launch(in GameLaunchRequest request)
    {
        if (!File.Exists(request.ExecutablePath))
            return new(false, $"ARK executable was not found at '{request.ExecutablePath}'.");

        if (TryCreateLaunchContext(request, out var context, out string? errorMessage))
        {
            var acquireResult = LinuxGameRuntimeBootstrap.Acquire();
            if (acquireResult.Assets is not { } assets)
                return new(false, acquireResult.ErrorMessage ?? "Failed to prepare Proton launch assets.");

            string settingsPath;
            try
            {
                settingsPath = WriteRuntimeSettingsFile(request.RuntimeSettings);
            }
            catch (Exception ex)
            {
                return new(false, $"Failed to prepare TEK Game Runtime settings for Proton launch: {ex.Message}");
            }

            var startInfo = context.LaunchTool.Kind == LinuxLaunchToolKind.Wine
              ? CreateWineStartInfo(context, assets, request, settingsPath)
              : CreateProtonStartInfo(context, assets, request, settingsPath);

            ApplyConfiguredEnvironment(startInfo);
            startInfo = WrapLaunchStartInfo(startInfo);

            WriteLaunchDebugInfo(context, assets, request, settingsPath, startInfo);

            try
            {
                Process.Start(startInfo);
                return new(true, null);
            }
            catch (Exception ex)
            {
                return new(false, $"Failed to launch ARK through Proton and TEK Injector: {ex.Message}");
            }
        }

        return new(false, errorMessage ?? "Could not prepare a Linux Proton launch context for ARK.");
    }

    static bool TryCreateLaunchContext(in GameLaunchRequest request, out LinuxLaunchContext context, out string? errorMessage)
    {
        context = default;
        errorMessage = null;

        string? steamInstallPath = LauncherPlatform.Current.GetSteamInstallPath();
        if (string.IsNullOrEmpty(steamInstallPath))
        {
            errorMessage = "Could not locate the Steam installation on Linux. Install Steam so the launcher can resolve a Proton toolchain.";
            return false;
        }

        string? gameRoot = TryGetGameRoot(request.ExecutablePath);
        if (gameRoot is null)
        {
            errorMessage = "Could not resolve the ARK game root from the configured executable path.";
            return false;
        }

        string? libraryRoot = LinuxCompatDataResolver.TryGetSteamLibraryRoot(gameRoot);
        string compatDataPath = LinuxCompatDataResolver.GetResolvedCompatDataPath(gameRoot);

        string? compatToolName = TryGetCompatToolName(steamInstallPath);
        if (!LinuxLaunchToolResolver.TryResolve(Settings.LinuxLaunchTool, steamInstallPath, libraryRoot ?? string.Empty, compatToolName, out LinuxLaunchToolOption? launchTool, out errorMessage)
            || launchTool is null)
        {
            return false;
        }

        Directory.CreateDirectory(compatDataPath);

        context = new(launchTool, steamInstallPath, gameRoot, libraryRoot, compatDataPath, compatToolName ?? "Unknown");
        return true;
    }

    static ProcessStartInfo CreateProtonStartInfo(in LinuxLaunchContext context, in LinuxGameRuntimeAssets assets, in GameLaunchRequest request, string settingsPath)
    {
        string gameDirectory = Path.GetDirectoryName(request.ExecutablePath)!;

        var startInfo = new ProcessStartInfo(context.LaunchTool.ExecutablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = context.GameRoot
        };

        startInfo.Environment["STEAM_COMPAT_DATA_PATH"] = context.CompatDataPath;
        startInfo.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = context.SteamInstallPath;
        startInfo.Environment["STEAM_COMPAT_INSTALL_PATH"] = context.GameRoot;
        startInfo.Environment["STEAM_COMPAT_LIBRARY_PATHS"] = context.LibraryRoot;

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add(assets.InjectorExecutablePath);
        startInfo.ArgumentList.Add("--ti-exe-path");
        startInfo.ArgumentList.Add(ToWinePath(request.ExecutablePath));
        startInfo.ArgumentList.Add("--ti-current-dir");
        startInfo.ArgumentList.Add(ToWinePath(gameDirectory));
        startInfo.ArgumentList.Add("--ti-dll-path");
        startInfo.ArgumentList.Add(ToWinePath(assets.RuntimeLibraryPath));
        startInfo.ArgumentList.Add("--ti-settings-path");
        startInfo.ArgumentList.Add(ToWinePath(settingsPath));

        if (request.HighProcessPriority)
            startInfo.ArgumentList.Add("--ti-high-priority");
        if (request.RunAsAdmin)
            startInfo.ArgumentList.Add("--ti-run-as-admin");

        foreach (string arg in request.Arguments)
            startInfo.ArgumentList.Add(arg);

        return startInfo;
    }

    static ProcessStartInfo CreateWineStartInfo(in LinuxLaunchContext context, in LinuxGameRuntimeAssets assets, in GameLaunchRequest request, string settingsPath)
    {
        var startInfo = new ProcessStartInfo(context.LaunchTool.ExecutablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = context.GameRoot
        };

        startInfo.Environment["WINEPREFIX"] = Path.Combine(context.CompatDataPath, "pfx");
        startInfo.Environment["SteamAppId"] = ArkAppId;
        startInfo.Environment["SteamGameId"] = ArkAppId;

        startInfo.ArgumentList.Add(assets.InjectorExecutablePath);
        startInfo.ArgumentList.Add("--ti-exe-path");
        startInfo.ArgumentList.Add(ToWinePath(request.ExecutablePath));
        startInfo.ArgumentList.Add("--ti-current-dir");
        startInfo.ArgumentList.Add(ToWinePath(Path.GetDirectoryName(request.ExecutablePath)!));
        startInfo.ArgumentList.Add("--ti-dll-path");
        startInfo.ArgumentList.Add(ToWinePath(assets.RuntimeLibraryPath));
        startInfo.ArgumentList.Add("--ti-settings-path");
        startInfo.ArgumentList.Add(ToWinePath(settingsPath));

        if (request.HighProcessPriority)
            startInfo.ArgumentList.Add("--ti-high-priority");
        if (request.RunAsAdmin)
            startInfo.ArgumentList.Add("--ti-run-as-admin");

        foreach (string arg in request.Arguments)
            startInfo.ArgumentList.Add(arg);

        return startInfo;
    }

    static string? TryGetGameRoot(string executablePath)
    {
        DirectoryInfo? directory = new(Path.GetDirectoryName(executablePath)!);
        for (int i = 0; i < 3 && directory is not null; i++)
            directory = directory.Parent;
        return directory?.FullName;
    }

    static string WriteRuntimeSettingsFile(byte[] runtimeSettings)
    {
        byte[] output = runtimeSettings;
        JsonObject? settings = JsonNode.Parse(runtimeSettings)?.AsObject();
        if (settings is not null)
        {
            RewritePathSetting(settings, "workshop_dir_path");
            RewritePathSetting(settings, "workshop_am_path");
            RewritePathSetting(settings, "tek_sc_path");
            output = JsonSerializer.SerializeToUtf8Bytes(settings);
        }

        string launchDirectory = Path.Combine(LauncherBootstrap.AppDataFolder, "tek-game-runtime-linux", "launch");
        Directory.CreateDirectory(launchDirectory);

        string settingsPath = Path.Combine(launchDirectory, "tek-gr-settings.json");
        File.WriteAllBytes(settingsPath, output);
        return settingsPath;
    }

    static void RewritePathSetting(JsonObject settings, string propertyName)
    {
        if (settings[propertyName]?.GetValue<string>() is not string path || string.IsNullOrWhiteSpace(path))
            return;

        settings[propertyName] = ToWinePath(path);
    }

    static void ApplyConfiguredEnvironment(ProcessStartInfo startInfo)
    {
        if (Settings.LinuxUseVkBasalt)
            startInfo.Environment["ENABLE_VKBASALT"] = "1";

        if (Settings.LinuxUseWineFullscreenFsr)
        {
            startInfo.Environment["WINE_FULLSCREEN_FSR"] = "1";
            if (!string.IsNullOrWhiteSpace(Settings.LinuxWineFullscreenFsrStrength))
                startInfo.Environment["WINE_FULLSCREEN_FSR_STRENGTH"] = Settings.LinuxWineFullscreenFsrStrength.Trim();
        }

        foreach (KeyValuePair<string, string> variable in ParseEnvironmentVariables(Settings.LinuxExtraEnvironmentVariables))
            startInfo.Environment[variable.Key] = variable.Value;
    }

    static ProcessStartInfo WrapLaunchStartInfo(ProcessStartInfo startInfo)
    {
        List<LaunchWrapper> wrappers = BuildLaunchWrappers();
        if (wrappers.Count == 0)
            return startInfo;

        string commandFileName = startInfo.FileName;
        List<string> commandArguments = [.. startInfo.ArgumentList];
        for (int i = wrappers.Count - 1; i >= 0; i--)
        {
            LaunchWrapper wrapper = wrappers[i];
            var wrapperArguments = new List<string>(wrapper.Arguments);
            if (!string.IsNullOrWhiteSpace(wrapper.CommandArgumentName))
            {
                wrapperArguments.Add(wrapper.CommandArgumentName);
                wrapperArguments.Add(BuildShellCommand(commandFileName, commandArguments));
            }
            else if (wrapper.RequiresCommandSeparator)
            {
                wrapperArguments.Add("--");
                wrapperArguments.Add(commandFileName);
                wrapperArguments.AddRange(commandArguments);
            }
            else
            {
                wrapperArguments.Add(commandFileName);
                wrapperArguments.AddRange(commandArguments);
            }

            commandFileName = wrapper.FileName;
            commandArguments = wrapperArguments;
        }

        var wrappedStartInfo = new ProcessStartInfo(commandFileName)
        {
            UseShellExecute = false,
            WorkingDirectory = startInfo.WorkingDirectory
        };

        foreach (KeyValuePair<string, string?> variable in startInfo.Environment)
            wrappedStartInfo.Environment[variable.Key] = variable.Value;

        foreach (string argument in commandArguments)
            wrappedStartInfo.ArgumentList.Add(argument);

        return wrappedStartInfo;
    }

    static List<LaunchWrapper> BuildLaunchWrappers()
    {
        var wrappers = new List<LaunchWrapper>();
        if (Settings.LinuxUseGameMode)
            wrappers.Add(new("gamemoderun", [], false, null));
        if (Settings.LinuxUseMangoHud)
            wrappers.Add(new("mangohud", [], false, null));

        foreach (LaunchWrapper wrapper in ParseWrapperCommands(Settings.LinuxLaunchWrappers))
            wrappers.Add(wrapper);

        if (Settings.LinuxUseGamescope)
        {
            var gamescopeArguments = new List<string> { "-f" };
            gamescopeArguments.AddRange(ParseCommandLine(Settings.LinuxGamescopeArguments));
            if (Settings.LinuxUseGamescopeFsr)
            {
                gamescopeArguments.Add("-F");
                gamescopeArguments.Add("fsr");
                if (!string.IsNullOrWhiteSpace(Settings.LinuxGamescopeSharpness))
                {
                    gamescopeArguments.Add("--sharpness");
                    gamescopeArguments.Add(Settings.LinuxGamescopeSharpness.Trim());
                }
            }

            wrappers.Add(new("gamescope", [.. gamescopeArguments], true, null));
        }

        return wrappers;
    }

    static IEnumerable<KeyValuePair<string, string>> ParseEnvironmentVariables(string input)
    {
        foreach (string line in SplitNonEmptyLines(input))
        {
            int separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            string key = line[..separatorIndex].Trim();
            string value = line[(separatorIndex + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
                yield return new(key, value);
        }
    }

    static IEnumerable<LaunchWrapper> ParseWrapperCommands(string input)
    {
        foreach (string line in SplitNonEmptyLines(input))
        {
            string[] parts = ParseCommandLine(line);
            if (parts.Length == 0)
                continue;

            yield return new(parts[0], parts.Length > 1 ? parts[1..] : [], false, null);
        }
    }

    static string BuildShellCommand(string fileName, IReadOnlyList<string> arguments)
    {
        var parts = new List<string>(arguments.Count + 1)
    {
      QuoteShellArgument(fileName)
    };

        foreach (string argument in arguments)
            parts.Add(QuoteShellArgument(argument));

        return string.Join(' ', parts);
    }

    static string QuoteShellArgument(string value)
    {
        if (value.Length == 0)
            return "''";

        return $"'{value.Replace("'", "'\\''")}'";
    }

    static IEnumerable<string> SplitNonEmptyLines(string input)
    {
        return input.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    static string[] ParseCommandLine(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return [];

        var result = new List<string>();
        var builder = new System.Text.StringBuilder();
        bool inQuotes = false;

        foreach (char character in commandLine)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (builder.Length == 0)
                    continue;

                result.Add(builder.ToString());
                builder.Clear();
                continue;
            }

            builder.Append(character);
        }

        if (builder.Length > 0)
            result.Add(builder.ToString());

        return [.. result];
    }

    static string ToWinePath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return $"Z:{fullPath.Replace('/', '\\')}";
    }

    static string? TryGetCompatToolName(string? steamInstallPath)
    {
        if (string.IsNullOrEmpty(steamInstallPath))
            return null;

        ulong steamId64 = App.CurrentUserStatus.SteamId64;
        if (steamId64 == 0)
            return null;

        string localConfigPath = Path.Combine(steamInstallPath, "userdata", ((uint)steamId64).ToString(), "config", "localconfig.vdf");
        if (!File.Exists(localConfigPath))
            return null;

        using var reader = new StreamReader(localConfigPath);
        var mapping = new VDFNode(reader)["UserLocalConfigStore"]?["Software"]?["Valve"]?["Steam"]?["CompatToolMapping"]?[ArkAppId];
        return mapping?["name"]?.Value ?? mapping?["config"]?.Value;
    }

    static void WriteLaunchDebugInfo(in LinuxLaunchContext context, in LinuxGameRuntimeAssets assets, in GameLaunchRequest request, string settingsPath, ProcessStartInfo startInfo)
    {
        try
        {
            string path = Path.Combine(LauncherPlatform.Current.AppDataFolder, "last-linux-launch.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var writer = new StreamWriter(path, false);
            writer.WriteLine($"Launch tool: {context.LaunchTool.DisplayName}");
            writer.WriteLine($"Launch tool kind: {context.LaunchTool.Kind}");
            writer.WriteLine($"Launch executable: {context.LaunchTool.ExecutablePath}");
            writer.WriteLine($"Steam install path: {context.SteamInstallPath}");
            writer.WriteLine($"Game root: {context.GameRoot}");
            writer.WriteLine($"Steam library root: {context.LibraryRoot ?? "<managed by launcher>"}");
            writer.WriteLine($"Compat data path: {context.CompatDataPath}");
            writer.WriteLine($"Steam compat tool: {context.CompatTool}");
            writer.WriteLine($"Configured custom prefix: {Settings.LinuxCompatDataPath}");
            writer.WriteLine($"Configured extra env vars: {Settings.LinuxExtraEnvironmentVariables}");
            writer.WriteLine($"Configured wrappers: {Settings.LinuxLaunchWrappers}");
            writer.WriteLine($"Use GameMode: {Settings.LinuxUseGameMode}");
            writer.WriteLine($"Use Gamescope: {Settings.LinuxUseGamescope}");
            writer.WriteLine($"Gamescope args: {Settings.LinuxGamescopeArguments}");
            writer.WriteLine($"Use Gamescope FSR: {Settings.LinuxUseGamescopeFsr}");
            writer.WriteLine($"Gamescope sharpness: {Settings.LinuxGamescopeSharpness}");
            writer.WriteLine($"Use MangoHud: {Settings.LinuxUseMangoHud}");
            writer.WriteLine($"Use vkBasalt: {Settings.LinuxUseVkBasalt}");
            writer.WriteLine($"Use Wine FSR: {Settings.LinuxUseWineFullscreenFsr}");
            writer.WriteLine($"Wine FSR strength: {Settings.LinuxWineFullscreenFsrStrength}");
            writer.WriteLine($"Injector path: {assets.InjectorExecutablePath}");
            writer.WriteLine($"Runtime path: {assets.RuntimeLibraryPath}");
            writer.WriteLine($"Settings path: {settingsPath}");
            writer.WriteLine($"RunAsAdmin requested: {request.RunAsAdmin}");
            writer.WriteLine($"HighProcessPriority requested: {request.HighProcessPriority}");
            writer.WriteLine($"Arguments: {string.Join(' ', request.Arguments)}");
            writer.WriteLine($"Proton command: {startInfo.FileName} {string.Join(' ', startInfo.ArgumentList)}");
        }
        catch
        {
        }
    }

    readonly record struct LinuxLaunchContext(LinuxLaunchToolOption LaunchTool, string SteamInstallPath, string GameRoot, string? LibraryRoot, string CompatDataPath, string CompatTool);

    readonly record struct LaunchWrapper(string FileName, string[] Arguments, bool RequiresCommandSeparator, string? CommandArgumentName);
}