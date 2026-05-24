using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using TEKLauncher.Data;
using TEKLauncher.Steam;
using TEKLauncher.Utils;

namespace TEKLauncher.Platform;

sealed class LinuxGameLauncher : IGameLauncher
{
    public GameLaunchCapabilities Capabilities { get; } = new(false, false, true);

    public bool DirectXInstalled => true;

    public GameLaunchResult Launch(in GameLaunchRequest request)
    {
        var launchStopwatch = Stopwatch.StartNew();
        LauncherLog.Information("Linux launch entry. Exe={ExePath}", request.ExecutablePath);
        if (!File.Exists(request.ExecutablePath))
        {
            LauncherLog.Error("Linux launch aborted: executable missing at {ExePath}", request.ExecutablePath);
            return new(false, $"ARK executable was not found at '{request.ExecutablePath}'.");
        }

        if (TryCreateLaunchContext(request, out var context, out string? errorMessage))
        {
            LauncherLog.Debug("Linux launch context resolved. GameId={GameId}, SteamAppId={SteamAppId}, Tool={ToolKind}:{ToolPath}, CompatData={CompatDataPath}, CompatTool={CompatTool}",
                context.GameId,
                context.SteamAppId,
                context.LaunchTool.Kind,
                context.LaunchTool.ExecutablePath,
                context.CompatDataPath,
                context.CompatTool);

            LauncherLog.Debug("Linux launch selection. ConfiguredToolId={ConfiguredToolId}, ResolvedToolId={ResolvedToolId}",
                context.ConfiguredToolId,
                context.LaunchTool.Id);

            PromoteResolvedToolSelection(context);

            var acquireStopwatch = Stopwatch.StartNew();
            LauncherLog.Debug("Linux launch acquiring runtime assets...");
            var acquireResult = GameRuntimeBootstrap.Acquire();
            acquireStopwatch.Stop();
            LauncherLog.Debug("Linux launch runtime asset acquisition completed in {ElapsedMs} ms", acquireStopwatch.ElapsedMilliseconds);
            if (acquireResult.Assets is not { } assets)
            {
                LauncherLog.Error("Linux launch aborted: failed to acquire runtime assets. Error={Error}", acquireResult.ErrorMessage ?? "unknown");
                return new(false, acquireResult.ErrorMessage ?? "Failed to prepare Proton launch assets.");
            }

            string settingsPath;
            try
            {
                settingsPath = WriteRuntimeSettingsFile(request.RuntimeSettings);
            }
            catch (Exception ex)
            {
                LauncherLog.Error(ex, "Linux launch aborted: failed to write runtime settings file");
                return new(false, $"Failed to prepare TEK Game Runtime settings for Proton launch: {ex.Message}");
            }

            var startInfo = context.LaunchTool.Kind == LinuxLaunchToolKind.Wine
              ? CreateWineStartInfo(context, assets, request, settingsPath)
              : CreateProtonStartInfo(context, assets, request, settingsPath);

            ApplyConfiguredEnvironment(startInfo, context.GameId);
            startInfo = WrapLaunchStartInfo(startInfo, context.GameId);

            WriteLaunchDebugInfo(context, assets, request, settingsPath, startInfo);

            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            LauncherLog.Debug("Linux launch command prepared. FileName={FileName}, WorkingDirectory={WorkingDirectory}, Args={Args}",
                startInfo.FileName,
                startInfo.WorkingDirectory,
                string.Join(' ', startInfo.ArgumentList));

            try
            {
                Process? process = Process.Start(startInfo);
                if (process is null)
                {
                    LauncherLog.Error("Linux launch failed: Process.Start returned null");
                    return new(false, "Failed to launch ARK through Proton and TEK Injector: Process.Start returned null.");
                }

                LauncherLog.Information("Linux launch process started. PID={Pid}, ProcessName={ProcessName}", process.Id, process.ProcessName);
                AttachProcessOutputLogging(process, context.GameId);
                _ = Task.Run(() => LogEarlyProcessOutcome(process));
                launchStopwatch.Stop();
                LauncherLog.Debug("Linux launch pipeline finished in {ElapsedMs} ms", launchStopwatch.ElapsedMilliseconds);
                return new(true, null);
            }
            catch (Exception ex)
            {
                LauncherLog.Error(ex, "Linux launch failed during Process.Start");
                return new(false, $"Failed to launch ARK through Proton and TEK Injector: {ex.Message}");
            }
        }

        launchStopwatch.Stop();
        LauncherLog.Error("Linux launch failed: could not create launch context. Error={Error}", errorMessage ?? "unknown");
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
        string steamAppId = ResolveSteamAppIdString(request.RuntimeSettings);
        string gameId = Settings.GetGameIdBySteamAppId(steamAppId);
        string compatDataPath = LinuxCompatDataResolver.GetResolvedCompatDataPath(gameRoot, steamAppId);

        string? compatToolName = TryGetCompatToolName(steamInstallPath, steamAppId);
        string configuredToolId = LinuxLaunchToolResolver.NormalizeSelection(Settings.GetLinuxLaunchTool(gameId));
        if (!LinuxLaunchToolResolver.TryResolve(configuredToolId, steamInstallPath, libraryRoot ?? string.Empty, compatToolName, out LinuxLaunchToolOption? launchTool, out errorMessage)
            || launchTool is null)
        {
            return false;
        }

        Directory.CreateDirectory(compatDataPath);

        context = new(launchTool, steamInstallPath, gameRoot, libraryRoot, compatDataPath, compatToolName ?? "Unknown", steamAppId, gameId, configuredToolId);
        return true;
    }

