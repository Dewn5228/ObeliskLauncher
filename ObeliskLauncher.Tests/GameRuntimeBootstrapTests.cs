using System;
using System.IO;
using ObeliskLauncher.Platform;
using Xunit;

namespace ObeliskLauncher.Tests;

public sealed class GameRuntimeBootstrapTests
{
    [Theory]
    [InlineData("https://teknology-hub.com/software")]
    [InlineData("https://de.teknology-hub.com/software")]
    public void MirrorUrlPointsAtLatestStaticRelease(string root)
    {
        string url = GameRuntimeBootstrap.BuildMirrorUrl(root, "tek-injector", "libtek-injector.dll");

        Assert.Equal($"{root}/tek-injector/releases/latest/win-x86_64-static/libtek-injector.dll", url);
    }

    [Fact]
    public void RuntimeMirrorUrlUsesGameRuntimeRepository()
    {
        string url = GameRuntimeBootstrap.BuildMirrorUrl("https://teknology-hub.com/software", "tek-game-runtime", "libtek-game-runtime.dll");

        Assert.Equal("https://teknology-hub.com/software/tek-game-runtime/releases/latest/win-x86_64-static/libtek-game-runtime.dll", url);
    }

    [Theory]
    [InlineData("https://teknology-hub.com/software", "tek-injector")]
    [InlineData("https://de.teknology-hub.com/software", "tek-game-runtime")]
    public void VersionUrlPointsAtRepositoryVersionEndpoint(string root, string repository)
    {
        string url = GameRuntimeBootstrap.BuildVersionUrl(root, repository);

        Assert.Equal($"{root}/{repository}/version", url);
    }

    [Theory]
    [InlineData("2.0.2\n", "2.0.2")]
    [InlineData("v1.2.5", "1.2.5")]
    [InlineData(" v1.2.5-1 ", "1.2.5")]
    [InlineData("not-a-version", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizesMirrorVersion(string? raw, string? expected)
    {
        Assert.Equal(expected, GameRuntimeBootstrap.NormalizeMirrorVersion(raw));
    }

    [Theory]
    [InlineData("/cache/v2.0.2/libtek-injector.dll", "2.0.2")]
    [InlineData("/cache/v1.2.5/libtek-game-runtime.dll", "1.2.5")]
    [InlineData("/cache/mirror/libtek-injector.dll", null)]
    public void ExtractsVersionFromCachePath(string path, string? expectedVersion)
    {
        Version? version = GameRuntimeBootstrap.TryGetVersionFromPath(path);

        Assert.Equal(expectedVersion is null ? null : new Version(expectedVersion), version);
    }
}
