using ObeliskLauncher;
using Xunit;

namespace ObeliskLauncher.Tests;

public sealed class PlayCommandTests
{
    [Fact]
    public void DetectsPlayFlagCaseInsensitively()
    {
        Assert.True(PlayCommand.ContainsPlayFlag(["--play"]));
        Assert.True(PlayCommand.ContainsPlayFlag(["--PLAY", "--game", "ASA"]));
        Assert.False(PlayCommand.ContainsPlayFlag(["--refresh-catalog"]));
        Assert.False(PlayCommand.ContainsPlayFlag([]));
    }

    [Fact]
    public void ReadsGameOptionValue()
    {
        Assert.Equal("ASA", PlayCommand.GetGameOption(["--play", "--game", "ASA"]));
        Assert.Equal("ASE", PlayCommand.GetGameOption(["--game", "ASE", "--play"]));
    }

    [Fact]
    public void ReturnsNullWhenGameOptionHasNoValue()
    {
        Assert.Null(PlayCommand.GetGameOption(["--play"]));
        Assert.Null(PlayCommand.GetGameOption(["--play", "--game"]));
    }
}
