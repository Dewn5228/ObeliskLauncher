using System.IO;
using Avalonia.Platform;

namespace ObeliskLauncher;

static class LauncherResources
{
    public static Stream OpenRead(string relativePath)
    {
        string normalizedPath = Normalize(relativePath);

        return AssetLoader.Open(new Uri($"avares://ObeliskLauncher/{normalizedPath}"));
    }

    static string Normalize(string relativePath) => relativePath.TrimStart('/').Replace('\\', '/');
}