using System.Runtime.InteropServices;

namespace TEKLauncher.Platform;

sealed class WindowsTekSteamClientBootstrap : ITekSteamClientBootstrap
{
    const string PrimaryVersionUrl = "https://teknology-hub.com/software/tek-steamclient/version-2";
    const string MirrorVersionUrl = "https://de.teknology-hub.com/software/tek-steamclient/version-2";
    const string PrimaryDownloadUrl = "https://teknology-hub.com/software/tek-steamclient/releases/latest-2/win-x86_64-static/libtek-steamclient-2.dll";
    const string MirrorDownloadUrl = "https://de.teknology-hub.com/software/tek-steamclient/releases/latest-2/win-x86_64-static/libtek-steamclient-2.dll";

    public async Task<TekSteamClientBootstrapResult> InitializeAsync(string gamePath)
    {
        var latestTscVer = await Downloader.DownloadStringAsync(PrimaryVersionUrl, MirrorVersionUrl);
        for (bool updated = false; ;)
        {
            if (!File.Exists(TEKSteamClient.DllPath))
            {
                var data = await Downloader.DownloadBytesAsync(PrimaryDownloadUrl, MirrorDownloadUrl);
                if (data is null)
                    return new(false, false, null, "libtek-steamclient-2.dll", PrimaryDownloadUrl, null);
                File.WriteAllBytes(TEKSteamClient.DllPath, data);
            }

            if (updated)
                return new(true, true, null, null, null, null);

            nint handle;
            try
            {
                handle = NativeLibrary.Load(TEKSteamClient.DllPath);
            }
            catch (BadImageFormatException)
            {
                File.Delete(TEKSteamClient.DllPath);
                continue;
            }

            if (latestTscVer is not null)
            {
                string normalizedLatest = NormalizeVersion(latestTscVer);
                string normalizedCurrent = NormalizeVersion(Marshal.PtrToStringUTF8(TEKSteamClient.GetVersion())!);
                if (Version.TryParse(normalizedLatest, out var latestVersion) && Version.TryParse(normalizedCurrent, out var currentVersion) && latestVersion > currentVersion)
                {
                    try
                    {
                        NativeLibrary.Free(handle);
                        NativeLibrary.Free(handle);
                        File.Delete(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory, "libtek-steamclient-2.dll"));
                    }
                    catch { }
                    File.Delete(TEKSteamClient.DllPath);
                    updated = true;
                    continue;
                }
            }
            break;
        }

        string localeDir = Path.Combine(LauncherBootstrap.AppDataFolder, "tsc-locale");
        Directory.CreateDirectory(localeDir);
        TEKSteamClient.LoadLocale(localeDir);

        var ctx = new TEKSteamClient.LibCtx();
        var appMng = new TEKSteamClient.AppManager(ctx, gamePath);
        if (appMng.IsInvalid)
        {
            appMng.Dispose();
            ctx.Dispose();
            return new(false, false, appMng.CreationError.Message, null, null, null);
        }

        string modsDir = Path.Combine(gamePath, "Mods");
        try
        {
            if (!Directory.Exists(modsDir))
                Directory.CreateDirectory(modsDir);
        }
        catch { }

        var res = appMng.SetWorkshopDir(modsDir);
        if (!res.Success)
        {
            appMng.Dispose();
            ctx.Dispose();
            return new(false, false, res.Message, null, null, null);
        }

        var s3Res = await Task.Run(() => ctx.SyncS3Manifest("https://api.teknology-hub.com/s3"));
        var s3MirrorRes = await Task.Run(() => ctx.SyncS3Manifest("https://de.api.teknology-hub.com/s3"));

        string? warningMessage = null;
        if (!s3Res.Success && !s3MirrorRes.Success)
            warningMessage = $"Failed to connect to tek-s3u servers: {s3Res.AuxMessage}";
        if (s3Res.Uri != 0)
            Marshal.FreeHGlobal(s3Res.Uri);
        if (s3MirrorRes.Uri != 0)
            Marshal.FreeHGlobal(s3MirrorRes.Uri);

        LauncherServices.TekSteamClient.Initialize(ctx, appMng);
        return new(true, false, null, null, null, warningMessage);
    }

    static string NormalizeVersion(string version) => version.Contains('-') ? version.Split('-')[0] : version;
}