    static ProcessStartInfo CreateProtonStartInfo(in LinuxLaunchContext context, in GameRuntimeAssets assets, in GameLaunchRequest request, string settingsPath)
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

    static ProcessStartInfo CreateWineStartInfo(in LinuxLaunchContext context, in GameRuntimeAssets assets, in GameLaunchRequest request, string settingsPath)
    {
        var startInfo = new ProcessStartInfo(context.LaunchTool.ExecutablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = context.GameRoot
        };

        startInfo.Environment["WINEPREFIX"] = Path.Combine(context.CompatDataPath, "pfx");
        startInfo.Environment["SteamAppId"] = context.SteamAppId;
        startInfo.Environment["SteamGameId"] = context.SteamAppId;

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

    static void ApplyConfiguredEnvironment(ProcessStartInfo startInfo, string gameId)
    {
        if (Settings.GetLinuxUseVkBasalt(gameId))
            startInfo.Environment["ENABLE_VKBASALT"] = "1";

        if (Settings.GetLinuxUseWineFullscreenFsr(gameId))
        {
            startInfo.Environment["WINE_FULLSCREEN_FSR"] = "1";
            string fsrStrength = Settings.GetLinuxWineFullscreenFsrStrength(gameId);
            if (!string.IsNullOrWhiteSpace(fsrStrength))
                startInfo.Environment["WINE_FULLSCREEN_FSR_STRENGTH"] = fsrStrength.Trim();
        }

        foreach (KeyValuePair<string, string> variable in ParseEnvironmentVariables(Settings.GetLinuxExtraEnvironmentVariables(gameId)))
            startInfo.Environment[variable.Key] = variable.Value;
    }

    static ProcessStartInfo WrapLaunchStartInfo(ProcessStartInfo startInfo, string gameId)
    {
        List<LaunchWrapper> wrappers = BuildLaunchWrappers(gameId);
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

    static List<LaunchWrapper> BuildLaunchWrappers(string gameId)
    {
        var wrappers = new List<LaunchWrapper>();
        if (Settings.GetLinuxUseGameMode(gameId))
            wrappers.Add(new("gamemoderun", [], false, null));
        if (Settings.GetLinuxUseMangoHud(gameId))
            wrappers.Add(new("mangohud", [], false, null));

        foreach (LaunchWrapper wrapper in ParseWrapperCommands(Settings.GetLinuxLaunchWrappers(gameId)))
            wrappers.Add(wrapper);

        if (Settings.GetLinuxUseGamescope(gameId))
        {
            var gamescopeArguments = new List<string> { "-f" };
            gamescopeArguments.AddRange(ParseCommandLine(Settings.GetLinuxGamescopeArguments(gameId)));
            if (Settings.GetLinuxUseGamescopeFsr(gameId))
            {
                gamescopeArguments.Add("-F");
                gamescopeArguments.Add("fsr");
                string sharpness = Settings.GetLinuxGamescopeSharpness(gameId);
                if (!string.IsNullOrWhiteSpace(sharpness))
                {
                    gamescopeArguments.Add("--sharpness");
                    gamescopeArguments.Add(sharpness.Trim());
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

    static string? TryGetCompatToolName(string? steamInstallPath, string steamAppId)
    {
        if (string.IsNullOrEmpty(steamInstallPath))
            return null;

        ulong steamId64 = App.CurrentUserStatus.SteamId64;
        if (steamId64 == 0)
            return null;

        string localConfigPath = Path.Combine(steamInstallPath, "userdata", steamId64.ToString(), "config", "localconfig.vdf");
        if (!File.Exists(localConfigPath))
            return null;

        using var reader = new StreamReader(localConfigPath);
        var mapping = VdfParser.Parse(reader.ReadToEnd())["UserLocalConfigStore"]?["Software"]?["Valve"]?["Steam"]?["CompatToolMapping"]?[steamAppId];
        return mapping?["name"]?.Value ?? mapping?["config"]?.Value;
    }

    static string ResolveSteamAppIdString(byte[] runtimeSettings)
    {
        try
        {
            if (JsonNode.Parse(runtimeSettings)?["app_id"]?.GetValue<uint>() is uint appId && appId > 0)
                return appId.ToString();
        }
        catch
        {
        }

        return ActiveGameManager.Current.SteamAppId.ToString();
    }

    static void WriteLaunchDebugInfo(in LinuxLaunchContext context, in GameRuntimeAssets assets, in GameLaunchRequest request, string settingsPath, ProcessStartInfo startInfo)
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
            writer.WriteLine($"Steam app ID: {context.SteamAppId}");
            writer.WriteLine($"Configured custom prefix: {Settings.GetLinuxCompatDataPath(context.GameId)}");
            writer.WriteLine($"Configured extra env vars: {Settings.GetLinuxExtraEnvironmentVariables(context.GameId)}");
            writer.WriteLine($"Configured wrappers: {Settings.GetLinuxLaunchWrappers(context.GameId)}");
            writer.WriteLine($"Use GameMode: {Settings.GetLinuxUseGameMode(context.GameId)}");
            writer.WriteLine($"Use Gamescope: {Settings.GetLinuxUseGamescope(context.GameId)}");
            writer.WriteLine($"Gamescope args: {Settings.GetLinuxGamescopeArguments(context.GameId)}");
            writer.WriteLine($"Use Gamescope FSR: {Settings.GetLinuxUseGamescopeFsr(context.GameId)}");
            writer.WriteLine($"Gamescope sharpness: {Settings.GetLinuxGamescopeSharpness(context.GameId)}");
            writer.WriteLine($"Use MangoHud: {Settings.GetLinuxUseMangoHud(context.GameId)}");
            writer.WriteLine($"Use vkBasalt: {Settings.GetLinuxUseVkBasalt(context.GameId)}");
            writer.WriteLine($"Use Wine FSR: {Settings.GetLinuxUseWineFullscreenFsr(context.GameId)}");
            writer.WriteLine($"Wine FSR strength: {Settings.GetLinuxWineFullscreenFsrStrength(context.GameId)}");
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

    static void LogEarlyProcessOutcome(Process process)
    {
        try
        {
            if (process.WaitForExit(15000))
            {
                LauncherLog.Warning("Linux launch process exited early. PID={Pid}, ExitCode={ExitCode}", process.Id, process.ExitCode);
                return;
            }

            LauncherLog.Information("Linux launch process is still running after startup window. PID={Pid}", process.Id);
        }
        catch (Exception ex)
        {
            LauncherLog.Warning("Failed to inspect launch process outcome. Reason={Reason}", ex.Message);
        }
        finally
        {
            try
            {
                process.Dispose();
            }
            catch
            {
            }
        }
    }

    static void PromoteResolvedToolSelection(in LinuxLaunchContext context)
    {
        if (!context.ConfiguredToolId.Equals(LinuxLaunchToolResolver.AutomaticId, StringComparison.OrdinalIgnoreCase))
            return;

        if (context.LaunchTool.Id.Equals(LinuxLaunchToolResolver.AutomaticId, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            Settings.RegisterCustomLinuxLaunchTool(context.LaunchTool.Id);
            Settings.SetLinuxLaunchTool(context.LaunchTool.Id, context.GameId);
            Settings.Save();
            LauncherLog.Information("Linux launch auto-selection promoted to persisted tool. GameId={GameId}, ToolId={ToolId}", context.GameId, context.LaunchTool.Id);
        }
        catch (Exception ex)
        {
            LauncherLog.Warning("Failed to persist resolved Linux launch tool. GameId={GameId}, ToolId={ToolId}, Reason={Reason}", context.GameId, context.LaunchTool.Id, ex.Message);
        }
    }

    static void AttachProcessOutputLogging(Process process, string gameId)
    {
        try
        {
            string logsDir = Path.Combine(LauncherPlatform.Current.AppDataFolder, "logs");
            Directory.CreateDirectory(logsDir);
            string filePath = Path.Combine(logsDir, $"launch-child-{DateTime.Now:yyyyMMdd-HHmmss}-{gameId}.log");

            var writer = new StreamWriter(filePath, false) { AutoFlush = true };
            object gate = new();

            void WriteLine(string channel, string? line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    return;

                lock (gate)
                    writer.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{channel}] {line}");
            }

            process.OutputDataReceived += (_, args) => WriteLine("OUT", args.Data);
            process.ErrorDataReceived += (_, args) => WriteLine("ERR", args.Data);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _ = Task.Run(() =>
            {
                try
                {
                    process.WaitForExit();
                }
                catch
                {
                }
                finally
                {
                    lock (gate)
                    {
                        writer.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [SYS] Child process finished.");
                        writer.Dispose();
                    }
                }
            });

            LauncherLog.Information("Linux launch child output capture enabled. File={FilePath}", filePath);
        }
        catch (Exception ex)
        {
            LauncherLog.Warning("Failed to enable child output capture: {Reason}", ex.Message);
        }
    }

    readonly record struct LinuxLaunchContext(LinuxLaunchToolOption LaunchTool, string SteamInstallPath, string GameRoot, string? LibraryRoot, string CompatDataPath, string CompatTool, string SteamAppId, string GameId, string ConfiguredToolId);

    readonly record struct LaunchWrapper(string FileName, string[] Arguments, bool RequiresCommandSeparator, string? CommandArgumentName);
}