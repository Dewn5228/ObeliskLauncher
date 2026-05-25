namespace ObeliskLauncher.Platform;

interface IServerUiService
{
    void OnClusterAdded(Servers.Cluster cluster);

    void OnClusterListsCleared();

    void OnClusterServerCountChanged(Servers.Cluster cluster);

    void OnClusterStatusChanged();

    void OnServerAdded(Servers.Cluster cluster, Servers.Server server);

    void OnServerRemoved(Servers.Cluster cluster, Servers.Server server);
}