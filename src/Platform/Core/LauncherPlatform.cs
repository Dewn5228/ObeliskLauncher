using System;
using System.IO;

namespace ObeliskLauncher.Platform;

static class LauncherPlatform
{
    public static ILauncherPlatform Current { get; } = GetPlatformAndMigrate();

    static ILauncherPlatform GetPlatformAndMigrate()
    {
        var platform = OperatingSystem.IsWindows() ? (ILauncherPlatform)new WindowsLauncherPlatform() : (ILauncherPlatform)new LinuxLauncherPlatform();
        Migrate(platform.AppDataFolder);
        return platform;
    }

    static void Migrate(string newPath)
    {
        if (Directory.Exists(newPath))
            return;

        var legacyPaths = new System.Collections.Generic.List<string>();

        if (OperatingSystem.IsWindows())
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            legacyPaths.Add(Path.Combine(appData, "Obelisk Launcher"));
            legacyPaths.Add(Path.Combine(appData, "TEK Launcher"));
        }
        else
        {
            string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (!string.IsNullOrWhiteSpace(xdgDataHome))
            {
                legacyPaths.Add(Path.Combine(xdgDataHome, "Obelisk Launcher"));
                legacyPaths.Add(Path.Combine(xdgDataHome, "TEK Launcher"));
            }

            legacyPaths.Add(Path.Combine(home, ".local", "share", "Obelisk Launcher"));
            legacyPaths.Add(Path.Combine(home, ".local", "share", "TEK Launcher"));
        }

        foreach (string legacyPath in legacyPaths)
        {
            if (Directory.Exists(legacyPath))
            {
                try
                {
                    string? parent = Path.GetDirectoryName(newPath);
                    if (parent != null && !Directory.Exists(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    Directory.Move(legacyPath, newPath);
                    break;
                }
                catch
                {
                    // Fallback to next legacy path if move fails
                }
            }
        }
    }
}