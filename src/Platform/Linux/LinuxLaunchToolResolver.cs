using System.Linq;

namespace ObeliskLauncher.Platform;

public enum LinuxLaunchToolKind
{
    Proton,
    Wine
}

public sealed record LinuxLaunchToolOption(string Id, string DisplayName, LinuxLaunchToolKind Kind, string ExecutablePath);

static class LinuxLaunchToolResolver
{
    public const string AutomaticId = "auto";

    static readonly HashSet<string> s_wineHelperNames = new(StringComparer.OrdinalIgnoreCase)
  {
    "wineboot",
    "winebrowser",
    "winecfg",
    "wineconsole",
    "winedbg",
    "winefile",
    "winepath",
    "wineserver",
    "winetricks"
  };

    public static IReadOnlyList<LinuxLaunchToolOption> GetAvailableOptions(string? selectedId = null, string? gamePath = null, IEnumerable<string>? importedToolIds = null)
    {
        var options = new List<LinuxLaunchToolOption>
    {
      new(AutomaticId, "Automatic (Steam default / best available)", LinuxLaunchToolKind.Proton, string.Empty)
    };

        options.AddRange(DiscoverTools(GetSearchRoots(gamePath), importedToolIds)
          .OrderBy(option => option.Kind)
          .ThenByDescending(option => GetToolScore(option, null))
          .ThenBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase));

