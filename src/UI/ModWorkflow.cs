namespace TEKLauncher.UI;

public readonly record struct ModActionResult(string Message, int Severity);

static class ModWorkflow
{
    public static string Describe(Mod mod)
    {
        if (mod.Details.Status == 1 && !string.IsNullOrWhiteSpace(mod.Details.Name))
            return mod.Details.Name;

        if (!string.IsNullOrWhiteSpace(mod.Name))
            return mod.Name;

        return $"Mod {mod.Id}";
    }

    public static string GetDetailText(Mod mod)
    {
        string baseText = mod.Id.ToString();
        if (!string.IsNullOrWhiteSpace(mod.Name) && !string.Equals(mod.Name, Describe(mod), StringComparison.Ordinal))
            return $"{baseText} | {mod.Name}";

        return baseText;
    }

    public static string GetStatusColor(Mod.Status status) => status switch
    {
        Mod.Status.Installed => "#0AA63E",
        _ => "#D49B38"
    };

    public static string GetStatusText(Mod.Status status) => Locale.Get(status switch
    {
        Mod.Status.Installed => "common.installed",
        Mod.Status.UpdateAvailable => "common.updateAvailable",
        Mod.Status.Updating => "common.updating",
        Mod.Status.Deleting => "common.deleting",
        _ => "common.na"
    });

    public static async Task<ModActionResult> DeleteAsync(Mod mod)
    {
        if (Game.IsRunning)
            return new(Locale.Get("errors.modDeleteFail"), 2);

        string description = Describe(mod);
        await Task.Run(mod.Delete);
        return new($"{description} removed from installed mods.", 1);
    }
}