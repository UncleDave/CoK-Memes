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
        Assert.True(result.Sum(SourceSize) <= DiscordGazetteGateway.MaximumInputCharacters);
        Assert.Contains(result, source => source.Url.EndsWith("/100"));
    }

    [Fact]
    public void TinyMessagesAreStillBoundedBySourceCount()
    {
        var sources = Enumerable.Range(1, 1000).Select(index => Source(index, Now.AddSeconds(-index), "X"));
        var result = DiscordGazetteGateway.SelectSources(sources, Now.AddDays(-7), Now);
        Assert.InRange(result.Count, 1, DiscordGazetteGateway.MaximumSources);
    }

    [Fact]
    public void BusyRecentChatCannotCrowdOutOlderDaysOrQuieterChannels()
    {
        var milestone = Source(10000, Now.AddDays(-6), "We finally defeated the boss after four evenings of attempts.") with
        {
            Url = "https://discord.com/channels/1/4/10000",
        };
        var sources = Enumerable
            .Range(0, 7)
            .SelectMany(day =>
                Enumerable.Range(1, 100).Select(index => Source(day * 100 + index, Now.AddDays(-day).AddMinutes(-index), "Recent ordinary chatter."))
            );
        var result = DiscordGazetteGateway.SelectSources(sources.Append(milestone), Now.AddDays(-7), Now);
        Assert.Contains(milestone, result);
        Assert.Equal(7, result.Select(source => (int)(source.TimestampUtc - Now.AddDays(-7).UtcDateTime).TotalDays).Distinct().Count());
        Assert.InRange(result.Count, 1, DiscordGazetteGateway.MaximumSources);
        Assert.True(result.Sum(SourceSize) <= DiscordGazetteGateway.MaximumInputCharacters);
    }

    [Fact]
    public void LongMessagesStillLeaveRoomForEverySampledDay()
    {
        var sources = Enumerable
            .Range(0, 7)
            .SelectMany(day =>
                Enumerable.Range(1, 20).Select(index => Source(day * 100 + index, Now.AddDays(-day).AddMinutes(-index), new string('x', 4000)))
            );
        var result = DiscordGazetteGateway.SelectSources(sources, Now.AddDays(-7), Now);
        Assert.Equal(7, result.Select(source => source.TimestampUtc.Date).Distinct().Count());
        Assert.All(result, source => Assert.Equal(4000, source.Content.Length));
        Assert.True(result.Sum(SourceSize) <= DiscordGazetteGateway.MaximumInputCharacters);
    }

    [Fact]
    public void BlocksThatDoNotFitDoNotPreventSmallerBlocksElsewhereFromBeingSelected()
    {
        var oversized = Source(1, Now.AddDays(-6), "X") with
        {
            MentionedUsers = Enumerable.Range(1, 500).Select(id => new NotebookMentionedUser((ulong)id, "Member")).ToArray(),
        };
        var small = Source(2, Now.AddMinutes(-1), "Small, complete evidence.");
        var result = DiscordGazetteGateway.SelectSources([oversized, small], Now.AddDays(-7), Now);
        Assert.Equal(small, Assert.Single(result));
    }

    [Fact]
    public void SelectionBalancesChannelsWithinDaysAndKeepsNeighbouringMessagesTogether()
    {
        var sources = Enumerable
            .Range(0, 7)
            .SelectMany(day =>
                Enumerable
                    .Range(3, 2)
                    .SelectMany(channel =>
                        Enumerable
                            .Range(1, 100)
                            .Select(index =>
                                Source(day * 1000 + channel * 100 + index, Now.AddDays(-day).AddMinutes(-index), "X") with
                                {
                                    Url = $"https://discord.com/channels/1/{channel}/{day * 1000 + channel * 100 + index}",
                                }
                            )
                    )
            )
            .ToArray();
        var result = DiscordGazetteGateway.SelectSources(sources, Now.AddDays(-7), Now);
        var buckets = result.GroupBy(source => (Day: source.TimestampUtc.Date, Channel: source.Url.Split('/')[5]));
        Assert.Equal(14, buckets.Count());
        Assert.All(
            buckets,
            bucket =>
            {
                Assert.True(bucket.Count() >= 4);
                var timestamps = bucket.Take(4).Select(source => source.TimestampUtc).ToArray();
                Assert.Equal(TimeSpan.FromMinutes(3), timestamps[^1] - timestamps[0]);
            }
        );
        Assert.Equal(result, DiscordGazetteGateway.SelectSources(sources.Reverse(), Now.AddDays(-7), Now));
    }

    [Fact]
    public async Task ChannelSamplingReadsEveryDayWithBoundedWindowsAndForwardsCancellation()
    {
        var windows = new List<(DateTimeOffset Since, DateTimeOffset Until)>();
        var result = await DiscordGazetteGateway.ReadChannelSampleAsync(
            Now.AddDays(-7),
            Now,
            (since, until, token) =>
            {
                Assert.Equal(TestContext.Current.CancellationToken, token);
                windows.Add((since, until));
                var source = Source(windows.Count, since.AddHours(1), "Daily evidence");
                return Task.FromResult<NotebookObservationBatch?>(new(3, (ulong)windows.Count, [new((ulong)windows.Count, source)]));
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(7, windows.Count);
        Assert.Equal(Now.AddDays(-7), windows[0].Since);
        Assert.Equal(Now, windows[^1].Until);
        Assert.All(windows, window => Assert.Equal(TimeSpan.FromDays(1), window.Until - window.Since));
        Assert.Equal(7, result!.Count);
    }

    [Fact]
    public async Task FailedWindowDiscardsTheChannelsEarlierSamplesAndStopsReading()
    {
        var calls = 0;
        var result = await DiscordGazetteGateway.ReadChannelSampleAsync(
            Now.AddDays(-7),
            Now,
            (_, _, _) =>
                Task.FromResult<NotebookObservationBatch?>(
                    ++calls == 1 ? new(3, 1, [new(1, Source(1, Now.AddDays(-6), "No longer accessible"))]) : null
                ),
            TestContext.Current.CancellationToken
        );
        Assert.Null(result);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void SamplingWindowsRemainBoundedForLongerOrInvalidRanges()
    {
        Assert.Equal(7, DiscordGazetteGateway.GetSamplingWindows(Now.AddDays(-30), Now).Count);
        Assert.Single(DiscordGazetteGateway.GetSamplingWindows(Now.AddHours(-1), Now));
        Assert.Empty(DiscordGazetteGateway.GetSamplingWindows(Now, Now));
        Assert.Empty(DiscordGazetteGateway.GetSamplingWindows(Now.AddHours(1), Now));
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
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.ReadPreviousEditionsAsync(8, Now.AddDays(-7), Now, TestContext.Current.CancellationToken)
        );
        Assert.False(await gateway.VerifyAsync(8, [Source(1, Now, "Message")], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.PublishAsync(8, "Edition", new([new("issue.png", [1])]), "012345abcdef", TestContext.Current.CancellationToken)
        );
    }

    private static NotebookSource Source(int id, DateTimeOffset at, string content) =>
        new($"https://discord.com/channels/1/3/{id}", 9, "Raider", at.UtcDateTime, content);

    private static int SourceSize(NotebookSource source) =>
        source.Content.Length + source.AuthorName.Length + source.Url.Length + source.MentionedUsers.Count * 100 + 200;
}
