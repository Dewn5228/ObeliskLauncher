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

            errorMessage = "Could not resolve a usable Proton or Wine launch tool on Linux. Install a Proton compatibility tool in Steam or ensure Wine is available in PATH.";
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
    {
        IEnumerable<LinuxLaunchToolOption> protonTools = tools.Where(tool => tool.Kind == LinuxLaunchToolKind.Proton);
        if (!string.IsNullOrWhiteSpace(compatToolName))
        {
            LinuxLaunchToolOption? exact = protonTools.FirstOrDefault(tool => MatchesToolName(tool, compatToolName));
            if (exact is not null)
                return exact;
        }

        LinuxLaunchToolOption? firstProton = protonTools
          .OrderByDescending(tool => ScoreTool(tool, compatToolName))
          .ThenBy(tool => tool.DisplayName, StringComparer.CurrentCultureIgnoreCase)
          .FirstOrDefault();
        if (firstProton is not null)
            return firstProton;

        return tools.FirstOrDefault(tool => tool.Kind == LinuxLaunchToolKind.Wine);
    }

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
        int score = 0;
        if (!string.IsNullOrWhiteSpace(compatToolName) && MatchesToolName(option, compatToolName))
            score += 100;

        string normalizedPath = option.ExecutablePath.Replace('\\', '/');
        if (normalizedPath.Contains("/compatibilitytools.d/", StringComparison.OrdinalIgnoreCase))
            score += 60;
        if (normalizedPath.Contains("cachyos", StringComparison.OrdinalIgnoreCase)
            || option.DisplayName.Contains("cachyos", StringComparison.OrdinalIgnoreCase))
            score += 60;

        if (option.DisplayName.StartsWith("Proton ", StringComparison.OrdinalIgnoreCase))
            score += 30;
        if (option.DisplayName.Contains("Experimental", StringComparison.OrdinalIgnoreCase))
            score += 20;
        if (option.DisplayName.Contains("GE", StringComparison.OrdinalIgnoreCase))
            score += 10;
        return score;
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