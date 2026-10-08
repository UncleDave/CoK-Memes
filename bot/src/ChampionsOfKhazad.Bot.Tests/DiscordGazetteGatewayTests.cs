using System.Text.Json;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Tests;

public class DiscordGazetteGatewayTests
{
    [Fact]
    public void PublishingAPageRequiresBotAttachmentPermission()
    {
        var permissions = new ChannelPermissions(viewChannel: true, sendMessages: true, embedLinks: true);
        Assert.Contains("Attach Files", DiscordGazetteGateway.GetPublicationPermissionError(permissions, "ai-tavern"));
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, true, true, "View Channel")]
    [InlineData(true, false, true, "Send Messages")]
    [InlineData(true, true, false, "Embed Links")]
    public void PublicationErrorsIdentifyTheBotsMissingPostingPermission(bool view, bool send, bool embed, string missing)
    {
        var permissions = new ChannelPermissions(viewChannel: view, sendMessages: send, embedLinks: embed);
        Assert.Contains(missing, DiscordGazetteGateway.GetPublicationPermissionError(permissions, "ai-tavern"));
    }

    [Fact]
    public void PublicationDoesNotRequireReadHistoryPermissionOrAnAdminGuildMember()
    {
        var permissions = new ChannelPermissions(viewChannel: true, sendMessages: true, embedLinks: true, attachFiles: true);
        Assert.False(permissions.ReadMessageHistory);
        Assert.Null(DiscordGazetteGateway.GetPublicationPermissionError(permissions, "ai-tavern"));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void EnvironmentConfigurationPinsGazetteToTheExistingBotConversationChannel(string environment)
    {
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, $"appsettings.{environment}.json")));
        var root = config.RootElement;
        var destination = root.GetProperty("Gazette").GetProperty("DestinationChannelId").GetUInt64();
        var botConversationChannel = root.GetProperty("Followers").GetProperty("IgnoreBotMentionsInChannelId").GetUInt64();
        var mentionChannels = root.GetProperty("EventHandlers")
            .GetProperty("Mention")
            .GetProperty("ChannelIds")
            .EnumerateArray()
            .Select(id => id.GetUInt64());
        Assert.NotEqual(0UL, destination);
        Assert.Equal(botConversationChannel, destination);
        Assert.Contains(destination, mentionChannels);
    }

    [Theory]
    [InlineData("content")]
    [InlineData("view")]
    [InlineData("author")]
    [InlineData("timestamp")]
    [InlineData("url")]
    [InlineData("deleted")]
    public void VerificationRejectsChangedOrMissingEvidence(string change)
    {
        var source = Source(1, Now, "Message") with { ContentHash = "raw", ViewHash = "view" };
        var changed = change switch
        {
            "content" => source with { ContentHash = "edited" },
            "view" => source with { ViewHash = "metadata-edited" },
            "author" => source with { AuthorId = 2 },
            "timestamp" => source with { TimestampUtc = Now.AddMinutes(1).UtcDateTime },
            "url" => source with { Url = "https://discord.com/channels/1/3/99" },
            _ => source,
        };
        Assert.True(DiscordGazetteGateway.SourcesMatch([source], [source]));
        Assert.False(DiscordGazetteGateway.SourcesMatch([source], change == "deleted" ? null : [changed]));
    }

    [Fact]
    public void SelectionIncludesOnlyTheRequestedWindowDeduplicatesAndKeepsChronology()
    {
        var recent = Source(1, Now.AddDays(-1), "Recent");
        var oldest = Source(2, Now.AddDays(-7), "Oldest permitted");
        var result = DiscordGazetteGateway.SelectSources(
            [recent, Source(3, Now.AddDays(-8), "Old"), recent, Source(4, Now.AddMinutes(1), "Future"), oldest],
            Now.AddDays(-7),
            Now
        );
        Assert.Equal(new[] { oldest, recent }, result);
    }

    [Fact]
    public void SelectionIsBoundedAndKeepsCompleteMessagesRatherThanTruncatingCorrections()
    {
        var sources = Enumerable
            .Range(1, 100)
            .Select(index => Source(index, Now.AddMinutes(-index), new string('x', 3500) + " Correction: this was fictional."));
        var result = DiscordGazetteGateway.SelectSources(sources, Now.AddDays(-7), Now);
        Assert.InRange(result.Count, 1, 12);
        Assert.All(result, source => Assert.EndsWith("Correction: this was fictional.", source.Content));
        Assert.Contains(result, source => source.Url.EndsWith("/1"));
        Assert.DoesNotContain(result, source => source.Url.EndsWith("/100"));
    }

    [Fact]
    public void TinyMessagesAreStillBoundedBySourceCount()
    {
        var sources = Enumerable.Range(1, 1000).Select(index => Source(index, Now.AddSeconds(-index), "X"));
        var result = DiscordGazetteGateway.SelectSources(sources, Now.AddDays(-7), Now);
        Assert.InRange(result.Count, 1, DiscordGazetteGateway.MaximumSources);
    }

    [Fact]
    public async Task UnreadyContextFailsClosedWithoutReadingOrPublishing()
    {
        var gateway = new DiscordGazetteGateway(
            new(),
            new(null!),
            Options.Create(new GazetteOptions()),
            Options.Create(new DiscordMessageToolsOptions()),
            null!,
            TimeProvider.System
        );
        Assert.Null(gateway.GetDestination());
        Assert.Contains("guild connection is not ready", gateway.DestinationError);
        var batch = await gateway.ReadRecentAsync(8, Now.AddDays(-7), Now, TestContext.Current.CancellationToken);
        Assert.Empty(batch.Sources);
        Assert.False(await gateway.VerifyAsync(8, [Source(1, Now, "Message")], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.PublishAsync(8, "Edition", new("issue.png", [1]), "012345abcdef", TestContext.Current.CancellationToken)
        );
    }

    private static NotebookSource Source(int id, DateTimeOffset at, string content) =>
        new($"https://discord.com/channels/1/3/{id}", 9, "Raider", at.UtcDateTime, content);
}
