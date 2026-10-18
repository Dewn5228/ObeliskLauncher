using System;
using System.IO;
using System.Linq;
using ObeliskLauncher.Platform;
using Xunit;

namespace ObeliskLauncher.Tests;

public sealed class LinuxLaunchToolResolverTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("obelisk-launch-tools").FullName;
    readonly string? _savedHome = Environment.GetEnvironmentVariable("HOME");
    readonly string? _savedXdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
    readonly string? _savedXdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");

    public void Dispose()
    {
        RestoreEnv("HOME", _savedHome);
        RestoreEnv("XDG_DATA_HOME", _savedXdgData);
        RestoreEnv("XDG_CONFIG_HOME", _savedXdgConfig);
        try { Directory.Delete(_root, true); }
        catch { }
    }

    static void RestoreEnv(string name, string? value)
    {
        if (value is null)
            Environment.SetEnvironmentVariable(name, null);
        else
            Environment.SetEnvironmentVariable(name, value);
    }

    string MakeFile(params string[] segments)
    {
        string path = _root;
        foreach (string segment in segments)
            path = Path.Combine(path, segment);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    void PointHomeAtRoot()
    {
        Environment.SetEnvironmentVariable("HOME", _root);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", null);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
    }

    [Fact]
    public void DiscoversProtonToolFromLibraryCompatDir()
    {
        string gamePath = Path.Combine(_root, "steamapps", "common", "FakeGame");
        Directory.CreateDirectory(gamePath);
        MakeFile("compatibilitytools.d", "GE-Proton-Fake", "proton");

        var options = LinuxLaunchToolResolver.GetAvailableOptions(gamePath: gamePath);

        Assert.Contains(options, option =>
            option.Kind == LinuxLaunchToolKind.Proton && option.DisplayName == "GE-Proton-Fake");
    }

    [Fact]
    public void CommunityRankingPutsGeAboveCachyAboveExperimentalAboveStock()
    {
        var stock = new LinuxLaunchToolOption("proton:/tools/steamapps/common/Proton 9.0/proton", "Proton 9.0", LinuxLaunchToolKind.Proton, "/tools/steamapps/common/Proton 9.0/proton");
        var caxy = new LinuxLaunchToolOption("proton:/tools/compatibilitytools.d/proton-cachyos/proton", "proton-cachyos", LinuxLaunchToolKind.Proton, "/tools/compatibilitytools.d/proton-cachyos/proton");
        var experimental = new LinuxLaunchToolOption("proton:/tools/steamapps/common/Proton - Experimental/proton", "Proton - Experimental", LinuxLaunchToolKind.Proton, "/tools/steamapps/common/Proton - Experimental/proton");
        var ge = new LinuxLaunchToolOption("proton:/tools/compatibilitytools.d/GE-Proton-Fake/proton", "GE-Proton-Fake", LinuxLaunchToolKind.Proton, "/tools/compatibilitytools.d/GE-Proton-Fake/proton");

        LinuxLaunchToolOption? best = LinuxLaunchToolResolver.GetBestPick([stock, caxy, experimental, ge]);

        Assert.Equal(ge, best);
        Assert.True(LinuxLaunchToolResolver.GetToolScore(ge, null) > LinuxLaunchToolResolver.GetToolScore(caxy, null));
        Assert.True(LinuxLaunchToolResolver.GetToolScore(caxy, null) > LinuxLaunchToolResolver.GetToolScore(experimental, null));
        Assert.True(LinuxLaunchToolResolver.GetToolScore(experimental, null) > LinuxLaunchToolResolver.GetToolScore(stock, null));
    }

    [Fact]
    public void WineRankingPutsBottlesRunnersAboveWineGeAbovePlainWine()
    {
        var systemWine = new LinuxLaunchToolOption("wine:/usr/bin/wine", "wine (PATH)", LinuxLaunchToolKind.Wine, "/usr/bin/wine");
        var plainManaged = new LinuxLaunchToolOption("wine:/home/user/.local/share/lutris/runners/wine/lutris-7.2/bin/wine", "lutris-7.2 (Lutris) (Wine)", LinuxLaunchToolKind.Wine, "/home/user/.local/share/lutris/runners/wine/lutris-7.2/bin/wine");
        var wineGe = new LinuxLaunchToolOption("wine:/home/user/.config/heroic/tools/wine/Wine-GE-latest/bin/wine64", "Wine-GE-latest (Heroic) (Wine)", LinuxLaunchToolKind.Wine, "/home/user/.config/heroic/tools/wine/Wine-GE-latest/bin/wine64");
        var soda = new LinuxLaunchToolOption("wine:/home/user/.local/share/bottles/runners/soda-9.0/bin/wine64", "soda-9.0 (Bottles) (Wine)", LinuxLaunchToolKind.Wine, "/home/user/.local/share/bottles/runners/soda-9.0/bin/wine64");
        var kron4ek = new LinuxLaunchToolOption("wine:/home/user/.local/share/bottles/runners/kron4ek-wine-9.0/bin/wine64", "kron4ek-wine-9.0 (Bottles) (Wine)", LinuxLaunchToolKind.Wine, "/home/user/.local/share/bottles/runners/kron4ek-wine-9.0/bin/wine64");

        Assert.Equal(soda, LinuxLaunchToolResolver.GetBestPick([systemWine, plainManaged, wineGe, soda]));
        Assert.True(LinuxLaunchToolResolver.GetToolScore(soda, null) >= LinuxLaunchToolResolver.GetToolScore(kron4ek, null));
        Assert.True(LinuxLaunchToolResolver.GetToolScore(kron4ek, null) > LinuxLaunchToolResolver.GetToolScore(wineGe, null));
        Assert.True(LinuxLaunchToolResolver.GetToolScore(wineGe, null) > LinuxLaunchToolResolver.GetToolScore(plainManaged, null));
        Assert.True(LinuxLaunchToolResolver.GetToolScore(plainManaged, null) > LinuxLaunchToolResolver.GetToolScore(systemWine, null));
    }

    [Fact]
    public void BestPickSkipsAutomaticPlaceholderAndFallsBackToWine()
    {
        var automatic = new LinuxLaunchToolOption("auto", "Automatic", LinuxLaunchToolKind.Proton, string.Empty);
        var wine = new LinuxLaunchToolOption("wine:/tools/wine/bin/wine64", "wine64 (PATH)", LinuxLaunchToolKind.Wine, "/tools/wine/bin/wine64");

        LinuxLaunchToolOption? best = LinuxLaunchToolResolver.GetBestPick([automatic, wine]);

        Assert.Equal(wine, best);
    }

    [Fact]
    public void DescribePickReasonDistinguishesGeAndWine()
    {
        var ge = new LinuxLaunchToolOption("proton:/tools/compatibilitytools.d/GE-Proton-Fake/proton", "GE-Proton-Fake", LinuxLaunchToolKind.Proton, "/tools/compatibilitytools.d/GE-Proton-Fake/proton");
        var wine = new LinuxLaunchToolOption("wine:/tools/wine/bin/wine64", "wine64 (PATH)", LinuxLaunchToolKind.Wine, "/tools/wine/bin/wine64");

        Assert.NotEqual(
            LinuxLaunchToolResolver.DescribePickReason(ge),
            LinuxLaunchToolResolver.DescribePickReason(wine));
    }

    [Fact]
    public void DropdownListsHigherScoredProtonFirst()
    {
        string gamePath = Path.Combine(_root, "steamapps", "common", "FakeGame");
        Directory.CreateDirectory(gamePath);
        MakeFile("compatibilitytools.d", "AAA-Plain", "proton");
        MakeFile("compatibilitytools.d", "ZZZ-Experimental-Fake", "proton");

        var options = LinuxLaunchToolResolver.GetAvailableOptions(gamePath: gamePath).ToList();
        int experimental = options.FindIndex(option => option.DisplayName == "ZZZ-Experimental-Fake");
        int plain = options.FindIndex(option => option.DisplayName == "AAA-Plain");

        Assert.True(experimental >= 0 && plain >= 0);
        Assert.True(experimental < plain);
    }

    [Fact]
    public void DiscoversThirdPartyRunnersFromHome()
    {
        PointHomeAtRoot();
        MakeFile(".local", "share", "bottles", "runners", "soda-9.0", "bin", "wine64");
        MakeFile(".var", "app", "com.usebottles.bottles", "data", "bottles", "runners", "kron4ek-wine", "bin", "wine64");
        MakeFile(".local", "share", "lutris", "runners", "proton", "GE-Proton-Fake", "proton");
        MakeFile(".config", "heroic", "tools", "wine", "Wine-GE-Fake", "bin", "wine64");
        MakeFile(".config", "heroic", "tools", "proton", "Proton-GE-Fake", "proton");

        var options = LinuxLaunchToolResolver.GetAvailableOptions();

        Assert.Contains(options, option =>
            option.Kind == LinuxLaunchToolKind.Wine && option.DisplayName == "soda-9.0 (Bottles) (Wine)");
        Assert.Contains(options, option =>
            option.Kind == LinuxLaunchToolKind.Wine && option.DisplayName == "kron4ek-wine (Bottles) (Wine)");
        Assert.Contains(options, option =>
            option.Kind == LinuxLaunchToolKind.Proton && option.DisplayName == "GE-Proton-Fake (Lutris)");
        Assert.Contains(options, option =>
            option.Kind == LinuxLaunchToolKind.Wine && option.DisplayName == "Wine-GE-Fake (Heroic) (Wine)");
        Assert.Contains(options, option =>
            option.Kind == LinuxLaunchToolKind.Proton && option.DisplayName == "Proton-GE-Fake (Heroic)");
    }
}
