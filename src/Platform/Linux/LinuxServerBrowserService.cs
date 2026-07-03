using ObeliskLauncher.Servers;
using System.Net;

namespace ObeliskLauncher.Platform;

sealed class LinuxServerBrowserService : IServerBrowserService
{
    public void AddFavorite(IPEndPoint endpoint) => Steam.Api.AddFavorite(endpoint);

    public Server[]? GetServers(ServerBrowserListType type, string? clusterId = null) => Steam.Api.GetServers(type switch
    {
        ServerBrowserListType.LAN => Steam.Api.ServerListType.LAN,
        ServerBrowserListType.Favorites => Steam.Api.ServerListType.Favorites,
        _ => Steam.Api.ServerListType.Online
    }, clusterId);

    public void RemoveFavorite(IPEndPoint endpoint) => Steam.Api.RemoveFavorite(endpoint);

    public void Shutdown() => Steam.Api.Shutdown();
}