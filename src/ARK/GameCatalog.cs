using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using ObeliskLauncher.Steam.CM;

namespace ObeliskLauncher.ARK;

readonly record struct GameDlcInfo(
    string Name,
    uint AppId,
    uint DepotId,
    bool IsModContent,
    bool HasPPostfix,
    MapCode Code,
    string? FolderNameOverride,
    string? MapFileNameOverride,
    ulong? PreAquaticaManifestId);

sealed record GameCatalogEntry(
    string Id,
    string DisplayName,
    uint SteamAppId,
    uint RuntimeAppId,
    uint MainDepotId,
    uint WorkshopDepotId,
    string ServerGameDir,
    IReadOnlyList<string> AcceptedServerGameDirs,
    string SteamFolderName,
    string ExeFileName,
    string ExeRelativePath,
    string WorkshopDirRelativePath,
    bool SupportsSpacewarSpoof,
    bool IsWindowsBuild,
    IReadOnlyList<GameDlcInfo> DlcCatalog,
    IReadOnlyDictionary<uint, string> RuntimeDlcDisplayNames,
    IReadOnlyDictionary<uint, ulong> PreAquaticaManifestOverrides)
{
    public string BuildExePath(string rootPath)
        => string.IsNullOrWhiteSpace(rootPath)
            ? string.Empty
            : Path.Combine(rootPath, ExeRelativePath.Replace('/', Path.DirectorySeparatorChar));

    public string BuildWorkshopPath(string rootPath)
        => string.IsNullOrWhiteSpace(rootPath)
            ? string.Empty
            : Path.Combine(rootPath, WorkshopDirRelativePath.Replace('/', Path.DirectorySeparatorChar));
}

static class GameCatalog
{
    const int SupportedSchemaVersion = 1;
    const string CatalogResourceName = "ObeliskLauncher.assets.catalog.game-catalog.json";
    const string AppDataCatalogDirectoryName = "catalog";
    const string OverrideCatalogFileName = "game-catalog.override.json";
    const string RemoteCatalogFileName = "game-catalog.remote.json";
    const string AutoCatalogFileName = "game-catalog.auto.json";
    const string RemoteCatalogHashFileName = "game-catalog.remote.sha256";
    const string RemoteCatalogSignatureFileName = "game-catalog.remote.sig";
    const string SyncConfigFileName = "catalog-sync.json";
    const string AsaSettingsUrl = "https://nuclearist.ru/static/tek-gr-settings.json";

    static readonly object s_stateSync = new();

    static CatalogState s_state = LoadState();

    public const string AseGameId = "ASE";
    public const string AsaGameId = "ASA";

    public static IReadOnlyCollection<GameCatalogEntry> AllGames
    {
        get
        {
            lock (s_stateSync)
                return s_state.ByGameId.Values.ToArray();
        }
    }

    public static string? StartupNotice { get; private set; }

    static string CatalogDirectoryPath => Path.Combine(LauncherBootstrap.AppDataFolder, AppDataCatalogDirectoryName);

    static string OverrideCatalogPath => Path.Combine(CatalogDirectoryPath, OverrideCatalogFileName);

    static string RemoteCatalogPath => Path.Combine(CatalogDirectoryPath, RemoteCatalogFileName);

    static string AutoCatalogPath => Path.Combine(CatalogDirectoryPath, AutoCatalogFileName);

    static string RemoteCatalogHashPath => Path.Combine(CatalogDirectoryPath, RemoteCatalogHashFileName);

    static string RemoteCatalogSignaturePath => Path.Combine(CatalogDirectoryPath, RemoteCatalogSignatureFileName);

    static string SyncConfigPath => Path.Combine(CatalogDirectoryPath, SyncConfigFileName);

    public static string DefaultGameId
    {
        get
        {
            lock (s_stateSync)
                return s_state.ByGameId.ContainsKey(AseGameId) ? AseGameId : s_state.ByGameId.Keys.First();
        }
    }

