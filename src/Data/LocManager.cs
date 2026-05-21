namespace TEKLauncher.Data;

/// <summary>Manages launcher localizations.</summary>
static class LocManager
{
    /// <summary>List of all localized strings.</summary>
    static readonly string[] s_list = new string[229];
    /// <summary>List of ISO 639-1 codes of all currently supported languages.</summary>
    static readonly string[] s_supportedLangs = { "en", "es", "fr", "nl", "pt", "el", "ru", "zh" };
    static readonly bool[] s_availableLangs = BuildAvailableLanguageList();
    /// <summary>Gets the index of current launcher language in the list of all supported languages.</summary>
    public static int CurrentLanguageIndex
    {
        get
        {
            int index = Array.IndexOf(s_supportedLangs, CurrentLanguage);
            return index >= 0 && IsLanguageAvailable(index) ? index : 0;
        }
    }
    /// <summary>Gets or sets the ISO 639-1 code of current display language of the launcher.</summary>
    public static string? CurrentLanguage { get; set; }
    public static bool HasAdditionalLauncherLanguages => Array.IndexOf(s_availableLangs, true) != Array.LastIndexOf(s_availableLangs, true);
    /// <summary>Selects launcher display language based on setting value and OS language, and loads localized strings.</summary>
    /// <param name="cultureCode">Culture code of Windows' UI.</param>
    public static void Initialize(string cultureCode)
    {
        if (string.IsNullOrEmpty(CurrentLanguage) || Array.IndexOf(s_supportedLangs, CurrentLanguage) == -1)
        {
            if (cultureCode.Length < 2)
                CurrentLanguage = "en"; //Fallback to English if cultureCode is invalid
            else
            {
                cultureCode = cultureCode[..2]; //Leave only the language code
                if (cultureCode == "be" || cultureCode == "uk")
                    CurrentLanguage = "ru"; //Belarusian and Ukrainian are very similar to Russian so Russian loc can be used
                else if (Array.IndexOf(s_supportedLangs, cultureCode) == -1)
                    CurrentLanguage = "en"; //Fallback to English if OS language is not in the list of supported ones
                else
                    CurrentLanguage = cultureCode; //OS language is supported, use its loc
            }
        }

        if (TryLoadLocalization(CurrentLanguage!) || (CurrentLanguage != "en" && TryLoadLocalization("en")))
            return;

        PopulateFallbackStrings();
    }
    /// <summary>Sets new current language by its index.</summary>
    /// <param name="index">Index of the new language in supported languages list.</param>
    public static void SetCurrentLanguage(int index)
    {
        if (!IsLanguageAvailable(index))
        {
            CurrentLanguage = "en";
            return;
        }

        CurrentLanguage = s_supportedLangs[index];
    }
    public static bool IsLanguageAvailable(int index) => index == 0 || index >= 0 && index < s_availableLangs.Length && s_availableLangs[index];
    /// <summary>Converts an amount of bytes into its string representation.</summary>
    /// <param name="bytes">Amount of bytes.</param>
    /// <returns>The string representation of size.</returns>
    public static string BytesToString(long bytes) => bytes switch
    {
        >= 1073741824 => $"{bytes / 1073741824.0:0.##} {s_list[(int)LocCode.GB]}",
        >= 1048576 => $"{bytes / 1048576.0:0.#} {s_list[(int)LocCode.MB]}",
        _ => $"{bytes / 1024.0:0} {s_list[(int)LocCode.KB]}"
    };
    /// <summary>Converts an amount of bytes into its string representation.</summary>
    /// <param name="bytes">Amount of bytes.</param>
    /// <param name="unit">When this method returns, contains the acronym of measure unit.</param>
    /// <returns>The numeric part of string representation of size.</returns>
    public static string BytesToString(long bytes, out string unit)
    {
        if (bytes >= 1073741824)
        {
            unit = s_list[(int)LocCode.GB];
            return (bytes / 1073741824.0).ToString("0.##");
        }
        else if (bytes >= 1048576)
        {
            unit = s_list[(int)LocCode.MB];
            return (bytes / 1048576.0).ToString("0.#");
        }
        else
        {
            unit = s_list[(int)LocCode.KB];
            return (bytes / 1024.0).ToString("0");
        }
    }
    /// <summary>Retrieves a localized string by its identifier.</summary>
    /// <param name="code">Identifier of the localized string.</param>
    /// <returns>A localized string.</returns>
    public static string GetString(LocCode code) => s_list[(int)code];
    /// <summary>Converts an amount of seconds into its string representation.</summary>
    /// <param name="seconds">Amount of seconds.</param>
    /// <returns>The string representation of time interval.</returns>
    public static string SecondsToString(long seconds)
    {
        if (seconds == 0)
            return string.Concat("0", s_list[(int)LocCode.Second]);
        var resultBuilder = new StringBuilder(6);
        if (seconds >= 3600)
            resultBuilder.Append(seconds / 3600).Append(s_list[(int)LocCode.Hour]);
        long mod = seconds % 3600;
        if (mod >= 60)
            resultBuilder.Append(' ').Append(mod / 60).Append(s_list[(int)LocCode.Minute]);
        if (seconds < 300)
        {
            mod = seconds % 60;
            if (mod != 0)
                resultBuilder.Append(' ').Append(mod).Append(s_list[(int)LocCode.Second]);
        }
        int offset = resultBuilder[0] == ' ' ? 1 : 0;
        return resultBuilder.ToString(offset, resultBuilder.Length - offset);
    }

