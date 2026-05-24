using System.Diagnostics;
using System.Text.Json.Serialization;
using TEKLauncher.Steam.CM.Messages;
using TEKLauncher.Steam.CM.Messages.Bodies;

namespace TEKLauncher.Steam.CM;

/// <summary>Steam CM client.</summary>
static class Client
{
    /// <summary>Steam cell ID used in certain requests.</summary>
    public static uint CellId { get; set; }
    /// <summary>Disconnects client from the CM server.</summary>
    public static void Disconnect() => WebSocketConnection.Disconnect();
    /// <summary>Refreshes Steam CM server list via Web API.</summary>
    public static void RefreshServerList()
    {
        var serverList = Downloader.DownloadJsonAsync<CMListResponse>($"https://api.steampowered.com/ISteamDirectory/GetCMList/v1?cellid={CellId}").Result;
        if (serverList.Response?.Servers is null)
            return;
        var urls = new Uri[serverList.Response.Servers.Length];
        for (int i = 0; i < urls.Length; i++)
            urls[i] = new($"wss://{serverList.Response.Servers[i]}/cmsocket/");
        WebSocketConnection.ServerList = new(urls);
    }
    /// <summary>Retrieves details for specified mods.</summary>
    /// <param name="ids">IDs of the mods to retrieve details for.</param>
    /// <returns>An array of mod details; the array is empty if request fails.</returns>
    public static Mod.ModDetails[] GetModDetails(params ulong[] ids)
    {
        if (!WebSocketConnection.IsLoggedOn)
            try { WebSocketConnection.Connect(); }
            catch { return Array.Empty<Mod.ModDetails>(); }
        ulong jobId = GlobalId.NextJobId();
        var message = new Message<ModDetails>(MessageType.ServiceMethod);
        message.Body.Ids.AddRange(ids);
        message.Body.IncludeMetadata = true;
        message.Header.SourceJobId = jobId;
        message.Header.TargetJobName = "PublishedFile.GetDetails#1";
        var response = WebSocketConnection.GetMessage<ModDetailsResponse>(message, MessageType.ServiceMethodResponse, jobId);
        if (response is null)
            return Array.Empty<Mod.ModDetails>();
        var result = new Mod.ModDetails[response.Body.Details.Count];
        for (int i = 0; i < response.Body.Details.Count; i++)
        {
            var item = response.Body.Details[i];
            result[i] = new(item.AppId, item.Result == 1 ? 1 : item.Result == 9 ? 2 : 0, DateTimeOffset.FromUnixTimeSeconds(item.LastUpdated).Ticks, item.Id, item.HcontentFile, item.Name, item.PreviewUrl);
        }
        return result;
    }
    /// <summary>Queries mods available in the workshop.</summary>
    /// <param name="page">Current page number.</param>
    /// <param name="search">Search query.</param>
    /// <param name="total">When this method returns, contains the total number of available pages.</param>
    /// <returns>An array of mod details; the array is empty if request fails.</returns>
    public static Mod.ModDetails[] QueryMods(uint page, string? search, out uint total)
    {
        total = 0;
        IGameContext game = ActiveGameManager.Current;
        if (!WebSocketConnection.IsLoggedOn)
            try { WebSocketConnection.Connect(); }
            catch { return Array.Empty<Mod.ModDetails>(); }
        ulong jobId = GlobalId.NextJobId();
        var message = new Message<QueryMods>(MessageType.ServiceMethod);
        message.Body.Page = page;
        message.Body.ModsPerPage = 20;
        message.Body.AppId = game.SteamAppId;
        if (!string.IsNullOrEmpty(search))
            message.Body.SearchText = search;
        message.Body.ReturnMetadata = true;
        message.Header.SourceJobId = jobId;
        message.Header.TargetJobName = "PublishedFile.QueryFiles#1";
        var response = WebSocketConnection.GetMessage<QueryModsResponse>(message, MessageType.ServiceMethodResponse, jobId);
        if (response is null)
            return Array.Empty<Mod.ModDetails>();
        total = response.Body.Total;
        var result = new Mod.ModDetails[response.Body.Items.Count];
        for (int i = 0; i < response.Body.Items.Count; i++)
        {
            var item = response.Body.Items[i];
            result[i] = new(game.SteamAppId, item.Result == 1 ? 1 : item.Result == 9 ? 2 : 0, DateTimeOffset.FromUnixTimeSeconds(item.LastUpdated).Ticks, item.Id, item.HcontentFile, item.Name, item.PreviewUrl);
        }
        return result;
    }

