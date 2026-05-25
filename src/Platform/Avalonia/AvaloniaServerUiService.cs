namespace ObeliskLauncher.Platform;

sealed class AvaloniaServerUiService : IServerUiService
{
    public static event Action? ServerStateChanged;

    public void OnClusterAdded(Servers.Cluster cluster)
    {
        RaiseChanged();
    }

    public void OnClusterListsCleared()
    {
        RaiseChanged();
    }

    public void OnClusterServerCountChanged(Servers.Cluster cluster)
    {
        RaiseChanged();
    }

    public void OnClusterStatusChanged()
    {
        RaiseChanged();
    }

    public void OnServerAdded(Servers.Cluster cluster, Servers.Server server)
    {
        RaiseChanged();
    }

    public void OnServerRemoved(Servers.Cluster cluster, Servers.Server server)
    {
        RaiseChanged();
    }

    static void RaiseChanged()
    {
        ServerStateChanged?.Invoke();
    }
}