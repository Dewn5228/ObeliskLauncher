using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Threading;

namespace ObeliskLauncher.Avalonia.ViewModels;

public sealed class ServersClusterRowViewModel : INotifyPropertyChanged
{
    bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;

            _isSelected = value;
            PropertyChanged?.Invoke(this, new(nameof(IsSelected)));
        }
    }

    public required Servers.Cluster Cluster { get; init; }

    public string? DescriptionText { get; init; }

    public required string Detail { get; init; }

    public string? DiscordUrl { get; init; }

    public string? HosterText { get; init; }

    public bool HasDescription => !string.IsNullOrWhiteSpace(DescriptionText);

    public bool HasDiscord => !string.IsNullOrWhiteSpace(DiscordUrl);

    public bool HasHoster => !string.IsNullOrWhiteSpace(HosterText);

    public required bool IsSpecialCluster { get; init; }

    public bool CanRefresh => !IsSpecialCluster;

    public required string Name { get; init; }

    public required int ServerCount { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class ServerModRowViewModel : INotifyPropertyChanged
{
    readonly Mod.ModDetails _details;
    bool _isInstalled;

    internal ServerModRowViewModel(Mod.ModDetails details)
    {
        _details = details;
        RefreshInstalledState();
    }

    public string DisplayName => _details.Name;

    public ulong Id => _details.Id;

    public bool IsInstalled
    {
        get => _isInstalled;
        private set
        {
            if (_isInstalled == value)
                return;

            _isInstalled = value;
            PropertyChanged?.Invoke(this, new(nameof(IsInstalled)));
            PropertyChanged?.Invoke(this, new(nameof(CanInstall)));
        }
    }

    public bool CanInstall => !IsInstalled;

    public string WorkshopUrl => $"steam://openurl/https://steamcommunity.com/sharedfiles/filedetails/?id={Id}";

    internal Mod.ModDetails Details => _details;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshInstalledState()
    {
        lock (Mod.List)
            IsInstalled = Mod.List.Any(mod => mod.Id == Id);
    }
}

public sealed class ServerRowViewModel : INotifyPropertyChanged
{
    bool _isLoadingModDetails;
    bool _modDetailsLoaded;

    public required string Address { get; init; }

    public bool CanShowDetails => HasDescription || HasDiscord || HasHoster || HasMods;

    public required string DisplayMapName { get; init; }

    public required string DisplayName { get; init; }

    public string? DescriptionText { get; init; }

    public string? DiscordUrl { get; init; }

    public bool HasDescription => !string.IsNullOrWhiteSpace(DescriptionText);

    public bool HasDiscord => !string.IsNullOrWhiteSpace(DiscordUrl);

    public bool HasHoster => !string.IsNullOrWhiteSpace(HosterText);

    public bool HasModRows => ModRows.Count > 0;

    public bool HasMods => ModIds.Length > 0;

    public string? HosterText { get; init; }

    public required bool IsFavorite { get; init; }

    public bool IsLoadingModDetails
    {
        get => _isLoadingModDetails;
        private set => SetProperty(ref _isLoadingModDetails, value);
    }

    public required bool IsPvE { get; init; }

    public string ModeColor => IsPvE ? "#0AA63E" : "#9E2313";

    public string ModeText => IsPvE ? "PvE" : "PvP";

    public required ulong[] ModIds { get; init; }

    public ObservableCollection<ServerModRowViewModel> ModRows { get; } = [];

    public required int NumPlayers { get; init; }

    public required Servers.Server Server { get; init; }

    public required string Version { get; init; }

    public int MaxNumPlayers { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task EnsureModDetailsLoadedAsync()
    {
        if (!HasMods || _modDetailsLoaded || IsLoadingModDetails)
            return;

        IsLoadingModDetails = true;
        try
        {
            Mod.ModDetails[] details = await Task.Run(() => ResolveModDetails(ModIds));
            ModRows.Clear();
            foreach (Mod.ModDetails detail in details)
                ModRows.Add(new ServerModRowViewModel(detail));
            _modDetailsLoaded = true;
            PropertyChanged?.Invoke(this, new(nameof(HasModRows)));
        }
        finally
        {
            IsLoadingModDetails = false;
        }
    }

    public void RefreshModInstallStates()
    {
        foreach (ServerModRowViewModel row in ModRows)
            row.RefreshInstalledState();
    }

    static Mod.ModDetails[] ResolveModDetails(ulong[] modIds)
    {
        Mod.ModDetails[] details = Steam.CM.Client.GetModDetails(modIds);
        if (details.Length == 0)
            return [.. modIds.Select(id => new Mod.ModDetails { Id = id, Name = id.ToString() })];

        var detailsById = details.ToDictionary(item => item.Id);
        Mod.ModDetails[] resolved = new Mod.ModDetails[modIds.Length];
        for (int i = 0; i < modIds.Length; i++)
            resolved[i] = detailsById.TryGetValue(modIds[i], out Mod.ModDetails detail) ? detail : new Mod.ModDetails { Id = modIds[i], Name = modIds[i].ToString() };
        return resolved;
    }

    void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new(propertyName));

    void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }
}

