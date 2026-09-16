namespace ChampionsOfKhazad.Bot.Tests;

public class DiscordMessageServiceTests
{
    [Fact]
    public void SanitizeContentOnlyResolvesAccessibleChannels()
    {
        var channels = new Dictionary<ulong, string> { [1] = "general" };

        var result = DiscordMessageService.SanitizeContent("See <#1> and <#2>", channels);

        Assert.Equal("See #general and [unavailable channel]", result);
    }

    [Fact]
    public void SanitizeContentRemovesActionableMentions()
    {
        var result = DiscordMessageService.SanitizeContent("<@1> <@!2> <@&3> @everyone @here", new Dictionary<ulong, string>());

        Assert.Equal("[user mention] [user mention] [role mention] @\u200beveryone @\u200bhere", result);
    }
}