    static bool TryLoadLocalization(string languageCode)
    {
        try
        {
            using var resourceStream = LauncherResources.OpenRead($"res/loc/{languageCode}.txt");
            using var reader = new StreamReader(resourceStream);

            for (int i = 0; i < s_list.Length; i++)
            {
                string? line = reader.ReadLine();
                if (line is null)
                    return false;

                s_list[i] = line.Replace(@"\n", "\n");
            }

            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException or InvalidOperationException)
        {
            return false;
        }
    }

    static bool[] BuildAvailableLanguageList()
    {
        var available = new bool[s_supportedLangs.Length];
        available[0] = true;
        for (int i = 1; i < s_supportedLangs.Length; i++)
            available[i] = HasLocalizationResource(s_supportedLangs[i]);

        return available;
    }

    static bool HasLocalizationResource(string languageCode)
    {
        try
        {
            using var resourceStream = LauncherResources.OpenRead($"res/loc/{languageCode}.txt");
            return resourceStream.CanRead;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException or InvalidOperationException)
        {
            return false;
        }
    }

    static void PopulateFallbackStrings()
    {
        foreach (LocCode code in Enum.GetValues<LocCode>())
            s_list[(int)code] = CreateFallbackString(code);
    }

    static string CreateFallbackString(LocCode code)
    {
        string name = Enum.GetName(code) ?? code.ToString();
        var builder = new StringBuilder(name.Length + 8);

        for (int i = 0; i < name.Length; i++)
        {
            char current = name[i];
            if (i > 0 && char.IsUpper(current) && (char.IsLower(name[i - 1]) || i + 1 < name.Length && char.IsLower(name[i + 1])))
                builder.Append(' ');

            builder.Append(current);
        }

        return code switch
        {
            LocCode.NA => "N/A",
            LocCode.OK => "OK",
            LocCode.PlayTab => "Play",
            LocCode.ServersTab => "Servers",
            LocCode.GameOptionsTab => "Game Options",
            LocCode.DLCTab => "DLC",
            LocCode.ModsTab => "Mods",
            LocCode.LauncherSettingsTab => "Launcher Settings",
            LocCode.AboutTab => "About",
            LocCode.LanguageChangeInfo => "Restart the launcher to fully apply the language change.",
            _ => builder.ToString()
        };
    }
}