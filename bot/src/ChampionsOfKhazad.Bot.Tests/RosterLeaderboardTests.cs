using ChampionsOfKhazad.Bot.Roster;
using static ChampionsOfKhazad.Bot.Tests.RosterFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class RosterLeaderboardTests
{
    [Fact]
    public async Task RecordedLevelUpsBreakTiesAcrossPagesWithoutRequiringMilestoneDelivery()
    {
        using var fixture = new RosterFixture();
        fixture.Options.MilestoneChannelId = null;
        var unknown = Character("unknown", 27, "Alpha");
        var higher = Character("higher", 28, "Bravo");
        var first = Character("first", 26, "Zulu");
        var later = Enumerable.Range(1, 10).Select(index => Character($"later{index}", 26, $"Later{index:D2}")).ToArray();
        fixture.SetMembers([unknown, higher, first, .. later]);
        await fixture.Refresh();

        fixture.Advance();
        first = first with { Level = 27 };
        fixture.SetMembers([unknown, higher, first, .. later]);
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers([unknown, higher, first, .. later.Reverse().Select(member => member with { Level = 27 })]);
        await fixture.Refresh();

        var firstPage = await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token);
        var secondPage = await fixture.Queries.QueryAsync(50, page: 2, cancellationToken: fixture.Token);
        var rows = firstPage.Rows.Concat(secondPage.Rows).ToArray();
        Assert.Equal(["higher", "first", .. later.Select(member => member.Id), "unknown"], rows.Select(row => row.Character.Id));
        Assert.Equal([1, 2, .. Enumerable.Repeat(3, 10), 13], rows.Select(row => row.Rank));
        Assert.DoesNotContain("· recorded", string.Join('\n', RosterText.Leaderboard(firstPage)));
        Assert.Empty(fixture.Notices.Items);
    }

    [Fact]
    public async Task ProfessionTiesBelow300UseRecordedSkillProgressForTheRequestedTier()
    {
        using var fixture = new RosterFixture();
        fixture.Options.MilestoneChannelId = null;
        fixture.Source.HasSeparateProfessionRequests = true;
        fixture.Source.BeforeProfessionRead = () => fixture.Advance(1);
        var unknown = Character("1", 60, "Alpha");
        var first = Character("2", 60, "Zulu");
        var later = Character("3", 60, "Bravo");
        var joint = Character("4", 60, "Delta");
        fixture.SetMembers(unknown, first, joint, later);

        var otherTier = Professions(120, tier: "other").Skills[0];
        var initial = Professions(119, tier: "classic");
        var advanced = Professions(120, tier: "classic");
        fixture.Source.Professions[unknown.Key] = advanced with { Skills = [.. advanced.Skills, otherTier] };
        fixture.Source.Professions[first.Key] = initial with { Skills = [.. initial.Skills, otherTier] };
        fixture.Source.Professions[later.Key] = initial;
        fixture.Source.Professions[joint.Key] = initial;
        await fixture.Refresh();

        fixture.Advance(600);
        fixture.Source.Professions[first.Key] = advanced with { Skills = [.. advanced.Skills, otherTier] };
        await fixture.Refresh();
        fixture.Advance(600);
        fixture.Source.Professions[later.Key] = advanced;
        fixture.Source.Professions[joint.Key] = advanced;
        await fixture.Refresh();
        fixture.Advance(600);
        var oldestCheck = fixture.Clock.Now.AddSeconds(1);
        await fixture.Refresh();

        var page = await fixture.Queries.QueryAsync(50, profession: "171:classic", cancellationToken: fixture.Token);
        Assert.Equal(["Zulu", "Bravo", "Delta", "Alpha"], page.Rows.Select(row => row.Character.FirstName));
        Assert.Equal([1, 2, 2, 4], page.Rows.Select(row => row.Rank));
        Assert.Equal(
            $"""
            **Mock roster**
            **Mock Guild — 171:classic leaderboard**
            1. **Zulu Ironhide** — Warrior, Alchemy 120/300
            2. **Bravo Ironhide** — Warrior, Alchemy 120/300
            2. **Delta Ironhide** — Warrior, Alchemy 120/300
            4. **Alpha Ironhide** — Warrior, Alchemy 120/300
            Page 1/1
            Last checked {DiscordDate(oldestCheck)}
            """.ReplaceLineEndings("\n"),
            string.Join('\n', RosterText.Leaderboard(page))
        );

        var other = await fixture.Queries.QueryAsync(50, profession: "171:other", cancellationToken: fixture.Token);
        Assert.Equal(["Alpha", "Zulu"], other.Rows.Select(row => row.Character.FirstName));
        Assert.All(other.Rows, row => Assert.Equal(1, row.Rank));
        Assert.Empty(fixture.Notices.Items);
    }

    [Fact]
    public async Task Profession300ShowsRecordedTimesForTheRequestedTierWithoutATrophy()
    {
        using var fixture = new RosterFixture();
        fixture.Options.LevelCap = null;
        var training = Professions(299, tier: "classic");
        var mastered = Professions(300, tier: "classic");
        var first = Character("1", first: "Zulu", professions: training);
        var joint = Character("2", first: "Bravo", professions: training);
        var later = Character("3", first: "Aster", professions: training);
        var unknown = Character("4", first: "Alpha", professions: mastered);
        var otherTier = Character("5", first: "Other", professions: Professions(300, tier: "other"));
        fixture.SetMembers(unknown, later, first, joint, otherTier);
        await fixture.Refresh();
        fixture.Advance();
        var firstAt = fixture.Clock.Now;
        first = first with { Professions = mastered };
        joint = joint with { Professions = mastered };
        fixture.SetMembers(unknown, later, first, joint, otherTier);
        await fixture.Refresh();
        fixture.Advance();
        var laterAt = fixture.Clock.Now;
        fixture.SetMembers(unknown, later with { Professions = mastered }, first, joint, otherTier);
        await fixture.Refresh();

        var page = await fixture.Queries.QueryAsync(50, profession: "171:classic", cancellationToken: fixture.Token);
        Assert.Equal(
            $"""
            **Mock roster**
            **Mock Guild — 171:classic leaderboard**
            1. **Bravo Ironhide** — Warrior, Alchemy 300/300 · recorded {DiscordDate(firstAt)}
            1. **Zulu Ironhide** — Warrior, Alchemy 300/300 · recorded {DiscordDate(firstAt)}
            3. **Aster Ironhide** — Warrior, Alchemy 300/300 · recorded {DiscordDate(laterAt)}
            4. **Alpha Ironhide** — Warrior, Alchemy 300/300 · time unknown
            Page 1/1
            Last checked {DiscordDate(laterAt)}
            """.ReplaceLineEndings("\n"),
            string.Join('\n', RosterText.Leaderboard(page))
        );
    }

    [Fact]
    public async Task Level60ShowsRecordedTimesAndJointClassFirstsWithoutAGuildTrophy()
    {
        using var fixture = new RosterFixture();
        var borin = Character("1", 59, professions: Professions());
        var zulu = Character("2", 59, "Zulu", professions: Professions(id: "185", name: "Cooking"));
        var aster = Character("3", 59, "Aster", professions: Professions(id: "186", name: "Mining")) with { ClassName = "Mage" };
        var oath = Character("4", 59, "Oath");
        var unknown = Character("5", 60, "Alpha") with { ClassName = "Hunter" };
        fixture.SetMembers(unknown, aster, zulu, borin, oath);
        await fixture.Refresh();
        fixture.Advance();
        var firstAt = fixture.Clock.Now;
        borin = borin with { Level = 60 };
        zulu = zulu with { Level = 60 };
        fixture.SetMembers(unknown, aster, zulu, borin, oath);
        await fixture.Refresh();
        fixture.Advance();
        var laterAt = fixture.Clock.Now;
        fixture.SetMembers(unknown, aster with { Level = 60 }, zulu, borin, oath with { Level = 60 });
        await fixture.Refresh();

        var page = await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token);
        Assert.Equal(
            $"""
            **Mock roster**
            **Mock Guild — Level leaderboard**
            1. **Borin Ironhide** — Warrior 🥇, level 60 · recorded {DiscordDate(firstAt)}
            1. **Zulu Ironhide** — Warrior 🥇, level 60 · recorded {DiscordDate(firstAt)}
            3. **Aster Ironhide** — Mage 🥇, level 60 · recorded {DiscordDate(laterAt)}
            3. **Oath Ironhide** — Warrior, level 60 · recorded {DiscordDate(laterAt)}
            5. **Alpha Ironhide** — Hunter, level 60 · time unknown
            🥇 First recorded at 60 in each class
            Page 1/1
            Last checked {DiscordDate(laterAt)}
            """.ReplaceLineEndings("\n"),
            string.Join('\n', RosterText.Leaderboard(page))
        );

        var filtered = await fixture.Queries.QueryAsync(50, name: "Aster", cancellationToken: fixture.Token);
        Assert.Contains("**Aster Ironhide** — Mage 🥇,", string.Join('\n', RosterText.Leaderboard(filtered)));
    }

    [Fact]
    public async Task LegacyNoticesBackfillExistingCharactersUsingDetectionTimeNotSentTime()
    {
        using var fixture = new RosterFixture();
        var alchemy = Professions(75, tier: "classic");
        var trackKey = alchemy.Skills[0].TrackKey;
        var members = new[]
        {
            Character("1", 60, "Zulu", professions: alchemy),
            Character("2", 60, "Borin", professions: alchemy),
            Character("3", 60, "Alpha", professions: alchemy),
            Character("4", 21),
        };
        fixture.SetMembers(members);
        var scope = fixture.Service.ScopeId;
        var now = fixture.Clock.Now;
        fixture.Store.States[scope] = new()
        {
            Id = scope,
            ObservedAtUtc = now.AddMinutes(-1).UtcDateTime,
            Characters = members,
            Progress =
            [
                .. members.Select(member => new RosterProgress(member.Key, "level", member.Level)),
                .. members.Take(3).Select(member => new RosterProgress(member.Key, trackKey, 75)),
            ],
        };
        var firstAt = now.AddDays(-2);
        var secondAt = now.AddDays(-1);
        var first = new RosterNotice
        {
            Id = "first",
            ScopeId = scope,
            CharacterKey = members[0].Key,
            CharacterName = "Old Name",
            TrackKey = "level",
            Threshold = 60,
            CreatedAtUtc = firstAt.UtcDateTime,
        };
        await fixture.Notices.AddPendingAsync(first, fixture.Token);
        await fixture.Notices.MarkSentAsync(first.Id, now.UtcDateTime, 123, fixture.Token);
        await fixture.Notices.AddPendingAsync(
            first with
            {
                Id = "second",
                CharacterKey = members[1].Key,
                CreatedAtUtc = secondAt.UtcDateTime,
            },
            fixture.Token
        );
        await fixture.Notices.AddPendingAsync(
            first with
            {
                Id = "wrong-scope",
                ScopeId = "50:other-source",
                CharacterKey = members[2].Key,
            },
            fixture.Token
        );
        await fixture.Notices.AddPendingAsync(first with { Id = "other-track", TrackKey = trackKey, CharacterKey = members[2].Key }, fixture.Token);
        var skillAt = secondAt.AddHours(1);
        await fixture.Notices.AddPendingAsync(
            first with
            {
                Id = "alchemy",
                CharacterKey = members[1].Key,
                TrackKey = trackKey,
                Threshold = 75,
                CreatedAtUtc = skillAt.UtcDateTime,
            },
            fixture.Token
        );
        await fixture.Notices.AddPendingAsync(
            first with
            {
                Id = "other-tier",
                CharacterKey = members[2].Key,
                TrackKey = (alchemy.Skills[0] with { Tier = "other" }).TrackKey,
                Threshold = 75,
            },
            fixture.Token
        );
        await fixture.Notices.AddPendingAsync(first with { Id = "lower-threshold", CharacterKey = members[3].Key, Threshold = 20 }, fixture.Token);
        await fixture.Refresh();
        fixture.Notices.Items.Clear();

        fixture.Advance();
        fixture.SetMembers(members[0] with { FirstName = "Renamed", Level = 59 }, members[1], members[2], members[3]);
        await fixture.Refresh();
        var regressed = await fixture.Queries.QueryAsync(50, name: "Renamed", cancellationToken: fixture.Token);
        Assert.Null(Assert.Single(regressed.Rows).RecordedAtUtc);
        fixture.Advance();
        fixture.SetMembers(members[1..]);
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(members[0] with { FirstName = "Renamed" }, members[1], members[2], members[3]);
        await fixture.Refresh();

        var page = await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token);
        Assert.Equal(["1", "2", "3", "4"], page.Rows.Select(row => row.Character.Id));
        Assert.Null(page.Rows[^1].RecordedAtUtc);
        var text = string.Join('\n', RosterText.Leaderboard(page));
        Assert.Contains($"**Renamed Ironhide** — Warrior 🥇, level 60 · recorded {DiscordDate(firstAt)}", text);
        Assert.Contains($"**Borin Ironhide** — Warrior, level 60 · recorded {DiscordDate(secondAt)}", text);
        Assert.Contains("**Alpha Ironhide** — Warrior, level 60 · time unknown", text);
        Assert.DoesNotContain(DiscordDate(now), text);

        var skillPage = await fixture.Queries.QueryAsync(50, profession: "171:classic", cancellationToken: fixture.Token);
        Assert.Equal(["2", "3", "1"], skillPage.Rows.Select(row => row.Character.Id));
        Assert.Equal(skillAt.UtcDateTime, skillPage.Rows[0].RecordedAtUtc);
        Assert.All(skillPage.Rows[1..], row => Assert.Null(row.RecordedAtUtc));
    }

    [Fact]
    public async Task LegacyNoticeReadFailureDoesNotBlockCurrentMembershipOrMilestones()
    {
        using var fixture = new RosterFixture();
        var departed = Character("1", 19);
        var stayed = Character("2", 59, "Aster");
        fixture.SetMembers(departed, stayed);
        await fixture.Refresh();
        fixture.Advance();
        fixture.SetMembers(departed with { Level = 20 }, stayed);
        await fixture.Refresh();
        Assert.Equal(departed.Key, Assert.Single(fixture.Notices.Items.Values).CharacterKey);

        var scope = fixture.Service.ScopeId;
        var state = fixture.Store.States[scope];
        fixture.Store.States[scope] = state with { ProgressTimesBackfilled = false };
        fixture.Notices.ProgressNoticeReadFailure = new IOException("Legacy notice read failed");
        fixture.Advance();
        fixture.SetMembers(stayed with { Level = 60 });
        await fixture.Refresh();

        var page = await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token);
        Assert.False(page.LastRefreshFailed);
        var row = Assert.Single(page.Rows);
        Assert.Equal(stayed.Key, row.Character.Key);
        Assert.Equal(fixture.Clock.Now.UtcDateTime, row.RecordedAtUtc);
        Assert.Equal(stayed.Key, Assert.Single(fixture.Notices.Items.Values).CharacterKey);
        Assert.False(fixture.Store.States[scope].ProgressTimesBackfilled);

        fixture.Notices.ProgressNoticeReadFailure = null;
        fixture.Advance();
        await fixture.Refresh();
        Assert.True(fixture.Store.States[scope].ProgressTimesBackfilled);
        Assert.Single(fixture.Notices.Items);
    }

    private static string DiscordDate(DateTimeOffset timestamp) => $"<t:{timestamp.ToUnixTimeSeconds()}:f>";
}