        string normalizedSelection = NormalizeSelection(selectedId);
        if (!normalizedSelection.Equals(AutomaticId, StringComparison.OrdinalIgnoreCase)
            && options.All(option => !option.Id.Equals(normalizedSelection, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(CreateUnavailableOption(normalizedSelection));
        }

        return options;
    }

    public static string NormalizeSelection(string? selection)
      => string.IsNullOrWhiteSpace(selection) ? AutomaticId : selection;

    public static int GetToolScore(LinuxLaunchToolOption option, string? compatToolName)
      => ScoreTool(option, compatToolName);

    public static LinuxLaunchToolOption? GetBestPick(IEnumerable<LinuxLaunchToolOption> tools, string? compatToolName = null)
    {
        var usable = tools.Where(tool => !tool.Id.Equals(AutomaticId, StringComparison.OrdinalIgnoreCase)).ToList();
        return usable.Where(tool => tool.Kind == LinuxLaunchToolKind.Proton)
            .OrderByDescending(tool => ScoreTool(tool, compatToolName))
            .ThenBy(tool => tool.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault()
          ?? usable.Where(tool => tool.Kind == LinuxLaunchToolKind.Wine)
            .OrderByDescending(tool => ScoreTool(tool, compatToolName))
            .ThenBy(tool => tool.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();
    }

    public static string DescribePickReason(LinuxLaunchToolOption option, string? compatToolName = null)
    {
        if (!string.IsNullOrWhiteSpace(compatToolName) && MatchesToolName(option, compatToolName))
            return Locale.Get("linuxToolRank.steamAssigned");
        if (option.Kind == LinuxLaunchToolKind.Wine)
            return Locale.Get("linuxToolRank.wineFallback");
        if (option.DisplayName.Contains("Experimental", StringComparison.OrdinalIgnoreCase))
            return Locale.Get("linuxToolRank.experimental");
        string toolDirectoryName = GetToolDirectoryName(option);
        if (IsGeFamilyName(toolDirectoryName))
            return Locale.Get("linuxToolRank.geBuild");
        if (toolDirectoryName.Contains("cachy", StringComparison.OrdinalIgnoreCase)
            || option.ExecutablePath.Contains("cachy", StringComparison.OrdinalIgnoreCase))
            return Locale.Get("linuxToolRank.cachy");
        if (IsThirdPartyRunnerPath(option.ExecutablePath.Replace('\\', '/'))
            || option.ExecutablePath.Replace('\\', '/').Contains("/compatibilitytools.d/", StringComparison.OrdinalIgnoreCase))
            return Locale.Get("linuxToolRank.userManaged");
        return Locale.Get("linuxToolRank.stockProton");
    }

    public static bool TryResolve(string? selectionId, string steamInstallPath, string libraryRoot, string? compatToolName, out LinuxLaunchToolOption? option, out string? errorMessage)
    {
        string normalizedSelection = NormalizeSelection(selectionId);
        var discoveredTools = DiscoverTools([steamInstallPath, libraryRoot], Settings.CustomLinuxLaunchToolIds);

        if (normalizedSelection.Equals(AutomaticId, StringComparison.OrdinalIgnoreCase))
        {
            option = TryResolveAutomatic(discoveredTools, compatToolName);
            if (option is not null)
            {
                errorMessage = null;
                return true;
            }

            errorMessage = "Could not resolve a usable Proton or Wine launch tool on Linux. Install a Proton compatibility tool in Steam (or a Wine/Proton runner in Bottles, Lutris, or Heroic) or ensure Wine is available in PATH.";
            return false;
        }

        option = discoveredTools.FirstOrDefault(tool => tool.Id.Equals(normalizedSelection, StringComparison.OrdinalIgnoreCase));
        if (option is not null)
        {
            errorMessage = null;
            return true;
        }

        if (TryParseSelectionId(normalizedSelection, out LinuxLaunchToolKind kind, out string executablePath) && File.Exists(executablePath))
        {
            option = new(normalizedSelection, Path.GetFileName(Path.GetDirectoryName(executablePath) ?? executablePath), kind, executablePath);
            errorMessage = null;
            return true;
        }

        errorMessage = $"Configured Linux launch tool '{normalizedSelection}' is no longer available. Pick another compatibility tool in launcher settings.";
        return false;
    }

    static LinuxLaunchToolOption CreateUnavailableOption(string selectionId)
    {
        LinuxLaunchToolKind kind = TryParseSelectionId(selectionId, out LinuxLaunchToolKind parsedKind, out string executablePath)
          ? parsedKind
          : LinuxLaunchToolKind.Proton;
        string fileName = string.IsNullOrWhiteSpace(executablePath) ? selectionId : Path.GetFileName(executablePath);
        return new(selectionId, $"Unavailable: {fileName}", kind, executablePath);
    }

    static IEnumerable<string> GetSearchRoots(string? gamePath)
    {
        if (LauncherPlatform.Current.GetSteamInstallPath() is string steamInstallPath)
            yield return steamInstallPath;

        if (!string.IsNullOrWhiteSpace(gamePath) && TryGetSteamLibraryRoot(gamePath) is string libraryRoot)
            yield return libraryRoot;
    }

    static IReadOnlyList<LinuxLaunchToolOption> DiscoverTools(IEnumerable<string> roots, IEnumerable<string>? importedToolIds)
    {
        var options = new List<LinuxLaunchToolOption>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string root in roots.Where(static root => !string.IsNullOrWhiteSpace(root)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (LinuxLaunchToolOption option in EnumerateProtonTools(root))
                if (seenIds.Add(option.Id))
                    options.Add(option);

            foreach (LinuxLaunchToolOption option in EnumerateWineToolsFromSteam(root))
                if (seenIds.Add(option.Id))
                    options.Add(option);
        }

        foreach (string compatDirectory in GetWellKnownCompatDirectories())
        {
            foreach (LinuxLaunchToolOption option in EnumerateCollectionTools(compatDirectory, null))
                if (seenIds.Add(option.Id))
                    options.Add(option);
        }

        foreach ((string directory, string label) in GetWellKnownRunnerCollections())
        {
            foreach (LinuxLaunchToolOption option in EnumerateCollectionTools(directory, label))
                if (seenIds.Add(option.Id))
                    options.Add(option);
        }

        foreach (LinuxLaunchToolOption option in EnumerateWineToolsFromPath())
            if (seenIds.Add(option.Id))
                options.Add(option);

        if (importedToolIds is not null)
            foreach (LinuxLaunchToolOption option in EnumerateImportedTools(importedToolIds))
                if (seenIds.Add(option.Id))
                    options.Add(option);

        return options;
    }

    static IEnumerable<LinuxLaunchToolOption> EnumerateImportedTools(IEnumerable<string> toolIds)
    {
        foreach (string toolId in toolIds)
            if (TryCreateImportedOption(toolId, out LinuxLaunchToolOption? option) && option is not null)
                yield return option;
    }

    static bool TryCreateImportedOption(string selectionId, out LinuxLaunchToolOption? option)
    {
        option = null;
        if (!TryParseSelectionId(selectionId, out LinuxLaunchToolKind kind, out string executablePath) || !File.Exists(executablePath))
            return false;

        string toolDirectory = Path.GetFileName(Path.GetDirectoryName(executablePath) ?? executablePath);
        string displayName = kind == LinuxLaunchToolKind.Proton
          ? $"Custom Proton: {toolDirectory}"
          : $"Custom Wine: {toolDirectory}";
        option = new(selectionId, displayName, kind, Path.GetFullPath(executablePath));
        return true;
    }

    static LinuxLaunchToolOption? TryResolveAutomatic(IReadOnlyList<LinuxLaunchToolOption> tools, string? compatToolName)
      => GetBestPick(tools, compatToolName);

    static bool MatchesToolName(LinuxLaunchToolOption option, string compatToolName)
    {
        string directoryName = Path.GetFileName(Path.GetDirectoryName(option.ExecutablePath) ?? option.ExecutablePath);
        return directoryName.Equals(compatToolName, StringComparison.OrdinalIgnoreCase)
          || option.DisplayName.Equals(compatToolName, StringComparison.OrdinalIgnoreCase)
          || directoryName.Contains(compatToolName, StringComparison.OrdinalIgnoreCase)
          || compatToolName.Contains(directoryName, StringComparison.OrdinalIgnoreCase);
    }

    static int ScoreTool(LinuxLaunchToolOption option, string? compatToolName)
    {
        if (!string.IsNullOrWhiteSpace(compatToolName) && MatchesToolName(option, compatToolName))
            return 1000;

        string toolDirectoryName = GetToolDirectoryName(option);
        string normalizedPath = option.ExecutablePath.Replace('\\', '/');
        bool userManaged = normalizedPath.Contains("/compatibilitytools.d/", StringComparison.OrdinalIgnoreCase)
            || IsThirdPartyRunnerPath(normalizedPath);

        if (option.Kind == LinuxLaunchToolKind.Wine)
        {
            if (IsTopTierWineRunner(toolDirectoryName))
                return 100;
            int wineScore = 0;
            if (IsGeFamilyName(toolDirectoryName))
                wineScore += 50;
            if (userManaged)
                wineScore += 25;
            return wineScore;
        }

        if (IsGeFamilyName(toolDirectoryName))
            return 500;
        if (toolDirectoryName.Contains("cachy", StringComparison.OrdinalIgnoreCase))
            return 450;
        if (toolDirectoryName.Contains("bleeding", StringComparison.OrdinalIgnoreCase))
            return 425;
        if (toolDirectoryName.Contains("experimental", StringComparison.OrdinalIgnoreCase))
            return 400;
        if (userManaged)
            return 250;
        return 100;
    }

    static bool IsTopTierWineRunner(string toolDirectoryName)
      => toolDirectoryName.StartsWith("soda", StringComparison.OrdinalIgnoreCase)
        || toolDirectoryName.StartsWith("caffe", StringComparison.OrdinalIgnoreCase)
        || toolDirectoryName.Contains("kron4ek", StringComparison.OrdinalIgnoreCase);

    static bool IsGeFamilyName(string toolDirectoryName)
      => toolDirectoryName.Contains("GE-Proton", StringComparison.OrdinalIgnoreCase)
        || toolDirectoryName.Contains("Wine-GE", StringComparison.OrdinalIgnoreCase)
        || toolDirectoryName.StartsWith("GE-", StringComparison.OrdinalIgnoreCase);

    static string GetToolDirectoryName(LinuxLaunchToolOption option)
    {
        string? directory = Path.GetDirectoryName(option.ExecutablePath);
        if (option.Kind == LinuxLaunchToolKind.Wine)
        {
            for (int i = 0; i < 2 && directory is not null; i++)
            {
                string name = Path.GetFileName(directory);
                if (!name.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("files", StringComparison.OrdinalIgnoreCase))
                    break;
                directory = Path.GetDirectoryName(directory);
            }
        }
        return Path.GetFileName(directory ?? option.ExecutablePath);
    }

    static IEnumerable<LinuxLaunchToolOption> EnumerateProtonTools(string root)
    {
        foreach (string baseDirectory in GetSteamToolDirectories(root))
        {
            foreach (string toolDirectory in EnumerateDirectoriesSafe(baseDirectory))
            {
                string protonPath = Path.Combine(toolDirectory, "proton");
                if (File.Exists(protonPath))
                    yield return new($"proton:{Path.GetFullPath(protonPath)}", Path.GetFileName(toolDirectory), LinuxLaunchToolKind.Proton, Path.GetFullPath(protonPath));
            }
        }
    }

    static IEnumerable<LinuxLaunchToolOption> EnumerateWineToolsFromSteam(string root)
    {
        foreach (string baseDirectory in GetSteamToolDirectories(root))
        {
            foreach (string toolDirectory in EnumerateDirectoriesSafe(baseDirectory))
            {
                string? winePath = FindWineExecutable(toolDirectory);
                if (winePath is not null)
                    yield return new($"wine:{Path.GetFullPath(winePath)}", $"{Path.GetFileName(toolDirectory)} (Wine)", LinuxLaunchToolKind.Wine, Path.GetFullPath(winePath));
            }
        }
    }

    static IEnumerable<LinuxLaunchToolOption> EnumerateWineToolsFromPath()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            yield break;

        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (string filePath in EnumerateFilesSafe(directory))
            {
                string fileName = Path.GetFileName(filePath);
                if (!IsWineExecutableName(fileName))
                    continue;

                string fullPath = Path.GetFullPath(filePath);
                if (!seenPaths.Add(fullPath))
                    continue;

                yield return new($"wine:{fullPath}", $"{fileName} (PATH)", LinuxLaunchToolKind.Wine, fullPath);
            }
        }
    }

    static IEnumerable<string> GetSteamToolDirectories(string root)
    {
        yield return Path.Combine(root, "steamapps", "common");
        yield return Path.Combine(root, "compatibilitytools.d");
    }

    static IEnumerable<string> GetWellKnownCompatDirectories()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            yield return Path.Combine(home, ".local", "share", "Steam", "compatibilitytools.d");
            yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam", "compatibilitytools.d");
        }
    }

    static IEnumerable<(string Directory, string Label)> GetWellKnownRunnerCollections()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            yield break;

        string dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is string xdgData
            && !string.IsNullOrWhiteSpace(xdgData)
            ? xdgData
            : Path.Combine(home, ".local", "share");
        string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is string xdgConfig
            && !string.IsNullOrWhiteSpace(xdgConfig)
            ? xdgConfig
            : Path.Combine(home, ".config");

        yield return (Path.Combine(dataHome, "bottles", "runners"), "Bottles");
        yield return (Path.Combine(home, ".var", "app", "com.usebottles.bottles", "data", "bottles", "runners"), "Bottles");
        yield return (Path.Combine(dataHome, "lutris", "runners", "wine"), "Lutris");
        yield return (Path.Combine(dataHome, "lutris", "runners", "proton"), "Lutris");
        yield return (Path.Combine(configHome, "heroic", "tools", "wine"), "Heroic");
        yield return (Path.Combine(configHome, "heroic", "tools", "proton"), "Heroic");
    }

    static IEnumerable<LinuxLaunchToolOption> EnumerateCollectionTools(string baseDirectory, string? label)
    {
        foreach (string toolDirectory in EnumerateDirectoriesSafe(baseDirectory))
        {
            string name = Path.GetFileName(toolDirectory);
            string displayName = string.IsNullOrWhiteSpace(label) ? name : $"{name} ({label})";

            string protonPath = Path.Combine(toolDirectory, "proton");
            if (File.Exists(protonPath))
            {
                string fullPath = Path.GetFullPath(protonPath);
                yield return new($"proton:{fullPath}", displayName, LinuxLaunchToolKind.Proton, fullPath);
                continue;
            }

            string? winePath = FindWineExecutable(toolDirectory);
            if (winePath is not null)
            {
                string fullPath = Path.GetFullPath(winePath);
                yield return new($"wine:{fullPath}", $"{displayName} (Wine)", LinuxLaunchToolKind.Wine, fullPath);
            }
        }
    }

    static IEnumerable<string> EnumerateDirectoriesSafe(string directory)
    {
        try
        {
            return Directory.Exists(directory) ? Directory.EnumerateDirectories(directory) : [];
        }
        catch
        {
            return [];
        }
    }

    static IEnumerable<string> EnumerateFilesSafe(string directory)
    {
        try
        {
            return Directory.Exists(directory) ? Directory.EnumerateFiles(directory) : [];
        }
        catch
        {
            return [];
        }
    }

    static bool IsThirdPartyRunnerPath(string normalizedPath)
      => normalizedPath.Contains("/bottles/runners/", StringComparison.OrdinalIgnoreCase)
        || normalizedPath.Contains("/lutris/runners/", StringComparison.OrdinalIgnoreCase)
        || normalizedPath.Contains("/heroic/tools/", StringComparison.OrdinalIgnoreCase);

    static string? FindWineExecutable(string toolDirectory)
    {
        string[] candidates =
        [
          Path.Combine(toolDirectory, "files", "bin", "wine64"),
      Path.Combine(toolDirectory, "files", "bin", "wine"),
      Path.Combine(toolDirectory, "bin", "wine64"),
      Path.Combine(toolDirectory, "bin", "wine")
        ];

        return Array.Find(candidates, File.Exists);
    }

    static bool IsWineExecutableName(string fileName)
      => fileName.StartsWith("wine", StringComparison.OrdinalIgnoreCase)
         && !s_wineHelperNames.Contains(fileName);

    static bool TryParseSelectionId(string selectionId, out LinuxLaunchToolKind kind, out string executablePath)
    {
        const string protonPrefix = "proton:";
        const string winePrefix = "wine:";
        if (selectionId.StartsWith(protonPrefix, StringComparison.OrdinalIgnoreCase))
        {
            kind = LinuxLaunchToolKind.Proton;
            executablePath = selectionId[protonPrefix.Length..];
            return true;
        }

        if (selectionId.StartsWith(winePrefix, StringComparison.OrdinalIgnoreCase))
        {
            kind = LinuxLaunchToolKind.Wine;
            executablePath = selectionId[winePrefix.Length..];
            return true;
        }

        kind = LinuxLaunchToolKind.Proton;
        executablePath = string.Empty;
        return false;
    }

    static string? TryGetSteamLibraryRoot(string gamePath)
    {
        DirectoryInfo? directory = new(gamePath);
        while (directory is not null)
        {
            DirectoryInfo? parent = directory.Parent;
            if (directory.Name.Equals("common", StringComparison.OrdinalIgnoreCase)
                && parent?.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase) == true
                && parent.Parent is not null)
                return parent.Parent.FullName;

            directory = parent;
        }

        return null;
    }
}