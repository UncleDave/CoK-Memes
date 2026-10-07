using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class NotebookServiceTests
{
    [Fact]
    public async Task AcceptedNoteIsSeparateTentativeExpiringMemoryAndReviewContainsChoiceReason()
    {
        var fixture = new Fixture();
        var result = await fixture.RememberAsync();

        Assert.Contains("recorded for 30 days", result);
        var note = Assert.Single(fixture.Store.State.Notes);
        Assert.True(note.ReviewDelivered);
        Assert.Equal(fixture.Clock.Now.AddDays(30), note.ExpiresAtUtc);
        Assert.Equal("Useful raid anecdote", note.Reason);
        Assert.Equal("Supported by sources", note.ReviewReason);
        Assert.Equal(note.Id, Assert.Single(fixture.Reviewer.Notes).Id);
        Assert.Equal(NotebookWriteOutcome.Saved, Assert.Single(fixture.Store.State.WriteAttempts).Outcome);
        var search = await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken);
        Assert.Contains("NOT canon", search);
        Assert.Contains(note.Content, search);
        Assert.Contains(note.Sources[0].Url, search);
    }

    [Fact]
    public async Task FailedReviewDeliveryNeverActivatesMemoryAndStillConsumesBudget()
    {
        var fixture = new Fixture();
        fixture.Reviewer.Deliver = false;
        Assert.Contains("audit-only", await fixture.RememberAsync());
        Assert.False(Assert.Single(fixture.Store.State.Notes).ReviewDelivered);
        Assert.Equal(NotebookWriteOutcome.ReviewDeliveryFailed, Assert.Single(fixture.Store.State.WriteAttempts).Outcome);
        Assert.Contains("No accessible", await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectedAssessmentDoesNotWriteOrDm()
    {
        var fixture = new Fixture();
        fixture.Evaluator.Assessment = new NotebookAssessment(false, "Conflicts with canon")
        {
            RejectionCategory = NotebookRejectionCategory.DuplicateOrConflict,
        };
        Assert.Contains("independent review rejected", await fixture.RememberAsync());
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Empty(fixture.Reviewer.Notes);
        var attempt = Assert.Single(fixture.Store.State.WriteAttempts);
        Assert.Equal(NotebookWriteOutcome.ReviewRejected, attempt.Outcome);
        Assert.Equal(NotebookRejectionCategory.DuplicateOrConflict, attempt.RejectionCategory);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 8)]
    public async Task MissingOrOldSourcesAreRejectedBeforeAiReview(bool unavailable, int ageDays)
    {
        var fixture = new Fixture();
        fixture.Reader.Available = !unavailable;
        fixture.Reader.AgeDays = ageDays;
        Assert.Contains("verifiable human", await fixture.RememberAsync());
        Assert.Equal(0, fixture.Evaluator.Calls);
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Equal(NotebookWriteOutcome.InvalidSources, Assert.Single(fixture.Store.State.WriteAttempts).Outcome);
        Assert.Empty(fixture.Store.State.EvaluationAttempts);
    }

    [Fact]
    public async Task InvalidInputAndDmContextCannotWrite()
    {
        var fixture = new Fixture();
        fixture.Context.ChannelId = null;
        Assert.Contains("guild chat", await fixture.RememberAsync());
        fixture.Context.ChannelId = 3;
        Assert.Contains("Invalid note", await fixture.RememberAsync(kind: "fact"));
        Assert.Contains("Invalid note", await fixture.RememberAsync(content: new string('a', 401)));
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Equal(0, fixture.Evaluator.Calls);
        Assert.Equal(3, fixture.Store.State.WriteAttempts.Count);
        Assert.All(fixture.Store.State.WriteAttempts, attempt => Assert.Equal(NotebookWriteOutcome.InvalidInput, attempt.Outcome));
    }

    [Fact]
    public async Task PausePersistsAndResumeAllowsWrites()
    {
        var fixture = new Fixture();
        await fixture.Service.SetPausedAsync(true, TestContext.Current.CancellationToken);
        Assert.Contains("paused", await fixture.RememberAsync());
        Assert.Equal(0, fixture.Evaluator.Calls);
        await fixture.Service.SetPausedAsync(false, TestContext.Current.CancellationToken);
        Assert.Contains("recorded", await fixture.RememberAsync());
    }

    [Fact]
    public async Task DiscardPreventsRetrievalAndResubmissionOfSameSource()
    {
        var fixture = new Fixture();
        await fixture.RememberAsync();
        var note = Assert.Single(fixture.Store.State.Notes);
        Assert.True(await fixture.Service.DiscardAsync(note.Id, TestContext.Current.CancellationToken));
        Assert.Contains("No accessible", await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken));
        Assert.Contains("already", await fixture.RememberAsync(content: "Rephrased raid anecdote"));
        Assert.False(await fixture.Service.DiscardAsync("missing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExactTextDuplicatesAreBlockedEvenWithDifferentSources()
    {
        var fixture = new Fixture();
        await fixture.RememberAsync();
        Assert.Contains("already", await fixture.RememberAsync(sourceId: 2));
        Assert.Single(fixture.Store.State.Notes);
        Assert.Equal(1, fixture.Evaluator.Calls);
    }

    [Fact]
    public async Task MemberDailyBudgetIncludesDiscardedNotesAndResetsAfter24Hours()
    {
        var fixture = new Fixture();
        for (var i = 1; i <= NotebookService.DailyMemberLimit; i++)
        {
            Assert.Contains("recorded", await fixture.RememberAsync(sourceId: i, content: $"Raid anecdote {i}"));
            await fixture.Service.DiscardAsync(fixture.Store.State.Notes[^1].Id, TestContext.Current.CancellationToken);
        }
        Assert.Contains("write limit", await fixture.RememberAsync(sourceId: 4, content: "Raid anecdote 4"));
        fixture.Clock.Now = fixture.Clock.Now.AddHours(25);
        Assert.Contains("recorded", await fixture.RememberAsync(sourceId: 5, content: "Raid anecdote 5"));
    }

    [Fact]
    public async Task GuildDailyBudgetIsSharedAcrossMembers()
    {
        var fixture = new Fixture();
        for (var i = 1; i <= NotebookService.DailyGuildLimit; i++)
        {
            fixture.Context.UserId = (ulong)i;
            Assert.Contains("recorded", await fixture.RememberAsync(sourceId: i, content: $"Raid anecdote {i}"));
        }
        fixture.Context.UserId = 100;
        Assert.Contains("write limit", await fixture.RememberAsync(sourceId: 11, content: "Raid anecdote 11"));
        Assert.Equal(NotebookService.DailyGuildLimit, fixture.Store.State.Notes.Count);
    }

    [Fact]
    public async Task ActiveAndUndeliveredReservationsAreBounded()
    {
        var fixture = new Fixture();
        var note = fixture.CreateNote();
        fixture.Store.State = new NotebookState
        {
            Notes = Enumerable
                .Range(1, NotebookService.MaximumActiveNotes)
                .Select(i => note with { Id = i.ToString(), CreatedAtUtc = fixture.Clock.Now.AddDays(-2), ReviewDelivered = i % 2 == 0 })
                .ToArray(),
        };
        Assert.Contains("full", await fixture.RememberAsync());
        Assert.Equal(0, fixture.Evaluator.Calls);
    }

    [Fact]
    public async Task ExpiredNotesAreHiddenAndPrunedByNextWrite()
    {
        var fixture = new Fixture();
        await fixture.RememberAsync();
        fixture.Clock.Now = fixture.Clock.Now.AddDays(30);
        Assert.Contains("No accessible", await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken));
        Assert.Contains("recorded", await fixture.RememberAsync(sourceId: 2, content: "New raid anecdote"));
        Assert.Single(fixture.Store.State.Notes);
    }

    [Fact]
    public async Task RetrievalRechecksAccessAndSuppressesEditedSources()
    {
        var fixture = new Fixture();
        await fixture.RememberAsync();
        fixture.Reader.Available = false;
        Assert.Contains("No accessible", await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken));
        fixture.Reader.Available = true;
        fixture.Reader.Content = "Source has been edited";
        Assert.Contains("No accessible", await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentPauseDuringEvaluationRejectsStaleWrite()
    {
        var fixture = new Fixture();
        fixture.Evaluator.OnEvaluate = () => fixture.Service.SetPausedAsync(true, TestContext.Current.CancellationToken);
        Assert.Contains("changed during review", await fixture.RememberAsync());
        Assert.True(fixture.Store.State.Paused);
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Empty(fixture.Reviewer.Notes);
        Assert.Equal(NotebookWriteOutcome.CommitRejected, Assert.Single(fixture.Store.State.WriteAttempts).Outcome);
    }

    [Fact]
    public async Task RetrievalSuppressesEditsBeyondStoredExcerpt()
    {
        var fixture = new Fixture();
        await fixture.RememberAsync();
        fixture.Reader.ContentHash = "Changed hash, same excerpt";
        Assert.Contains("No accessible", await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DiscardDuringDeliveryCannotBeUndoneByActivation()
    {
        var fixture = new Fixture();
        fixture.Reviewer.OnNotify = note => fixture.Service.DiscardAsync(note.Id, TestContext.Current.CancellationToken);
        await fixture.RememberAsync();
        var saved = Assert.Single(fixture.Store.State.Notes);
        Assert.True(saved.ReviewDelivered);
        Assert.NotNull(saved.DiscardedAtUtc);
        Assert.False(saved.IsActive(fixture.Clock.Now));
    }

    [Fact]
    public async Task CancellationIsNotSwallowed()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.RememberAsync("raid", "observation", "Raid note", "Useful", ["source"], fixture.Context, cancellation.Token)
        );
        Assert.Empty(fixture.Store.State.Notes);
    }

    [Fact]
    public async Task PendingDmNoteIsIncludedInConflictsBeforeEitherNoteActivates()
    {
        var fixture = new Fixture();
        fixture.Evaluator.Assess = (_, existing) => new NotebookAssessment(!existing.Any(note => note.Subject == "raid"), "Conflict check");
        string? secondResult = null;
        fixture.Reviewer.OnNotify = async note =>
        {
            if (note.Content == "Raid ended at 20:00")
                secondResult = await fixture.RememberAsync(sourceId: 2, content: "Raid ended at 21:00");
        };

        Assert.Contains("recorded", await fixture.RememberAsync(content: "Raid ended at 20:00"));
        Assert.Contains("Not saved", secondResult);
        var pending = Assert.Single(fixture.Evaluator.ExistingSnapshots[1]);
        Assert.False(pending.ReviewDelivered);
        Assert.Single(fixture.Store.State.Notes);
        Assert.Single(fixture.Reviewer.Notes);
    }

    [Fact]
    public async Task DiscardedMemoryIsIncludedAsNegativeFeedbackForParaphrasesWithNewSources()
    {
        var fixture = new Fixture();
        fixture.Evaluator.Assess = (_, existing) =>
            new NotebookAssessment(!existing.Any(note => note.DiscardedAtUtc is not null), "Previously discarded memory");
        await fixture.RememberAsync(content: "Raid ended at 20:00");
        await fixture.Service.DiscardAsync(fixture.Store.State.Notes[0].Id, TestContext.Current.CancellationToken);

        Assert.Contains("independent review rejected", await fixture.RememberAsync(sourceId: 2, content: "Raid finished at 20:00"));
        Assert.NotNull(Assert.Single(fixture.Evaluator.ExistingSnapshots[1]).DiscardedAtUtc);
        Assert.Single(fixture.Store.State.Notes);
        Assert.Single(fixture.Reviewer.Notes);
    }

    [Fact]
    public async Task RejectedEvaluationsConsumeMemberBudgetAndResetAfter24Hours()
    {
        var fixture = new Fixture();
        fixture.Evaluator.Assessment = new NotebookAssessment(false, "Unsubstantiated assertion");
        for (var i = 1; i <= NotebookService.DailyMemberEvaluationLimit; i++)
            Assert.Contains("Not saved", await fixture.RememberAsync(sourceId: i, content: $"Proposal {i}"));

        Assert.Contains("evaluation limit", await fixture.RememberAsync(sourceId: 4, content: "Proposal 4"));
        Assert.Equal(NotebookService.DailyMemberEvaluationLimit, fixture.Evaluator.Calls);
        Assert.Equal(NotebookService.DailyMemberEvaluationLimit, fixture.Store.State.EvaluationAttempts.Count);
        Assert.Empty(fixture.Store.State.Notes);

        fixture.Clock.Now = fixture.Clock.Now.AddHours(25);
        Assert.Contains("Not saved", await fixture.RememberAsync(sourceId: 5, content: "Proposal 5"));
        Assert.Equal(4, fixture.Evaluator.Calls);
        Assert.Single(fixture.Store.State.EvaluationAttempts);
    }

    [Fact]
    public async Task RejectedEvaluationsShareGuildBudgetAcrossMembers()
    {
        var fixture = new Fixture();
        fixture.Evaluator.Assessment = new NotebookAssessment(false, "Unsubstantiated assertion");
        for (var i = 1; i <= NotebookService.DailyGuildEvaluationLimit; i++)
            await fixture.RememberAsync(sourceId: i, content: $"Proposal {i}", userId: (ulong)i);

        Assert.Contains("evaluation limit", await fixture.RememberAsync(sourceId: 100, content: "Another proposal", userId: 100));
        Assert.Equal(NotebookService.DailyGuildEvaluationLimit, fixture.Evaluator.Calls);
        Assert.Equal(NotebookService.DailyGuildEvaluationLimit, fixture.Store.State.EvaluationAttempts.Count);
        Assert.Empty(fixture.Store.State.Notes);
    }

    [Fact]
    public async Task ConcurrentEvaluationsCannotExceedReservedMemberBudget()
    {
        var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Evaluator.Assessment = new NotebookAssessment(false, "Rejected");
        fixture.Evaluator.OnEvaluate = () => release.Task;

        var tasks = Enumerable.Range(1, 12).Select(i => fixture.RememberAsync(sourceId: i, content: $"Proposal {i}")).ToArray();
        Assert.Equal(NotebookService.DailyMemberEvaluationLimit, fixture.Evaluator.Calls);
        Assert.Equal(NotebookService.DailyMemberEvaluationLimit, fixture.Store.State.EvaluationAttempts.Count);
        release.SetResult();
        var results = await Task.WhenAll(tasks);
        Assert.Equal(9, results.Count(result => result.Contains("evaluation limit", StringComparison.Ordinal)));
        Assert.Empty(fixture.Store.State.Notes);
    }

    [Fact]
    public async Task FailedEvaluationStillConsumesAttemptBudget()
    {
        var fixture = new Fixture();
        fixture.Evaluator.OnEvaluate = () => throw new InvalidOperationException("AI service unavailable");
        Assert.Contains("failed", await fixture.RememberAsync());
        Assert.Single(fixture.Store.State.EvaluationAttempts);
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Empty(fixture.Reviewer.Notes);
        Assert.Equal(NotebookWriteOutcome.Failed, Assert.Single(fixture.Store.State.WriteAttempts).Outcome);
    }

    [Fact]
    public async Task CancellationDuringEvaluationDoesNotRefundAttempt()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Evaluator.OnEvaluate = () =>
        {
            cancellation.Cancel();
            return Task.FromCanceled(cancellation.Token);
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.RememberAsync(
                "raid",
                "observation",
                "Raid anecdote",
                "Useful",
                ["https://discord.com/channels/1/3/1"],
                fixture.Context,
                cancellation.Token
            )
        );
        Assert.Single(fixture.Store.State.EvaluationAttempts);
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Equal(NotebookWriteOutcome.Cancelled, Assert.Single(fixture.Store.State.WriteAttempts).Outcome);
    }

    [Fact]
    public async Task RejectionDoesNotExposeReviewerExplanationsDerivedFromOtherChannelAudiences()
    {
        var fixture = new Fixture();
        fixture.Evaluator.Assessment = new NotebookAssessment(false, "Conflicts with a restricted-source note: private quoted detail")
        {
            RejectionCategory = NotebookRejectionCategory.PrivacyOrSafety,
        };
        var result = await fixture.RememberAsync();
        Assert.DoesNotContain("private quoted detail", result);
        Assert.Contains("independent review rejected", result);
        var attempt = Assert.Single(fixture.Store.State.WriteAttempts);
        Assert.Equal(NotebookRejectionCategory.PrivacyOrSafety, attempt.RejectionCategory);
        Assert.DoesNotContain("private quoted detail", attempt.ToString());
    }

    [Fact]
    public async Task SearchCannotCheckEveryMissingMatchingMessage()
    {
        var fixture = new Fixture();
        fixture.Store.State = new NotebookState
        {
            Notes = Enumerable
                .Range(1, 100)
                .Select(i =>
                    fixture.CreateNote() with
                    {
                        Id = i.ToString(),
                        ReviewDelivered = true,
                        Sources = [new NotebookSource($"https://discord.com/channels/1/3/{i}", 42, "Member", fixture.Clock.Now, "Human source text")],
                    }
                )
                .ToArray(),
        };
        fixture.Reader.Available = false;

        var result = await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken);
        Assert.Contains("results may be incomplete", result);
        Assert.Equal(NotebookService.MaximumSearchCandidates, fixture.Reader.Calls);
    }

    [Fact]
    public async Task SearchRanksSubjectMatchesAheadOfIncidentalContentMatches()
    {
        var fixture = new Fixture();
        var source = new NotebookSource("https://discord.com/channels/1/3/1", 42, "Member", fixture.Clock.Now, fixture.Reader.Content)
        {
            ContentHash = fixture.Reader.ContentHash,
            ViewHash = fixture.Reader.ViewHash,
        };
        fixture.Store.State = new NotebookState
        {
            Notes =
            [
                .. Enumerable
                    .Range(1, 20)
                    .Select(i =>
                        fixture.CreateNote() with
                        {
                            Id = i.ToString(),
                            Subject = "Other event",
                            Content = "Raid was mentioned in passing",
                            ReviewDelivered = true,
                            Sources = [source],
                        }
                    ),
                fixture.CreateNote() with
                {
                    Id = "best-match",
                    ReviewDelivered = true,
                    CreatedAtUtc = fixture.Clock.Now.AddDays(-2),
                    Sources = [source],
                },
            ],
        };
        var result = await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken);
        Assert.Contains("best-match", result);
        Assert.Equal(NotebookService.MaximumSearchResults, fixture.Reader.Calls);
    }

    [Fact]
    public async Task SearchDeadlineCancelsBlockedSourceReadsWithoutClaimingNoNotesExist()
    {
        var fixture = new Fixture();
        fixture.Store.State = new NotebookState { Notes = [fixture.CreateNote() with { ReviewDelivered = true }] };
        fixture.Reader.BeforeRead = token => Task.Delay(Timeout.InfiniteTimeSpan, token);

        var search = fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken);
        Assert.False(search.IsCompleted);
        fixture.Clock.FireTimers();
        var result = await search;
        Assert.Contains("time limit", result);
        Assert.Contains("do not infer that no notes exist", result);
        Assert.Equal(1, fixture.Reader.Calls);
    }

    [Fact]
    public async Task CallerCancellationDuringSearchPropagatesInsteadOfBecomingTimeout()
    {
        var fixture = new Fixture();
        fixture.Store.State = new NotebookState { Notes = [fixture.CreateNote() with { ReviewDelivered = true }] };
        fixture.Reader.BeforeRead = token => Task.Delay(Timeout.InfiniteTimeSpan, token);
        using var cancellation = new CancellationTokenSource();
        var search = fixture.Service.SearchAsync("raid", fixture.Context, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search);
    }

    [Fact]
    public async Task CompleteLongSourceIsPassedToEvaluatorIncludingLateCorrection()
    {
        var fixture = new Fixture();
        fixture.Reader.Content = "Raid ended at 20:00. " + new string('x', 1000) + " Correction: this was a fictional example.";
        fixture.Evaluator.Assess = (candidate, _) =>
            new NotebookAssessment(!candidate.Sources.Any(source => source.Content.Contains("Correction")), "Corrected claim");
        Assert.Contains("independent review rejected", await fixture.RememberAsync());
        Assert.Contains("Correction", Assert.Single(fixture.Evaluator.Candidates).Sources[0].Content);
        Assert.Empty(fixture.Store.State.Notes);
    }

    [Fact]
    public async Task OversizedSourceCannotReachEvaluationEvenIfReaderReturnsIt()
    {
        var fixture = new Fixture();
        fixture.Reader.Content = new string('x', NotebookSource.MaximumContentLength + 1);
        Assert.Contains("complete text", await fixture.RememberAsync());
        Assert.Equal(0, fixture.Evaluator.Calls);
        Assert.Empty(fixture.Store.State.EvaluationAttempts);
    }

    [Fact]
    public async Task HiddenMatchesProduceSameSearchResponseAsEmptyNotebookWithoutNetworkReads()
    {
        var fixture = new Fixture();
        var empty = await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken);
        fixture.Store.State = new NotebookState
        {
            Notes = Enumerable.Range(1, 100).Select(i => fixture.CreateNote() with { Id = i.ToString(), ReviewDelivered = true }).ToArray(),
        };
        fixture.Reader.ChannelsAccessible = false;
        var hidden = await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken);
        Assert.Equal(empty, hidden);
        Assert.Equal(0, fixture.Reader.Calls);
    }

    [Fact]
    public async Task HiddenHighlyRankedMatchesDoNotCrowdOutReadableNotes()
    {
        var fixture = new Fixture();
        fixture.Store.State = new NotebookState
        {
            Notes =
            [
                .. Enumerable
                    .Range(1, 20)
                    .Select(i =>
                        fixture.CreateNote() with
                        {
                            Id = $"hidden-{i}",
                            ReviewDelivered = true,
                            Sources =
                            [
                                new NotebookSource($"https://discord.com/channels/1/4/{i}", 42, "Member", fixture.Clock.Now, fixture.Reader.Content)
                                {
                                    ContentHash = fixture.Reader.ContentHash,
                                    ViewHash = fixture.Reader.ViewHash,
                                },
                            ],
                        }
                    ),
                fixture.CreateNote() with
                {
                    Id = "readable",
                    Subject = "Other event",
                    Content = "Raid anecdote",
                    ReviewDelivered = true,
                },
            ],
        };
        foreach (var note in fixture.Store.State.Notes.Where(note => note.Id.StartsWith("hidden", StringComparison.Ordinal)))
            fixture.Reader.HiddenUrls.Add(note.Sources[0].Url);

        var result = await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken);
        Assert.Contains("readable", result);
        Assert.DoesNotContain("hidden-", result);
        Assert.Equal(1, fixture.Reader.Calls);
    }

    [Fact]
    public async Task EverySourceChannelMustBeAccessibleBeforeRankingOrReading()
    {
        var fixture = new Fixture();
        var readable = fixture.CreateNote().Sources[0];
        var hidden = readable with { Url = "https://discord.com/channels/1/4/2" };
        fixture.Reader.HiddenUrls.Add(hidden.Url);
        fixture.Store.State = new NotebookState { Notes = [fixture.CreateNote() with { ReviewDelivered = true, Sources = [readable, hidden] }] };
        Assert.Contains("No accessible", await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken));
        Assert.Equal(0, fixture.Reader.Calls);
    }

    [Fact]
    public async Task ChannelAccessIsStillRecheckedAfterMetadataFiltering()
    {
        var fixture = new Fixture();
        fixture.Store.State = new NotebookState { Notes = [fixture.CreateNote() with { ReviewDelivered = true }] };
        fixture.Reader.BeforeRead = _ =>
        {
            fixture.Reader.ChannelsAccessible = false;
            return Task.CompletedTask;
        };
        var result = await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken);
        Assert.Contains("No accessible", result);
        Assert.DoesNotContain("\"Content\"", result);
        Assert.Equal(1, fixture.Reader.Calls);
    }

    [Fact]
    public async Task RejectedUnrelatedReservationDoesNotInvalidateGoodReviewOrLoseItsAttempt()
    {
        var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Evaluator.BeforeEvaluate = (candidate, _) => candidate.Content == "Good first observation" ? release.Task : Task.CompletedTask;
        fixture.Evaluator.Assess = (candidate, _) => new NotebookAssessment(candidate.Content != "Rejected other proposal", "Independent verdict");
        var first = fixture.RememberAsync(content: "Good first observation");
        Assert.Contains("rejected", await fixture.RememberAsync(sourceId: 2, content: "Rejected other proposal"));
        release.SetResult();

        Assert.Contains("recorded", await first);
        Assert.Equal("Good first observation", Assert.Single(fixture.Store.State.Notes).Content);
        Assert.Equal(2, fixture.Store.State.EvaluationAttempts.Count);
        Assert.Equal(1, fixture.Store.State.ReviewRevision);
    }

    [Fact]
    public async Task CommittedNoteDuringReviewStillInvalidatesStaleAssessment()
    {
        var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Evaluator.BeforeEvaluate = (candidate, _) => candidate.Content == "First proposal" ? release.Task : Task.CompletedTask;
        var first = fixture.RememberAsync(content: "First proposal");
        Assert.Contains("recorded", await fixture.RememberAsync(sourceId: 2, content: "Second proposal"));
        release.SetResult();
        Assert.Contains("changed during review", await first);
        Assert.Equal("Second proposal", Assert.Single(fixture.Store.State.Notes).Content);
        Assert.Equal(2, fixture.Store.State.EvaluationAttempts.Count);
        Assert.Single(fixture.Reviewer.Notes);
    }

    [Fact]
    public async Task DiscardDuringReviewStillInvalidatesStaleAssessment()
    {
        var fixture = new Fixture();
        await fixture.RememberAsync(content: "Existing observation");
        fixture.Evaluator.OnEvaluate = () => fixture.Service.DiscardAsync(fixture.Store.State.Notes[0].Id, TestContext.Current.CancellationToken);
        Assert.Contains("changed during review", await fixture.RememberAsync(sourceId: 2, content: "New observation"));
        Assert.Single(fixture.Store.State.Notes);
        Assert.NotNull(fixture.Store.State.Notes[0].DiscardedAtUtc);
        Assert.Equal(2, fixture.Store.State.ReviewRevision);
    }

    [Fact]
    public async Task DeliveryProgressDoesNotInvalidateReviewOrGetOverwrittenByCommit()
    {
        var fixture = new Fixture();
        fixture.Store.State = new NotebookState { ReviewRevision = 1, Notes = [fixture.CreateNote() with { Id = "existing" }] };
        fixture.Evaluator.OnEvaluate = async () =>
        {
            var state = fixture.Store.State;
            await fixture.Store.TrySaveAsync(
                state with
                {
                    Notes = [state.Notes[0] with { ReviewDelivered = true }],
                },
                TestContext.Current.CancellationToken
            );
        };
        Assert.Contains("recorded", await fixture.RememberAsync(sourceId: 2, content: "New observation"));
        Assert.Equal(2, fixture.Store.State.Notes.Count);
        Assert.All(fixture.Store.State.Notes, note => Assert.True(note.ReviewDelivered));
        Assert.Equal(2, fixture.Store.State.ReviewRevision);
    }

    [Fact]
    public async Task CommitRetriesQuotaOnlyCasCollisionWithoutLosingAttemptsOrRepeatingAiReview()
    {
        var fixture = new Fixture();
        var injected = false;
        fixture.Store.BeforeSave = proposed =>
        {
            if (!injected && proposed.Notes.Count > fixture.Store.State.Notes.Count)
            {
                injected = true;
                var current = fixture.Store.State;
                fixture.Store.State = current with
                {
                    Revision = current.Revision + 1,
                    EvaluationAttempts = [.. current.EvaluationAttempts, new NotebookEvaluationAttempt(2, fixture.Clock.Now)],
                };
            }
            return Task.CompletedTask;
        };
        Assert.Contains("recorded", await fixture.RememberAsync());
        Assert.True(injected);
        Assert.Single(fixture.Store.State.Notes);
        Assert.Equal(2, fixture.Store.State.EvaluationAttempts.Count);
        Assert.Contains(fixture.Store.State.EvaluationAttempts, attempt => attempt.UserId == 2);
        Assert.Equal(1, fixture.Evaluator.Calls);
        Assert.Equal(1, fixture.Store.State.ReviewRevision);
    }

    [Fact]
    public async Task NoteChangeBetweenCommitReadAndWriteStillRejectsStaleReview()
    {
        var fixture = new Fixture();
        var injected = false;
        fixture.Store.BeforeSave = proposed =>
        {
            if (!injected && proposed.Notes.Count > fixture.Store.State.Notes.Count)
            {
                injected = true;
                var current = fixture.Store.State;
                fixture.Store.State = current with
                {
                    Revision = current.Revision + 1,
                    ReviewRevision = current.ReviewRevision + 1,
                    Notes = [fixture.CreateNote() with { Id = "concurrent-note", Content = "Other observation", ReviewDelivered = true }],
                };
            }
            return Task.CompletedTask;
        };
        Assert.Contains("changed during review", await fixture.RememberAsync());
        Assert.Equal("concurrent-note", Assert.Single(fixture.Store.State.Notes).Id);
        Assert.Empty(fixture.Reviewer.Notes);
        Assert.Equal(1, fixture.Evaluator.Calls);
    }

    [Fact]
    public async Task EvidenceBodiesAndMentionMappingsAreReviewedButNotArchived()
    {
        var fixture = new Fixture();
        fixture.Reader.Content = "Raid observation. Unrelated source conversation is not a notebook memory.";
        fixture.Reader.MentionedUsers = [new NotebookMentionedUser(42, "Member")];
        await fixture.RememberAsync();
        var evidence = Assert.Single(fixture.Evaluator.Candidates).Sources[0];
        Assert.Equal(fixture.Reader.Content, evidence.Content);
        Assert.Single(evidence.MentionedUsers);
        Assert.Equal(evidence.Content, Assert.Single(fixture.Reviewer.Notes).Sources[0].Content);
        var archived = Assert.Single(fixture.Store.State.Notes).Sources[0];
        Assert.Empty(archived.Content);
        Assert.Empty(archived.MentionedUsers);
        Assert.Equal(evidence.ContentHash, archived.ContentHash);
        Assert.Equal(evidence.ViewHash, archived.ViewHash);
        Assert.Contains("NOT canon", await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnchangedRawSourceWithChangedRedactedViewCannotExposeOldMemory()
    {
        var fixture = new Fixture();
        fixture.Reader.Content = "Raid conversation in #members-only";
        await fixture.RememberAsync();
        var archived = fixture.Store.State.Notes[0].Sources[0];
        fixture.Reader.ContentHash = archived.ContentHash;
        fixture.Reader.Content = "Raid conversation in [unavailable channel]";
        Assert.Equal(archived.ContentHash, fixture.Reader.ContentHash);
        Assert.NotEqual(archived.ViewHash, fixture.Reader.ViewHash);
        var result = await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken);
        Assert.Contains("No accessible", result);
        Assert.DoesNotContain("\"Content\"", result);
    }

    [Fact]
    public async Task MissingOrMalformedSourceChecksumCannotCreateUnverifiableMemory()
    {
        var fixture = new Fixture();
        fixture.Reader.ContentHash = "";
        Assert.Contains("Not saved", await fixture.RememberAsync());
        fixture.Reader.ContentHash = new string('z', 64);
        Assert.Contains("Not saved", await fixture.RememberAsync());
        Assert.Equal(0, fixture.Evaluator.Calls);
    }

    [Fact]
    public async Task ChangedIdentityLabelsAlsoInvalidateAnOtherwiseUnchangedSource()
    {
        var fixture = new Fixture();
        fixture.Reader.MentionedUsers = [new NotebookMentionedUser(123, "Dave")];
        await fixture.RememberAsync();
        fixture.Reader.MentionedUsers = [new NotebookMentionedUser(123, "Alice")];
        Assert.Contains("No accessible", await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ViewChecksumIgnoresMentionOrderingButDetectsLabelAndRedactionChanges()
    {
        NotebookMentionedUser[] mentions = [new(1, "Alice"), new(2, "Bob")];
        var hash = NotebookSource.CalculateViewHash("Evidence", "Member", mentions);
        Assert.Equal(hash, NotebookSource.CalculateViewHash("Evidence", "Member", mentions.Reverse().ToArray()));
        Assert.NotEqual(hash, NotebookSource.CalculateViewHash("Redacted", "Member", mentions));
        Assert.NotEqual(hash, NotebookSource.CalculateViewHash("Evidence", "Changed author", mentions));
    }

    [Fact]
    public async Task DiscardDuringLookupIsObservedBeforeAnyMemoryIsReturned()
    {
        var fixture = new Fixture();
        await fixture.RememberAsync();
        fixture.Reader.BeforeRead = _ => fixture.Service.DiscardAsync(fixture.Store.State.Notes[0].Id, TestContext.Current.CancellationToken);
        var result = await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken);
        Assert.Contains("No accessible", result);
        Assert.DoesNotContain("\"Content\"", result);
    }

    [Fact]
    public async Task ExpiryDuringLookupIsObservedBeforeAnyMemoryIsReturned()
    {
        var fixture = new Fixture();
        await fixture.RememberAsync();
        fixture.Clock.Now = fixture.Clock.Now.AddDays(30).AddSeconds(-1);
        fixture.Reader.BeforeRead = _ =>
        {
            fixture.Clock.Now = fixture.Clock.Now.AddSeconds(2);
            return Task.CompletedTask;
        };
        Assert.Contains("No accessible", await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PermissionChangesDuringFinalDatabaseReadAreCheckedBeforeReturn()
    {
        var fixture = new Fixture();
        await fixture.RememberAsync();
        var reads = 0;
        fixture.Store.BeforeRead = () =>
        {
            if (++reads == 2)
                fixture.Reader.ChannelsAccessible = false;
            return Task.CompletedTask;
        };
        Assert.Contains("No accessible", await fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken));
        Assert.Equal(1, fixture.Reader.Calls - 1); // The first read was admission, then one lookup verification.
    }

    [Fact]
    public async Task SourcesThatAgeOutDuringReviewAreNotCommitted()
    {
        var fixture = new Fixture();
        fixture.Reader.AgeDays = 7;
        fixture.Evaluator.OnEvaluate = () =>
        {
            fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
            return Task.CompletedTask;
        };
        Assert.Contains("expired before", await fixture.RememberAsync());
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Empty(fixture.Reviewer.Notes);
        Assert.Single(fixture.Store.State.EvaluationAttempts);
    }

    [Fact]
    public async Task WriteDeadlineStopsWaitingForUncooperativeEvaluatorAndCannotCommitLateResult()
    {
        var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Evaluator.OnEvaluate = () => release.Task;
        var remember = fixture.RememberAsync();
        Assert.False(remember.IsCompleted);
        fixture.Clock.FireTimers();
        Assert.Contains("two-minute time limit", await remember);
        release.SetResult();
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Empty(fixture.Reviewer.Notes);
        Assert.Single(fixture.Store.State.EvaluationAttempts);
        Assert.Equal(NotebookWriteOutcome.TimedOut, Assert.Single(fixture.Store.State.WriteAttempts).Outcome);
    }

    [Fact]
    public async Task WriteDeadlineDuringDeliveryLeavesMemoryAuditOnlyEvenIfDeliveryFinishesLater()
    {
        var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Reviewer.OnNotify = _ => release.Task;
        var remember = fixture.RememberAsync();
        Assert.Single(fixture.Store.State.Notes);
        fixture.Clock.FireTimers();
        Assert.Contains("time limit", await remember);
        release.SetResult();
        Assert.False(fixture.Store.State.Notes[0].ReviewDelivered);
        Assert.False(fixture.Store.State.Notes[0].IsActive(fixture.Clock.Now));
    }

    [Fact]
    public async Task SearchDeadlineBoundsEvenAReaderThatIgnoresCancellation()
    {
        var fixture = new Fixture();
        fixture.Store.State = new NotebookState { Notes = [fixture.CreateNote() with { ReviewDelivered = true }] };
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Reader.BeforeRead = _ => release.Task;
        var search = fixture.Service.SearchAsync("raid", fixture.Context, TestContext.Current.CancellationToken);
        fixture.Clock.FireTimers();
        Assert.Contains("time limit", await search);
        release.SetResult();
    }

    [Fact]
    public async Task DiagnosticHistoryKeepsOnlyTheNewestBoundedDayOfOutcomesWithoutChangingReviewRevision()
    {
        var fixture = new Fixture();
        fixture.Store.State = new NotebookState
        {
            ReviewRevision = 7,
            WriteAttempts =
            [
                new NotebookWriteAttempt(fixture.Clock.Now.AddDays(-1), NotebookWriteOutcome.Failed),
                .. Enumerable
                    .Range(1, NotebookService.MaximumDiagnosticAttempts)
                    .Select(i => new NotebookWriteAttempt(fixture.Clock.Now.AddSeconds(-i), NotebookWriteOutcome.InvalidInput)),
            ],
        };
        fixture.Reader.Available = false;
        Assert.Contains("verifiable human", await fixture.RememberAsync());
        Assert.Equal(NotebookService.MaximumDiagnosticAttempts, fixture.Store.State.WriteAttempts.Count);
        Assert.DoesNotContain(fixture.Store.State.WriteAttempts, attempt => attempt.Outcome == NotebookWriteOutcome.Failed);
        Assert.DoesNotContain(fixture.Store.State.WriteAttempts, attempt => attempt.CompletedAtUtc == fixture.Clock.Now.AddSeconds(-100));
        Assert.Contains(fixture.Store.State.WriteAttempts, attempt => attempt.CompletedAtUtc == fixture.Clock.Now.AddSeconds(-1));
        Assert.Equal(fixture.Store.State.WriteAttempts.OrderBy(attempt => attempt.CompletedAtUtc), fixture.Store.State.WriteAttempts);
        Assert.Equal(NotebookWriteOutcome.InvalidSources, fixture.Store.State.WriteAttempts[^1].Outcome);
        Assert.Equal(7, fixture.Store.State.ReviewRevision);
        Assert.Empty(fixture.Store.State.EvaluationAttempts);
    }

    [Fact]
    public async Task DiagnosticPersistenceFailureDoesNotTurnASavedNoteIntoAFailure()
    {
        var fixture = new Fixture();
        fixture.Store.BeforeSave = proposed =>
            proposed.WriteAttempts.Count > 0 ? throw new InvalidOperationException("Diagnostics unavailable") : Task.CompletedTask;
        Assert.Contains("recorded", await fixture.RememberAsync());
        Assert.True(Assert.Single(fixture.Store.State.Notes).ReviewDelivered);
        Assert.Single(fixture.Store.State.EvaluationAttempts);
        Assert.Empty(fixture.Store.State.WriteAttempts);
    }

    [Fact]
    public async Task DiagnosticDeadlineBoundsAnUncooperativeStoreWithoutChangingWriteSuccess()
    {
        var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.BeforeSave = proposed => proposed.WriteAttempts.Count > 0 ? release.Task : Task.CompletedTask;
        var remember = fixture.RememberAsync();
        Assert.False(remember.IsCompleted);
        Assert.True(Assert.Single(fixture.Store.State.Notes).ReviewDelivered);
        fixture.Clock.FireTimers();
        Assert.Contains("recorded", await remember);
        release.SetResult();
    }

    [Fact]
    public async Task DiagnosticUpdateRetriesCasConflictWithoutLosingConcurrentOutcomes()
    {
        var fixture = new Fixture();
        var injected = false;
        fixture.Store.BeforeSave = proposed =>
        {
            if (!injected && proposed.WriteAttempts.Count > 0)
            {
                injected = true;
                fixture.Store.State = fixture.Store.State with
                {
                    Revision = fixture.Store.State.Revision + 1,
                    WriteAttempts = [new NotebookWriteAttempt(fixture.Clock.Now, NotebookWriteOutcome.InvalidInput)],
                };
            }
            return Task.CompletedTask;
        };
        Assert.Contains("recorded", await fixture.RememberAsync());
        Assert.True(injected);
        Assert.Equal(2, fixture.Store.State.WriteAttempts.Count);
        Assert.Contains(fixture.Store.State.WriteAttempts, attempt => attempt.Outcome == NotebookWriteOutcome.InvalidInput);
        Assert.Contains(fixture.Store.State.WriteAttempts, attempt => attempt.Outcome == NotebookWriteOutcome.Saved);
        Assert.Equal(1, fixture.Store.State.ReviewRevision);
    }

    private sealed class Fixture
    {
        public TestClock Clock { get; } = new();
        public MemoryStore Store { get; } = new();
        public Reader Reader { get; }
        public Evaluator Evaluator { get; } = new();
        public Reviewer Reviewer { get; } = new();
        public MessageContext Context { get; } = new();
        public NotebookService Service { get; }

        public Fixture()
        {
            Reader = new Reader(Clock);
            Service = new NotebookService(Store, Reader, Evaluator, Reviewer, Clock, NullLogger<NotebookService>.Instance);
        }

        public Task<string> RememberAsync(
            int sourceId = 1,
            string content = "A specific raid anecdote",
            string kind = "observation",
            ulong? userId = null
        ) =>
            Service.RememberAsync(
                "raid",
                kind,
                content,
                "Useful raid anecdote",
                [$"https://discord.com/channels/1/3/{sourceId}"],
                userId is null ? Context : new MessageContext { UserId = userId.Value, ChannelId = Context.ChannelId },
                TestContext.Current.CancellationToken
            );

        public NotebookNote CreateNote() =>
            new(
                "id",
                1,
                "raid",
                "observation",
                "Raid note",
                "Useful",
                [
                    new NotebookSource("https://discord.com/channels/1/3/1", 42, "Member", Clock.Now, Reader.Content)
                    {
                        ContentHash = Reader.ContentHash,
                        ViewHash = Reader.ViewHash,
                    },
                ],
                Clock.Now,
                Clock.Now.AddDays(30)
            );
    }

    private sealed class TestClock : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];
        public DateTime Now { get; set; } = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

        public override DateTimeOffset GetUtcNow() => new(Now);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state);
            _timers.Add(timer);
            return timer;
        }

        public void FireTimers()
        {
            foreach (var timer in _timers.ToArray())
                timer.Fire();
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        private bool _disposed;

        public bool Change(TimeSpan dueTime, TimeSpan period) => !_disposed;

        public void Dispose() => _disposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void Fire()
        {
            if (!_disposed)
                callback(state);
        }
    }

    private sealed class MemoryStore : INotebookStore
    {
        public NotebookState State { get; set; } = new();
        public Func<NotebookState, Task>? BeforeSave { get; set; }
        public Func<Task>? BeforeRead { get; set; }

        public async Task<NotebookState> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (BeforeRead is not null)
                await BeforeRead();
            return State;
        }

        public async Task<bool> TrySaveAsync(NotebookState state, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (BeforeSave is not null)
                await BeforeSave(state);
            if (State.Revision != state.Revision)
                return false;
            State = state with { Revision = state.Revision + 1 };
            return true;
        }
    }

    private sealed class Reader(TestClock clock) : INotebookSourceReader
    {
        public int Calls { get; private set; }
        public Func<CancellationToken, Task>? BeforeRead { get; set; }
        public bool Available { get; set; } = true;
        public bool ChannelsAccessible { get; set; } = true;
        public HashSet<string> HiddenUrls { get; } = new(StringComparer.Ordinal);
        public int AgeDays { get; set; }
        public string Content { get; set; } = "Human source text";
        private string? _contentHashOverride;
        public string ContentHash
        {
            get => _contentHashOverride ?? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Content)));
            set => _contentHashOverride = value;
        }
        public IReadOnlyList<NotebookMentionedUser> MentionedUsers { get; set; } = [];
        public string ViewHash => NotebookSource.CalculateViewHash(Content, "Member", MentionedUsers);

        public IReadOnlySet<string> GetAccessibleSourceUrls(IReadOnlyList<string> urls, IMessageContext messageContext) =>
            urls.Where(url => ChannelsAccessible && !HiddenUrls.Contains(url)).ToHashSet(StringComparer.Ordinal);

        public async Task<IReadOnlyList<NotebookSource>?> ReadSourcesAsync(
            IReadOnlyList<string> urls,
            IMessageContext messageContext,
            CancellationToken cancellationToken
        )
        {
            Calls++;
            if (BeforeRead is not null)
                await BeforeRead(cancellationToken);
            return Available && ChannelsAccessible && urls.All(url => !HiddenUrls.Contains(url))
                ? urls.Select(url => new NotebookSource(url, 42, "Member", clock.Now.AddDays(-AgeDays), Content)
                    {
                        ContentHash = ContentHash,
                        ViewHash = ViewHash,
                        MentionedUsers = MentionedUsers,
                    })
                    .ToArray()
                : null;
        }
    }

    private sealed class Evaluator : INotebookEvaluator
    {
        public int Calls { get; private set; }
        public NotebookAssessment Assessment { get; set; } = new(true, "Supported by sources");
        public Func<Task>? OnEvaluate { get; set; }
        public Func<NotebookNote, IReadOnlyList<NotebookNote>, NotebookAssessment>? Assess { get; set; }
        public List<IReadOnlyList<NotebookNote>> ExistingSnapshots { get; } = [];
        public List<NotebookNote> Candidates { get; } = [];
        public Func<NotebookNote, IReadOnlyList<NotebookNote>, Task>? BeforeEvaluate { get; set; }

        public async Task<NotebookAssessment> EvaluateAsync(
            NotebookNote candidate,
            IReadOnlyList<NotebookNote> existing,
            CancellationToken cancellationToken
        )
        {
            Calls++;
            ExistingSnapshots.Add(existing);
            Candidates.Add(candidate);
            if (BeforeEvaluate is not null)
                await BeforeEvaluate(candidate, existing);
            if (OnEvaluate is not null)
                await OnEvaluate();
            return Assess?.Invoke(candidate, existing) ?? Assessment;
        }
    }

    private sealed class Reviewer : INotebookReviewer
    {
        public bool Deliver { get; set; } = true;
        public List<NotebookNote> Notes { get; } = [];
        public Func<NotebookNote, Task>? OnNotify { get; set; }

        public async Task<bool> NotifyAsync(NotebookNote note, CancellationToken cancellationToken)
        {
            Notes.Add(note);
            if (OnNotify is not null)
                await OnNotify(note);
            return Deliver;
        }
    }

    private sealed class MessageContext : IMessageContext
    {
        public ulong UserId { get; set; } = 1;
        public string UserName => "Member";
        public ulong? ChannelId { get; set; } = 3;

        public Task Reply(string message) => Task.CompletedTask;
    }
}