    /// <summary>Queries apps via PICS to determine which have real Steam depot content.</summary>
    /// <param name="appIds">IDs of the apps to query.</param>
    /// <returns>
    ///   A set of app IDs that have at least one depot; returns <see langword="null"/> if the request failed.
    /// </returns>
    public static HashSet<uint>? GetAppsWithDepots(params uint[] appIds)
    {
        if (!WebSocketConnection.IsLoggedOn)
            try { WebSocketConnection.Connect(); }
            catch { return null; }

        var tokenMessage = new Message<PicsAccessTokenRequest>(MessageType.PicsAccessTokenRequest);
        tokenMessage.Body.AppIds.AddRange(appIds);
        var tokenResponse = WebSocketConnection.GetMessage<PicsAccessTokenResponse>(tokenMessage, MessageType.PicsAccessTokenResponse);

        var tokenMap = new Dictionary<uint, ulong>();
        if (tokenResponse is not null)
            foreach (var token in tokenResponse.Body.AppTokens)
                if (token.HasAppId && token.HasAccessToken)
                    tokenMap[token.AppId] = token.AccessToken;

        var infoMessage = new Message<PicsProductInfoRequest>(MessageType.PicsProductInfoRequest);
        foreach (uint appId in appIds)
        {
            var entry = new PicsProductInfoRequest.Types.App { AppId = appId };
            if (tokenMap.TryGetValue(appId, out ulong token))
                entry.AccessToken = token;
            infoMessage.Body.Apps.Add(entry);
        }

        var responses = WebSocketConnection.GetMessages<PicsProductInfoResponse>(
            infoMessage,
            MessageType.PicsProductInfoResponse,
            body => !body.ResponsePending);
        if (responses is null)
            return null;
        var result = new HashSet<uint>();
        foreach (var body in responses)
            foreach (var app in body.Apps)
            {
                if (!app.HasAppId)
                    continue;
                if (app.HasBuffer && app.Buffer.Length > 0)
                {
                    string vdfText = Encoding.UTF8.GetString(app.Buffer.ToByteArray());
                    var root = VdfParser.Parse(vdfText);
                    VdfNode? depots = null;
                    foreach (var child in root.Children.Values)
                        depots ??= child["depots"];
                    if (depots is not null && depots.Children.Count > 0)
                        result.Add(app.AppId);
                }
                else if (app.HasSha && app.HasSize && app.Size > 0 && body.HasHttpHost)
                {
                    string sha = Convert.ToHexStringLower(app.Sha.ToByteArray());
                    string url = $"http://{body.HttpHost}/appinfo/{app.AppId}/sha/{sha}.txt.gz";
                    try
                    {
                        using var httpClient = new System.Net.Http.HttpClient();
                        httpClient.Timeout = TimeSpan.FromSeconds(15);
                        byte[] compressed = httpClient.GetByteArrayAsync(url).GetAwaiter().GetResult();
                        using var compressedStream = new MemoryStream(compressed);
                        using var gzipStream = new System.IO.Compression.GZipStream(compressedStream, System.IO.Compression.CompressionMode.Decompress);
                        using var reader = new StreamReader(gzipStream, Encoding.UTF8);
                        string vdfText = reader.ReadToEnd();
                        var root = VdfParser.Parse(vdfText);
                        VdfNode? depots = null;
                        foreach (var child in root.Children.Values)
                            depots ??= child["depots"];
                        if (depots is not null && depots.Children.Count > 0)
                            result.Add(app.AppId);
                    }
                    catch { }
                }
            }
        return result;
    }

    /// <summary>Represents Steam Web API CM server list response JSON object.</summary>
    readonly record struct CMListResponse
    {
        [JsonPropertyName("response")]
        public ServerList? Response { get; init; }
        public record ServerList
        {
            [JsonPropertyName("serverlist_websockets")]
            public string[]? Servers { get; init; }
        }
    }
    /// <summary>Generates global IDs.</summary>
    static class GlobalId
    {
        /// <summary>Increments with every new generated job ID.</summary>
        static ulong s_counter;
        /// <summary>Mask for <see cref="s_counter"/> that also includes fields that need to be initialized only once.</summary>
        static readonly ulong s_mask;
        /// <summary>Initializes <see cref="s_mask"/>.</summary>
        static GlobalId()
        {
            using var currentProcess = Process.GetCurrentProcess();
            s_mask = 0x3FF0000000000 | (((((ulong)currentProcess.StartTime.Ticks - 0x8C6BDABF8998000) / 10000000) & 0xFFFFF) << 20);
        }
        /// <summary>Generates next unique job ID.</summary>
        /// <returns>A job ID.</returns>
        public static ulong NextJobId() => s_mask | ++s_counter;
    }
}