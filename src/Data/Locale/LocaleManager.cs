using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using ObeliskLauncher;

namespace ObeliskLauncher.Data;

static class Locale
{
    const string DefaultLanguage = "en";
    private const string Section = ".";
    private static readonly string[] AllLanguages = ["en", "es", "fr", "nl", "pt", "el", "ru", "zh", "ar"];
    private static readonly Dictionary<string, string> FlatStrings = new(StringComparer.OrdinalIgnoreCase);
    public static event Action? LanguageChanged;
    public static int CurrentIndex { get; private set; }
    public static string CurrentLanguage { get; set; } = DefaultLanguage;

    public static string Get(string key, params object[] args)
    {
        if (FlatStrings.TryGetValue(key, out string? tmpl))
            return args.Length > 0 ? string.Format(tmpl, args) : tmpl;
        return $"?{key}?";
    }

    // Use invariant numbers and a non-breaking space so that a value stays attached
    // to its localized unit in Arabic without changing the numeric order.
    public static string BytesToString(long bytes)
    {
        string value = BytesToString(bytes, out string unit);
        return value + "\u00A0" + unit;
    }

    public static string BytesToString(long bytes, out string unit)
    {
        double scaled;
        string format;
        if (bytes >= 1073741824)
        {
            scaled = bytes / 1073741824.0;
            format = "0.##";
            unit = Get("common.bytes.gb");
        }
        else if (bytes >= 1048576)
        {
            scaled = bytes / 1048576.0;
            format = "0.#";
            unit = Get("common.bytes.mb");
        }
        else
        {
            scaled = bytes / 1024.0;
            format = "0";
            unit = Get("common.bytes.kb");
        }
        return scaled.ToString(format, CurrentLanguage == "ar" ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture);
    }

    public static string SecondsToString(long seconds)
    {
        if (seconds == 0)
            return string.Concat("0", Get("time.second"));

        var sb = new StringBuilder(6);
        if (seconds >= 3600)
            sb.Append(seconds / 3600).Append(Get("time.hour"));
        long rem = seconds % 3600;
        if (rem >= 60)
            sb.Append(' ').Append(rem / 60).Append(Get("time.minute"));
        if (seconds < 300)
        {
            rem = seconds % 60;
            if (rem != 0)
                sb.Append(' ').Append(rem).Append(Get("time.second"));
        }
        int off = sb[0] == ' ' ? 1 : 0;
        return sb.ToString(off, sb.Length - off);
    }

    public static void Init(string cultureCode)
    {
        string lang = IsSupportedLanguage(CurrentLanguage)
            ? CurrentLanguage
            : ResolveCulture(cultureCode);
        CurrentLanguage = lang;
        if (!LoadLanguage(lang))
        {
            lang = DefaultLanguage;
            CurrentLanguage = lang;
            LoadLanguage(lang);
        }
        CurrentIndex = Array.IndexOf(AllLanguages, lang);
    }

    public static void SetLanguage(int index)
    {
        if (index < 0 || index >= AllLanguages.Length)
            index = Array.IndexOf(AllLanguages, DefaultLanguage);
        string lang = AllLanguages[index];
        if (!LoadLanguage(lang))
            return;
        CurrentLanguage = lang;
        CurrentIndex = index;
        LanguageChanged?.Invoke();
    }

    public static bool IsLanguageAvailable(int index) => index >= 0 && index < AllLanguages.Length;

    static bool IsSupportedLanguage(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
        string lang = code.Trim().Length >= 2 ? code.Trim()[..2] : code.Trim();
        return Array.IndexOf(AllLanguages, lang) >= 0;
    }

    private static string ResolveCulture(string cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode) || cultureCode.Length < 2)
            return DefaultLanguage;
        string lang = cultureCode[..2];
        if (lang == "be" || lang == "uk")
            lang = "ru";
        return Array.IndexOf(AllLanguages, lang) < 0 ? DefaultLanguage : lang;
    }

    private static bool LoadLanguage(string code)
    {
        FlatStrings.Clear();

        if (!TryReadLanguage(DefaultLanguage, FlatStrings))
            return false;

        if (code.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            return true;

        var localized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!TryReadLanguage(code, localized))
            return false;

        foreach (var entry in localized)
            FlatStrings[entry.Key] = entry.Value;

        return true;
    }

    private static bool TryReadLanguage(string code, Dictionary<string, string> target)
    {
        try
        {
            using Stream stm = LauncherResources.OpenRead("locales/" + code + ".json");
            using var reader = new StreamReader(stm);
            JsonNode? root = JsonNode.Parse(reader.ReadToEnd());
            if (root is not JsonObject obj)
                return false;
            foreach (var kv in Flatten(obj))
                target[kv.Key] = kv.Value!.ToString()!;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<KeyValuePair<string, JsonNode?>> Flatten(JsonObject node, string prefix = "")
    {
        foreach (var prop in node)
        {
            if (prop.Key.Equals("_meta", StringComparison.OrdinalIgnoreCase))
                continue;
            string full = prefix.Length == 0 ? prop.Key : prefix + Section + prop.Key;
            if (prop.Value is JsonObject child)
            {
                foreach (var kv in Flatten(child, full))
                    yield return kv;
            }
            else
            {
                yield return new KeyValuePair<string, JsonNode?>(full, prop.Value);
            }
        }
    }
}
