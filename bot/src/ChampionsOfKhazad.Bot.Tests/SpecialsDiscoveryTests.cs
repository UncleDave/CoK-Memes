using System.Text.Json;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;

namespace ChampionsOfKhazad.Bot.Tests;

public class SpecialsDiscoveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HistoryPaginatesPastRecentChatterAndVisitsEveryChannelNotJustTheFirstTwelve()
    {
        var calls = new List<(ulong Channel, ulong? Before)>();
        var rename = Source(10, 1, Now.AddDays(-6), "The guild has been renamed.");
        var banner = Source(20, 20, Now.AddDays(-5), "The banner competition is open.");
        var result = await DiscordSpecialsGateway.ReadHistoryAsync(
            Enumerable.Range(1, 20).Select(id => (ulong)id).ToArray(),
            (id, before, token) =>
            {
                Assert.Equal(TestContext.Current.CancellationToken, token);
                calls.Add((id, before));
                return Task.FromResult<DiscordHistoryPage?>(
                    before is null
                        ? new([], 100, false, 100, 100)
                        : new(
                            id == 1 ? [rename]
                                : id == 20 ? [banner]
                                : [],
                            1,
                            true,
                            20,
                            0
                        )
                );
            },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(40, result.HistoryReads);
        Assert.Equal(20, result.CompleteChannels);
        Assert.Equal(0, result.FailedChannels + result.LimitedChannels);
        Assert.Equal(Enumerable.Range(1, 20).Select(id => (ulong)id), calls.Take(20).Select(call => call.Channel));
        Assert.All(calls.Skip(20), call => Assert.Equal(100UL, call.Before));
        Assert.Equal(new[] { rename, banner }, result.Sources);
    }

    [Fact]
    public async Task HistoryLimitsAreExplicitAndEmptyHumanPagesStillAdvanceTheirRawCursor()
    {
        var result = await DiscordSpecialsGateway.ReadHistoryAsync(
            [1, 2, 3, 4, 5, 6],
            (id, before, _) => Task.FromResult<DiscordHistoryPage?>(new([], (before ?? 1000) - 1, false, 100, 100)),
            TestContext.Current.CancellationToken
        );
        Assert.Equal(DiscordSpecialsGateway.MaximumHistoryReads, result.HistoryReads);
        Assert.Equal(6, result.LimitedChannels);
        Assert.Equal(0, result.CompleteChannels + result.FailedChannels);
        Assert.Empty(result.Sources);
    }

    [Fact]
    public async Task FailedOrNonAdvancingChannelsLoseTheirPartialEvidence()
    {
        var source = Source(100, 1, Now.AddDays(-1), "Incomplete channel evidence");
        var result = await DiscordSpecialsGateway.ReadHistoryAsync(
            [1, 2, 3],
            (id, before, _) =>
                Task.FromResult<DiscordHistoryPage?>(
                    before is null ? new([source with { Url = $"https://discord.com/channels/1/{id}/100" }], 100, false, 100, 0)
                    : id == 1 ? null
                    : id == 2 ? new([], 100, false, 100, 0)
                    : new([], 1, true, 1, 0)
                ),
            TestContext.Current.CancellationToken
        );
        Assert.Equal(2, result.FailedChannels);
        Assert.Equal(1, result.CompleteChannels);
        Assert.Equal("https://discord.com/channels/1/3/100", Assert.Single(result.Sources).Url);
    }

    [Fact]
    public async Task PerChannelReadLimitRetainsPartialEvidenceAndReportsTheGap()
    {
        var result = await DiscordSpecialsGateway.ReadHistoryAsync(
            [1],
            (_, before, _) =>
                Task.FromResult<DiscordHistoryPage?>(
                    new([Source((int)(before ?? 1000), 1, Now.AddMinutes(-1), "Chat")], (before ?? 1000) - 1, false, 100, 0)
                ),
            TestContext.Current.CancellationToken
        );
        Assert.Equal(DiscordSpecialsGateway.MaximumPagesPerChannel, result.HistoryReads);
        Assert.Equal(1, result.LimitedChannels);
        Assert.Equal(0, result.CompleteChannels + result.FailedChannels);
        Assert.Equal(20, result.Sources.Count);
    }

    [Fact]
    public void DiscoveryAssessesAllMessagesRatherThanTakingTheGazettesSmallSourceSample()
    {
        var sources = Enumerable.Range(1, 1200).Select(index => Source(index, 1, Now.AddDays(-2).AddSeconds(index), "Ordinary chatter.")).ToArray();
        sources[0] = sources[0] with { Content = "The guild has been renamed." };
        sources[950] = sources[950] with { Content = "The banner competition opens today." };
        var plan = SpecialsDiscoveryPlan.Create(sources, Now.AddDays(-7), Now);
        Assert.Equal(0, plan.UnassessedMessages);
        Assert.Equal(1200, plan.Batches.SelectMany(batch => batch).DistinctBy(source => source.Url).Count());
        Assert.Contains(sources[0], plan.Batches.SelectMany(batch => batch));
        Assert.Contains(sources[950], plan.Batches.SelectMany(batch => batch));
        Assert.All(
            plan.Batches,
            batch =>
            {
                Assert.InRange(batch.Count, 1, 100);
                Assert.True(JsonSerializer.Serialize(new { sources = batch }).Length <= SpecialsDiscoveryPlan.MaximumBatchCharacters);
            }
        );
    }

    [Fact]
    public void LongUnicodeRecordsAreKeptWholeAndUnassessedMessagesAreCounted()
    {
        var sources = Enumerable.Range(1, 200).Select(index => Source(index, index % 2, Now.AddDays(-2), new string('界', 4000))).ToArray();
        var plan = SpecialsDiscoveryPlan.Create(sources, Now.AddDays(-7), Now);
        Assert.Equal(SpecialsDiscoveryPlan.MaximumBatches, plan.Batches.Count);
        var assessed = plan.Batches.SelectMany(batch => batch).DistinctBy(source => source.Url).ToArray();
        Assert.Equal(sources.Length - assessed.Length, plan.UnassessedMessages);
        Assert.True(plan.UnassessedMessages > 0);
        Assert.All(assessed, source => Assert.Equal(4000, source.Content.Length));
        Assert.Equal(2, assessed.Select(source => source.Url.Split('/')[5]).Distinct().Count());
    }

    [Fact]
    public void ContextIncludesNeighbouringCorrectionsAndCrossChannelAnchorsButNotUnrelatedDistantChat()
    {
        var announcement = Source(10, 1, Now.AddDays(-3), "Competition announced.");
        var clarification = Source(11, 1, Now.AddDays(-3).AddMinutes(1), "Entries close next week, no winner yet.");
        var discussion = Source(20, 2, Now.AddDays(-2), "Banner discussion.");
        var distant = Source(12, 1, Now.AddDays(-1), "Unrelated.");
        var context = Assert.Single(
            SpecialsDiscoveryPlan.AddContext(
                [new("Competition", [announcement.Url, discussion.Url])],
                [announcement, clarification, distant, discussion]
            )
        );
        Assert.Contains(announcement, context.Sources);
        Assert.Contains(clarification, context.Sources);
        Assert.Contains(discussion, context.Sources);
        Assert.DoesNotContain(distant, context.Sources);
    }

    [Fact]
    public void TextPublicationDoesNotRequireImagesOrEmbedPermissions()
    {
        Assert.Null(DiscordSpecialsGateway.GetTextPublicationError(new ChannelPermissions(viewChannel: true, sendMessages: true), "tavern"));
        Assert.Contains("Send Messages", DiscordSpecialsGateway.GetTextPublicationError(new ChannelPermissions(viewChannel: true), "tavern"));
    }

    internal static NotebookSource Source(int id, int channel, DateTimeOffset when, string content) =>
        new($"https://discord.com/channels/1/{channel}/{id}", 9, "Member", when.UtcDateTime, content);
}
