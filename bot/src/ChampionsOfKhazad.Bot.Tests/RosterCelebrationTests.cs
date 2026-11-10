using ChampionsOfKhazad.Bot.Roster;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using static ChampionsOfKhazad.Bot.Tests.RosterFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class RosterCelebrationTests
{
    private static readonly string[] Classes = ["Druid", "Hunter", "Mage", "Paladin", "Priest", "Rogue", "Shaman", "Warlock", "Warrior"];
    private static readonly string[] ProfessionIds = ["171", "164", "333", "202", "182", "165", "186", "393", "197", "185", "356", "129"];

    [Fact]
    public async Task FirstSixtyCombinesGuildAndClassFirstsInOneGoldEmbedWithoutOrdinaryDuplicate()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(level: 59));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(level: 60));
        await fixture.Refresh();
        var notice = Assert.Single(fixture.Notices.Items.Values);
        Assert.Equal(RosterCelebrationKind.First, notice.Celebration);
        Assert.Equal(["First recorded level 60 in the guild", "First recorded Warrior at level 60"], notice.Honours);
        var batch = Assert.Single(RosterText.NoticeBatches([notice]));
        Assert.Equal("[Mock] Guild firsts — Grub's honours ledger", batch.Title);
        Assert.Equal(0xF1C40Fu, batch.Colour);
        Assert.Equal(
            "**Borin Ironhide** — **level 60** · Warrior\n"
                + "🏆 **First recorded level 60 in the guild**\n🏆 **First recorded Warrior at level 60**\n\n"
                + "*“Strength earned. Stand proud.” — Grub*",
            batch.Text
        );
        var embed = DiscordRosterNotificationSender.CreateEmbed(batch)!;
        Assert.Equal(batch.Title, embed.Title);
        Assert.Equal(batch.Text, embed.Description);
        Assert.Equal(batch.Colour, embed.Color!.Value.RawValue);
        fixture.Advance();
        fixture.SetMembers(Character(level: 59));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(level: 60));
        await fixture.Refresh();
        Assert.Single(fixture.Notices.Items);
    }

    [Fact]
    public async Task LaterSixtyAndThreeHundredRemainFancyWhileOrdinaryMilestonesStayPlain()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(level: 60, professions: Professions(300)), Character("2", 49, professions: Professions(224)));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(level: 60, professions: Professions(300)), Character("2", 50, professions: Professions(225)));
        await fixture.Refresh();
        var plain = Assert.Single(RosterText.NoticeBatches(fixture.Notices.Items.Values));
        Assert.Null(plain.Title);
        Assert.Null(DiscordRosterNotificationSender.CreateEmbed(plain));
        Assert.Equal("[Mock] **Borin Ironhide** reached level 50!\n[Mock] **Borin Ironhide** reached 225 Alchemy!", plain.Text);
        fixture.Advance();
        fixture.SetMembers(Character(level: 60, professions: Professions(300)), Character("2", 60, professions: Professions(300)));
        await fixture.Refresh();
        var special = fixture.Notices.Items.Values.Where(notice => notice.Threshold is 60 or 300).ToArray();
        Assert.Equal(2, special.Length);
        Assert.All(
            special,
            notice =>
            {
                Assert.Equal(RosterCelebrationKind.Personal, notice.Celebration);
                Assert.Empty(notice.Honours);
            }
        );
        var batch = Assert.Single(RosterText.NoticeBatches(special));
        Assert.Equal("[Mock] Strength and mastery earned", batch.Title);
        Assert.Equal(0x2ECC71u, batch.Colour);
    }

    [Fact]
    public async Task JointFirstsRecogniseEveryArrivalWithoutDependingOnRosterOrdering()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character("2", 59, "Zara", professions: Professions(299)), Character("1", 59, professions: Professions(299)));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character("1", 60, professions: Professions(300)), Character("2", 60, "Zara", professions: Professions(300)));
        await fixture.Refresh();
        Assert.Equal(4, fixture.Notices.Items.Count);
        Assert.All(
            fixture.Notices.Items.Values,
            notice =>
            {
                Assert.Equal(RosterCelebrationKind.First, notice.Celebration);
                Assert.All(notice.Honours, heading => Assert.StartsWith("Joint first recorded", heading));
            }
        );
        var batch = Assert.Single(RosterText.NoticeBatches(fixture.Notices.Items.Values));
        Assert.Equal(4, batch.Notices.Length);
        Assert.Contains("Borin Ironhide", batch.Text);
        Assert.Contains("Zara Ironhide", batch.Text);
    }

    [Fact]
    public async Task AllNineClassesIncludingPaladinAreRequiredAndCompletionCombinesWithLastClassFirst()
    {
        using var fixture = new RosterFixture();
        var characters = Classes
            .Select((name, index) => Character(index.ToString(), name == "Paladin" ? 59 : 60) with { ClassName = name })
            .ToArray();
        fixture.SetMembers(characters);
        await fixture.Refresh();
        Assert.Empty(fixture.Notices.Items);
        fixture.Advance();
        fixture.SetMembers(characters.Select(member => member with { Level = 60 }).ToArray());
        await fixture.Refresh();
        var first = Assert.Single(fixture.Notices.Items.Values, notice => notice.Celebration == RosterCelebrationKind.First);
        Assert.Equal(["First recorded Paladin at level 60"], first.Honours);
        var guild = Assert.Single(fixture.Notices.Items.Values, notice => notice.Celebration == RosterCelebrationKind.Guild);
        Assert.Equal(["Every class at level 60"], guild.Honours);
        var batch = Assert.Single(RosterText.NoticeBatches(fixture.Notices.Items.Values));
        Assert.Equal(2, batch.Notices.Length);
        Assert.Contains("Every class at level 60", batch.Text);
        fixture.Advance();
        fixture.SetMembers(characters.Where(member => member.ClassName != "Paladin").ToArray());
        await fixture.Refresh();
        // Guild accomplishments survive departure; the departed character's pending notice does not.
        Assert.Equal(guild, Assert.Single(fixture.Notices.Items.Values));
        fixture.Advance();
        fixture.SetMembers(characters.Select(member => member with { Level = 60 }).ToArray());
        await fixture.Refresh();
        Assert.Single(fixture.Notices.Items);
    }

    [Fact]
    public async Task EveryProfessionIncludesSecondariesAndRequiresAllTwelveIdsNotJustObservedSkills()
    {
        using var fixture = new RosterFixture();
        var characters = ProfessionIds
            .Select(
                (id, index) =>
                    Character(
                        index.ToString(),
                        professions: Professions(id == "129" ? 299 : 300, id: id, name: id == "129" ? "First Aid" : $"Profession {id}")
                    )
            )
            .ToArray();
        fixture.SetMembers(characters);
        await fixture.Refresh();
        Assert.Empty(fixture.Notices.Items);
        fixture.Advance();
        fixture.SetMembers(
            characters
                .Select(member => member.Id == "11" ? member with { Professions = Professions(300, id: "129", name: "First Aid") } : member)
                .ToArray()
        );
        await fixture.Refresh();
        var profession = Assert.Single(fixture.Notices.Items.Values, notice => notice.ProfessionName is not null);
        Assert.Equal("First Aid", profession.ProfessionName);
        Assert.Equal(RosterCelebrationKind.First, profession.Celebration);
        Assert.Equal(["First recorded 300 First Aid"], profession.Honours);
        var guild = Assert.Single(fixture.Notices.Items.Values, notice => notice.Celebration == RosterCelebrationKind.Guild);
        Assert.Equal(["Every profession at 300"], guild.Honours);
        Assert.Single(RosterText.NoticeBatches(fixture.Notices.Items.Values));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task GuildCountsAreGlobalDistinctCharactersAndOnlyHighestCrossedCountIsAnnounced(int count)
    {
        using var fixture = new RosterFixture();
        var characters = Enumerable.Range(1, count).Select(index => Character(index.ToString(), 59, $"Player{index}")).ToArray();
        fixture.SetMembers(characters);
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(characters.Select(member => member with { Level = 60 }).ToArray());
        await fixture.Refresh();
        var guild = Assert.Single(fixture.Notices.Items.Values, notice => notice.Celebration == RosterCelebrationKind.Guild);
        Assert.Equal([$"{count} guild characters at level 60"], guild.Honours);
        Assert.DoesNotContain("class", guild.Honours[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("profession", guild.Honours[0], StringComparison.OrdinalIgnoreCase);
        var stored = fixture.Store.States[fixture.Service.ScopeId].Celebrations!;
        Assert.Contains("count:level:60:5", stored.Claimed);
        if (count >= 10)
            Assert.Contains("count:level:60:10", stored.Claimed);
        fixture.Advance();
        fixture.SetMembers(characters);
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(characters.Select(member => member with { Level = 60 }).ToArray());
        await fixture.Refresh();
        Assert.Single(fixture.Notices.Items.Values, notice => notice.Celebration == RosterCelebrationKind.Guild);
    }

    [Fact]
    public async Task FiveTenAndTwentyCanEachCelebrateOnceAsTheGuildGrows()
    {
        using var fixture = new RosterFixture();
        var characters = Enumerable.Range(1, 20).Select(index => Character(index.ToString(), 59)).ToArray();
        fixture.SetMembers(characters);
        await fixture.Refresh();
        foreach (var count in new[] { 5, 10, 20 })
        {
            fixture.Advance();
            fixture.SetMembers(characters.Select((member, index) => member with { Level = index < count ? 60 : 59 }).ToArray());
            await fixture.Refresh();
        }
        var honours = fixture
            .Notices.Items.Values.Where(notice => notice.Celebration == RosterCelebrationKind.Guild)
            .SelectMany(notice => notice.Honours)
            .Order()
            .ToArray();
        Assert.Equal(["10 guild characters at level 60", "20 guild characters at level 60", "5 guild characters at level 60"], honours);
    }

    [Fact]
    public async Task ProfessionCountsNeverProduceFiveTenOrTwentyMilestones()
    {
        using var fixture = new RosterFixture();
        var characters = Enumerable.Range(1, 20).Select(index => Character(index.ToString(), professions: Professions(299))).ToArray();
        fixture.SetMembers(characters);
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(characters.Select(member => member with { Professions = Professions(300) }).ToArray());
        await fixture.Refresh();
        Assert.Equal(20, fixture.Notices.Items.Count);
        Assert.DoesNotContain(fixture.Notices.Items.Values, notice => notice.Celebration == RosterCelebrationKind.Guild);
    }

    [Fact]
    public async Task LegacySnapshotSeedsExistingAchievementsSilentlyButStillAnnouncesANewCrossing()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(level: 60), Character("2", 59) with { ClassName = "Paladin" });
        await fixture.Refresh();
        var old = fixture.Store.States[fixture.Service.ScopeId];
        fixture.Store.States[fixture.Service.ScopeId] = old with { Celebrations = null };
        fixture.Advance();
        fixture.SetMembers(Character(level: 60), Character("2", 60) with { ClassName = "Paladin" });
        await fixture.Refresh();
        var notice = Assert.Single(fixture.Notices.Items.Values);
        Assert.Equal(["First recorded Paladin at level 60"], notice.Honours);
        Assert.DoesNotContain("First recorded level 60 in the guild", notice.Honours);
    }

    [Fact]
    public async Task LegacyDepartedCapAndProfessionHighWaterMarksCannotBecomeInventedFirsts()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(level: 59, professions: Professions(299)));
        await fixture.Refresh();
        var old = fixture.Store.States[fixture.Service.ScopeId];
        fixture.Store.States[fixture.Service.ScopeId] = old with
        {
            Celebrations = null,
            Progress = [.. old.Progress, new("id:departed", "level", 60), new("id:departed", "profession:3:171:", 300)],
        };
        fixture.Advance();
        fixture.SetMembers(Character(level: 60, professions: Professions(300)));
        await fixture.Refresh();
        Assert.Equal(2, fixture.Notices.Items.Count);
        Assert.All(fixture.Notices.Items.Values, notice => Assert.Equal(RosterCelebrationKind.Personal, notice.Celebration));
    }

    [Fact]
    public async Task CappedJoinersUnknownSkillsAndRejoinsStaySilentAndDoNotStealOrReopenFirsts()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(level: 59), Character("2", 59));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(level: 60, professions: Professions(300)), Character("2", 59), Character("3", 60));
        await fixture.Refresh();
        var personal = Assert.Single(fixture.Notices.Items.Values);
        Assert.Equal(RosterCelebrationKind.Personal, personal.Celebration);
        Assert.Empty(personal.Honours);
        fixture.Advance();
        fixture.SetMembers(Character("2", 59, professions: Professions(299)));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character("2", 60, professions: Professions(300)));
        await fixture.Refresh();
        Assert.Equal(2, fixture.Notices.Items.Count);
        Assert.All(fixture.Notices.Items.Values, notice => Assert.Equal(RosterCelebrationKind.Personal, notice.Celebration));
    }

    [Fact]
    public async Task FirstsRemainClaimedAcrossRestartRenameAndDeparture()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(level: 59), Character("2", 59));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(level: 60), Character("2", 59));
        await fixture.Refresh();
        var sender = new Sender { Fail = true };
        await Assert.ThrowsAsync<IOException>(() => fixture.Service.DeliverAsync(sender, fixture.Token));
        using var restarted = new GuildRosterService(fixture.Options, fixture.Source, fixture.Store, fixture.Notices, fixture.Clock);
        sender.Fail = false;
        await restarted.DeliverAsync(sender, fixture.Token);
        Assert.NotNull(Assert.Single(fixture.Notices.Items.Values).SentAtUtc);
        Assert.NotNull(sender.Batches[^1].Title);
        fixture.Advance();
        fixture.SetMembers(Character(level: 60, last: "Renamed"), Character("2", 59));
        await restarted.RefreshAsync(fixture.Token, force: true);
        fixture.Advance();
        fixture.SetMembers(Character("2", 60));
        await restarted.RefreshAsync(fixture.Token, force: true);
        var second = Assert.Single(fixture.Notices.Items.Values, notice => notice.CharacterKey == "id:2");
        Assert.Equal(RosterCelebrationKind.Personal, second.Celebration);
        Assert.Empty(second.Honours);
    }

    [Fact]
    public async Task GuildOnlyNoticesStillDeliverAfterTheirContributorsLeave()
    {
        using var fixture = new RosterFixture();
        var characters = Enumerable.Range(1, 5).Select(index => Character(index.ToString(), 59)).ToArray();
        fixture.SetMembers(characters);
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(characters.Select(member => member with { Level = 60 }).ToArray());
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers();
        await fixture.Refresh();
        var notice = Assert.Single(fixture.Notices.Items.Values);
        Assert.Equal(RosterCelebrationKind.Guild, notice.Celebration);
        var sender = new Sender();
        await fixture.Service.DeliverAsync(sender, fixture.Token);
        Assert.Equal("[Mock] Guild milestone — Grub's honours ledger", Assert.Single(sender.Batches).Title);
        Assert.NotNull(fixture.Notices.Items[notice.Id].SentAtUtc);
    }

    [Fact]
    public async Task FailedConflictingAndDisabledRefreshesDoNotLeakCelebrations()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(level: 59));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(level: 60));
        fixture.Source.Failure = new IOException();
        await Assert.ThrowsAsync<IOException>(fixture.Refresh);
        fixture.Source.Failure = null;
        fixture.Store.Conflict = true;
        await fixture.Refresh();
        Assert.Empty(fixture.Notices.Items);
        Assert.DoesNotContain("first:level:60", fixture.Store.States[fixture.Service.ScopeId].Celebrations!.Claimed);
        fixture.Store.Conflict = false;
        fixture.Options.MilestoneChannelId = null;
        await fixture.Refresh();
        Assert.Empty(fixture.Notices.Items);
        fixture.Options.MilestoneChannelId = 100;
        fixture.Advance();
        await fixture.Refresh();
        Assert.Empty(fixture.Notices.Items);
    }

    [Fact]
    public async Task ProfessionFirstsStaySeparateByTrackAndKnownRanksAreRequired()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(
            Character("1", professions: Professions(300, tier: "one")),
            Character("2", professions: Professions(299, tier: "two")),
            Character("3", professions: Professions(null, id: "185", name: "Cooking"))
        );
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(
            Character("1", professions: Professions(300, tier: "one")),
            Character("2", professions: Professions(300, tier: "two")),
            Character("3", professions: Professions(null, id: "185", name: "Cooking"))
        );
        await fixture.Refresh();
        var notice = Assert.Single(fixture.Notices.Items.Values);
        Assert.Equal("profession:3:171:two", notice.TrackKey);
        Assert.Equal(RosterCelebrationKind.First, notice.Celebration);
        fixture.Advance();
        fixture.SetMembers(
            Character("1", professions: Professions(300, tier: "one")),
            Character("2", professions: Professions(300, tier: "two")),
            Character("3", professions: Professions(300, id: "185", name: "Cooking"))
        );
        await fixture.Refresh();
        Assert.Single(fixture.Notices.Items);
        Assert.Contains("first:profession:3:185::300", fixture.Store.States[fixture.Service.ScopeId].Celebrations!.Claimed);
    }

    [Fact]
    public async Task CappedJoinersAloneSeedCollectiveMilestonesWithoutPostingThem()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(level: 59));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers([Character(level: 59), .. Enumerable.Range(2, 5).Select(index => Character(index.ToString(), 60))]);
        await fixture.Refresh();
        Assert.Empty(fixture.Notices.Items);
        Assert.Contains("count:level:60:5", fixture.Store.States[fixture.Service.ScopeId].Celebrations!.Claimed);
        fixture.Advance();
        fixture.SetMembers(Enumerable.Range(1, 6).Select(index => Character(index.ToString(), 60)).ToArray());
        await fixture.Refresh();
        Assert.Equal(RosterCelebrationKind.Personal, Assert.Single(fixture.Notices.Items.Values).Celebration);
    }

    [Fact]
    public async Task AProfessionCapRemainsACelebrationWhenOrdinaryThresholdsAreReconfigured()
    {
        using var fixture = new RosterFixture();
        fixture.Options.ProfessionMilestones = [75, 150, 225];
        fixture.SetMembers(Character(professions: Professions(299, id: "356", name: "Fishing")));
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(Character(professions: Professions(300, id: "356", name: "Fishing")));
        await fixture.Refresh();
        var notice = Assert.Single(fixture.Notices.Items.Values);
        Assert.Equal(300, notice.Threshold);
        Assert.Equal(RosterCelebrationKind.First, notice.Celebration);
        Assert.Equal("Fishing", notice.ProfessionName);
        var batch = Assert.Single(RosterText.NoticeBatches([notice]));
        Assert.Contains("Skill earned through work. Put it to use.", batch.Text);
    }

    [Fact]
    public void LegacyBsonAndNewCelebrationStateAndNoticesRoundtripWithoutAMigration()
    {
        var state = new RosterState
        {
            Id = "scope",
            Celebrations = new() { Claimed = ["first:level:60", "count:level:60:5"] },
        };
        var restored = BsonSerializer.Deserialize<RosterState>(state.ToBson());
        Assert.Equal(state.Celebrations.Claimed, restored.Celebrations!.Claimed);
        var legacy = state.ToBsonDocument();
        legacy.Remove(Field<RosterState>(nameof(RosterState.Celebrations)));
        Assert.Null(BsonSerializer.Deserialize<RosterState>(legacy).Celebrations);
        var notice = new RosterNotice
        {
            Id = "notice",
            ScopeId = "scope",
            CharacterKey = "id:1",
            CharacterName = "Borin Ironhide",
            ClassName = "Warrior",
            TrackKey = "level",
            Threshold = 60,
            Celebration = RosterCelebrationKind.First,
            Honours = ["First recorded level 60 in the guild"],
        };
        var roundtrip = BsonSerializer.Deserialize<RosterNotice>(notice.ToBson());
        Assert.Equal(notice.Celebration, roundtrip.Celebration);
        Assert.Equal(notice.Honours, roundtrip.Honours);
        Assert.Null(roundtrip.SentAtUtc);
        var oldNotice = notice.ToBsonDocument();
        oldNotice.Remove(Field<RosterNotice>(nameof(RosterNotice.Celebration)));
        oldNotice.Remove(Field<RosterNotice>(nameof(RosterNotice.Honours)));
        var restoredOld = BsonSerializer.Deserialize<RosterNotice>(oldNotice);
        Assert.Equal(RosterCelebrationKind.None, restoredOld.Celebration);
        Assert.Empty(restoredOld.Honours);
    }

    [Fact]
    public void LargeJointCelebrationsAreEscapedSplitWithoutLossAndNeverMergeDifferentObservations()
    {
        var now = DateTime.UtcNow;
        var notices = Enumerable
            .Range(1, 30)
            .Select(index => new RosterNotice
            {
                Id = index.ToString(),
                ScopeId = "scope",
                CharacterKey = $"id:{index}",
                CharacterName = "@everyone **" + new string('*', 180),
                ClassName = "Warrior",
                TrackKey = "level",
                Threshold = 60,
                Celebration = RosterCelebrationKind.First,
                Honours = ["Joint first recorded level 60 in the guild", "Joint first recorded Warrior at level 60"],
                CreatedAtUtc = index < 20 ? now : now.AddMinutes(1),
            })
            .ToArray();
        var batches = RosterText.NoticeBatches(notices).ToArray();
        Assert.True(batches.Length > 1);
        Assert.Equal(30, batches.Sum(batch => batch.Notices.Length));
        Assert.Equal(30, batches.SelectMany(batch => batch.Notices).Select(notice => notice.Id).Distinct().Count());
        Assert.All(
            batches,
            batch =>
            {
                Assert.InRange(batch.Text.Length, 1, 2000);
                Assert.InRange(batch.Title!.Length, 1, 256);
                Assert.DoesNotContain("@everyone", batch.Text, StringComparison.Ordinal);
                Assert.Contains("\\*", batch.Text);
                Assert.Single(batch.Notices.Select(notice => notice.CreatedAtUtc).Distinct());
                Assert.NotNull(DiscordRosterNotificationSender.CreateEmbed(batch));
            }
        );
    }

    private static string Field<T>(string property) => BsonClassMap.LookupClassMap(typeof(T)).GetMemberMap(property).ElementName;

    private sealed class Sender : IRosterNotificationSender
    {
        public bool Fail { get; set; }
        public List<RosterNoticeBatch> Batches { get; } = [];

        public Task<ulong> SendAsync(ulong guildId, ulong channelId, RosterNoticeBatch batch, CancellationToken cancellationToken)
        {
            Batches.Add(batch);
            if (Fail)
                throw new IOException("Discord unavailable");
            return Task.FromResult(123UL);
        }
    }
}
