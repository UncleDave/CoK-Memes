using System.Reflection;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;

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

    [Fact]
    public void NotebookEvidencePreservesCorrectionsAfterTheNormalToolExcerptLimit()
    {
        var original = "Raid completed at 20:00. " + new string('x', 1000) + " Correction: the claim above is fictional.";
        Assert.DoesNotContain("Correction", DiscordMessageService.SanitizeContent(original, new Dictionary<ulong, string>()));
        Assert.Equal(original, DiscordMessageService.SanitizeNotebookSourceContent(original, new Dictionary<ulong, string>()));
    }

    [Theory]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void NotebookEvidenceIsCompleteOrRejected(int length, bool allowed)
    {
        var content = new string('x', length);
        var result = DiscordMessageService.SanitizeNotebookSourceContent(content, new Dictionary<ulong, string>());
        if (allowed)
            Assert.Equal(content, result);
        else
            Assert.Null(result);
        Assert.Equal(4000, NotebookSource.MaximumContentLength);
    }

    [Fact]
    public void NotebookEvidenceRejectsOversizeContentAfterMentionExpansionInsteadOfTruncating()
    {
        var content = new string('x', 3950) + "<#1>";
        Assert.Null(DiscordMessageService.SanitizeNotebookSourceContent(content, new Dictionary<ulong, string> { [1] = new string('n', 100) }));
    }

    [Fact]
    public void NotebookEvidenceSanitizesMentionsWithoutLosingRemainingText()
    {
        var content = "<#1> <#2> <@1> <@!2> <@&3> @everyone @here " + new string('x', 1100) + " Correction.";
        var result = DiscordMessageService.SanitizeNotebookSourceContent(content, new Dictionary<ulong, string> { [1] = "general" });
        Assert.StartsWith("#general [unavailable channel] [Discord user 1] [Discord user 2] [role mention] @\u200beveryone @\u200bhere", result);
        Assert.EndsWith("Correction.", result);
    }

    [Theory]
    [InlineData("https://discord.com/channels/1/3/42", true)]
    [InlineData("https://discord.com/channels/2/3/42", false)]
    [InlineData("https://discord.com/channels/1/3/42\n", false)]
    [InlineData("https://discord.com/channels/1/3/42?other=1", false)]
    [InlineData("https://discord.com/channels/1/3/42/extra", false)]
    [InlineData("https://discord.com.evil.example/channels/1/3/42", false)]
    [InlineData("https://discord.com/channels/1/18446744073709551616/42", false)]
    [InlineData("https://discord.com/channels/1/0/42", false)]
    [InlineData("https://discord.com/channels/1/3/0", false)]
    public void NotebookSourceReferencesRequireAnExactUrlInTheConfiguredGuild(string url, bool allowed)
    {
        Assert.Equal(allowed, DiscordMessageService.TryParseNotebookSourceUrl(url, 1, out _, out _));
    }

    [Fact]
    public void NotebookMetadataFilterAllowsOnlyConfiguredGuildAndReadableChannels()
    {
        const string readable = "https://discord.com/channels/1/3/42";
        var urls = new[] { readable, "https://discord.com/channels/1/4/43", "https://discord.com/channels/2/3/44", "not a message URL" };
        var result = DiscordMessageService.FilterNotebookSourceUrls(urls, 1, new HashSet<ulong> { 3 });
        Assert.Equal(readable, Assert.Single(result));
        Assert.Empty(DiscordMessageService.FilterNotebookSourceUrls(urls, 1, new HashSet<ulong>()));
    }

    [Theory]
    [InlineData(3UL, 42UL, false, MessageSource.User, "Human observation", true)]
    [InlineData(4UL, 42UL, false, MessageSource.User, "Wrong channel", false)]
    [InlineData(3UL, 43UL, false, MessageSource.User, "Wrong message", false)]
    [InlineData(3UL, 42UL, true, MessageSource.User, "Bot response", false)]
    [InlineData(3UL, 42UL, false, MessageSource.Bot, "Bot response", false)]
    [InlineData(3UL, 42UL, false, MessageSource.Webhook, "Webhook", false)]
    [InlineData(3UL, 42UL, false, MessageSource.System, "System event", false)]
    [InlineData(3UL, 42UL, false, MessageSource.User, " ", false)]
    public void SourceEvidenceMustBindToRequestedHumanMessage(
        ulong channelId,
        ulong messageId,
        bool bot,
        MessageSource source,
        string content,
        bool allowed
    )
    {
        var author = Stub<IUser>(method => method.Name == "get_IsBot" ? bot : throw new NotSupportedException(method.Name));
        var channel = Stub<IMessageChannel>(method => method.Name == "get_Id" ? channelId : throw new NotSupportedException(method.Name));
        var message = Stub<IMessage>(method =>
            method.Name switch
            {
                "get_Id" => messageId,
                "get_Channel" => channel,
                "get_Author" => author,
                "get_Source" => source,
                "get_Content" => content,
                _ => throw new NotSupportedException(method.Name),
            }
        );
        Assert.Equal(allowed, DiscordMessageService.IsNotebookEvidenceMessage(message, 3, 42));
        Assert.False(DiscordMessageService.IsNotebookEvidenceMessage(null, 3, 42));
    }

    private static T Stub<T>(Func<MethodInfo, object?> invoke)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, DiscordTestProxy>();
        ((DiscordTestProxy)(object)proxy).InvokeMethod = (method, _) => invoke(method);
        return proxy;
    }
}
