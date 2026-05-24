using System.Linq;
using System.Text.Json.Nodes;

namespace TEKLauncher;

static class CatalogRefreshCommand
{
    static readonly string[] s_defaultAsaSettingsUrls =
    [
      "https://nuclearist.ru/static/tek-gr-settings.json"
    ];

    static readonly Dictionary<string, JsonObject> s_defaultGameTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ASE"] = new()
        {
            ["id"] = "ASE",
            ["displayName"] = "ARK: Survival Evolved",
            ["steamAppId"] = 346110,
            ["runtimeAppId"] = 346110,
            ["mainDepotId"] = 346111,
            ["workshopDepotId"] = 346110,
            ["serverGameDir"] = "ark_survival_evolved",
            ["acceptedServerGameDirs"] = new JsonArray("ark_survival_evolved"),
            ["steamFolderName"] = "ARK",
            ["exeFileName"] = "ShooterGame.exe",
            ["exeRelativePath"] = "ShooterGame/Binaries/Win64/ShooterGame.exe",
            ["workshopDirRelativePath"] = "Mods",
            ["supportsSpacewarSpoof"] = true,
            ["isWindowsBuild"] = true,
            ["runtimeDlcDisplayNames"] = new JsonObject
            {
                ["473850"] = "The Center - ARK Expansion Map",
                ["508150"] = "Primitive+ - ARK Total Conversion",
                ["512540"] = "ARK: Scorched Earth - Expansion Pack",
                ["642250"] = "Ragnarok - ARK Expansion Map",
                ["696680"] = "ARK: Survival Evolved Season Pass",
                ["708770"] = "ARK: Aberration - Expansion Pack",
                ["887380"] = "ARK: Extinction - Expansion Pack",
                ["1100810"] = "Valguero - ARK Expansion Map",
                ["1113410"] = "ARK: Genesis Season Pass",
                ["1270830"] = "Crystal Isles - ARK Expansion Map",
                ["1691800"] = "Lost Island - ARK Expansion Map",
                ["1887560"] = "Fjordur - ARK Expansion Map",
                ["3537070"] = "Aquatica - ARK Expansion Map"
            },
            ["preAquaticaManifestOverrides"] = new JsonObject
            {
                ["346114"] = 5573587184752106093,
                ["375351"] = 8265777340034981821,
                ["375354"] = 7952753366101555648,
                ["375357"] = 1447242805278740772,
                ["473851"] = 2551727096735353757,
                ["473854"] = 847717640995143866,
                ["473857"] = 1054814513659387220,
                ["1318685"] = 8189621638927588129,
                ["1691801"] = 3147973472387347535,
                ["1887561"] = 580528532335699271
            },
            ["dlcCatalog"] = new JsonArray
      {
        new JsonObject
        {
          ["name"] = "The Center",
          ["appId"] = 473850,
          ["depotId"] = 346114,
          ["isModContent"] = true,
          ["hasPPostfix"] = false,
          ["mapCode"] = "TheCenter"
        },
        new JsonObject
        {
          ["name"] = "Scorched Earth",
          ["appId"] = 512540,
          ["depotId"] = 375351,
          ["isModContent"] = false,
          ["hasPPostfix"] = true,
          ["mapCode"] = "ScorchedEarth"
        },
        new JsonObject
        {
          ["name"] = "Ragnarok",
          ["appId"] = 642250,
          ["depotId"] = 375354,
          ["isModContent"] = true,
          ["hasPPostfix"] = false,
          ["mapCode"] = "Ragnarok"
        },
        new JsonObject
        {
          ["name"] = "Aberration",
          ["appId"] = 708770,
          ["depotId"] = 375357,
          ["isModContent"] = false,
          ["hasPPostfix"] = true,
          ["mapCode"] = "Aberration"
        },
        new JsonObject
        {
          ["name"] = "Extinction",
          ["appId"] = 887380,
          ["depotId"] = 473851,
          ["isModContent"] = false,
          ["hasPPostfix"] = false,
          ["mapCode"] = "Extinction"
        },
        new JsonObject
        {
          ["name"] = "Valguero",
          ["appId"] = 1100810,
          ["depotId"] = 473854,
          ["isModContent"] = true,
          ["hasPPostfix"] = true,
          ["mapCode"] = "Valguero"
        },
        new JsonObject
        {
          ["name"] = "Genesis Part 1 & 2",
          ["appId"] = 1113410,
          ["depotId"] = 473857,
          ["isModContent"] = false,
          ["hasPPostfix"] = false,
          ["mapCode"] = "Genesis",
          ["folderNameOverride"] = "Genesis"
        },
        new JsonObject
        {
          ["name"] = "Crystal Isles",
          ["appId"] = 1270830,
          ["depotId"] = 1318685,
          ["isModContent"] = true,
          ["hasPPostfix"] = false,
          ["mapCode"] = "CrystalIsles"
        },
        new JsonObject
        {
          ["name"] = "Lost Island",
          ["appId"] = 1691800,
          ["depotId"] = 1691801,
          ["isModContent"] = true,
          ["hasPPostfix"] = false,
          ["mapCode"] = "LostIsland"
        },
        new JsonObject
        {
          ["name"] = "Fjordur",
          ["appId"] = 1887560,
          ["depotId"] = 1887561,
          ["isModContent"] = true,
          ["hasPPostfix"] = false,
          ["mapCode"] = "Fjordur",
          ["folderNameOverride"] = "FjordurOfficial",
          ["mapFileNameOverride"] = "Fjordur"
        },
        new JsonObject
        {
          ["name"] = "Aquatica",
          ["appId"] = 3537070,
          ["depotId"] = 3537070,
          ["isModContent"] = false,
          ["hasPPostfix"] = false,
          ["mapCode"] = "Aquatica",
          ["folderNameOverride"] = "Abyss",
          ["mapFileNameOverride"] = "Aquatica"
        }
      }
        },
        ["ASA"] = new()
        {
            ["id"] = "ASA",
            ["displayName"] = "ARK: Survival Ascended",
            ["steamAppId"] = 2399830,
            ["runtimeAppId"] = 2399830,
            ["mainDepotId"] = 2399831,
            ["workshopDepotId"] = 2399830,
            ["serverGameDir"] = "ark_survival_ascended",
            ["acceptedServerGameDirs"] = new JsonArray("ark_survival_ascended"),
            ["steamFolderName"] = "ARK Survival Ascended",
            ["exeFileName"] = "ArkAscended.exe",
            ["exeRelativePath"] = "ShooterGame/Binaries/Win64/ArkAscended.exe",
            ["workshopDirRelativePath"] = "Mods",
            ["supportsSpacewarSpoof"] = true,
            ["isWindowsBuild"] = true,
            ["runtimeDlcDisplayNames"] = new JsonObject(),
            ["preAquaticaManifestOverrides"] = new JsonObject(),
            ["dlcCatalog"] = new JsonArray()
        }
    };

    public static bool TryRun(string[] args, out int exitCode)
    {
        if (!args.Any(static arg => string.Equals(arg, "--refresh-catalog", StringComparison.OrdinalIgnoreCase)))
        {
            exitCode = 0;
            return false;
        }

        exitCode = Run(args);
        return true;
    }

    static int Run(string[] args)
    {
        LauncherLog.Initialize();
        LauncherLog.Information("Catalog refresh invoked. Args={Args}", args);

        try
        {
            RefreshOptions options = ParseOptions(args);
            RefreshCatalog(options);
            LauncherLog.Information("Catalog refresh completed. CatalogPath={CatalogPath}", options.CatalogPath);
            Console.WriteLine($"wrote catalog: {options.CatalogPath}");
            return 0;
        }
        catch (Exception ex)
        {
            LauncherLog.Error(ex, "Catalog refresh failed");
            Console.Error.WriteLine($"catalog refresh failed: {ex.Message}");
            return 1;
        }
    }

    static RefreshOptions ParseOptions(string[] args)
    {
        string catalogPath = Path.Combine(Environment.CurrentDirectory, "assets", "catalog", "game-catalog.json");
        bool dryRun = false;
        var asaSettingsUrls = new List<string>();
        bool useAsaSettings = true;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, "--refresh-catalog", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(arg, "--catalog", StringComparison.OrdinalIgnoreCase))
            {
                catalogPath = ReadValue(args, ref i, "--catalog");
                continue;
            }

            if (string.Equals(arg, "--asa-settings-url", StringComparison.OrdinalIgnoreCase))
            {
                asaSettingsUrls.Add(ReadValue(args, ref i, "--asa-settings-url"));
                continue;
            }

            if (string.Equals(arg, "--skip-asa-settings", StringComparison.OrdinalIgnoreCase))
            {
                useAsaSettings = false;
                continue;
            }

            if (string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
                continue;
            }
        }

        return new(
          catalogPath,
          dryRun,
          useAsaSettings,
          asaSettingsUrls.Count == 0 ? s_defaultAsaSettingsUrls : [.. asaSettingsUrls]);
    }

    static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
            throw new InvalidOperationException($"Missing value for {optionName}");
        index++;
        return args[index];
    }

    static void RefreshCatalog(RefreshOptions options)
    {
        JsonNode? asaSettingsNode = null;
        if (options.UseAsaSettings)
            asaSettingsNode = DownloadOptionalJson(options.AsaSettingsUrls, "ASA settings");

        JsonObject catalog = LoadOrCreateCatalog(options.CatalogPath);

        JsonArray games = catalog["games"] as JsonArray
          ?? throw new InvalidOperationException("Catalog has no valid 'games' array.");

        ReplaceGame(games, (JsonObject)s_defaultGameTemplates["ASE"].DeepClone());
        int replacedAsaCount = ReplaceAsaFromTekGrSettings(games, asaSettingsNode);

        Console.WriteLine($"updated games={games.Count} replaced_asa_dlc={replacedAsaCount}");
        if (options.DryRun)
            return;

        string outputPath = Path.GetFullPath(options.CatalogPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, catalog.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }

    static JsonNode? DownloadOptionalJson(IReadOnlyList<string> urls, string sourceName)
    {
        if (urls.Count == 0)
            return null;

        try
        {
            string? payload = Downloader.DownloadStringAsync([.. urls]).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(payload))
            {
                Console.Error.WriteLine($"warning: {sourceName} download returned empty payload");
                return null;
            }

            JsonNode? parsed = JsonNode.Parse(payload);
            if (parsed is null)
            {
                Console.Error.WriteLine($"warning: {sourceName} payload is empty");
                return null;
            }

            return parsed;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"warning: failed to download {sourceName}: {ex.Message}");
            return null;
        }
    }

    static JsonObject LoadOrCreateCatalog(string catalogPath)
    {
        string fullPath = Path.GetFullPath(catalogPath);
        if (File.Exists(fullPath))
        {
            JsonNode? existing = JsonNode.Parse(File.ReadAllText(fullPath));
            if (existing is JsonObject existingObject)
                return existingObject;
            throw new InvalidOperationException("Catalog exists but is not a valid JSON object.");
        }

        Console.Error.WriteLine($"warning: catalog not found at {fullPath}, creating default catalog");
        var games = new JsonArray
    {
      s_defaultGameTemplates["ASE"].DeepClone(),
      s_defaultGameTemplates["ASA"].DeepClone()
    };
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["games"] = games
        };
    }

    static int ReplaceAsaFromTekGrSettings(JsonArray games, JsonNode? asaSettingsNode)
    {
        if (asaSettingsNode is not JsonObject settings)
            return 0;

        if (!TryGetUInt(settings["app_id"], out uint appId)
          && !TryGetUInt(settings["appId"], out appId)
          && !TryGetUInt(settings["id"], out appId))
            return 0;

        JsonObject? dlcNode = settings["dlc"] as JsonObject;
        if (dlcNode is null || dlcNode.Count == 0)
            return 0;

        JsonObject? asaGame = games
          .OfType<JsonObject>()
          .FirstOrDefault(game => string.Equals(game["id"]?.GetValue<string>(), "ASA", StringComparison.OrdinalIgnoreCase));

        if (asaGame is null)
        {
            asaGame = (JsonObject)s_defaultGameTemplates["ASA"].DeepClone();
            games.Add(asaGame);
        }

        asaGame["steamAppId"] = appId;
        asaGame["runtimeAppId"] = appId;

        var runtimeNames = new JsonObject();
        var dlcCatalog = new JsonArray();

        foreach ((string appIdText, JsonNode? nameNode) in dlcNode.OrderBy(static x => x.Key, StringComparer.Ordinal))
        {
            if (!uint.TryParse(appIdText, out uint dlcAppId) || dlcAppId == 0)
                continue;

            string? name = nameNode?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = $"Unknown DLC {dlcAppId}";

            runtimeNames[dlcAppId.ToString(CultureInfo.InvariantCulture)] = name;
            dlcCatalog.Add(new JsonObject
            {
                ["name"] = name,
                ["appId"] = dlcAppId,
                ["depotId"] = dlcAppId,
                ["isModContent"] = false,
                ["hasPPostfix"] = false,
                ["mapCode"] = "Mod"
            });
        }

        asaGame["runtimeDlcDisplayNames"] = runtimeNames;
        asaGame["dlcCatalog"] = dlcCatalog;

        return dlcCatalog.Count;
    }

    static void ReplaceGame(JsonArray games, JsonObject replacement)
    {
        string? replacementId = replacement["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(replacementId))
            throw new InvalidOperationException("Replacement game entry has no id.");

        for (int i = 0; i < games.Count; i++)
        {
            if (games[i] is not JsonObject existing)
                continue;

            if (string.Equals(existing["id"]?.GetValue<string>(), replacementId, StringComparison.OrdinalIgnoreCase))
            {
                games[i] = replacement;
                return;
            }
        }

        games.Add(replacement);
    }

    static bool TryGetUInt(JsonNode? node, out uint value)
    {
        value = 0;
        if (node is null)
            return false;

        if (node is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue(out uint uintValue))
            {
                value = uintValue;
                return true;
            }

            if (jsonValue.TryGetValue(out int intValue) && intValue > 0)
            {
                value = (uint)intValue;
                return true;
            }

            if (jsonValue.TryGetValue(out long longValue) && longValue > 0 && longValue <= uint.MaxValue)
            {
                value = (uint)longValue;
                return true;
            }

            if (jsonValue.TryGetValue(out string? strValue)
              && !string.IsNullOrWhiteSpace(strValue)
              && uint.TryParse(strValue, out uint parsed))
            {
                value = parsed;
                return true;
            }
        }

        return false;
    }

    readonly record struct RefreshOptions(
      string CatalogPath,
      bool DryRun,
      bool UseAsaSettings,
      IReadOnlyList<string> AsaSettingsUrls);
}
