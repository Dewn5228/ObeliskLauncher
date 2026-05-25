using ObeliskLauncher.Servers;

namespace ObeliskLauncher.Platform;

sealed class LinuxServerBrowserService : IServerBrowserService
{
    public void AddFavorite(IPEndPoint endpoint) => global::ObeliskLauncher.Steam.LinuxServerBrowser.AddFavorite(endpoint);

    public Server[]? GetServers(ServerBrowserListType type, string? clusterId = null)
    {
        Server[]? servers = global::ObeliskLauncher.Steam.LinuxServerBrowser.GetServers(type, clusterId);
        return servers;
    }

    public void RemoveFavorite(IPEndPoint endpoint) => global::ObeliskLauncher.Steam.LinuxServerBrowser.RemoveFavorite(endpoint);

    public void Shutdown() => global::ObeliskLauncher.Steam.LinuxServerBrowser.Shutdown();
}