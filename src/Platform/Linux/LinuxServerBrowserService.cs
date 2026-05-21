using TEKLauncher.Servers;

namespace TEKLauncher.Platform;

sealed class LinuxServerBrowserService : IServerBrowserService
{
    public void AddFavorite(IPEndPoint endpoint) => global::TEKLauncher.Steam.LinuxServerBrowser.AddFavorite(endpoint);

    public Server[]? GetServers(ServerBrowserListType type, string? clusterId = null)
    {
        Server[]? servers = global::TEKLauncher.Steam.LinuxServerBrowser.GetServers(type, clusterId);
        return servers;
    }

    public void RemoveFavorite(IPEndPoint endpoint) => global::TEKLauncher.Steam.LinuxServerBrowser.RemoveFavorite(endpoint);

    public void Shutdown() => global::TEKLauncher.Steam.LinuxServerBrowser.Shutdown();
}