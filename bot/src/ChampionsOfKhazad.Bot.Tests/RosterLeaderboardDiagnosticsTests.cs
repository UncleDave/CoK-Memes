using ChampionsOfKhazad.Bot.Roster;
using static ChampionsOfKhazad.Bot.Tests.RosterFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class RosterLeaderboardDiagnosticsTests
{
    [Fact]
    public void RepresentativeProfessionMessageKeepsRanksPaginationAndSpecificFailureDetailsWithoutGenericWarnings()
    {
        var now = new DateTime(2026, 11, 6, 12, 40, 0, DateTimeKind.Utc);
        var skill = Professions(102, id: "182", name: "Herbalism").Skills[0] with { ObservedAtUtc = now.AddMinutes(-2) };
        var page = new RosterPage
        {
            GuildName = "Crossroads Mega Chippy",
            Profession = "Herbalism",
            Page = 1,
            TotalPages = 2,
            ObservedAtUtc = now.AddMinutes(-1),
            CheckedAtUtc = now,
            LastRefreshFailed = true,
            ProfessionCoverage = new()
            {
                TotalCharacters = 40,
                AffectedCharacters = 3,
                UnavailableCharacters = 2,
                FailedRefreshCharacters = 1,
            },
            Rows = [new(1, Character(first: "Huks", last: "Example") with { ProfessionsCheckedAtUtc = now }, skill)],
        };
        Assert.Equal(
            $"""
            **Crossroads Mega Chippy — Herbalism leaderboard**
            1. **Huks Example** — Warrior, Herbalism 102/300
            Page 1/2
            Guild profession coverage: 3 of 40 characters affected (2 unavailable, 1 failed refresh).
            Profession checks (this page): {Relative(now)}
            Profession source timestamps (this page): {Relative(now.AddMinutes(-2))}
            Roster check: {Relative(now)}
            The latest refresh failed; showing the last collected data.
            """.ReplaceLineEndings("\n"),
            string.Join('\n', RosterText.Leaderboard(page))
        );
    }

    [Fact]
    public async Task CoverageCountsTheWholeGuildWithoutDoubleCountingAffectedCharactersOrTreatingKnownAbsenceAsMissing()
    {
        using var fixture = new RosterFixture();
        await fixture.Refresh();
        var state = fixture.Store.States[fixture.Service.ScopeId];
        fixture.Store.States[fixture.Service.ScopeId] = state with
        {
            Characters =
            [
                Character("1", professions: Professions()),
                Character("2") with
                {
                    ProfessionRefreshFailed = true,
                },
                Character("3", professions: Professions(100, complete: false)),
                Character("4", professions: Professions(90)) with
                {
                    ProfessionRefreshFailed = true,
                },
                Character("5", professions: Professions(null)),
                Character("6", professions: new() { Complete = true }),
                Character("7", professions: Professions(null, id: "185", name: "Cooking")),
            ],
        };

        var page = await fixture.Queries.QueryAsync(50, profession: "Alchemy", cancellationToken: fixture.Token);
        Assert.Equal(3, page.Rows.Length);
        Assert.Equal(
            new ProfessionCoverage
            {
                TotalCharacters = 7,
                AffectedCharacters = 4,
                UnavailableCharacters = 1,
                PartialCharacters = 1,
                FailedRefreshCharacters = 2,
                UnknownRankCharacters = 1,
            },
            page.ProfessionCoverage
        );
        var text = string.Join('\n', RosterText.Leaderboard(page));
        Assert.Contains(
            "Guild profession coverage: 4 of 7 characters affected (1 unavailable, 1 partial, 2 failed refreshes, 1 unknown rank).",
            text
        );
        Assert.DoesNotContain("Profession data is incomplete.", text);
        Assert.DoesNotContain("Observed <t:", text);
        Assert.Contains("Alchemy 100/300", text);
        Assert.Contains("Alchemy 90/300", text);

        var levels = await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token);
        Assert.Null(levels.ProfessionCoverage);
        Assert.DoesNotContain("profession coverage", string.Join('\n', RosterText.Leaderboard(levels)));
    }

    [Fact]
    public async Task NameFilteredCoverageDoesNotClaimToDescribeTheWholeGuild()
    {
        using var fixture = new RosterFixture();
        fixture.SetMembers(Character(first: "Known", professions: Professions()), Character("2", first: "Missing"));
        await fixture.Refresh();
        var known = await fixture.Queries.QueryAsync(50, profession: "Alchemy", name: "Known", cancellationToken: fixture.Token);
        Assert.False(known.IncompleteProfessionCoverage);
        Assert.DoesNotContain("profession coverage", string.Join('\n', RosterText.Leaderboard(known)));

        var missing = await fixture.Queries.QueryAsync(50, profession: "Alchemy", name: "Missing", cancellationToken: fixture.Token);
        var text = string.Join('\n', RosterText.Leaderboard(missing));
        Assert.Equal("no_match", missing.Status);
        Assert.Contains("No matching characters with known ranks were found.", text);
        Assert.Contains("Matching-character profession coverage: 1 of 1 character affected (1 unavailable).", text);
        Assert.DoesNotContain("Profession source timestamps", text);
        Assert.DoesNotContain("Roster source timestamp", text);
    }

    [Fact]
    public async Task SourceAgeAndCheckRangesArePageSpecificAndDoNotMakeAnUpdatedPlayerLookDayOld()
    {
        using var fixture = new RosterFixture();
        await fixture.Refresh();
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var state = fixture.Store.States[fixture.Service.ScopeId];
        fixture.Store.States[fixture.Service.ScopeId] = state with
        {
            Characters = Enumerable
                .Range(1, 11)
                .Select(index =>
                {
                    var source =
                        index == 1 ? now.AddDays(-1)
                        : index == 11 ? now.AddDays(-2)
                        : now.AddMinutes(-1);
                    return Character(
                        index.ToString(),
                        first: $"Player{index:D2}",
                        professions: Professions(100 - index) with
                        {
                            ObservedAtUtc = source,
                            Skills = [Professions(100 - index).Skills[0] with { ObservedAtUtc = source }],
                        }
                    ) with
                    {
                        ProfessionsCheckedAtUtc = index == 1 ? now.AddMinutes(-5) : now,
                    };
                })
                .ToArray(),
        };

        var first = await fixture.Queries.QueryAsync(50, profession: "Alchemy", cancellationToken: fixture.Token);
        var second = await fixture.Queries.QueryAsync(50, profession: "Alchemy", page: 2, cancellationToken: fixture.Token);
        var text = string.Join('\n', RosterText.Leaderboard(first));
        Assert.Contains($"Profession checks (this page): oldest {Relative(now.AddMinutes(-5))}; newest {Relative(now)}", text);
        Assert.Contains($"Profession source timestamps (this page): oldest {Relative(now.AddDays(-1))}; newest {Relative(now.AddMinutes(-1))}", text);
        Assert.DoesNotContain(Relative(now.AddDays(-2)), text);
        Assert.DoesNotContain("Observed <t:", text);
        Assert.Contains("Player02 Ironhide", text);
        Assert.Contains("Alchemy 98/300", text);
        Assert.Contains("Page 1/2", text);
        Assert.DoesNotContain("profession coverage", text);
        var secondText = string.Join('\n', RosterText.Leaderboard(second));
        Assert.Contains($"Profession source timestamps (this page): {Relative(now.AddDays(-2))}", secondText);
        Assert.DoesNotContain("oldest", secondText);
    }

    [Fact]
    public async Task FailedProfessionAttemptIsVisibleAlongsideRetainedSkillAndItsOriginalSourceTimestamp()
    {
        using var fixture = new RosterFixture();
        fixture.Source.HasSeparateProfessionRequests = true;
        fixture.Source.Professions["id:1"] = Professions();
        await fixture.Refresh();
        var original = fixture.Store.States[fixture.Service.ScopeId].Characters[0].Professions!.Skills[0].ObservedAtUtc;
        fixture.Advance(601);
        fixture.Source.ProfessionFailure = new IOException("private-response-details");
        await fixture.Refresh();
        var page = await fixture.Queries.QueryAsync(50, profession: "Alchemy", cancellationToken: fixture.Token);
        var text = string.Join('\n', RosterText.Leaderboard(page));
        Assert.False(page.LastRefreshFailed);
        Assert.Contains("1 failed refresh", text);
        Assert.Contains("Alchemy 74/300", text);
        Assert.Contains($"Profession checks (this page): {Relative(fixture.Clock.Now.UtcDateTime)}", text);
        Assert.Contains($"Profession source timestamps (this page): {Relative(original)}", text);
        Assert.DoesNotContain("private-response-details", text);
    }

    [Fact]
    public async Task LevelOutputSeparatesRosterCheckAndSourceTimestampAndPreservesFailureStatus()
    {
        using var fixture = new RosterFixture();
        await fixture.Refresh();
        var observed = fixture.Clock.Now.UtcDateTime;
        fixture.Advance();
        fixture.Source.Failure = new IOException("private-details");
        await Assert.ThrowsAsync<IOException>(fixture.Refresh);
        var page = await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token);
        var text = string.Join('\n', RosterText.Leaderboard(page));
        Assert.Contains($"Roster check: {Relative(fixture.Clock.Now.UtcDateTime)}", text);
        Assert.Contains($"Roster source timestamp: {Relative(observed)}", text);
        Assert.Contains("The latest refresh failed; showing the last collected data.", text);
        Assert.Contains("level 19", text);
        Assert.DoesNotContain("private-details", text);
    }

    [Fact]
    public async Task MissingProfessionCheckTimeIsNotReplacedByTheMembershipCheck()
    {
        using var fixture = new RosterFixture();
        await fixture.Refresh();
        var state = fixture.Store.States[fixture.Service.ScopeId];
        fixture.Store.States[fixture.Service.ScopeId] = state with { Characters = [Character(professions: Professions())] };
        var page = await fixture.Queries.QueryAsync(50, profession: "Alchemy", cancellationToken: fixture.Token);
        Assert.DoesNotContain("Profession checks", string.Join('\n', RosterText.Leaderboard(page)));
    }

    [Fact]
    public async Task ProfessionScanDiagnosticsDistinguishPartialMissingFailedAndOlderResponsesWithoutLeakingExceptions()
    {
        using var fixture = new RosterFixture();
        var logger = new RosterDiagnosticLogger<GuildRosterService>();
        using var service = new GuildRosterService(fixture.Options, fixture.Source, fixture.Store, fixture.Notices, fixture.Clock, logger);
        fixture.Source.HasSeparateProfessionRequests = true;
        fixture.SetMembers(Character(), Character("2"), Character("3"));
        fixture.Source.Professions["id:1"] = Professions();
        fixture.Source.Professions["id:2"] = Professions(complete: false);
        await service.RefreshAsync(fixture.Token);
        var scan = Assert.Single(logger.Entries);
        Assert.Equal(3, scan.Properties["Attempts"]);
        Assert.Equal(1, scan.Properties["Unavailable"]);
        Assert.Equal(1, scan.Properties["Partial"]);
        Assert.Equal(0, scan.Properties["Failures"]);

        fixture.Advance();
        await service.RefreshAsync(fixture.Token);
        Assert.Single(logger.Entries);
        fixture.Advance(600);
        fixture.Source.Professions["id:1"] = Professions() with { ObservedAtUtc = fixture.Clock.Now.UtcDateTime.AddDays(-1) };
        await service.RefreshAsync(fixture.Token);
        Assert.Equal(1, logger.Entries.Last().Properties["Older"]);

        fixture.Advance(600);
        fixture.Source.ProfessionFailure = new InvalidDataException("private-response-details");
        await service.RefreshAsync(fixture.Token);
        Assert.Equal(3, logger.Entries.Last().Properties["Failures"]);
        Assert.Contains(logger.Entries, entry => Equals(entry.Properties.GetValueOrDefault("FailureCategory"), "invalid_data"));
        Assert.All(
            logger.Entries,
            entry =>
            {
                Assert.Null(entry.Exception);
                Assert.DoesNotContain("private-response-details", entry.Message);
            }
        );
    }

    [Fact]
    public async Task ProfessionChecksAndScanDurationReflectEachCompletedAttemptRatherThanTheBatchStart()
    {
        using var fixture = new RosterFixture();
        var logger = new RosterDiagnosticLogger<GuildRosterService>();
        using var service = new GuildRosterService(fixture.Options, fixture.Source, fixture.Store, fixture.Notices, fixture.Clock, logger);
        fixture.Source.HasSeparateProfessionRequests = true;
        fixture.SetMembers(Character(), Character("2"));
        fixture.Source.BeforeProfessionRead = () => fixture.Advance(10);
        var startedAt = fixture.Clock.Now.UtcDateTime;
        await service.RefreshAsync(fixture.Token);
        var state = fixture.Store.States[service.ScopeId];
        Assert.Equal(startedAt.AddSeconds(10), state.Characters[0].ProfessionsCheckedAtUtc);
        Assert.Equal(startedAt.AddSeconds(20), state.Characters[1].ProfessionsCheckedAtUtc);
        Assert.Equal(startedAt.AddSeconds(20), state.CheckedAtUtc);
        Assert.Equal(20_000d, Assert.Single(logger.Entries).Properties["DurationMs"]);
    }

    private static string Relative(DateTime value) => $"<t:{new DateTimeOffset(value, TimeSpan.Zero).ToUnixTimeSeconds()}:R>";
}