    public static void InitializeAndSync()
    {
        EnsureSyncConfigTemplate();

        lock (s_stateSync)
            s_state = LoadState();

        CatalogSyncResult syncResult = SyncRemoteCatalogAsync().GetAwaiter().GetResult();
        bool asaAutoSyncApplied = SyncAsaCatalogAsync().GetAwaiter().GetResult();
        if (syncResult.Applied || asaAutoSyncApplied)
            lock (s_stateSync)
                s_state = LoadState();

        StartupNotice = BuildStartupNotice(syncResult);
    }

    static void EnsureSyncConfigTemplate()
    {
        try
        {
            Directory.CreateDirectory(CatalogDirectoryPath);
            if (File.Exists(SyncConfigPath))
                return;

            var template = new CatalogSyncConfig(
              false,
              null,
              new List<string>(),
              null,
              new List<string>(),
              null,
              new List<string>(),
              null,
              true,
              true);
            File.WriteAllText(SyncConfigPath, JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }

    public static GameCatalogEntry GetByGameId(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            throw new ArgumentException("Game ID is required.", nameof(gameId));

        lock (s_stateSync)
            if (s_state.ByGameId.TryGetValue(gameId, out GameCatalogEntry? entry))
                return entry;

        throw new InvalidOperationException($"Game ID '{gameId}' is not present in catalog.");
    }

    public static bool TryGetGameIdBySteamAppId(uint steamAppId, out string gameId)
    {
        lock (s_stateSync)
            return s_state.GameIdBySteamAppId.TryGetValue(steamAppId, out gameId!);
    }

    public static async Task<CatalogSyncResult> SyncRemoteCatalogAsync()
    {
        try
        {
            Directory.CreateDirectory(CatalogDirectoryPath);
            if (!File.Exists(SyncConfigPath))
                return new(false, false, "Catalog sync config not found; using embedded + local override catalog only.");

            CatalogSyncConfig? syncConfig;
            try
            {
                syncConfig = JsonSerializer.Deserialize<CatalogSyncConfig>(await File.ReadAllTextAsync(SyncConfigPath).ConfigureAwait(false));
            }
            catch
            {
                return new(false, false, "Catalog sync config is invalid; remote catalog sync skipped.");
            }

            if (syncConfig is null || !syncConfig.Enabled)
                return new(false, false, "Catalog sync disabled; using local catalog sources only.");

            string[] catalogUrls = NormalizeUrls(syncConfig.CatalogUrls, syncConfig.CatalogUrl);
            string[] hashUrls = NormalizeUrls(syncConfig.HashUrls, syncConfig.HashUrl);
            string[] signatureUrls = NormalizeUrls(syncConfig.SignatureUrls, syncConfig.SignatureUrl);
            if (catalogUrls.Length == 0)
                return new(false, false, "Catalog sync enabled but no catalog URL is configured.");

            byte[]? catalogBytes = await Downloader.DownloadBytesAsync(catalogUrls).ConfigureAwait(false);
            if (catalogBytes is null || catalogBytes.Length == 0)
                return new(false, false, "Catalog sync failed: remote catalog download was unsuccessful.");

            string computedHash = Convert.ToHexString(SHA256.HashData(catalogBytes)).ToLowerInvariant();

            if (syncConfig.RequireHashVerification)
            {
                if (hashUrls.Length == 0)
                    return new(false, false, "Catalog sync failed: hash verification is enabled but no hash URL is configured.");

                string? hashText = await Downloader.DownloadStringAsync(hashUrls).ConfigureAwait(false);
                if (!TryExtractSha256(hashText, out string expectedHash))
                    return new(false, false, "Catalog sync failed: downloaded hash file is invalid.");

                if (!string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    return new(false, false, "Catalog sync failed: remote catalog hash does not match expected hash.");
            }

            if (syncConfig.RequireSignatureVerification)
            {
                if (signatureUrls.Length == 0)
                    return new(false, false, "Catalog sync failed: signature verification is enabled but no signature URL is configured.");
                if (string.IsNullOrWhiteSpace(syncConfig.PublicKeyPem))
                    return new(false, false, "Catalog sync failed: signature verification is enabled but no public key is configured.");

                string? signatureText = await Downloader.DownloadStringAsync(signatureUrls).ConfigureAwait(false);
                if (!TryVerifySignature(syncConfig.PublicKeyPem, computedHash, signatureText))
                    return new(false, false, "Catalog sync failed: signature verification failed.");
            }

            try
            {
                CatalogDocument? remoteDoc = JsonSerializer.Deserialize<CatalogDocument>(catalogBytes);
                ValidateCatalogDocument(remoteDoc);
            }
            catch
            {
                return new(false, false, "Catalog sync failed: remote catalog payload is invalid.");
            }

            await File.WriteAllBytesAsync(RemoteCatalogPath, catalogBytes).ConfigureAwait(false);
            await File.WriteAllTextAsync(RemoteCatalogHashPath, computedHash).ConfigureAwait(false);
            await File.WriteAllTextAsync(RemoteCatalogSignaturePath, "verified").ConfigureAwait(false);
            return new(true, true, "Catalog synchronized successfully from remote source.");
        }
        catch
        {
            return new(false, false, "Catalog sync failed unexpectedly; continuing with local catalog sources.");
        }
    }

    static CatalogState LoadState()
    {
        Directory.CreateDirectory(CatalogDirectoryPath);

        CatalogDocument combined = LoadEmbeddedDocument();

        if (TryReadCatalogDocumentFromPath(RemoteCatalogPath, out CatalogDocument? remoteDocument))
            combined = MergeDocuments(combined, remoteDocument!);

        if (TryReadCatalogDocumentFromPath(AutoCatalogPath, out CatalogDocument? autoDocument))
            combined = MergeDocuments(combined, autoDocument!);

        if (TryReadCatalogDocumentFromPath(OverrideCatalogPath, out CatalogDocument? overrideDocument))
            combined = MergeDocuments(combined, overrideDocument!);

        return ConvertDocumentToState(combined);
    }

    static CatalogState ConvertDocumentToState(CatalogDocument document)
    {
        ValidateCatalogDocument(document);

        var byGameId = new Dictionary<string, GameCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (CatalogGame source in document.Games)
            byGameId[source.Id] = ConvertGame(source);

        var bySteamAppId = new Dictionary<uint, string>();
        foreach (GameCatalogEntry game in byGameId.Values)
            if (!bySteamAppId.TryAdd(game.SteamAppId, game.Id))
                throw new InvalidOperationException($"Catalog contains duplicate Steam app ID '{game.SteamAppId}'.");

        return new(byGameId, bySteamAppId);
    }

    static CatalogDocument LoadEmbeddedDocument()
    {
        Assembly assembly = typeof(GameCatalog).Assembly;
        Stream? stream = assembly.GetManifestResourceStream(CatalogResourceName);
        if (stream is null)
            return CreateDefaultCatalogDocument();

        using (stream)
        {
            CatalogDocument? document = JsonSerializer.Deserialize<CatalogDocument>(stream);
            if (document is null)
                return CreateDefaultCatalogDocument();

            ValidateCatalogDocument(document);
            return document;
        }
    }

    static CatalogDocument CreateDefaultCatalogDocument() => new(
        SupportedSchemaVersion,
        new List<CatalogGame>
        {
        new(
          AseGameId,
          "ARK: Survival Evolved",
          346110,
          346110,
          346111,
          346110,
          "ark_survival_evolved",
          new List<string> { "ark_survival_evolved" },
          "ARK",
          "ShooterGame.exe",
          "ShooterGame/Binaries/Win64/ShooterGame.exe",
          "Mods",
          true,
          true,
          new Dictionary<string, string>(),
          new Dictionary<string, ulong>(),
          new List<CatalogDlc>()),
        new(
          AsaGameId,
          "ARK: Survival Ascended",
          2399830,
          2399830,
          2399831,
          2399830,
          "ark_survival_ascended",
          new List<string> { "ark_survival_ascended", "ark_survival_evolved" },
          "ARK Survival Ascended",
          "ArkAscended.exe",
          "ShooterGame/Binaries/Win64/ArkAscended.exe",
          "Mods",
          true,
          true,
          new Dictionary<string, string>(),
          new Dictionary<string, ulong>(),
          new List<CatalogDlc>())
        });

    static bool TryReadCatalogDocumentFromPath(string path, out CatalogDocument? document)
    {
        document = null;
        if (!File.Exists(path))
            return false;

        try
        {
            document = JsonSerializer.Deserialize<CatalogDocument>(File.ReadAllBytes(path));
            ValidateCatalogDocument(document);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static CatalogDocument MergeDocuments(CatalogDocument baseline, CatalogDocument overlay)
    {
        ValidateCatalogDocument(baseline);
        ValidateCatalogDocument(overlay);

        var byGameId = new Dictionary<string, CatalogGame>(StringComparer.OrdinalIgnoreCase);
        foreach (CatalogGame game in baseline.Games)
            byGameId[game.Id] = game;

        foreach (CatalogGame game in overlay.Games)
            byGameId[game.Id] = game;

        return baseline with
        {
            Games = byGameId.Values.ToList()
        };
    }

    static void ValidateCatalogDocument(CatalogDocument? document)
    {
        if (document is null)
            throw new InvalidOperationException("Game catalog file could not be deserialized.");

        if (document.SchemaVersion != SupportedSchemaVersion)
            throw new InvalidOperationException($"Unsupported game catalog schema version '{document.SchemaVersion}'. Expected '{SupportedSchemaVersion}'.");

        if (document.Games is null || document.Games.Count == 0)
            throw new InvalidOperationException("Game catalog contains no games.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CatalogGame game in document.Games)
        {
            if (string.IsNullOrWhiteSpace(game.Id))
                throw new InvalidOperationException("Catalog contains a game without ID.");
            if (!seen.Add(game.Id))
                throw new InvalidOperationException($"Catalog contains duplicate game ID '{game.Id}'.");
        }
    }

    static string[] NormalizeUrls(List<string>? multiUrls, string? singleUrl)
    {
        var urls = new List<string>();
        if (multiUrls is not null)
            urls.AddRange(multiUrls.Where(url => !string.IsNullOrWhiteSpace(url)));
        if (!string.IsNullOrWhiteSpace(singleUrl))
            urls.Add(singleUrl);

        return urls.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    static bool TryExtractSha256(string? hashText, out string hash)
    {
        hash = string.Empty;
        if (string.IsNullOrWhiteSpace(hashText))
            return false;

        string candidate = hashText
          .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
          .FirstOrDefault() ?? string.Empty;
        if (candidate.Length != 64)
            return false;

        foreach (char ch in candidate)
            if (!Uri.IsHexDigit(ch))
                return false;

        hash = candidate.ToLowerInvariant();
        return true;
    }

    static bool TryVerifySignature(string publicKeyPem, string payload, string? signatureText)
    {
        if (string.IsNullOrWhiteSpace(signatureText))
            return false;

        string trimmedSignature = signatureText.Trim();
        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(trimmedSignature);
        }
        catch
        {
            return false;
        }

        try
        {
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            byte[] payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
            return rsa.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    static async Task<bool> SyncAsaCatalogAsync()
    {
        try
        {
            string? payload = await Downloader.DownloadStringAsync([AsaSettingsUrl]).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(payload))
                return false;

            JsonNode? parsed = JsonNode.Parse(payload);
            if (parsed is not JsonObject settings)
                return false;
            if (settings["dlc"] is not JsonObject dlcObject || dlcObject.Count == 0)
                return false;

            uint steamAppId = GetUIntOrDefault(settings["app_id"], 2399830);

            GameCatalogEntry asaSource;
            lock (s_stateSync)
            {
                if (!s_state.ByGameId.TryGetValue(AsaGameId, out asaSource!))
                    return false;
            }

            var runtimeNames = new Dictionary<string, string>();
            var dlcCatalog = new List<CatalogDlc>();

            var dlcEntries = new List<(uint appId, string name)>();
            foreach ((string appIdText, JsonNode? nameNode) in dlcObject.OrderBy(static x => x.Key, StringComparer.Ordinal))
            {
                if (!uint.TryParse(appIdText, out uint dlcAppId) || dlcAppId == 0)
                    continue;

                string name = nameNode?.GetValue<string>()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                    name = $"Unknown DLC {dlcAppId}";

                dlcEntries.Add((dlcAppId, name));
            }

            HashSet<uint>? appsWithDepots = null;
            if (dlcEntries.Count > 0)
            {
                try { appsWithDepots = Client.GetAppsWithDepots([.. dlcEntries.Select(static e => e.appId)]); }
                catch { }
            }

            var existingDepotIds = asaSource.DlcCatalog
              .GroupBy(static dlc => dlc.AppId)
              .ToDictionary(static group => group.Key, static group => group.Last().DepotId);

            foreach ((uint dlcAppId, string name) in dlcEntries)
            {
                uint depotId;
                if (appsWithDepots is null)
                    depotId = existingDepotIds.GetValueOrDefault(dlcAppId, 0u);
                else
                    depotId = appsWithDepots.Contains(dlcAppId) ? dlcAppId : 0u;

                runtimeNames[dlcAppId.ToString(CultureInfo.InvariantCulture)] = name;
                dlcCatalog.Add(new CatalogDlc(name, dlcAppId, depotId, false, false, "Mod", null, null, null));
            }

            if (dlcCatalog.Count == 0)
                return false;

            var acceptedDirs = asaSource.AcceptedServerGameDirs.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (acceptedDirs.Count == 0)
                acceptedDirs.Add(asaSource.ServerGameDir);

            var preAquatica = asaSource.PreAquaticaManifestOverrides
              .ToDictionary(static kvp => kvp.Key.ToString(CultureInfo.InvariantCulture), static kvp => kvp.Value);

            CatalogGame asaGame = new(
              AsaGameId,
              asaSource.DisplayName,
              steamAppId,
              steamAppId,
              asaSource.MainDepotId,
              asaSource.WorkshopDepotId,
              asaSource.ServerGameDir,
              acceptedDirs,
              asaSource.SteamFolderName,
              asaSource.ExeFileName,
              asaSource.ExeRelativePath,
              asaSource.WorkshopDirRelativePath,
              asaSource.SupportsSpacewarSpoof,
              asaSource.IsWindowsBuild,
              runtimeNames,
              preAquatica,
              dlcCatalog);

            CatalogDocument autoDocument = new(SupportedSchemaVersion, new List<CatalogGame> { asaGame });
            string serialized = JsonSerializer.Serialize(autoDocument, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;

            if (File.Exists(AutoCatalogPath))
            {
                string currentText = await File.ReadAllTextAsync(AutoCatalogPath).ConfigureAwait(false);
                if (string.Equals(currentText, serialized, StringComparison.Ordinal))
                    return false;
            }

            await File.WriteAllTextAsync(AutoCatalogPath, serialized).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static uint GetUIntOrDefault(JsonNode? node, uint fallback)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue(out uint uintValue))
                return uintValue;
            if (value.TryGetValue(out int intValue) && intValue > 0)
                return (uint)intValue;
            if (value.TryGetValue(out long longValue) && longValue > 0 && longValue <= uint.MaxValue)
                return (uint)longValue;
            if (value.TryGetValue(out string? text) && uint.TryParse(text, out uint parsed))
                return parsed;
        }

        return fallback;
    }

    static string? BuildStartupNotice(CatalogSyncResult syncResult)
    {
        if (syncResult.Applied)
            return syncResult.Message;

        return syncResult.Attempted ? syncResult.Message : null;
    }

    static GameCatalogEntry ConvertGame(CatalogGame source)
    {
        if (string.IsNullOrWhiteSpace(source.DisplayName))
            throw new InvalidOperationException($"Catalog game '{source.Id}' is missing displayName.");
        if (string.IsNullOrWhiteSpace(source.SteamFolderName))
            throw new InvalidOperationException($"Catalog game '{source.Id}' is missing steamFolderName.");
        if (string.IsNullOrWhiteSpace(source.ServerGameDir))
            throw new InvalidOperationException($"Catalog game '{source.Id}' is missing serverGameDir.");
        if (string.IsNullOrWhiteSpace(source.ExeFileName))
            throw new InvalidOperationException($"Catalog game '{source.Id}' is missing exeFileName.");
        if (string.IsNullOrWhiteSpace(source.ExeRelativePath))
            throw new InvalidOperationException($"Catalog game '{source.Id}' is missing exeRelativePath.");
        if (string.IsNullOrWhiteSpace(source.WorkshopDirRelativePath))
            throw new InvalidOperationException($"Catalog game '{source.Id}' is missing workshopDirRelativePath.");

        List<string> acceptedServerDirs = source.AcceptedServerGameDirs is null || source.AcceptedServerGameDirs.Count == 0
          ? new List<string> { source.ServerGameDir }
          : new List<string>(source.AcceptedServerGameDirs);

        var dlcCatalog = new List<GameDlcInfo>(source.DlcCatalog?.Count ?? 0);
        if (source.DlcCatalog is not null)
            foreach (CatalogDlc dlc in source.DlcCatalog)
                dlcCatalog.Add(ConvertDlc(source.Id, dlc));

        var runtimeDisplayNames = new Dictionary<uint, string>();
        if (source.RuntimeDlcDisplayNames is not null)
        {
            foreach ((string appIdText, string displayName) in source.RuntimeDlcDisplayNames)
            {
                if (!uint.TryParse(appIdText, out uint appId))
                    throw new InvalidOperationException($"Catalog game '{source.Id}' has invalid runtimeDlcDisplayNames key '{appIdText}'.");
                runtimeDisplayNames[appId] = displayName;
            }
        }

        var preAquaticaOverrides = new Dictionary<uint, ulong>();
        if (source.PreAquaticaManifestOverrides is not null)
        {
            foreach ((string depotIdText, ulong manifestId) in source.PreAquaticaManifestOverrides)
            {
                if (!uint.TryParse(depotIdText, out uint depotId))
                    throw new InvalidOperationException($"Catalog game '{source.Id}' has invalid preAquaticaManifestOverrides key '{depotIdText}'.");
                preAquaticaOverrides[depotId] = manifestId;
            }
        }

        return new(
            source.Id,
            source.DisplayName,
            source.SteamAppId,
            source.RuntimeAppId == 0 ? source.SteamAppId : source.RuntimeAppId,
            source.MainDepotId,
            source.WorkshopDepotId == 0 ? source.SteamAppId : source.WorkshopDepotId,
            source.ServerGameDir,
            acceptedServerDirs,
            source.SteamFolderName,
            source.ExeFileName,
            source.ExeRelativePath,
            source.WorkshopDirRelativePath,
            source.SupportsSpacewarSpoof,
            source.IsWindowsBuild,
            dlcCatalog,
            runtimeDisplayNames,
            preAquaticaOverrides);
    }

    static GameDlcInfo ConvertDlc(string gameId, CatalogDlc dlc)
    {
        if (string.IsNullOrWhiteSpace(dlc.Name))
            throw new InvalidOperationException($"Catalog game '{gameId}' has DLC entry without name.");
        if (string.IsNullOrWhiteSpace(dlc.MapCode))
            throw new InvalidOperationException($"Catalog game '{gameId}' has DLC '{dlc.Name}' without mapCode.");
        if (!Enum.TryParse(dlc.MapCode, true, out MapCode code))
            throw new InvalidOperationException($"Catalog game '{gameId}' has DLC '{dlc.Name}' with invalid mapCode '{dlc.MapCode}'.");

        return new(
            dlc.Name,
            dlc.AppId,
            dlc.DepotId,
            dlc.IsModContent,
            dlc.HasPPostfix,
            code,
            dlc.FolderNameOverride,
            dlc.MapFileNameOverride,
            dlc.PreAquaticaManifestId);
    }

    sealed record CatalogDocument(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("games")] List<CatalogGame> Games);

    sealed record CatalogState(
      Dictionary<string, GameCatalogEntry> ByGameId,
      Dictionary<uint, string> GameIdBySteamAppId);

    sealed record CatalogSyncConfig(
      [property: JsonPropertyName("enabled")] bool Enabled,
      [property: JsonPropertyName("catalogUrl")] string? CatalogUrl,
      [property: JsonPropertyName("catalogUrls")] List<string>? CatalogUrls,
      [property: JsonPropertyName("hashUrl")] string? HashUrl,
      [property: JsonPropertyName("hashUrls")] List<string>? HashUrls,
      [property: JsonPropertyName("signatureUrl")] string? SignatureUrl,
      [property: JsonPropertyName("signatureUrls")] List<string>? SignatureUrls,
      [property: JsonPropertyName("publicKeyPem")] string? PublicKeyPem,
      [property: JsonPropertyName("requireHashVerification")] bool RequireHashVerification = true,
      [property: JsonPropertyName("requireSignatureVerification")] bool RequireSignatureVerification = true);

    public readonly record struct CatalogSyncResult(bool Attempted, bool Applied, string Message);

    sealed record CatalogGame(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("steamAppId")] uint SteamAppId,
        [property: JsonPropertyName("runtimeAppId")] uint RuntimeAppId,
        [property: JsonPropertyName("mainDepotId")] uint MainDepotId,
        [property: JsonPropertyName("workshopDepotId")] uint WorkshopDepotId,
        [property: JsonPropertyName("serverGameDir")] string ServerGameDir,
        [property: JsonPropertyName("acceptedServerGameDirs")] List<string>? AcceptedServerGameDirs,
        [property: JsonPropertyName("steamFolderName")] string SteamFolderName,
        [property: JsonPropertyName("exeFileName")] string ExeFileName,
        [property: JsonPropertyName("exeRelativePath")] string ExeRelativePath,
        [property: JsonPropertyName("workshopDirRelativePath")] string WorkshopDirRelativePath,
        [property: JsonPropertyName("supportsSpacewarSpoof")] bool SupportsSpacewarSpoof,
        [property: JsonPropertyName("isWindowsBuild")] bool IsWindowsBuild,
        [property: JsonPropertyName("runtimeDlcDisplayNames")] Dictionary<string, string>? RuntimeDlcDisplayNames,
        [property: JsonPropertyName("preAquaticaManifestOverrides")] Dictionary<string, ulong>? PreAquaticaManifestOverrides,
        [property: JsonPropertyName("dlcCatalog")] List<CatalogDlc>? DlcCatalog);

    sealed record CatalogDlc(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("appId")] uint AppId,
        [property: JsonPropertyName("depotId")] uint DepotId,
        [property: JsonPropertyName("isModContent")] bool IsModContent,
        [property: JsonPropertyName("hasPPostfix")] bool HasPPostfix,
        [property: JsonPropertyName("mapCode")] string MapCode,
        [property: JsonPropertyName("folderNameOverride")] string? FolderNameOverride,
        [property: JsonPropertyName("mapFileNameOverride")] string? MapFileNameOverride,
        [property: JsonPropertyName("preAquaticaManifestId")] ulong? PreAquaticaManifestId);
}
