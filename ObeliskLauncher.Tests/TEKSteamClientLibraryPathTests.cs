using ObeliskLauncher.Utils;
using Xunit;

namespace ObeliskLauncher.Tests;

public sealed class TEKSteamClientLibraryPathTests
{
    [Fact]
    public void RegisteredPathTakesPrecedenceWhenItExists()
    {
        string? selected = TEKSteamClient.SelectLibraryPath(
            "/registered/libtek-steamclient.so.2",
            "/canonical/libtek-steamclient.so.2",
            path => true);

        Assert.Equal("/registered/libtek-steamclient.so.2", selected);
    }

    [Fact]
    public void FallsBackToCanonicalPathWhenRegistrationIsMissing()
    {
        string? selected = TEKSteamClient.SelectLibraryPath(
            null,
            "/canonical/libtek-steamclient.so.2",
            path => path == "/canonical/libtek-steamclient.so.2");

        Assert.Equal("/canonical/libtek-steamclient.so.2", selected);
    }

    [Fact]
    public void IgnoresRegisteredPathThatNoLongerExists()
    {
        string? selected = TEKSteamClient.SelectLibraryPath(
            "/registered/libtek-steamclient.so.2",
            "/canonical/libtek-steamclient.so.2",
            path => path == "/canonical/libtek-steamclient.so.2");

        Assert.Equal("/canonical/libtek-steamclient.so.2", selected);
    }

    [Fact]
    public void ReturnsNullWhenNoTrustedCopyExists()
    {
        string? selected = TEKSteamClient.SelectLibraryPath(
            "/registered/libtek-steamclient.so.2",
            "/canonical/libtek-steamclient.so.2",
            _ => false);

        Assert.Null(selected);
    }
}
