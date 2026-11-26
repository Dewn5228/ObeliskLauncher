using ObeliskLauncher.Platform;
using Xunit;

namespace ObeliskLauncher.Tests;

public sealed class LinuxGameLauncherOverlayTests
{
    const string Overlay = "/home/user/.local/share/Steam/ubuntu12_64/gameoverlayrenderer.so";

    [Fact]
    public void SetsOverlayWhenNoPreloadExists()
    {
        Assert.Equal(Overlay, LinuxGameLauncher.BuildOverlayPreloadValue(null, Overlay));
    }

    [Fact]
    public void PrependsOverlayToExistingPreloadEntries()
    {
        string? value = LinuxGameLauncher.BuildOverlayPreloadValue("/usr/lib/libfoo.so", Overlay);

        Assert.Equal($"{Overlay} /usr/lib/libfoo.so", value);
    }

    [Fact]
    public void NormalizesColonSeparatedExistingEntries()
    {
        string? value = LinuxGameLauncher.BuildOverlayPreloadValue("/a.so:/b.so", Overlay);

        Assert.Equal($"{Overlay} /a.so /b.so", value);
    }

    [Fact]
    public void SkipsWhenOverlayIsAlreadyPreloadedFromAnotherPath()
    {
        string? value = LinuxGameLauncher.BuildOverlayPreloadValue(
            "/other/steam/ubuntu12_64/gameoverlayrenderer.so",
            Overlay);

        Assert.Null(value);
    }
}
