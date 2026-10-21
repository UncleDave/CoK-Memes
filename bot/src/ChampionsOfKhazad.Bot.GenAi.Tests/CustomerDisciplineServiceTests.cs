namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class CustomerDisciplineServiceTests
{
    [Fact]
    public async Task ReadingMissingCustomerReturnsUnbarredStateWithoutCreatingARecord()
    {
        var fixture = new Fixture();

        var state = await fixture.Service.GetAsync(1, 2, TestContext.Current.CancellationToken);

        Assert.Equal("1:2", state.Id);
        Assert.Equal(0, state.Revision);
        Assert.Empty(state.Strikes);
        Assert.Equal(0UL, state.LastPardonMessageId);
        Assert.False(state.IsBarred);
        Assert.Null(fixture.Store.Snapshot("1:2"));
        Assert.Equal(0, fixture.Store.SaveAttempts);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void StateDerivesBarredStatusFromStrikeCount(int strikeCount, bool barred)
    {
        var state = CustomerDisciplineState.Create(1, 2) with
        {
            Strikes = Enumerable.Range(1, strikeCount).Select(index => new CustomerStrike((ulong)index, "Reason", DateTime.UnixEpoch)).ToArray(),
        };

        Assert.Equal("1:2", state.Id);
        Assert.Equal(barred, state.IsBarred);
    }

    [Fact]
    public async Task FirstSecondAndThirdStrikesPersistAndFurtherStrikesCannotExceedTheCap()
    {
        var fixture = new Fixture();
        for (var index = 1; index <= 3; index++)
        {
            fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
            var state = AssertSuccess(await fixture.StrikeAsync((ulong)index, $"Reason {index}"), changed: true);

            Assert.Equal(index, state.Strikes.Length);
            Assert.Equal(index == 3, state.IsBarred);
            Assert.Equal(new CustomerStrike((ulong)index, $"Reason {index}", fixture.Clock.Now.UtcDateTime), state.Strikes[^1]);
            Assert.Equal(index, fixture.Store.Snapshot("1:2")!.Revision);
        }

        var capped = AssertSuccess(await fixture.StrikeAsync(4, "Must not be recorded"), changed: false);

        Assert.True(capped.IsBarred);
        Assert.Equal(new ulong[] { 1, 2, 3 }, capped.Strikes.Select(strike => strike.MessageId));
        Assert.Equal(3, fixture.Store.SaveAttempts);
        Assert.Equal(3, fixture.Store.Snapshot("1:2")!.Revision);
    }

    [Fact]
    public async Task ReasonsAreTrimmedBeforeValidationAndTwoHundredCharactersAreAccepted()
    {
        var fixture = new Fixture();
        var reason = new string('a', 200);

        var state = AssertSuccess(await fixture.StrikeAsync(1, " \t" + reason + "\r\n "), changed: true);

        Assert.Equal(reason, Assert.Single(state.Strikes).Reason);
        Assert.Equal(DateTimeKind.Utc, state.Strikes[0].AtUtc.Kind);
        Assert.Equal(reason, Assert.Single(fixture.Store.Snapshot("1:2")!.Strikes).Reason);
    }

    [Fact]
    public async Task CustomersAreIsolatedByBothGuildAndUserIncludingDuplicateMessagesAndPardons()
    {
        var fixture = new Fixture();
        var cancellation = TestContext.Current.CancellationToken;
        AssertSuccess(await fixture.Service.IssueStrikeAsync(1, 2, 10, "First customer", cancellation), changed: true);
        AssertSuccess(await fixture.Service.IssueStrikeAsync(1, 3, 10, "Other user", cancellation), changed: true);
        AssertSuccess(await fixture.Service.IssueStrikeAsync(2, 2, 10, "Other guild", cancellation), changed: true);

        AssertSuccess(await fixture.Service.PardonAsync(1, 2, 20, cancellation), changed: true);

        Assert.Empty((await fixture.Service.GetAsync(1, 2, cancellation)).Strikes);
        Assert.Equal("Other user", Assert.Single((await fixture.Service.GetAsync(1, 3, cancellation)).Strikes).Reason);
        Assert.Equal("Other guild", Assert.Single((await fixture.Service.GetAsync(2, 2, cancellation)).Strikes).Reason);
        Assert.Equal(0UL, (await fixture.Service.GetAsync(1, 3, cancellation)).LastPardonMessageId);
        Assert.Equal(0UL, (await fixture.Service.GetAsync(2, 2, cancellation)).LastPardonMessageId);
        Assert.Empty((await fixture.Service.GetAsync(2, 3, cancellation)).Strikes);
    }

    [Fact]
    public async Task RecreatedServiceReadsDurableStrikesRatherThanInstanceState()
    {
        var fixture = new Fixture();
        await fixture.StrikeAsync(10, "First reason");
        fixture.Clock.Now = fixture.Clock.Now.AddHours(1);
        await fixture.StrikeAsync(20, "Second reason");
        var expected = fixture.Store.Snapshot("1:2")!;
        var restarted = new CustomerDisciplineService(fixture.Store, new TestClock());

        var state = await restarted.GetAsync(1, 2, TestContext.Current.CancellationToken);

        AssertState(expected, state);
        Assert.Equal(2, fixture.Store.SaveAttempts);
        AssertSuccess(await restarted.IssueStrikeAsync(1, 2, 30, "Third reason", TestContext.Current.CancellationToken), changed: true);
        Assert.True((await fixture.Service.GetAsync(1, 2, TestContext.Current.CancellationToken)).IsBarred);
    }

    [Fact]
    public async Task DuplicateStrikeCannotReplaceItsReasonTimestampOrRevision()
    {
        var fixture = new Fixture();
        await fixture.StrikeAsync(10, "Original reason");
        var original = fixture.Store.Snapshot("1:2")!;
        fixture.Clock.Now = fixture.Clock.Now.AddDays(1);

        var state = AssertSuccess(await fixture.StrikeAsync(10, "Replacement reason"), changed: false);

        AssertState(original, state);
        AssertState(original, fixture.Store.Snapshot("1:2")!);
        Assert.Equal(1, fixture.Store.SaveAttempts);
    }

    [Fact]
    public async Task PardonClearsAllStrikesUnbarsCustomerAndPersistsItsMessageBarrier()
    {
        var fixture = new Fixture();
        for (ulong messageId = 1; messageId <= 3; messageId++)
            await fixture.StrikeAsync(messageId, "Reason");

        var state = AssertSuccess(await fixture.PardonAsync(100), changed: true);

        Assert.Empty(state.Strikes);
        Assert.False(state.IsBarred);
        Assert.Equal(100UL, state.LastPardonMessageId);
        var restarted = new CustomerDisciplineService(fixture.Store, fixture.Clock);
        var persisted = await restarted.GetAsync(1, 2, TestContext.Current.CancellationToken);
        Assert.Empty(persisted.Strikes);
        Assert.False(persisted.IsBarred);
        Assert.Equal(100UL, persisted.LastPardonMessageId);
        Assert.Equal(4, persisted.Revision);
    }

    [Theory]
    [InlineData(99UL)]
    [InlineData(100UL)]
    public async Task PardonCannotClearAStrikeFromItsOwnMessageOrANewerMessage(ulong pardonMessageId)
    {
        var fixture = new Fixture();
        await fixture.StrikeAsync(100, "New cheek");

        var state = AssertSuccess(await fixture.PardonAsync(pardonMessageId), changed: false);

        Assert.Equal(100UL, Assert.Single(state.Strikes).MessageId);
        Assert.Equal(0UL, state.LastPardonMessageId);
        Assert.Equal(1, fixture.Store.SaveAttempts);
    }

    [Theory]
    [InlineData(99UL)]
    [InlineData(100UL)]
    public async Task StrikesAtOrBeforeThePardonMessageCannotRecreateClearedStrikes(ulong messageId)
    {
        var fixture = new Fixture();
        await fixture.StrikeAsync(10, "Original reason");
        await fixture.PardonAsync(100);
        var original = fixture.Store.Snapshot("1:2")!;
        var restarted = new CustomerDisciplineService(fixture.Store, fixture.Clock);

        var state = AssertSuccess(
            await restarted.IssueStrikeAsync(1, 2, messageId, "Stale reason", TestContext.Current.CancellationToken),
            changed: false
        );

        AssertState(original, state);
        Assert.Equal(2, fixture.Store.SaveAttempts);
    }

    [Fact]
    public async Task NewMessagesCanAccumulateThreeFreshStrikesAfterPardon()
    {
        var fixture = new Fixture();
        await fixture.StrikeAsync(10, "Cleared reason");
        await fixture.PardonAsync(100);

        for (ulong messageId = 101; messageId <= 103; messageId++)
            AssertSuccess(await fixture.StrikeAsync(messageId, "New reason"), changed: true);

        var state = await fixture.Service.GetAsync(1, 2, TestContext.Current.CancellationToken);
        Assert.True(state.IsBarred);
        Assert.Equal(new ulong[] { 101, 102, 103 }, state.Strikes.Select(strike => strike.MessageId));
        Assert.Equal(100UL, state.LastPardonMessageId);
        Assert.Equal(5, state.Revision);
    }

    [Theory]
    [InlineData(50UL)]
    [InlineData(100UL)]
    public async Task OlderOrDuplicatePardonCannotClearNewStrikesOrMoveTheBarrierBackwards(ulong messageId)
    {
        var fixture = new Fixture();
        await fixture.StrikeAsync(10, "Old reason");
        await fixture.PardonAsync(100);
        await fixture.StrikeAsync(150, "New reason");
        var original = fixture.Store.Snapshot("1:2")!;

        var state = AssertSuccess(await fixture.PardonAsync(messageId), changed: false);

        AssertState(original, state);
        AssertState(original, fixture.Store.Snapshot("1:2")!);
        Assert.Equal(3, fixture.Store.SaveAttempts);
    }

    [Fact]
    public async Task PardoningCustomerWithoutStrikesSavesAndAdvancesTheBarrierWithoutClaimingStrikesWereCleared()
    {
        var fixture = new Fixture();

        var empty = AssertSuccess(await fixture.PardonAsync(50), changed: false);

        Assert.Empty(empty.Strikes);
        Assert.Equal(50UL, empty.LastPardonMessageId);
        Assert.Equal(50UL, fixture.Store.Snapshot("1:2")!.LastPardonMessageId);
        Assert.Equal(1, fixture.Store.SaveAttempts);
        await fixture.StrikeAsync(60, "Reason");
        await fixture.PardonAsync(100);
        var state = AssertSuccess(await fixture.PardonAsync(200), changed: false);

        Assert.Empty(state.Strikes);
        Assert.Equal(200UL, state.LastPardonMessageId);
        Assert.Equal(4, fixture.Store.SaveAttempts);
        AssertSuccess(await fixture.StrikeAsync(150, "Stale reason"), changed: false);
        AssertSuccess(await fixture.StrikeAsync(201, "Fresh reason"), changed: true);
    }

    [Fact]
    public async Task PardonOnEmptyRecordPreventsAnOlderInFlightStrikeFromCreatingARecord()
    {
        var fixture = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.BeforeSave = (state, cancellation) =>
        {
            if (state.Strikes.Length > 0)
            {
                entered.TrySetResult();
                return release.Task.WaitAsync(cancellation);
            }
            return Task.CompletedTask;
        };
        var strike = fixture.StrikeAsync(100, "Old cheek");
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            AssertSuccess(await fixture.PardonAsync(200), changed: false);
        }
        finally
        {
            release.TrySetResult();
        }

        var state = AssertSuccess(await strike, changed: false);
        Assert.Empty(state.Strikes);
        Assert.Equal(200UL, state.LastPardonMessageId);
    }

    [Fact]
    public async Task InFlightPardonCannotClearNewMisconductAfterTheAppeal()
    {
        var fixture = new Fixture();
        await fixture.StrikeAsync(10, "Original cheek");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.BeforeSave = (state, cancellation) =>
        {
            if (state.LastPardonMessageId == 100)
            {
                entered.TrySetResult();
                return release.Task.WaitAsync(cancellation);
            }
            return Task.CompletedTask;
        };
        var pardon = fixture.PardonAsync(100);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            AssertSuccess(await fixture.StrikeAsync(150, "New cheek"), changed: true);
        }
        finally
        {
            release.TrySetResult();
        }

        var state = AssertSuccess(await pardon, changed: false);
        Assert.Equal(new ulong[] { 10, 150 }, state.Strikes.Select(strike => strike.MessageId));
        Assert.Equal(0UL, state.LastPardonMessageId);
    }

    [Fact]
    public async Task CompetingServicesRetryCompareAndSwapWithoutLosingStrikesOrExceedingThree()
    {
        var fixture = new Fixture();
        var barrier = new SaveBarrier(12);
        fixture.Store.BeforeSave = (state, cancellation) => state.Revision == 0 ? barrier.ArriveAsync(cancellation) : Task.CompletedTask;

        var results = await Task.WhenAll(
            Enumerable
                .Range(1, 12)
                .Select(index =>
                    new CustomerDisciplineService(fixture.Store, fixture.Clock).IssueStrikeAsync(
                        1,
                        2,
                        (ulong)index,
                        $"Reason {index}",
                        TestContext.Current.CancellationToken
                    )
                )
        );

        Assert.Equal(3, results.Count(result => result.Changed));
        Assert.All(results, result => AssertSuccess(result, result.Changed));
        var state = fixture.Store.Snapshot("1:2")!;
        Assert.True(state.IsBarred);
        Assert.Equal(3, state.Strikes.Length);
        Assert.Equal(3, state.Strikes.Select(strike => strike.MessageId).Distinct().Count());
        Assert.Equal(3, state.Revision);
        Assert.True(fixture.Store.Collisions >= 11);
        foreach (var result in results.Where(result => result.Changed))
            Assert.Contains(state.Strikes, strike => strike == result.State!.Strikes[^1]);
    }

    [Fact]
    public async Task CompetingDuplicateMessagesProduceExactlyOneStrike()
    {
        var fixture = new Fixture();
        var barrier = new SaveBarrier(8);
        fixture.Store.BeforeSave = (state, cancellation) => state.Revision == 0 ? barrier.ArriveAsync(cancellation) : Task.CompletedTask;

        var results = await Task.WhenAll(
            Enumerable
                .Range(1, 8)
                .Select(index =>
                    new CustomerDisciplineService(fixture.Store, fixture.Clock).IssueStrikeAsync(
                        1,
                        2,
                        10,
                        $"Reason {index}",
                        TestContext.Current.CancellationToken
                    )
                )
        );

        var winner = Assert.Single(results, result => result.Changed);
        Assert.All(results, result => AssertSuccess(result, result.Changed));
        var state = fixture.Store.Snapshot("1:2")!;
        Assert.Equal(Assert.Single(winner.State!.Strikes), Assert.Single(state.Strikes));
        Assert.Equal(10UL, state.Strikes[0].MessageId);
        Assert.Equal(1, state.Revision);
        Assert.Equal(7, fixture.Store.Collisions);
    }

    [Theory]
    [InlineData(150UL, false)]
    [InlineData(250UL, true)]
    public async Task InFlightStrikeRechecksThePardonBarrierAfterCompareAndSwapConflict(ulong messageId, bool changed)
    {
        var fixture = new Fixture();
        await fixture.StrikeAsync(10, "Original reason");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.BeforeSave = (state, cancellation) =>
        {
            if (state.Revision == 1 && state.Strikes.Any(strike => strike.MessageId == messageId))
            {
                entered.TrySetResult();
                return release.Task.WaitAsync(cancellation);
            }
            return Task.CompletedTask;
        };
        var inFlight = fixture.StrikeAsync(messageId, "In-flight reason");
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            AssertSuccess(await fixture.PardonAsync(200), changed: true);
        }
        finally
        {
            release.TrySetResult();
        }

        var state = AssertSuccess(await inFlight, changed);

        Assert.Equal(200UL, state.LastPardonMessageId);
        Assert.Equal(changed ? 1 : 0, state.Strikes.Length);
        if (changed)
            Assert.Equal(messageId, Assert.Single(state.Strikes).MessageId);
        var persisted = fixture.Store.Snapshot("1:2")!;
        Assert.Equal(200UL, persisted.LastPardonMessageId);
        Assert.Equal(changed ? 3 : 2, persisted.Revision);
        Assert.Equal(changed ? 1 : 0, persisted.Strikes.Length);
        Assert.Equal(1, fixture.Store.Collisions);
    }

    [Fact]
    public async Task InFlightOlderPardonCannotUndoNewerPardonAndSubsequentStrike()
    {
        var fixture = new Fixture();
        await fixture.StrikeAsync(10, "Original reason");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.BeforeSave = (state, cancellation) =>
        {
            if (state.LastPardonMessageId == 50)
            {
                entered.TrySetResult();
                return release.Task.WaitAsync(cancellation);
            }
            return Task.CompletedTask;
        };
        var inFlight = fixture.PardonAsync(50);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            AssertSuccess(await fixture.PardonAsync(100), changed: true);
            AssertSuccess(await fixture.StrikeAsync(150, "New reason"), changed: true);
        }
        finally
        {
            release.TrySetResult();
        }

        var state = AssertSuccess(await inFlight, changed: false);

        Assert.Equal(100UL, state.LastPardonMessageId);
        Assert.Equal(150UL, Assert.Single(state.Strikes).MessageId);
        Assert.Equal(3, fixture.Store.Snapshot("1:2")!.Revision);
        Assert.Equal(1, fixture.Store.Collisions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MutationCanSucceedOnTheTenthCompareAndSwapAttempt(bool pardon)
    {
        var fixture = new Fixture();
        if (pardon)
            await fixture.StrikeAsync(10, "Original reason");
        var initialAttempts = fixture.Store.SaveAttempts;
        fixture.Store.RejectionsRemaining = 9;

        AssertSuccess(await (pardon ? fixture.PardonAsync(100) : fixture.StrikeAsync(100, "New reason")), changed: true);

        Assert.Equal(10, fixture.Store.SaveAttempts - initialAttempts);
        Assert.Equal(9, fixture.Store.Collisions);
        var state = fixture.Store.Snapshot("1:2")!;
        Assert.Equal(pardon ? 0 : 1, state.Strikes.Length);
        Assert.Equal(pardon ? 100UL : 0UL, state.LastPardonMessageId);
        Assert.Equal(pardon ? 2 : 1, state.Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TenFailedCompareAndSwapAttemptsReturnErrorWithoutClaimingAStateOrChangingStorage(bool pardon)
    {
        var fixture = new Fixture();
        if (pardon)
            await fixture.StrikeAsync(10, "Original reason");
        var original = fixture.Store.Snapshot("1:2");
        var initialAttempts = fixture.Store.SaveAttempts;
        fixture.Store.RejectionsRemaining = 11;

        var result = await (pardon ? fixture.PardonAsync(100) : fixture.StrikeAsync(100, "New reason"));

        AssertFailure(result);
        Assert.Equal(10, fixture.Store.SaveAttempts - initialAttempts);
        Assert.Equal(10, fixture.Store.Collisions);
        var persisted = fixture.Store.Snapshot("1:2");
        if (original is null)
            Assert.Null(persisted);
        else
            AssertState(original, Assert.IsType<CustomerDisciplineState>(persisted));
    }

    [Theory]
    [InlineData(0UL, 2UL, 3UL, false)]
    [InlineData(1UL, 0UL, 3UL, false)]
    [InlineData(1UL, 2UL, 0UL, false)]
    [InlineData(0UL, 2UL, 3UL, true)]
    [InlineData(1UL, 0UL, 3UL, true)]
    [InlineData(1UL, 2UL, 0UL, true)]
    public async Task InvalidMutationIdentifiersAreRejectedWithoutReadingOrWriting(ulong guildId, ulong userId, ulong messageId, bool pardon)
    {
        var fixture = new Fixture();
        var cancellation = TestContext.Current.CancellationToken;

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            pardon
                ? fixture.Service.PardonAsync(guildId, userId, messageId, cancellation)
                : fixture.Service.IssueStrikeAsync(guildId, userId, messageId, "Reason", cancellation)
        );

        Assert.Equal(0, fixture.Store.ReadAttempts);
        Assert.Equal(0, fixture.Store.SaveAttempts);
    }

    [Theory]
    [InlineData(0UL, 2UL)]
    [InlineData(1UL, 0UL)]
    public async Task InvalidReadIdentifiersAreRejectedBeforeAccessingStorage(ulong guildId, ulong userId)
    {
        var fixture = new Fixture();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => fixture.Service.GetAsync(guildId, userId, TestContext.Current.CancellationToken));

        Assert.Equal(0, fixture.Store.ReadAttempts);
        Assert.Equal(0, fixture.Store.SaveAttempts);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public async Task EmptyOrWhitespaceReasonCannotIssueAStrike(string reason)
    {
        var fixture = new Fixture();

        AssertFailure(await fixture.StrikeAsync(10, reason));

        Assert.Equal(0, fixture.Store.ReadAttempts);
        Assert.Equal(0, fixture.Store.SaveAttempts);
    }

    [Fact]
    public async Task ReasonLongerThanTwoHundredCharactersIsRejectedWithoutTruncationOrWriting()
    {
        var fixture = new Fixture();

        AssertFailure(await fixture.StrikeAsync(10, " " + new string('a', 201) + " "));

        Assert.Equal(0, fixture.Store.ReadAttempts);
        Assert.Equal(0, fixture.Store.SaveAttempts);
        Assert.Null(fixture.Store.Snapshot("1:2"));
    }

    [Theory]
    [InlineData("get")]
    [InlineData("strike")]
    [InlineData("pardon")]
    public async Task ReadFailuresPropagateToTheCaller(string operation)
    {
        var fixture = new Fixture();
        var failure = new InvalidOperationException("Storage unavailable");
        fixture.Store.ReadFailure = failure;

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.ExecuteAsync(operation, TestContext.Current.CancellationToken)
        );

        Assert.Same(failure, thrown);
        Assert.Equal(0, fixture.Store.SaveAttempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveFailuresPropagateWithoutClaimingSuccessOrChangingExistingState(bool pardon)
    {
        var fixture = new Fixture();
        await fixture.StrikeAsync(10, "Original reason");
        var original = fixture.Store.Snapshot("1:2")!;
        var failure = new InvalidOperationException("Storage unavailable");
        fixture.Store.SaveFailure = failure;

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pardon ? fixture.PardonAsync(100) : fixture.StrikeAsync(100, "New reason")
        );

        Assert.Same(failure, thrown);
        AssertState(original, fixture.Store.Snapshot("1:2")!);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("strike")]
    [InlineData("pardon")]
    public async Task CallerCancellationPropagatesWithoutChangingStorage(string operation)
    {
        var fixture = new Fixture();
        await fixture.StrikeAsync(10, "Original reason");
        var original = fixture.Store.Snapshot("1:2")!;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.ExecuteAsync(operation, cancellation.Token));

        AssertState(original, fixture.Store.Snapshot("1:2")!);
        Assert.Equal(1, fixture.Store.SaveAttempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringSavePropagatesAndDoesNotCommitTheProposedMutation(bool pardon)
    {
        var fixture = new Fixture();
        await fixture.StrikeAsync(10, "Original reason");
        var original = fixture.Store.Snapshot("1:2")!;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.BeforeSave = (_, cancellation) =>
        {
            entered.TrySetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
        };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var mutation = fixture.ExecuteAsync(pardon ? "pardon" : "strike", cancellation.Token);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mutation);

        AssertState(original, fixture.Store.Snapshot("1:2")!);
    }

    private static CustomerDisciplineState AssertSuccess(CustomerDisciplineResult result, bool changed)
    {
        Assert.Equal(changed, result.Changed);
        Assert.Null(result.Error);
        return Assert.IsType<CustomerDisciplineState>(result.State);
    }

    private static void AssertFailure(CustomerDisciplineResult result)
    {
        Assert.False(result.Changed);
        Assert.Null(result.State);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    private static void AssertState(CustomerDisciplineState expected, CustomerDisciplineState actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Revision, actual.Revision);
        Assert.Equal(expected.LastPardonMessageId, actual.LastPardonMessageId);
        Assert.Equal(expected.Strikes, actual.Strikes);
        Assert.Equal(expected.IsBarred, actual.IsBarred);
    }

    private sealed class Fixture
    {
        public TestClock Clock { get; } = new();
        public MemoryStore Store { get; } = new();
        public CustomerDisciplineService Service { get; }

        public Fixture() => Service = new CustomerDisciplineService(Store, Clock);

        public Task<CustomerDisciplineResult> StrikeAsync(ulong messageId, string reason) =>
            Service.IssueStrikeAsync(1, 2, messageId, reason, TestContext.Current.CancellationToken);

        public Task<CustomerDisciplineResult> PardonAsync(ulong messageId) =>
            Service.PardonAsync(1, 2, messageId, TestContext.Current.CancellationToken);

        public Task ExecuteAsync(string operation, CancellationToken cancellationToken) =>
            operation switch
            {
                "get" => Service.GetAsync(1, 2, cancellationToken),
                "strike" => Service.IssueStrikeAsync(1, 2, 100, "New reason", cancellationToken),
                "pardon" => Service.PardonAsync(1, 2, 100, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class SaveBarrier(int participants)
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public Task ArriveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivals) == participants)
                _release.TrySetResult();
            return _release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class MemoryStore : ICustomerDisciplineStore
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, CustomerDisciplineState> _states = new(StringComparer.Ordinal);

        public int ReadAttempts { get; private set; }
        public int SaveAttempts { get; private set; }
        public int Collisions { get; private set; }
        public int RejectionsRemaining { get; set; }
        public Exception? ReadFailure { get; set; }
        public Exception? SaveFailure { get; set; }
        public Func<CustomerDisciplineState, CancellationToken, Task>? BeforeSave { get; set; }

        public Task<IReadOnlyList<CustomerDisciplineState>> ListAsync(
            ulong guildId,
            bool barredOnly,
            int offset,
            int limit,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public Task<CustomerDisciplineState?> GetAsync(string id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lock)
            {
                ReadAttempts++;
                if (ReadFailure is not null)
                    throw ReadFailure;
                return Task.FromResult(Snapshot(id));
            }
        }

        public async Task<bool> TrySaveAsync(CustomerDisciplineState state, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (BeforeSave is not null)
                await BeforeSave(state, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lock)
            {
                SaveAttempts++;
                if (SaveFailure is not null)
                    throw SaveFailure;
                var revision = _states.GetValueOrDefault(state.Id)?.Revision ?? 0;
                if (RejectionsRemaining > 0 || state.Revision != revision)
                {
                    if (RejectionsRemaining > 0)
                        RejectionsRemaining--;
                    Collisions++;
                    return false;
                }
                _states[state.Id] = state with { Revision = state.Revision + 1, Strikes = state.Strikes.ToArray() };
                return true;
            }
        }

        public CustomerDisciplineState? Snapshot(string id)
        {
            lock (_lock)
            {
                return _states.TryGetValue(id, out var state) ? state with { Strikes = state.Strikes.ToArray() } : null;
            }
        }
    }
}
