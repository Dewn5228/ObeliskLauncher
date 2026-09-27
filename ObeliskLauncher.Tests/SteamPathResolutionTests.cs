using System;
using System.IO;
using ObeliskLauncher.Platform;
using Xunit;

namespace ObeliskLauncher.Tests;

public sealed class SteamPathResolutionTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("obelisk-steam-paths").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_root, true); }
        catch { }
    }

    string MakeDir(params string[] segments)
    {
        string path = _root;
        foreach (string segment in segments)
            path = Path.Combine(path, segment);
        Directory.CreateDirectory(path);
        return path;
    }

    string MakeFile(string path, string contents = "")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    [Fact]
    public void StaleEmptyDirectoryDoesNotShadowRealInstall()
    {
        string stale = MakeDir(".local", "share", "Steam");
        string real = MakeDir(".steam", "debian-installation");
        MakeFile(Path.Combine(real, "config", "libraryfolders.vdf"));

        string[] candidates = [stale, real];

        Assert.Equal(real, LinuxLauncherPlatform.SelectSteamInstallPath(candidates));
        Assert.Null(LinuxLauncherPlatform.SelectSteamClientDllPath(candidates));
    }

    [Fact]
    public void SteamClientDllIsFoundInNonInstallPathDirectory()
    {
        string stale = MakeDir(".local", "share", "Steam");
        string real = MakeDir(".steam", "debian-installation");
        string expected = MakeFile(Path.Combine(real, "linux64", "steamclient.so"), "not a real library");

        string[] candidates = [stale, real];

        Assert.Equal(expected, LinuxLauncherPlatform.SelectSteamClientDllPath(candidates));
    }

    [Fact]
    public void NoCandidatesResolveToNull()
    {
        string missing = Path.Combine(_root, "does-not-exist");
        string empty = MakeDir("empty");

        Assert.Null(LinuxLauncherPlatform.SelectSteamInstallPath([missing]));
        Assert.Null(LinuxLauncherPlatform.SelectSteamClientDllPath([missing, empty]));
    }

    [Fact]
    public void FallsBackToFirstExistingDirectoryWhenNoMarkerFound()
    {
        string first = MakeDir("a");
        string second = MakeDir("b");

        Assert.Equal(first, LinuxLauncherPlatform.SelectSteamInstallPath([first, second]));
    }

    [Theory]
    [InlineData("linux64")]
    [InlineData("steamrt64")]
    [InlineData("ubuntu12_64")]
    public void EveryKnownClientLayoutIsSearched(string architectureDir)
    {
        string install = MakeDir("steam");
        string expected = MakeFile(Path.Combine(install, architectureDir, "steamclient.so"));

        Assert.Equal(expected, LinuxLauncherPlatform.SelectSteamClientDllPath([install]));
    }
}
