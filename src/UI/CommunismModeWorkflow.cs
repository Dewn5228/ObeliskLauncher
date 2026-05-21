using TEKLauncher.Data;

namespace TEKLauncher.UI;

static class CommunismModeWorkflow
{
    const string AudioDownloadUrl = "https://drive.google.com/uc?export=download&id=1f6qBEQKhFDELItES_CvrSEv0pep3b5r-";
    const string ImageDownloadUrl = "https://drive.google.com/uc?export=download&id=19LvV2jtDDjtg9bSd8-Ec8mqUGyOaVV8a";

    public static string AppTitle => Settings.CommunismMode ? "Краснознамённый имени В.И. Ленина ТЕК Лаунчер" : "TEK Launcher";

    static string DirectoryPath => Path.Combine(LauncherBootstrap.AppDataFolder, "CM");

    static string AudioPath => Path.Combine(DirectoryPath, "Audio.wav");

    static string ImagePath => Path.Combine(DirectoryPath, "Image.jpg");

    public static async Task ApplyAsync()
    {
        if (!Settings.CommunismMode)
            return;

        await EnsureAssetsAsync();
    }

    public static string? GetPlayImagePath() => File.Exists(ImagePath) ? ImagePath : null;

    public static void StopAudio()
    {
    }

    static async Task EnsureAssetsAsync()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);

            if (!File.Exists(ImagePath))
            {
                string tempImagePath = Path.Combine(DirectoryPath, "Dw_Image.jpg");
                if (await Downloader.DownloadFileAsync(tempImagePath, new(), ImageDownloadUrl))
                    File.Move(tempImagePath, ImagePath, true);
            }

            if (!File.Exists(AudioPath))
            {
                string tempAudioPath = Path.Combine(DirectoryPath, "Dw_Audio.wav");
                if (await Downloader.DownloadFileAsync(tempAudioPath, new(), AudioDownloadUrl))
                    File.Move(tempAudioPath, AudioPath, true);
            }
        }
        catch
        {
        }
    }

}