public sealed class ServersSectionScreenViewModel : LauncherSectionScreenViewModel
{
    ServersClusterRowViewModel? _selectedCluster;
    ServerRowViewModel[] _selectedClusterServers = [];
    bool _isRefreshing;
    ServersClusterRowViewModel[] _rows = [];
    string _statusText = Locale.Get("common.loading");
    volatile bool _snapshotUpdatePending;

    public ServersSectionScreenViewModel()
      : base(LauncherSection.Servers)
    {
        AvaloniaServerUiService.ServerStateChanged += HandleServerStateChanged;
        RefreshSnapshot();
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetProperty(ref _isRefreshing, value);
    }

    public ServersClusterRowViewModel? SelectedCluster
    {
        get => _selectedCluster;
        private set => SetProperty(ref _selectedCluster, value);
    }

    public ServerRowViewModel[] SelectedClusterServers
    {
        get => _selectedClusterServers;
        private set => SetProperty(ref _selectedClusterServers, value);
    }

    public ServersClusterRowViewModel[] Rows
    {
        get => _rows;
        private set => SetProperty(ref _rows, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public async Task RefreshAsync()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;
        try
        {
            await Task.Run(Servers.Cluster.ReloadLists);
            RefreshSnapshot();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public async Task RefreshSelectedClusterAsync()
    {
        if (IsRefreshing || SelectedCluster is null || !SelectedCluster.CanRefresh)
            return;

        IsRefreshing = true;
        try
        {
            await Task.Run(() => ReloadClusterServers(SelectedCluster.Cluster));
            RefreshSnapshot();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public Task JoinAsync(ServerRowViewModel row)
    {
        Game.Launch(row.Server);
        return Task.CompletedTask;
    }

    public void SelectCluster(ServersClusterRowViewModel row)
    {
        if (SelectedCluster is not null)
            SelectedCluster.IsSelected = false;

        SelectedCluster = row;

        if (SelectedCluster is not null)
            SelectedCluster.IsSelected = true;

        RefreshSelectedClusterServers();
    }

    public void RefreshSnapshot()
    {
        StatusText = Servers.Cluster.CurrentStatus switch
        {
            0 => Locale.Get("common.loading"),
            1 => Locale.Get("errors.clustersReloadSteamNotRunning"),
            2 => Locale.Get("errors.clustersReloadFail"),
            3 => Locale.Get("errors.clustersReloadSuccess"),
            _ => Locale.Get("common.loading")
        };

        var rows = new List<ServersClusterRowViewModel>();

        AddClusterRows(rows, Servers.Cluster.Lan);
        AddClusterRows(rows, Servers.Cluster.Favorites);
        AddClusterRows(rows, Servers.Cluster.Unclustered);
        lock (Servers.Cluster.OnlineClusters)
            foreach (var cluster in Servers.Cluster.OnlineClusters)
                AddClusterRows(rows, cluster);

        Rows = rows.ToArray();
        if (Rows.Length == 0)
        {
            SelectedCluster = null;
            SelectedClusterServers = [];
            return;
        }

        if (SelectedCluster is null)
            SelectCluster(Rows[0]);
        else
        {
            var current = Array.Find(Rows, row => row.Cluster == SelectedCluster.Cluster) ?? Rows[0];
            SelectCluster(current);
        }
    }

    public void ToggleFavorite(ServerRowViewModel row)
    {
        if (row.IsFavorite)
            row.Server.RemoveFavorite();
        else
            row.Server.AddFavorite();

        RefreshSnapshot();
    }

    public override void Activate() => RefreshSnapshot();

    void HandleServerStateChanged()
    {
        if (_snapshotUpdatePending)
            return;

        _snapshotUpdatePending = true;
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(250);
            _snapshotUpdatePending = false;
            RefreshSnapshot();
        });
    }

    static void AddClusterRows(List<ServersClusterRowViewModel> rows, Servers.Cluster cluster)
    {
        int count;
        lock (cluster.Servers)
            count = cluster.Servers.Count;

        if (count == 0 && cluster.IsSpecialCluster)
            return;

        rows.Add(new ServersClusterRowViewModel
        {
            Cluster = cluster,
            DescriptionText = FormatDescription(cluster.Description),
            Detail = cluster.Hoster ?? cluster.Id,
            DiscordUrl = cluster.Discord,
            HosterText = cluster.Hoster is null ? null : string.Format(Locale.Get("serversTab.hostedBy"), cluster.Hoster),
            IsSpecialCluster = cluster.IsSpecialCluster,
            Name = cluster.Name,
            ServerCount = count
        });
    }

    void RefreshSelectedClusterServers()
    {
        if (SelectedCluster is null)
        {
            SelectedClusterServers = [];
            return;
        }

        HashSet<Servers.Server> favoriteServers;
        lock (Servers.Cluster.Favorites.Servers)
            favoriteServers = [.. Servers.Cluster.Favorites.Servers];

        List<ServerRowViewModel> servers;
        lock (SelectedCluster.Cluster.Servers)
            servers = [.. SelectedCluster.Cluster.Servers.Select(server => new ServerRowViewModel
      {
        Address = server.Address,
        DescriptionText = FormatDescription(server.Info?.ServerDescription ?? (SelectedCluster.Cluster.IsSpecialCluster ? server.Info?.ClusterDescription : null)),
        DisplayMapName = server.DisplayMapName,
        DisplayName = SelectedCluster.Cluster.IsSpecialCluster ? server.Name : server.DisplayName,
        DiscordUrl = SelectedCluster.Cluster.IsSpecialCluster ? server.Info?.Discord : null,
        HosterText = SelectedCluster.Cluster.IsSpecialCluster && !string.IsNullOrWhiteSpace(server.Info?.HosterName)
          ? string.Format(Locale.Get("serversTab.hostedBy"), server.Info!.HosterName)
          : null,
        IsFavorite = favoriteServers.Contains(server),
        IsPvE = server.IsPvE,
        MaxNumPlayers = server.MaxNumPlayers,
        ModIds = server.ModIds,
        NumPlayers = server.NumPlayers,
        Server = server,
        Version = server.Version ?? Locale.Get("common.na")
      })];

        SelectedClusterServers = [.. servers.OrderBy(server => server.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }

    static string? FormatDescription(Servers.Description? description)
    {
        if (description is null)
            return null;

        var builder = new StringBuilder();

        void AppendMultiplier<T>(T? multiplier, string nameCode) where T : struct
        {
            if (!multiplier.HasValue)
                return;

            if (builder.Length > 0)
                builder.AppendLine();
            builder.Append(Locale.Get(nameCode));
            builder.Append(' ');
            builder.Append(multiplier.Value);
            if (nameCode != "serversTab.maxWildDinoLevel")
                builder.Append('x');
        }

        AppendMultiplier(description.MaxDinoLvl, "serversTab.maxWildDinoLevel");
        AppendMultiplier(description.Taming, "serversTab.taming");
        AppendMultiplier(description.Experience, "serversTab.experience");
        AppendMultiplier(description.Harvesting, "serversTab.harvesting");
        AppendMultiplier(description.Breeding, "serversTab.breeding");
        AppendMultiplier(description.Stacks, "serversTab.stacks");

        if (description.Other is not null)
            foreach (string line in description.Other.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                if (builder.Length > 0)
                    builder.AppendLine();
                builder.Append(line);
            }

        return builder.Length == 0 ? null : builder.ToString();
    }

    static void ReloadClusterServers(Servers.Cluster cluster)
    {
        Servers.Server[]? servers = LauncherServices.ServerBrowser.GetServers(ServerBrowserListType.Online, cluster.Id);
        lock (cluster.Servers)
            cluster.Servers.Clear();

        if (servers is null)
            return;

        Parallel.ForEach(servers, new ParallelOptions { MaxDegreeOfParallelism = 10 }, server =>
        {
            if (!server.Query())
                return;

            lock (cluster.Servers)
                cluster.Servers.Add(server);
        });
    }
}