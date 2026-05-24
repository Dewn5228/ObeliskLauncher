using System;
using System.IO;
using System.Linq;
using Xunit;
using TEKLauncher.Utils;

namespace TEKLauncher.Tests;

public class VdfParserTests
{
    [Fact]
    public void TestArkansasFantasticTamesDlcFixture()
    {
        string filePath = Path.Combine("Fixtures", "4558510.vdf");
        string text = File.ReadAllText(filePath);
        var root = VdfParser.Parse(text);

        Assert.NotNull(root);
        Assert.True(root.Children.TryGetValue("appinfo", out var appInfo));
        Assert.NotNull(appInfo);
        Assert.False(appInfo.Children.TryGetValue("depots", out _));
    }

    [Fact]
    public void TestTheCenterAscendedDlcFixture()
    {
        string filePath = Path.Combine("Fixtures", "2827030.vdf");
        string text = File.ReadAllText(filePath);
        var root = VdfParser.Parse(text);

        Assert.NotNull(root);
        Assert.True(root.Children.TryGetValue("appinfo", out var appInfo));
        Assert.NotNull(appInfo);
        Assert.True(appInfo.Children.TryGetValue("depots", out var depotsNode));
        Assert.NotNull(depotsNode);

        var numericKeys = depotsNode.Children.Keys
          .Where(k => uint.TryParse(k, out _))
          .Select(uint.Parse)
          .ToList();

        Assert.Single(numericKeys);
        Assert.Equal(2827030u, numericKeys[0]);
    }

    [Fact]
    public void TestEmptyVdf()
    {
        var root = VdfParser.Parse("");
        Assert.NotNull(root);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void TestCommentsInVdf()
    {
        string vdfText = @"
      // This is a comment at the start of the file
      ""appinfo""
      {
        // Another comment
        ""appid"" ""12345""
      }
    ";
        var root = VdfParser.Parse(vdfText);

        Assert.NotNull(root);
        Assert.True(root.Children.TryGetValue("appinfo", out var appInfo));
        Assert.NotNull(appInfo);
        Assert.True(appInfo.Children.TryGetValue("appid", out var appIdNode));
        Assert.NotNull(appIdNode);
        Assert.Equal("12345", appIdNode.Value);
    }

    [Fact]
    public void TestUnquotedTokens()
    {
        string vdfText = @"
      appinfo
      {
        appid 54321
      }
    ";
        var root = VdfParser.Parse(vdfText);

        Assert.NotNull(root);
        Assert.True(root.Children.TryGetValue("appinfo", out var appInfo));
        Assert.NotNull(appInfo);
        Assert.True(appInfo.Children.TryGetValue("appid", out var appIdNode));
        Assert.NotNull(appIdNode);
        Assert.Equal("54321", appIdNode.Value);
    }
}
