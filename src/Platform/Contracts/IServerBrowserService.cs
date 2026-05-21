using TEKLauncher.Servers;

namespace TEKLauncher.Platform;

enum ServerBrowserListType
{
    LAN,
    Favorites,
    Online
}

interface IServerBrowserService
{
    void AddFavorite(IPEndPoint endpoint);
    Server[]? GetServers(ServerBrowserListType type, string? clusterId = null);
    void RemoveFavorite(IPEndPoint endpoint);
    void Shutdown();
}