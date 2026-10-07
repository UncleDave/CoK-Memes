using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class NotebookObserverServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task DiscoveryCanSaveZeroOrSeveralDistinctNotesWithoutChargingASpeaker(int candidateCount)
    {
        var fixture = new Fixture();
        fixture.Reader.SetMessages(4);
        fixture.Discoverer.Discover = (sources, _, _) =>
            Task.FromResult<IReadOnlyList<NotebookProposal>>(sources.Take(candidateCount).Select(Proposal).ToArray());

        await fixture.ObserveAsync();

        Assert.Single(fixture.Store.State.Observer.DiscoveryAttempts);
        Assert.Equal(candidateCount, fixture.Store.State.Notes.Count);
        Assert.Equal(candidateCount, fixture.Evaluator.Candidates.Count);
        Assert.Equal(candidateCount, fixture.Reviewer.Notes.Count);
        Assert.Equal(candidateCount, fixture.Store.State.EvaluationAttempts.Count);
        Assert.All(
            fixture.Store.State.Notes,
            note =>
            {
                Assert.Equal(NotebookOrigin.Background, note.Origin);
                Assert.Equal(0UL, note.RequestedBy);
                Assert.True(note.IsActive(fixture.Clock.Now));
                Assert.Equal(fixture.Clock.Now.AddDays(NotebookService.LifetimeDays), note.ExpiresAtUtc);
                Assert.All(
                    note.Sources,
                    source =>
                    {
                        Assert.Empty(source.Content);
                        Assert.Empty(source.MentionedUsers);
                        Assert.Equal(64, source.ContentHash.Length);
                        Assert.Equal(64, source.ViewHash.Length);
                    }
                );
            }
        );
        Assert.All(
            fixture.Store.State.EvaluationAttempts,
            attempt =>
            {
                Assert.Equal(NotebookOrigin.Background, attempt.Origin);
                Assert.Equal(0UL, attempt.UserId);
            }
        );
        Assert.All(fixture.Store.State.WriteAttempts, attempt => Assert.Equal(NotebookOrigin.Background, attempt.Origin));
        Assert.Equal(candidateCount, Assert.Single(fixture.Store.State.Observer.Scans).Saved);
        Assert.Equal(NotebookObservationOutcome.Completed, fixture.Store.State.Observer.Scans[0].Outcome);
        Assert.DoesNotContain("Human evidence", JsonSerializer.Serialize(fixture.Store.State.Observer));
        Assert.All(fixture.Reviewer.Notes, note => Assert.Contains("Human evidence", note.Sources[0].Content));
    }

    [Fact]
    public async Task RejectingOneCandidateDoesNotPreventUnrelatedNotesAndLaterReviewSeesEarlierAcceptedNote()
    {
        var fixture = new Fixture();
        fixture.Reader.SetMessages(3);
        fixture.Evaluator.Assess = (candidate, _) =>
            new NotebookAssessment(candidate.Content != Proposal(fixture.Reader.Messages[3][0].Source).Content, "Independent evidence review");

        await fixture.ObserveAsync();

        Assert.Equal(3, fixture.Evaluator.Candidates.Count);
        Assert.Empty(fixture.Evaluator.ExistingSnapshots[0]);
        Assert.Empty(fixture.Evaluator.ExistingSnapshots[1]);
        var prior = Assert.Single(fixture.Evaluator.ExistingSnapshots[2]);
        Assert.Equal(fixture.Evaluator.Candidates[1].Content, prior.Content);
        Assert.True(prior.ReviewDelivered);
        Assert.Equal(2, fixture.Store.State.Notes.Count);
        Assert.Equal(2, fixture.Reviewer.Notes.Count);
        Assert.Equal(3, fixture.Store.State.EvaluationAttempts.Count);
        Assert.Equal(
            [NotebookWriteOutcome.ReviewRejected, NotebookWriteOutcome.Saved, NotebookWriteOutcome.Saved],
            fixture.Store.State.WriteAttempts.Select(attempt => attempt.Outcome)
        );
    }

    [Fact]
    public async Task EmptyBatchSkipsDiscoveryAndReviewAndCooldownStartsAtCompletion()
    {
        var fixture = new Fixture();
        fixture.Reader.SetMessages(0);
        fixture.Reader.BeforeBatch = (_, _) =>
        {
            fixture.Clock.Advance(TimeSpan.FromMinutes(1));
            return Task.CompletedTask;
        };
        var started = fixture.Clock.Now;

        await fixture.ObserveAsync();

        Assert.Empty(fixture.Discoverer.Inputs);
        Assert.Empty(fixture.Store.State.Observer.DiscoveryAttempts);
        Assert.Empty(fixture.Store.State.EvaluationAttempts);
        Assert.Empty(fixture.Store.State.Notes);
        var scan = Assert.Single(fixture.Store.State.Observer.Scans);
        Assert.Equal(NotebookObservationOutcome.NoMessages, scan.Outcome);
        Assert.Equal(started, scan.StartedAtUtc);
        Assert.Equal(fixture.Clock.Now, fixture.Store.State.Observer.LastCompletedAtUtc);
        fixture.Clock.Advance(TimeSpan.FromMinutes(fixture.Options.CooldownMinutes - 1));
        Assert.False(await fixture.Observer.IsEligibleAsync(TestContext.Current.CancellationToken));
        await fixture.ObserveAsync();
        Assert.Single(fixture.Reader.Reads);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await fixture.CreateObserver().IsEligibleAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("paused")]
    [InlineData("cooldown")]
    [InlineData("active lease")]
    [InlineData("no channels")]
    [InlineData("discovery budget")]
    [InlineData("background review budget")]
    [InlineData("background note budget")]
    [InlineData("guild review budget")]
    [InlineData("guild note budget")]
    public async Task IneligibleObserverDoesNotReadDiscordOrCallModels(string blocker)
    {
        var fixture = new Fixture(new NotebookObserverOptions { Enabled = blocker != "disabled" });
        switch (blocker)
        {
            case "paused":
                await fixture.Notebook.SetPausedAsync(true, TestContext.Current.CancellationToken);
                break;
            case "cooldown":
                fixture.Store.Update(state => state with { Observer = state.Observer with { LastCompletedAtUtc = fixture.Clock.Now } });
                break;
            case "active lease":
                fixture.Store.Update(state =>
                    state with
                    {
                        Observer = state.Observer with { ActiveScanId = "other scan", LeaseExpiresAtUtc = fixture.Clock.Now.AddMinutes(1) },
                    }
                );
                break;
            case "no channels":
                fixture.Reader.ChannelIds.Clear();
                break;
            case "discovery budget":
                fixture.Store.Update(state =>
                    state with
                    {
                        Observer = state.Observer with
                        {
                            DiscoveryAttempts = Enumerable.Repeat(fixture.Clock.Now, fixture.Options.DailyDiscoveryLimit).ToArray(),
                        },
                    }
                );
                break;
            case "background review budget":
                fixture.SeedEvaluations(fixture.Options.DailyReviewLimit, NotebookOrigin.Background);
                break;
            case "background note budget":
                fixture.SeedNotes(fixture.Options.DailyNoteLimit, NotebookOrigin.Background);
                break;
            case "guild review budget":
                fixture.SeedEvaluations(NotebookService.DailyGuildEvaluationLimit, NotebookOrigin.Interactive);
                break;
            case "guild note budget":
                fixture.SeedNotes(NotebookService.DailyGuildLimit, NotebookOrigin.Interactive);
                break;
        }

        Assert.False(await fixture.Observer.IsEligibleAsync(TestContext.Current.CancellationToken));
        await fixture.ObserveAsync();

        Assert.Empty(fixture.Reader.Reads);
        Assert.Empty(fixture.Discoverer.Inputs);
        Assert.Empty(fixture.Evaluator.Candidates);
        Assert.Empty(fixture.Reviewer.Notes);
    }

    [Fact]
    public async Task EligibilityChecksDoNotReadBatchesOrSpendDiscoveryBudget()
    {
        var fixture = new Fixture();

        Assert.True(await fixture.Observer.IsEligibleAsync(TestContext.Current.CancellationToken));
        Assert.True(await fixture.CreateObserver().IsEligibleAsync(TestContext.Current.CancellationToken));

        Assert.Empty(fixture.Reader.Reads);
        Assert.Empty(fixture.Discoverer.Inputs);
        Assert.Empty(fixture.Store.State.Observer.DiscoveryAttempts);
        Assert.Null(fixture.Store.State.Observer.ActiveScanId);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("rejected")]
    [InlineData("partial write budget")]
    [InlineData("bounded input")]
    public async Task SuccessfulDiscoveryConsumesTheFetchedBatchAcrossObserverRecreation(string result)
    {
        var fixture = new Fixture(
            new NotebookObserverOptions
            {
                DailyNoteLimit = result == "partial write budget" ? 1 : 5,
                MaximumInputCharacters = result == "bounded input" ? 4000 : 20000,
            }
        );
        fixture.Reader.SetMessages(2, contentLength: result == "bounded input" ? 2200 : 80);
        if (result == "empty")
            fixture.Discoverer.Discover = (_, _, _) => Task.FromResult<IReadOnlyList<NotebookProposal>>([]);
        if (result == "rejected")
            fixture.Evaluator.Assess = (_, _) => new NotebookAssessment(false, "Not noteworthy");

        await fixture.ObserveAsync();

        var lastMessageId = fixture.Reader.Messages[3][^1].MessageId;
        Assert.Equal(lastMessageId, Assert.Single(fixture.Store.State.Observer.Checkpoints).LastMessageId);
        if (result == "partial write budget")
            Assert.Single(fixture.Store.State.Notes);
        if (result == "bounded input")
        {
            Assert.Single(Assert.Single(fixture.Discoverer.Inputs));
            Assert.True(JsonSerializer.Serialize(fixture.Discoverer.Inputs[0][0]).Length <= fixture.Options.MaximumInputCharacters);
        }
        var reviews = fixture.Store.State.EvaluationAttempts.Count;
        fixture.Clock.Advance(TimeSpan.FromMinutes(fixture.Options.CooldownMinutes));

        await fixture.CreateObserver().ObserveAsync(TestContext.Current.CancellationToken);

        if (result == "partial write budget")
            Assert.Single(fixture.Reader.Reads);
        else
        {
            Assert.Equal(lastMessageId, fixture.Reader.Reads[^1].AfterMessageId);
            Assert.Equal(NotebookObservationOutcome.NoMessages, fixture.Store.State.Observer.Scans[^1].Outcome);
        }
        Assert.Single(fixture.Discoverer.Inputs);
        Assert.Single(fixture.Store.State.Observer.DiscoveryAttempts);
        Assert.Equal(reviews, fixture.Store.State.EvaluationAttempts.Count);
    }

    [Fact]
    public async Task FailedDiscoveryCostsACallButDoesNotAdvanceCheckpointAndCanBeRetriedLater()
    {
        var fixture = new Fixture();
        fixture.Discoverer.Discover = (_, _, _) => throw new InvalidOperationException("Discovery unavailable");

        await fixture.ObserveAsync();

        Assert.Single(fixture.Store.State.Observer.DiscoveryAttempts);
        Assert.Empty(fixture.Store.State.Observer.Checkpoints);
        Assert.Empty(fixture.Store.State.EvaluationAttempts);
        Assert.Equal(NotebookObservationOutcome.DiscoveryFailed, Assert.Single(fixture.Store.State.Observer.Scans).Outcome);
        fixture.Clock.Advance(TimeSpan.FromMinutes(fixture.Options.CooldownMinutes));
        fixture.Discoverer.Discover = (_, _, _) => Task.FromResult<IReadOnlyList<NotebookProposal>>([]);

        await fixture.CreateObserver().ObserveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MessageId(fixture.Clock.Now.AddHours(-1)), fixture.Reader.Reads[^1].AfterMessageId);
        Assert.Equal(2, fixture.Store.State.Observer.DiscoveryAttempts.Count);
        Assert.Equal(fixture.Reader.Messages[3][^1].MessageId, Assert.Single(fixture.Store.State.Observer.Checkpoints).LastMessageId);
    }

    [Theory]
    [InlineData("background review")]
    [InlineData("guild review")]
    [InlineData("background note")]
    [InlineData("guild note")]
    public async Task BackgroundAndSharedGuildBudgetsBoundTheScanIndependently(string budget)
    {
        var fixture = new Fixture();
        fixture.Reader.SetMessages(2);
        switch (budget)
        {
            case "background review":
                fixture.SeedEvaluations(fixture.Options.DailyReviewLimit - 1, NotebookOrigin.Background);
                break;
            case "guild review":
                fixture.SeedEvaluations(NotebookService.DailyGuildEvaluationLimit - 1, NotebookOrigin.Interactive);
                break;
            case "background note":
                fixture.SeedNotes(fixture.Options.DailyNoteLimit - 1, NotebookOrigin.Background);
                break;
            case "guild note":
                fixture.SeedNotes(NotebookService.DailyGuildLimit - 1, NotebookOrigin.Interactive);
                break;
        }
        var before = fixture.Store.State;

        await fixture.ObserveAsync();

        Assert.Single(fixture.Store.State.Observer.DiscoveryAttempts);
        Assert.Single(fixture.Evaluator.Candidates);
        Assert.Single(fixture.Reviewer.Notes);
        Assert.Equal(before.EvaluationAttempts.Count + 1, fixture.Store.State.EvaluationAttempts.Count);
        Assert.Equal(before.Notes.Count + 1, fixture.Store.State.Notes.Count);
        Assert.Equal(NotebookOrigin.Background, fixture.Store.State.EvaluationAttempts[^1].Origin);
        Assert.Equal(0UL, fixture.Store.State.EvaluationAttempts[^1].UserId);
        Assert.Equal(0UL, fixture.Store.State.Notes[^1].RequestedBy);
        if (budget.EndsWith("review", StringComparison.Ordinal))
            Assert.Equal(1, Assert.Single(fixture.Discoverer.MaximumCandidates));
        Assert.False(await fixture.CreateObserver().IsEligibleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LastDiscoverySlotCanYieldNoCandidatesWithoutSpendingReviewOrNoteAllowance()
    {
        var fixture = new Fixture();
        fixture.Store.Update(state =>
            state with
            {
                Observer = state.Observer with
                {
                    DiscoveryAttempts = Enumerable.Repeat(fixture.Clock.Now, fixture.Options.DailyDiscoveryLimit - 1).ToArray(),
                },
            }
        );
        fixture.Discoverer.Discover = (_, _, _) => Task.FromResult<IReadOnlyList<NotebookProposal>>([]);

        await fixture.ObserveAsync();
        fixture.Clock.Advance(TimeSpan.FromMinutes(fixture.Options.CooldownMinutes));
        await fixture.CreateObserver().ObserveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(fixture.Options.DailyDiscoveryLimit, fixture.Store.State.Observer.DiscoveryAttempts.Count);
        Assert.Single(fixture.Reader.Reads);
        Assert.Single(fixture.Discoverer.Inputs);
        Assert.Empty(fixture.Store.State.EvaluationAttempts);
        Assert.Empty(fixture.Store.State.Notes);
    }

    [Fact]
    public async Task InteractiveMemberQuotasDoNotChargeThatMemberForBackgroundObservation()
    {
        var fixture = new Fixture();
        fixture.SeedNotes(NotebookService.DailyMemberLimit, NotebookOrigin.Interactive);
        fixture.SeedEvaluations(NotebookService.DailyMemberEvaluationLimit, NotebookOrigin.Interactive);
        fixture.Reader.SetMessages(4);

        await fixture.ObserveAsync();

        Assert.Equal(4, fixture.Evaluator.Candidates.Count);
        Assert.Equal(4, fixture.Reviewer.Notes.Count);
        Assert.Equal(3, fixture.Store.State.Notes.Count(note => note.Origin == NotebookOrigin.Interactive && note.RequestedBy == 42));
        Assert.Equal(
            3,
            fixture.Store.State.EvaluationAttempts.Count(attempt => attempt.Origin == NotebookOrigin.Interactive && attempt.UserId == 42)
        );
        Assert.Equal(4, fixture.Store.State.Notes.Count(note => note.Origin == NotebookOrigin.Background && note.RequestedBy == 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PauseDuringReadOrDiscoveryPreventsSavingAndCheckpointAdvance(bool duringDiscovery)
    {
        var fixture = new Fixture();
        if (duringDiscovery)
            fixture.Discoverer.Discover = async (sources, _, _) =>
            {
                await fixture.Notebook.SetPausedAsync(true, TestContext.Current.CancellationToken);
                return sources.Select(Proposal).ToArray();
            };
        else
            fixture.Reader.BeforeBatch = (_, _) => fixture.Notebook.SetPausedAsync(true, TestContext.Current.CancellationToken);

        await fixture.ObserveAsync();

        Assert.True(fixture.Store.State.Paused);
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Empty(fixture.Reviewer.Notes);
        Assert.Empty(fixture.Store.State.EvaluationAttempts);
        Assert.Empty(fixture.Store.State.Observer.Checkpoints);
        Assert.Equal(duringDiscovery ? 1 : 0, fixture.Store.State.Observer.DiscoveryAttempts.Count);
        Assert.Equal(NotebookObservationOutcome.Paused, Assert.Single(fixture.Store.State.Observer.Scans).Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PermissionRemovalBeforeModelCallFiltersEvidenceEvenIfDiscoveryWasAlreadyReserved(bool duringReservation)
    {
        var fixture = new Fixture();
        fixture.Reader.SetMessages(1, 4);
        if (duringReservation)
            fixture.Store.BeforeSave = proposed =>
            {
                if (proposed.Observer.DiscoveryAttempts.Count > fixture.Store.State.Observer.DiscoveryAttempts.Count)
                    fixture.Reader.ChannelIds.Remove(3);
                return Task.CompletedTask;
            };
        else
            fixture.Reader.BeforeBatch = (channelId, _) =>
            {
                if (channelId == 3)
                    fixture.Reader.ChannelIds.Remove(3);
                return Task.CompletedTask;
            };

        await fixture.ObserveAsync();

        var observed = Assert.Single(Assert.Single(fixture.Discoverer.Inputs));
        Assert.Contains("/4/", observed.Url);
        Assert.Equal(4UL, Assert.Single(fixture.Store.State.Observer.Checkpoints).ChannelId);
        Assert.Equal(observed.Url, Assert.Single(fixture.Store.State.Notes).Sources[0].Url);
    }

    [Theory]
    [InlineData("deleted")]
    [InlineData("permission revoked")]
    public async Task CandidateEvidenceIsReverifiedAfterDiscoveryAndMissingOrRestrictedSourcesCannotBeSaved(string change)
    {
        var fixture = new Fixture();
        fixture.Discoverer.Discover = (sources, _, _) =>
        {
            if (change == "deleted")
                fixture.Reader.MissingUrls.Add(sources[0].Url);
            else
                fixture.Reader.ChannelIds.Remove(3);
            return Task.FromResult<IReadOnlyList<NotebookProposal>>(sources.Select(Proposal).ToArray());
        };

        await fixture.ObserveAsync();

        Assert.Single(fixture.Reader.EvidenceReads);
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Empty(fixture.Store.State.EvaluationAttempts);
        Assert.Empty(fixture.Evaluator.Candidates);
        Assert.Empty(fixture.Reviewer.Notes);
        Assert.Single(fixture.Store.State.Observer.DiscoveryAttempts);
        Assert.Equal(NotebookWriteOutcome.InvalidSources, Assert.Single(fixture.Store.State.WriteAttempts).Outcome);
    }

    [Fact]
    public async Task ConcurrentScansUseAnAtomicLeaseAndOnlyOneDiscoveryCall()
    {
        var fixture = new Fixture();
        var beginSaves = 0;
        var bothStarting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.BeforeSave = async proposed =>
        {
            if (proposed.Observer.ActiveScanId is not null && fixture.Store.State.Observer.ActiveScanId is null)
            {
                if (Interlocked.Increment(ref beginSaves) == 2)
                    bothStarting.SetResult();
                await bothStarting.Task;
            }
        };
        var discoveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<IReadOnlyList<NotebookProposal>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Discoverer.Discover = (_, _, _) =>
        {
            discoveryStarted.SetResult();
            return release.Task;
        };

        var first = fixture.ObserveAsync();
        var second = fixture.CreateObserver().ObserveAsync(TestContext.Current.CancellationToken);
        await discoveryStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Single(fixture.Discoverer.Inputs);
        Assert.Single(fixture.Store.State.Observer.DiscoveryAttempts);
        Assert.False(await fixture.CreateObserver().IsEligibleAsync(TestContext.Current.CancellationToken));
        release.SetResult([]);
        await Task.WhenAll(first, second);

        Assert.Single(fixture.Reader.Reads);
        Assert.Single(fixture.Store.State.Observer.Scans);
        Assert.Null(fixture.Store.State.Observer.ActiveScanId);
    }

    [Fact]
    public async Task DiscoveryReservationRetriesCasCollisionWithoutExceedingTheDailyQuota()
    {
        var fixture = new Fixture(new NotebookObserverOptions { DailyDiscoveryLimit = 1 });
        var injected = false;
        fixture.Store.BeforeSave = proposed =>
        {
            if (!injected && proposed.Observer.DiscoveryAttempts.Count > fixture.Store.State.Observer.DiscoveryAttempts.Count)
            {
                injected = true;
                fixture.Store.Update(state => state with { Observer = state.Observer with { DiscoveryAttempts = [fixture.Clock.Now] } });
            }
            return Task.CompletedTask;
        };

        await fixture.ObserveAsync();

        Assert.True(injected);
        Assert.Single(fixture.Store.State.Observer.DiscoveryAttempts);
        Assert.Empty(fixture.Discoverer.Inputs);
        Assert.Empty(fixture.Store.State.Observer.Checkpoints);
        Assert.Equal(NotebookObservationOutcome.BudgetExhausted, Assert.Single(fixture.Store.State.Observer.Scans).Outcome);
    }

    [Fact]
    public async Task BackgroundReviewReservationRetriesCasCollisionWithoutOverspendingOrOverwritingTheQuota()
    {
        var fixture = new Fixture(new NotebookObserverOptions { DailyReviewLimit = 1 });
        var injected = false;
        fixture.Store.BeforeSave = proposed =>
        {
            if (!injected && proposed.EvaluationAttempts.Count > fixture.Store.State.EvaluationAttempts.Count)
            {
                injected = true;
                fixture.Store.Update(state =>
                    state with
                    {
                        EvaluationAttempts = [new NotebookEvaluationAttempt(0, fixture.Clock.Now) { Origin = NotebookOrigin.Background }],
                    }
                );
            }
            return Task.CompletedTask;
        };

        await fixture.ObserveAsync();

        Assert.True(injected);
        Assert.Single(fixture.Store.State.Observer.DiscoveryAttempts);
        Assert.Single(fixture.Store.State.EvaluationAttempts);
        Assert.Empty(fixture.Evaluator.Candidates);
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Equal(NotebookWriteOutcome.WriteBlocked, Assert.Single(fixture.Store.State.WriteAttempts).Outcome);
        Assert.Equal(fixture.Reader.Messages[3][^1].MessageId, Assert.Single(fixture.Store.State.Observer.Checkpoints).LastMessageId);
    }

    [Fact]
    public async Task ScanDeadlineStopsWaitingForUncooperativeDiscoveryAndCannotSaveItsLateResult()
    {
        var fixture = new Fixture();
        var release = new TaskCompletionSource<IReadOnlyList<NotebookProposal>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Discoverer.Discover = (_, _, _) => release.Task;

        var observation = fixture.ObserveAsync();
        Assert.Single(fixture.Discoverer.Inputs);
        Assert.False(observation.IsCompleted);
        fixture.Clock.Advance(TimeSpan.FromSeconds(fixture.Options.ScanTimeoutSeconds));
        await observation.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(NotebookObservationOutcome.TimedOut, Assert.Single(fixture.Store.State.Observer.Scans).Outcome);
        Assert.Single(fixture.Store.State.Observer.DiscoveryAttempts);
        Assert.Empty(fixture.Store.State.Observer.Checkpoints);
        release.SetResult([Proposal(fixture.Reader.Messages[3][0].Source)]);
        await release.Task;
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Empty(fixture.Evaluator.Candidates);
        Assert.Empty(fixture.Reviewer.Notes);
        Assert.Null(fixture.Store.State.Observer.ActiveScanId);
    }

    [Fact]
    public async Task CallerCancellationDuringDiscoveryPropagatesAndRetainsTheChargedBudget()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        var release = new TaskCompletionSource<IReadOnlyList<NotebookProposal>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Discoverer.Discover = (_, _, _) => release.Task;

        var observation = fixture.Observer.ObserveAsync(cancellation.Token);
        Assert.Single(fixture.Discoverer.Inputs);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => observation);

        Assert.Equal(NotebookObservationOutcome.Cancelled, Assert.Single(fixture.Store.State.Observer.Scans).Outcome);
        Assert.Single(fixture.Store.State.Observer.DiscoveryAttempts);
        Assert.Empty(fixture.Store.State.Observer.Checkpoints);
        Assert.Empty(fixture.Store.State.EvaluationAttempts);
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Null(fixture.Store.State.Observer.ActiveScanId);
        release.SetResult([]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationOrScanDeadlineDuringReviewKeepsBothChargesWithoutCommittingALateVerdict(bool callerCancellation)
    {
        var fixture = new Fixture(new NotebookObserverOptions { ScanTimeoutSeconds = 30 });
        using var cancellation = new CancellationTokenSource();
        var release = new TaskCompletionSource<NotebookAssessment>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writeRecorded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.BeforeSave = proposed =>
        {
            if (proposed.WriteAttempts.Any(attempt => attempt.Outcome == NotebookWriteOutcome.Cancelled))
                writeRecorded.TrySetResult();
            return Task.CompletedTask;
        };
        fixture.Evaluator.Evaluate = (_, _, _) => release.Task;

        var observation = fixture.Observer.ObserveAsync(cancellation.Token);
        Assert.Single(fixture.Evaluator.Candidates);
        if (callerCancellation)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => observation);
        }
        else
        {
            fixture.Clock.Advance(TimeSpan.FromSeconds(fixture.Options.ScanTimeoutSeconds));
            await observation.WaitAsync(TestContext.Current.CancellationToken);
        }
        await writeRecorded.Task.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Single(fixture.Store.State.Observer.DiscoveryAttempts);
        Assert.Single(fixture.Store.State.EvaluationAttempts);
        Assert.Equal(
            callerCancellation ? NotebookObservationOutcome.Cancelled : NotebookObservationOutcome.TimedOut,
            Assert.Single(fixture.Store.State.Observer.Scans).Outcome
        );
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Empty(fixture.Reviewer.Notes);
        release.SetResult(new NotebookAssessment(true, "Late verdict"));
        await release.Task;
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Empty(fixture.Reviewer.Notes);
    }

    [Fact]
    public async Task FailedAdminDeliveryLeavesAuditOnlyNoteAndStopsFurtherCandidates()
    {
        var fixture = new Fixture();
        fixture.Reader.SetMessages(2);
        fixture.Reviewer.Deliver = false;

        await fixture.ObserveAsync();

        var note = Assert.Single(fixture.Store.State.Notes);
        Assert.False(note.ReviewDelivered);
        Assert.False(note.IsActive(fixture.Clock.Now));
        Assert.Single(fixture.Store.State.EvaluationAttempts);
        Assert.Single(fixture.Reviewer.Notes);
        Assert.Equal(NotebookWriteOutcome.ReviewDeliveryFailed, Assert.Single(fixture.Store.State.WriteAttempts).Outcome);
        Assert.Equal(NotebookObservationOutcome.Interrupted, Assert.Single(fixture.Store.State.Observer.Scans).Outcome);
    }

    [Fact]
    public async Task ExpiredLeaseCanBeRecoveredAndOldScanCannotClearTheNewLeaseOrCommit()
    {
        var fixture = new Fixture();
        var oldResult = new TaskCompletionSource<IReadOnlyList<NotebookProposal>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newResult = new TaskCompletionSource<IReadOnlyList<NotebookProposal>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Discoverer.Discover = (_, _, _) => fixture.Discoverer.Inputs.Count == 1 ? oldResult.Task : newResult.Task;

        var oldScan = fixture.ObserveAsync();
        var oldId = fixture.Store.State.Observer.ActiveScanId;
        fixture.Clock.Advance(TimeSpan.FromSeconds(fixture.Options.ScanTimeoutSeconds + 1), fireTimers: false);
        var recovered = fixture.CreateObserver();
        Assert.True(await recovered.IsEligibleAsync(TestContext.Current.CancellationToken));
        var newScan = recovered.ObserveAsync(TestContext.Current.CancellationToken);
        var newId = fixture.Store.State.Observer.ActiveScanId;
        Assert.NotNull(newId);
        Assert.NotEqual(oldId, newId);
        Assert.Equal(2, fixture.Discoverer.Inputs.Count);

        oldResult.SetResult([Proposal(fixture.Reader.Messages[3][0].Source)]);
        await oldScan;

        Assert.Equal(newId, fixture.Store.State.Observer.ActiveScanId);
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Empty(fixture.Store.State.Observer.Checkpoints);
        Assert.False(newScan.IsCompleted);
        newResult.SetResult([Proposal(fixture.Reader.Messages[3][0].Source)]);
        await newScan;
        Assert.Single(fixture.Store.State.Notes);
        Assert.Equal(2, fixture.Store.State.Observer.DiscoveryAttempts.Count);
        Assert.Null(fixture.Store.State.Observer.ActiveScanId);
    }

    [Fact]
    public async Task ExpiredLeaseDuringReviewCannotCommitOrDeliverWhileReplacementScanIsActive()
    {
        var fixture = new Fixture();
        var oldReview = new TaskCompletionSource<NotebookAssessment>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newDiscovery = new TaskCompletionSource<IReadOnlyList<NotebookProposal>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Evaluator.Evaluate = (_, _, _) =>
            fixture.Evaluator.Candidates.Count == 1 ? oldReview.Task : Task.FromResult(new NotebookAssessment(true, "Supported new incident"));
        var oldScan = fixture.ObserveAsync();
        Assert.Single(fixture.Evaluator.Candidates);
        var oldId = fixture.Store.State.Observer.ActiveScanId;
        fixture.Clock.Advance(TimeSpan.FromSeconds(fixture.Options.ScanTimeoutSeconds + 1), fireTimers: false);
        fixture.Reader.SetMessages(1);
        fixture.Discoverer.Discover = (_, _, _) => newDiscovery.Task;
        var replacement = fixture.CreateObserver().ObserveAsync(TestContext.Current.CancellationToken);
        var newId = fixture.Store.State.Observer.ActiveScanId;
        Assert.NotEqual(oldId, newId);
        oldReview.SetResult(new NotebookAssessment(true, "Late old verdict"));
        await oldScan;
        Assert.Equal(newId, fixture.Store.State.Observer.ActiveScanId);
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Empty(fixture.Reviewer.Notes);
        newDiscovery.SetResult([Proposal(fixture.Reader.Messages[3][0].Source)]);
        await replacement;
        Assert.Single(fixture.Store.State.Notes);
        Assert.Single(fixture.Reviewer.Notes);
    }

    [Fact]
    public async Task LostLeaseDuringReviewReservationCannotChargeOrStartTheOldReview()
    {
        var fixture = new Fixture();
        var replaced = false;
        fixture.Store.BeforeSave = proposed =>
        {
            if (!replaced && proposed.EvaluationAttempts.Count > fixture.Store.State.EvaluationAttempts.Count)
            {
                replaced = true;
                fixture.Store.Update(state =>
                    state with
                    {
                        Observer = state.Observer with { ActiveScanId = "replacement", LeaseExpiresAtUtc = fixture.Clock.Now.AddMinutes(1) },
                    }
                );
            }
            return Task.CompletedTask;
        };
        await fixture.ObserveAsync();
        Assert.True(replaced);
        Assert.Empty(fixture.Store.State.EvaluationAttempts);
        Assert.Empty(fixture.Evaluator.Candidates);
        Assert.Empty(fixture.Store.State.Notes);
        Assert.Empty(fixture.Reviewer.Notes);
        Assert.Equal("replacement", fixture.Store.State.Observer.ActiveScanId);
    }

    [Fact]
    public async Task LostLeaseDuringDeliveryCannotActivateTheOldNote()
    {
        var fixture = new Fixture();
        fixture.Reviewer.Notify = (_, _) =>
        {
            fixture.Store.Update(state =>
                state with
                {
                    Observer = state.Observer with { ActiveScanId = "replacement", LeaseExpiresAtUtc = fixture.Clock.Now.AddMinutes(1) },
                }
            );
            return Task.FromResult(true);
        };
        await fixture.ObserveAsync();
        var note = Assert.Single(fixture.Store.State.Notes);
        Assert.False(note.ReviewDelivered);
        Assert.False(note.IsActive(fixture.Clock.Now));
        Assert.Equal("replacement", fixture.Store.State.Observer.ActiveScanId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PauseDuringDeliveryKeepsTheBackgroundNoteAuditOnlyEvenAfterResume(bool resumeBeforeDeliveryFinishes)
    {
        var fixture = new Fixture();
        fixture.Reviewer.Notify = async (_, _) =>
        {
            await fixture.Notebook.SetPausedAsync(true, TestContext.Current.CancellationToken);
            if (resumeBeforeDeliveryFinishes)
                await fixture.Notebook.SetPausedAsync(false, TestContext.Current.CancellationToken);
            return true;
        };
        await fixture.ObserveAsync();
        var note = Assert.Single(fixture.Store.State.Notes);
        Assert.False(note.ReviewDelivered);
        Assert.False(note.IsActive(fixture.Clock.Now));
        Assert.Equal(NotebookWriteOutcome.CommitRejected, Assert.Single(fixture.Store.State.WriteAttempts).Outcome);
        Assert.Equal(0, Assert.Single(fixture.Store.State.Observer.Scans).Saved);
    }

    [Fact]
    public async Task FailedChannelsDoNotStarveHealthyChannelsOnLaterScans()
    {
        var fixture = new Fixture();
        foreach (var channelId in new ulong[] { 4, 5, 6, 7 })
            fixture.Reader.SetMessages(1, channelId);
        fixture.Reader.BeforeBatch = (channelId, _) =>
            channelId < 7 ? throw new InvalidOperationException("Temporarily inaccessible") : Task.CompletedTask;
        await fixture.ObserveAsync();
        fixture.Clock.Advance(TimeSpan.FromMinutes(fixture.Options.CooldownMinutes));
        await fixture.CreateObserver().ObserveAsync(TestContext.Current.CancellationToken);
        Assert.Contains(fixture.Reader.Reads, read => read.ChannelId == 7);
        Assert.Contains(fixture.Discoverer.Inputs.SelectMany(sources => sources), source => source.Url.Contains("/7/", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.Store.State.Observer.Checkpoints, checkpoint => checkpoint.ChannelId < 7);
    }

    [Fact]
    public async Task SharedInputBudgetDoesNotAlwaysFavorTheFirstChannel()
    {
        var fixture = new Fixture();
        fixture.Reader.SetMessages(20, 3, contentLength: 3000);
        fixture.Reader.SetMessages(20, 4, contentLength: 3000);
        fixture.Discoverer.Discover = (_, _, _) => Task.FromResult<IReadOnlyList<NotebookProposal>>([]);
        await fixture.ObserveAsync();
        fixture.Clock.Advance(TimeSpan.FromMinutes(fixture.Options.CooldownMinutes));
        fixture.Reader.SetMessages(20, 3, contentLength: 3000);
        fixture.Reader.SetMessages(20, 4, contentLength: 3000);
        await fixture.CreateObserver().ObserveAsync(TestContext.Current.CancellationToken);
        Assert.All(
            fixture.Discoverer.Inputs,
            sources =>
            {
                Assert.Contains(sources, source => source.Url.Contains("/3/", StringComparison.Ordinal));
                Assert.Contains(sources, source => source.Url.Contains("/4/", StringComparison.Ordinal));
            }
        );
    }

    [Theory]
    [InlineData(4000, false)]
    [InlineData(20000, true)]
    public async Task FirstInputPriorityRotatesWithinDifferentSelectionCohorts(int inputBudget, bool unicode)
    {
        var fixture = new Fixture(new NotebookObserverOptions { MaximumInputCharacters = inputBudget });
        fixture.Discoverer.Discover = (_, _, _) => Task.FromResult<IReadOnlyList<NotebookProposal>>([]);
        for (var scan = 0; scan < 8; scan++)
        {
            foreach (var channelId in Enumerable.Range(3, 8).Select(id => (ulong)id))
            {
                fixture.Reader.SetMessages(1, channelId, contentLength: 2200);
                if (unicode)
                {
                    var message = fixture.Reader.Messages[channelId][0];
                    var content = new string('界', 2000);
                    fixture.Reader.Messages[channelId] =
                    [
                        message with
                        {
                            Source = message.Source with
                            {
                                Content = content,
                                ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))),
                                ViewHash = NotebookSource.CalculateViewHash(content, message.Source.AuthorName, message.Source.MentionedUsers),
                            },
                        },
                    ];
                }
            }
            await fixture.CreateObserver().ObserveAsync(TestContext.Current.CancellationToken);
            fixture.Clock.Advance(TimeSpan.FromMinutes(fixture.Options.CooldownMinutes));
        }
        Assert.All(fixture.Discoverer.Inputs, sources => Assert.Single(sources));
        var observed = fixture
            .Discoverer.Inputs.SelectMany(sources => sources)
            .Select(source => ulong.Parse(source.Url.Split('/')[5]))
            .Distinct()
            .Order()
            .ToArray();
        Assert.Equal(Enumerable.Range(3, 8).Select(id => (ulong)id), observed);
    }

    [Fact]
    public async Task FailedChannelReadDoesNotPreventDiscoveryFromOtherChannelsOrConsumeTheFailedChannel()
    {
        var fixture = new Fixture();
        fixture.Reader.SetMessages(1, 4);
        fixture.Reader.BeforeBatch = (channelId, _) =>
            channelId == 3 ? throw new InvalidOperationException("Discord read unavailable") : Task.CompletedTask;

        await fixture.ObserveAsync();

        Assert.Equal(4UL, Assert.Single(fixture.Store.State.Observer.Checkpoints).ChannelId);
        Assert.Contains("/4/", Assert.Single(Assert.Single(fixture.Discoverer.Inputs)).Url);
        Assert.Single(fixture.Store.State.Notes);
        Assert.Equal(1, Assert.Single(fixture.Store.State.Observer.Scans).ReadFailures);
    }

    [Fact]
    public async Task BoundedChannelSelectionRotatesTowardChannelsNotYetScanned()
    {
        var fixture = new Fixture(new NotebookObserverOptions { MaximumChannelsPerScan = 2, MaximumMessagesPerChannel = 2 });
        foreach (var channelId in new ulong[] { 3, 4, 5, 6 })
            fixture.Reader.SetMessages(3, channelId);
        fixture.Discoverer.Discover = (_, _, _) => Task.FromResult<IReadOnlyList<NotebookProposal>>([]);

        await fixture.ObserveAsync();
        fixture.Clock.Advance(TimeSpan.FromMinutes(fixture.Options.CooldownMinutes));
        await fixture.CreateObserver().ObserveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new ulong[] { 3, 4, 5, 6 }, fixture.Reader.Reads.Select(read => read.ChannelId));
        Assert.All(fixture.Reader.Reads, read => Assert.Equal(2, read.Limit));
        Assert.All(fixture.Discoverer.Inputs, sources => Assert.Equal(4, sources.Count));
        Assert.Equal(4, fixture.Store.State.Observer.Checkpoints.Count);
    }

    [Fact]
    public async Task FreshObserverLimitsCatchUpToOneHourAndDoesNotSendOldOrFutureSourcesToDiscovery()
    {
        var fixture = new Fixture();
        var recent = fixture.Reader.Messages[3][0];
        var older = fixture.Reader.CreateMessage(2, 3, fixture.Clock.Now.AddMinutes(-61));
        var future = fixture.Reader.CreateMessage(3, 3, fixture.Clock.Now.AddMinutes(1));
        fixture.Reader.Messages[3] = [older, recent, future];

        await fixture.ObserveAsync();

        Assert.Equal(MessageId(fixture.Clock.Now.AddHours(-1)), Assert.Single(fixture.Reader.Reads).AfterMessageId);
        Assert.Equal(recent.Source.Url, Assert.Single(Assert.Single(fixture.Discoverer.Inputs)).Url);
        Assert.Equal(recent.Source.Url, Assert.Single(fixture.Store.State.Notes).Sources[0].Url);
    }

    private static ulong MessageId(DateTime utc) => (ulong)(new DateTimeOffset(utc).ToUnixTimeMilliseconds() - 1420070400000L) << 22;

    private static NotebookProposal Proposal(NotebookSource source) =>
        new(
            "Raid incident",
            "observation",
            $"Member reported raid incident {source.Url.Split('/')[^1]}",
            "A specific firsthand report",
            [source.Url]
        );

    private sealed class Fixture
    {
        public TestClock Clock { get; } = new();
        public MemoryStore Store { get; } = new();
        public BackgroundReader Reader { get; }
        public Discoverer Discoverer { get; } = new();
        public Evaluator Evaluator { get; } = new();
        public Reviewer Reviewer { get; } = new();
        public NotebookObserverOptions Options { get; }
        public NotebookService Notebook { get; }
        public NotebookObserverService Observer { get; }

        public Fixture(NotebookObserverOptions? options = null)
        {
            Options = options ?? new NotebookObserverOptions();
            Reader = new BackgroundReader(Clock);
            Reader.SetMessages(1);
            Notebook = new NotebookService(
                Store,
                new InteractiveReader(),
                Evaluator,
                Reviewer,
                Clock,
                NullLogger<NotebookService>.Instance,
                Reader,
                Microsoft.Extensions.Options.Options.Create(Options)
            );
            Observer = CreateObserver();
        }

        public NotebookObserverService CreateObserver() =>
            new(
                Store,
                Notebook,
                Reader,
                Discoverer,
                Microsoft.Extensions.Options.Options.Create(Options),
                Clock,
                NullLogger<NotebookObserverService>.Instance
            );

        public Task ObserveAsync() => Observer.ObserveAsync(TestContext.Current.CancellationToken);

        public void SeedEvaluations(int count, NotebookOrigin origin) =>
            Store.Update(state =>
                state with
                {
                    EvaluationAttempts = Enumerable
                        .Range(1, count)
                        .Select(index => new NotebookEvaluationAttempt(
                            origin == NotebookOrigin.Background ? 0UL : 42UL + (ulong)((index - 1) / NotebookService.DailyMemberEvaluationLimit),
                            Clock.Now
                        )
                        {
                            Origin = origin,
                        })
                        .ToArray(),
                }
            );

        public void SeedNotes(int count, NotebookOrigin origin) =>
            Store.Update(state =>
                state with
                {
                    Notes = Enumerable
                        .Range(1, count)
                        .Select(index => new NotebookNote(
                            $"seed-{index}",
                            origin == NotebookOrigin.Background ? 0UL : 42UL + (ulong)((index - 1) / NotebookService.DailyMemberLimit),
                            "Existing incident",
                            "observation",
                            $"Existing distinct incident {index}",
                            "Already reviewed",
                            [Reader.CreateMessage(index, 99, Clock.Now.AddMinutes(-10)).Source with { Content = string.Empty, MentionedUsers = [] }],
                            Clock.Now,
                            Clock.Now.AddDays(30)
                        )
                        {
                            Origin = origin,
                            ReviewDelivered = true,
                        })
                        .ToArray(),
                }
            );
    }

    private sealed class MemoryStore : INotebookStore
    {
        private readonly object _gate = new();
        private NotebookState _state = new();
        public Func<NotebookState, Task>? BeforeSave { get; set; }

        public NotebookState State
        {
            get
            {
                lock (_gate)
                    return _state;
            }
        }

        public void Update(Func<NotebookState, NotebookState> update)
        {
            lock (_gate)
                _state = update(_state) with { Revision = _state.Revision + 1 };
        }

        public Task<NotebookState> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(State);
        }

        public async Task<bool> TrySaveAsync(NotebookState state, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (BeforeSave is not null)
                await BeforeSave(state);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (state.Revision != _state.Revision)
                    return false;
                _state = state with { Revision = state.Revision + 1 };
                return true;
            }
        }
    }

    private sealed class BackgroundReader(TestClock clock) : INotebookBackgroundSourceReader
    {
        public HashSet<ulong> ChannelIds { get; } = [];
        public Dictionary<ulong, IReadOnlyList<NotebookObservationMessage>> Messages { get; } = [];
        public HashSet<string> MissingUrls { get; } = new(StringComparer.Ordinal);
        public List<(ulong ChannelId, ulong AfterMessageId, int Limit)> Reads { get; } = [];
        public List<IReadOnlyList<string>> EvidenceReads { get; } = [];
        public Func<ulong, CancellationToken, Task>? BeforeBatch { get; set; }

        public void SetMessages(int count, ulong channelId = 3, int contentLength = 80)
        {
            ChannelIds.Add(channelId);
            Messages[channelId] = Enumerable
                .Range(1, count)
                .Select(index => CreateMessage(index, channelId, clock.Now.AddMinutes(-5), contentLength))
                .ToArray();
        }

        public NotebookObservationMessage CreateMessage(int index, ulong channelId, DateTime timestamp, int contentLength = 80)
        {
            var messageId = MessageId(timestamp) + (ulong)index;
            var content = $"Human evidence for distinct raid incident {index}. ".PadRight(contentLength, 'x');
            NotebookMentionedUser[] mentions = [new(42, "Member")];
            return new NotebookObservationMessage(
                messageId,
                new NotebookSource($"https://discord.com/channels/1/{channelId}/{messageId}", 42, "Member", timestamp, content)
                {
                    ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))),
                    ViewHash = NotebookSource.CalculateViewHash(content, "Member", mentions),
                    MentionedUsers = mentions,
                }
            );
        }

        public IReadOnlyList<ulong> GetChannelIds() => ChannelIds.Order().ToArray();

        public async Task<NotebookObservationBatch?> ReadBatchAsync(
            ulong channelId,
            ulong afterMessageId,
            int limit,
            CancellationToken cancellationToken
        )
        {
            Reads.Add((channelId, afterMessageId, limit));
            if (BeforeBatch is not null)
                await BeforeBatch(channelId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var messages = Messages[channelId]
                .Where(message => message.MessageId > afterMessageId)
                .OrderBy(message => message.MessageId)
                .Take(limit)
                .ToArray();
            return new NotebookObservationBatch(channelId, messages.Length == 0 ? afterMessageId : messages[^1].MessageId, messages);
        }

        public Task<IReadOnlyList<NotebookSource>?> ReadSourcesAsync(IReadOnlyList<string> urls, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EvidenceReads.Add(urls.ToArray());
            var available = Messages
                .Where(pair => ChannelIds.Contains(pair.Key))
                .SelectMany(pair => pair.Value)
                .Select(message => message.Source)
                .Where(source => !MissingUrls.Contains(source.Url))
                .ToDictionary(source => source.Url, StringComparer.Ordinal);
            return Task.FromResult<IReadOnlyList<NotebookSource>?>(
                urls.All(available.ContainsKey) ? urls.Select(url => available[url]).ToArray() : null
            );
        }
    }

    private sealed class InteractiveReader : INotebookSourceReader
    {
        public IReadOnlySet<string> GetAccessibleSourceUrls(IReadOnlyList<string> urls, IMessageContext messageContext) =>
            throw new InvalidOperationException("Background observation must not use requester-oriented source access");

        public Task<IReadOnlyList<NotebookSource>?> ReadSourcesAsync(
            IReadOnlyList<string> urls,
            IMessageContext messageContext,
            CancellationToken cancellationToken
        ) => throw new InvalidOperationException("Background observation must verify evidence through the background reader");
    }

    private sealed class Discoverer : INotebookDiscoverer
    {
        public List<IReadOnlyList<NotebookSource>> Inputs { get; } = [];
        public List<int> MaximumCandidates { get; } = [];
        public Func<IReadOnlyList<NotebookSource>, int, CancellationToken, Task<IReadOnlyList<NotebookProposal>>>? Discover { get; set; }

        public Task<IReadOnlyList<NotebookProposal>> DiscoverAsync(
            IReadOnlyList<NotebookSource> sources,
            int maximumCandidates,
            CancellationToken cancellationToken
        )
        {
            Inputs.Add(sources.ToArray());
            MaximumCandidates.Add(maximumCandidates);
            return Discover?.Invoke(sources, maximumCandidates, cancellationToken)
                ?? Task.FromResult<IReadOnlyList<NotebookProposal>>(sources.Take(maximumCandidates).Select(Proposal).ToArray());
        }
    }

    private sealed class Evaluator : INotebookEvaluator
    {
        public List<NotebookNote> Candidates { get; } = [];
        public List<IReadOnlyList<NotebookNote>> ExistingSnapshots { get; } = [];
        public Func<NotebookNote, IReadOnlyList<NotebookNote>, NotebookAssessment>? Assess { get; set; }
        public Func<NotebookNote, IReadOnlyList<NotebookNote>, CancellationToken, Task<NotebookAssessment>>? Evaluate { get; set; }

        public Task<NotebookAssessment> EvaluateAsync(
            NotebookNote candidate,
            IReadOnlyList<NotebookNote> existing,
            CancellationToken cancellationToken
        )
        {
            Candidates.Add(candidate);
            ExistingSnapshots.Add(existing.ToArray());
            return Evaluate?.Invoke(candidate, existing, cancellationToken)
                ?? Task.FromResult(Assess?.Invoke(candidate, existing) ?? new NotebookAssessment(true, "Supported by verified human sources"));
        }
    }

    private sealed class Reviewer : INotebookReviewer
    {
        public List<NotebookNote> Notes { get; } = [];
        public bool Deliver { get; set; } = true;
        public Func<NotebookNote, CancellationToken, Task<bool>>? Notify { get; set; }

        public Task<bool> NotifyAsync(NotebookNote note, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Notes.Add(note);
            return Notify?.Invoke(note, cancellationToken) ?? Task.FromResult(Deliver);
        }
    }

    private sealed class TestClock : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];
        public DateTime Now { get; private set; } = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        public override DateTimeOffset GetUtcNow() => new(Now);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, dueTime);
            lock (_timers)
                _timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan elapsed, bool fireTimers = true)
        {
            Now += elapsed;
            if (!fireTimers)
                return;
            ManualTimer[] timers;
            lock (_timers)
                timers = _timers.ToArray();
            foreach (var timer in timers)
                timer.FireIfDue();
        }
    }

    private sealed class ManualTimer(TestClock clock, TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
    {
        private bool _disposed;
        private DateTime? _dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.Now + dueTime;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed)
                return false;
            _dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock.Now + dueTime;
            return true;
        }

        public void FireIfDue()
        {
            if (_disposed || _dueAt is null || _dueAt > clock.Now)
                return;
            _dueAt = null;
            callback(state);
        }

        public void Dispose() => _disposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
