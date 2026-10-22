using ChampionsOfKhazad.Bot.Roster;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using static ChampionsOfKhazad.Bot.Tests.RosterFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class GuildRosterTests
{
    [Fact]
    public async Task FirstObservationIsSilentAndRanksAreSharedAcrossPagesWithFullNames()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Enumerable.Range(1, 12).Select(index => Character(index.ToString(), 20, $"Player{index:D2}", "Fullname")).ToArray());
        await fixture.Refresh();
        Assert.Empty(fixture.Notices.Items);
        var first = await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token);
        var second = await fixture.Queries.QueryAsync(50, page: 2, cancellationToken: fixture.Token);
        Assert.Equal(10, first.Rows.Length);
        Assert.Equal(2, second.Rows.Length);
        Assert.All(first.Rows.Concat(second.Rows), row => Assert.Equal(1, row.Rank));
        Assert.Equal("Player11 Fullname", second.Rows[0].Character.FullName);
        Assert.True(first.IsMock);
        Assert.Contains("Mock roster", string.Join('\n', RosterText.Leaderboard(first)));
        Assert.DoesNotContain("realm", System.Text.Json.JsonSerializer.Serialize(first), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SkippedThresholdsQueueOnlyHighestAndSamePollCannotRepeatThem()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(professions: Professions()));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(level: 41, professions: Professions(226)));
        await fixture.Refresh();
        await fixture.Refresh();
        Assert.Equal([40, 225], fixture.Notices.Items.Values.Select(item => item.Threshold).Order().ToArray());
        Assert.All(fixture.Notices.Items.Values, item => Assert.Null(item.SentAtUtc));
        fixture.Advance();
        fixture.SetMembers(Character(level: 29, professions: Professions(150)));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(level: 41, professions: Professions(226)));
        await fixture.Refresh();
        Assert.Equal(2, fixture.Notices.Items.Count);
    }

    [Fact]
    public async Task KnownZeroUnknownAndSecondaryProfessionsHaveDistinctMilestoneBehaviour()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(professions: Professions(0, id: "185", name: "Cooking")));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(professions: Professions(null, id: "185", name: "Cooking")));
        await fixture.Refresh();
        Assert.Empty(fixture.Notices.Items);
        fixture.Advance();
        fixture.SetMembers(Character(professions: Professions(75, id: "185", name: "Cooking")));
        await fixture.Refresh();
        Assert.Equal("Cooking", Assert.Single(fixture.Notices.Items.Values).ProfessionName);
    }

    [Fact]
    public async Task TrainedMaximumChangingDoesNotTriggerButConfiguredNonRoundLevelCapDoes()
    {
        using var fixture = new RosterFixture();
        fixture.Options.LevelCap = 25;
        fixture.SetMembers(Character(level: 24, professions: Professions(75)));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(
            Character(
                level: 25,
                professions: Professions(75) with
                {
                    Skills =
                    [
                        new()
                        {
                            Id = "171",
                            Name = "Alchemy",
                            Skill = 75,
                            MaxSkill = 150,
                        },
                    ],
                }
            )
        );
        await fixture.Refresh();
        var notice = Assert.Single(fixture.Notices.Items.Values);
        Assert.Equal(25, notice.Threshold);
        Assert.Null(notice.ProfessionName);
    }

    [Fact]
    public async Task RenamesKeepIdsNameReuseIsNewAndDepartureCancelsPendingThenRejoinsSilently()
    {
        using var fixture = new RosterFixture();
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(level: 20, last: "Newname"));
        await fixture.Refresh();
        Assert.Equal("Borin Newname", Assert.Single(fixture.Notices.Items.Values).CharacterName);
        fixture.Advance();
        fixture.SetMembers();
        await fixture.Refresh();
        Assert.Empty(fixture.Notices.Items);
        Assert.Equal("empty", (await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token)).Status);
        fixture.Advance();
        fixture.SetMembers(Character(level: 40));
        await fixture.Refresh();
        Assert.Empty(fixture.Notices.Items);
        fixture.Advance();
        fixture.SetMembers(Character("2", 1));
        await fixture.Refresh();
        Assert.Empty(fixture.Notices.Items);
        fixture.Advance();
        fixture.SetMembers(Character("2", 10));
        await fixture.Refresh();
        Assert.Equal("id:2", Assert.Single(fixture.Notices.Items.Values).CharacterKey);
    }

    [Fact]
    public async Task NameOnlyIdentityIsStableWhilePresentButStartsFreshAfterAKnownDeparture()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character() with { Id = null });
        await fixture.Refresh();
        var key = fixture.Store.States[fixture.Service.ScopeId].Characters[0].Key;
        fixture.Advance();
        fixture.SetMembers(Character(level: 20) with { Id = null });
        await fixture.Refresh();
        Assert.Equal(key, Assert.Single(fixture.Notices.Items.Values).CharacterKey);
        fixture.Advance();
        fixture.SetMembers();
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(level: 1) with { Id = null });
        await fixture.Refresh();
        Assert.NotEqual(key, fixture.Store.States[fixture.Service.ScopeId].Characters[0].Key);
        fixture.Advance();
        fixture.SetMembers(Character(level: 10) with { Id = null });
        await fixture.Refresh();
        Assert.Equal(10, Assert.Single(fixture.Notices.Items.Values).Threshold);
    }

    [Fact]
    public async Task ResponseForDifferentSourceGuildCannotOverwriteAcceptedRoster()
    {
        using var fixture = new RosterFixture();
        fixture.Source.Observation = fixture.Source.Observation with { GuildId = "one" };
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(level: 20));
        fixture.Source.Observation = fixture.Source.Observation with { GuildId = "two" };
        await Assert.ThrowsAsync<InvalidDataException>(fixture.Refresh);
        Assert.Equal("one", fixture.Store.States[fixture.Service.ScopeId].SourceGuildId);
        Assert.Equal(19, fixture.Store.States[fixture.Service.ScopeId].Characters[0].Level);
        Assert.Empty(fixture.Notices.Items);
    }

    [Fact]
    public async Task InvalidPartialStaleAndFailedRostersPreserveMembersAndDoNotAnnounce()
    {
        using var fixture = new RosterFixture();
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers();
        fixture.Source.Observation = fixture.Source.Observation with { Complete = false };
        await Assert.ThrowsAsync<InvalidDataException>(fixture.Refresh);
        Assert.Single((await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token)).Rows);
        fixture.SetMembers(Character(level: 30));
        fixture.Source.Observation = fixture.Source.Observation with { ObservedAtUtc = fixture.Clock.GetUtcNow().UtcDateTime.AddDays(-1) };
        await Assert.ThrowsAsync<InvalidDataException>(fixture.Refresh);
        fixture.Source.Failure = new IOException("Private source failure");
        await Assert.ThrowsAsync<IOException>(fixture.Refresh);
        var page = await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token);
        Assert.True(page.LastRefreshFailed);
        Assert.Equal(19, Assert.Single(page.Rows).Character.Level);
        Assert.Empty(fixture.Notices.Items);
        Assert.DoesNotContain("Private", string.Join('\n', RosterText.Leaderboard(page)));
    }

    [Fact]
    public async Task DuplicateIdsAndInvalidProfessionsAreRejectedWithoutPublishingPartialData()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(), Character());
        await Assert.ThrowsAsync<InvalidDataException>(fixture.Refresh);
        fixture.SetMembers(Character(professions: Professions(-1)));
        await Assert.ThrowsAsync<InvalidDataException>(fixture.Refresh);
        Assert.Null(fixture.Store.States[fixture.Service.ScopeId].ObservedAtUtc);
        Assert.Equal("refresh_failed", (await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token)).Status);
    }

    [Fact]
    public async Task SeparateProfessionRequestsAreLessFrequentAndCannotOverwriteLevelOrNewerSkills()
    {
        using var fixture = new RosterFixture();
        fixture.Source.HasSeparateProfessionRequests = true;
        fixture.Source.Professions["id:1"] = Professions() with { ObservedAtUtc = fixture.Clock.GetUtcNow().UtcDateTime };
        await fixture.Refresh();
        Assert.Equal(1, fixture.Source.ProfessionReads);
        fixture.Advance();
        fixture.SetMembers(Character(level: 20));
        await fixture.Refresh();
        Assert.Equal(1, fixture.Source.ProfessionReads);
        var page = await fixture.Queries.QueryAsync(50, profession: "Alchemy", cancellationToken: fixture.Token);
        Assert.Equal(74, Assert.Single(page.Rows).Profession!.Skill);
        Assert.Equal(20, Assert.Single(page.Rows).Character.Level);
        fixture.Advance(600);
        fixture.Source.Professions["id:1"] = Professions(150) with { ObservedAtUtc = fixture.Clock.GetUtcNow().UtcDateTime.AddHours(-1) };
        await fixture.Refresh();
        Assert.Equal(74, (await fixture.Queries.QueryAsync(50, profession: "Alchemy", cancellationToken: fixture.Token)).Rows[0].Profession!.Skill);
        fixture.Source.ProfessionFailure = new IOException("Unavailable");
        fixture.Advance(600);
        await fixture.Refresh();
        Assert.True((await fixture.Queries.QueryAsync(50, profession: "Alchemy", cancellationToken: fixture.Token)).IncompleteProfessionCoverage);
        Assert.Equal(20, (await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token)).Rows[0].Character.Level);
    }

    [Fact]
    public async Task CombinedResponseUsesNoExtraRequestsAndMissingSectionRetainsItsOriginalAge()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(professions: Professions()));
        await fixture.Refresh();
        var time = fixture.Store.States[fixture.Service.ScopeId].Characters[0].Professions!.ObservedAtUtc;
        fixture.Advance();
        fixture.SetMembers(Character(level: 20));
        await fixture.Refresh();
        var result = await fixture.Queries.QueryAsync(50, profession: "Alchemy", cancellationToken: fixture.Token);
        Assert.Equal(time, result.Rows[0].Profession!.ObservedAtUtc);
        Assert.Equal(0, fixture.Source.ProfessionReads);
    }

    [Fact]
    public async Task UnknownEmptyAmbiguousAndNoMatchProfessionsAreNotConflated()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(
            Character(),
            Character("2", professions: Professions(75, tier: "one")),
            Character("3", professions: Professions(150, tier: "two"))
        );
        await fixture.Refresh();
        var ambiguous = await fixture.Queries.QueryAsync(50, profession: "alchemy", cancellationToken: fixture.Token);
        Assert.Equal("ambiguous_profession", ambiguous.Status);
        Assert.True(ambiguous.IncompleteProfessionCoverage);
        var exact = await fixture.Queries.QueryAsync(50, profession: "171:two", minimumSkill: 100, cancellationToken: fixture.Token);
        Assert.Equal("3", Assert.Single(exact.Rows).Character.Id);
        Assert.Equal("no_match", (await fixture.Queries.QueryAsync(50, profession: "Fishing", cancellationToken: fixture.Token)).Status);
        Assert.Equal("invalid_query", (await fixture.Queries.QueryAsync(50, page: -1, cancellationToken: fixture.Token)).Status);
        Assert.Equal("page_out_of_range", (await fixture.Queries.QueryAsync(50, page: int.MaxValue, cancellationToken: fixture.Token)).Status);
    }

    [Fact]
    public async Task AmbiguousProfessionChoicesArePaginatedRatherThanUnboundedToolOrDiscordOutput()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(
            Character(
                professions: new ProfessionObservation
                {
                    Complete = true,
                    Skills = Enumerable
                        .Range(1, 11)
                        .Select(index => new ProfessionSkill
                        {
                            Id = "171",
                            Name = "Alchemy",
                            Tier = index.ToString(),
                            Skill = 75,
                        })
                        .ToArray(),
                }
            )
        );
        await fixture.Refresh();
        var first = await fixture.Queries.QueryAsync(50, profession: "alchemy", cancellationToken: fixture.Token);
        var last = await fixture.Queries.QueryAsync(50, profession: "alchemy", page: 2, cancellationToken: fixture.Token);
        Assert.Equal("ambiguous_profession", first.Status);
        Assert.Equal(10, first.ProfessionChoices.Length);
        Assert.Single(last.ProfessionChoices);
        Assert.Equal(2, last.TotalPages);
        Assert.All(RosterText.Leaderboard(first), message => Assert.InRange(message.Length, 1, 2000));
    }

    [Fact]
    public async Task ForeignGuildAndDisabledQueriesDoNotReadTheDatabaseAndLookupFailuresPropagate()
    {
        using var fixture = new RosterFixture();
        Assert.Equal("unavailable", (await fixture.Queries.QueryAsync(51, cancellationToken: fixture.Token)).Status);
        Assert.Equal(0, fixture.Store.Reads);
        fixture.Options.Enabled = false;
        await fixture.Refresh();
        Assert.Equal(0, fixture.Source.RosterReads);
        Assert.Equal("unavailable", (await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token)).Status);
        Assert.Equal(0, fixture.Store.Reads);
        fixture.Options.Enabled = true;
        fixture.Store.FailReads = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token));
    }

    [Fact]
    public async Task RefreshesCoalesceAndConflictingSaveDoesNotCreateNotices()
    {
        using var fixture = new RosterFixture();
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => fixture.Service.RefreshAsync(fixture.Token)));
        Assert.Equal(1, fixture.Source.RosterReads);
        fixture.Advance();
        fixture.SetMembers(Character(level: 20));
        fixture.Store.Conflict = true;
        await fixture.Refresh();
        Assert.Empty(fixture.Notices.Items);
        Assert.Equal(19, fixture.Store.States[fixture.Service.ScopeId].Characters[0].Level);
    }

    [Fact]
    public async Task PendingSendSentOrderingSurvivesServiceRestartAndKnownFailuresStayPending()
    {
        using var fixture = new RosterFixture();
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(level: 20));
        await fixture.Refresh();
        var sender = new Sender(fixture.Notices) { Failure = true };
        await Assert.ThrowsAsync<IOException>(() => fixture.Service.DeliverAsync(sender, fixture.Token));
        Assert.Null(Assert.Single(fixture.Notices.Items.Values).SentAtUtc);
        using var restarted = new GuildRosterService(fixture.Options, fixture.Source, fixture.Store, fixture.Notices, fixture.Clock);
        sender.Failure = false;
        await restarted.DeliverAsync(sender, fixture.Token);
        var notice = Assert.Single(fixture.Notices.Items.Values);
        Assert.NotNull(notice.SentAtUtc);
        Assert.Equal(123UL, notice.DiscordMessageId);
        Assert.Equal(["pending", "send", "send", "sent"], fixture.Notices.Steps);
        await restarted.DeliverAsync(sender, fixture.Token);
        Assert.Equal(2, sender.Texts.Count);
        Assert.Contains("[Mock]", sender.Texts[0]);
        Assert.Contains("Borin Ironhide", sender.Texts[0]);
    }

    [Theory]
    [InlineData(false, 100UL)]
    [InlineData(true, null)]
    public async Task MockNotificationsNeedExplicitPermissionAndDestination(bool allowMock, ulong? channel)
    {
        using var fixture = new RosterFixture();
        fixture.Options.AllowMockNotifications = allowMock;
        fixture.Options.MilestoneChannelId = channel;
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(level: 20));
        await fixture.Refresh();
        Assert.Empty(fixture.Notices.Items);
        await fixture.Service.DeliverAsync(new Sender(fixture.Notices), fixture.Token);
    }

    [Fact]
    public void DiscordCopyIsEscapedBoundedAndDoesNotLoseRowsOrNotices()
    {
        var name = "@everyone **" + new string('*', 180);
        var notices = Enumerable
            .Range(1, 30)
            .Select(index => new RosterNotice
            {
                Id = index.ToString(),
                ScopeId = "mock",
                CharacterKey = index.ToString(),
                CharacterName = name,
                TrackKey = "level",
                Threshold = 20,
                IsMock = true,
            })
            .ToArray();
        var batches = RosterText.NoticeBatches(notices).ToArray();
        Assert.All(batches, batch => Assert.InRange(batch.Text.Length, 1, 2000));
        Assert.Equal(30, batches.Sum(batch => batch.Notices.Length));
        Assert.DoesNotContain("@everyone", string.Join('\n', batches.Select(batch => batch.Text)), StringComparison.Ordinal);
        Assert.Contains("\\*", batches[0].Text);
        var page = new RosterPage
        {
            IsMock = true,
            GuildName = "Test",
            TotalPages = 1,
            Page = 1,
            Rows = Enumerable.Range(1, 10).Select(index => new RosterEntry(index, Character(index.ToString(), first: name), null)).ToArray(),
        };
        Assert.All(RosterText.Leaderboard(page), text => Assert.InRange(text.Length, 1, 2000));
    }

    [Fact]
    public void RosterAndNoticeBsonRoundtripPreservesIdsUnknownFieldsUtcDatesAndPendingStatus()
    {
        var now = DateTime.UtcNow;
        var state = new RosterState
        {
            Id = "50:mock",
            ObservedAtUtc = now,
            Characters = [Character(professions: Professions(null))],
            Progress = [new("id:1", "level", 20)],
        };
        var restored = BsonSerializer.Deserialize<RosterState>(state.ToBson());
        Assert.Equal(DateTimeKind.Utc, restored.ObservedAtUtc!.Value.Kind);
        Assert.Null(restored.Characters[0].Professions!.Skills[0].Skill);
        Assert.Equal("id:1", restored.Characters[0].Key);
        Assert.Equal(20, restored.Progress[0].HighWater);
        Assert.Null(restored.Characters[0].ProfessionsCheckedAtUtc);
    }

    private sealed class Sender(MemoryRosterNotices store) : IRosterNotificationSender
    {
        public bool Failure { get; set; }
        public List<string> Texts { get; } = [];

        public Task<ulong> SendAsync(ulong guildId, ulong channelId, string text, CancellationToken cancellationToken)
        {
            Assert.Equal(50UL, guildId);
            Assert.Equal(100UL, channelId);
            Assert.All(store.Items.Values, notice => Assert.Null(notice.SentAtUtc));
            store.Steps.Add("send");
            Texts.Add(text);
            if (Failure)
                throw new IOException("Discord unavailable");
            return Task.FromResult(123UL);
        }
    }
}
