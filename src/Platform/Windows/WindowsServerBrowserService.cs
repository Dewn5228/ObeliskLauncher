using ObeliskLauncher.Servers;

namespace ObeliskLauncher.Platform;

sealed class WindowsServerBrowserService : IServerBrowserService
{
    public void AddFavorite(IPEndPoint endpoint) => Steam.ServerBrowser.AddFavorite(endpoint);

    public Server[]? GetServers(ServerBrowserListType type, string? clusterId = null) => Steam.ServerBrowser.GetServers(type switch
    {
        ServerBrowserListType.LAN => Steam.ServerBrowser.ServerListType.LAN,
        ServerBrowserListType.Favorites => Steam.ServerBrowser.ServerListType.Favorites,
        _ => Steam.ServerBrowser.ServerListType.Online
    }, clusterId);

    public void RemoveFavorite(IPEndPoint endpoint) => Steam.ServerBrowser.RemoveFavorite(endpoint);

    public void Shutdown() => Steam.ServerBrowser.Shutdown();
}