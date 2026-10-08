using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot.Tests;

public class GazetteIssueServiceTests
{
    [Fact]
    public async Task ReadingOrDiscardingDraftNumbersDoesNotIncrementAndRecreatedServiceKeepsPublishedSequence()
    {
        var store = new MemoryGazetteIssueStore();
        var service = new GazetteIssueService(store, TimeProvider.System);
        var cancellation = TestContext.Current.CancellationToken;
        Assert.Equal(1, await service.GetNextAsync(cancellation));
        Assert.Equal(1, await service.GetNextAsync(cancellation));
        Assert.Equal(0, store.Saves);
        Assert.True(await service.TryReserveAsync(1, "approval", 8, cancellation));
        await service.MarkPublishedAsync(1, "approval", 99, cancellation);
        Assert.Equal(2, await new GazetteIssueService(store, TimeProvider.System).GetNextAsync(cancellation));
        Assert.Equal(99UL, Assert.Single(store.State.Publications).MessageId);
    }

    [Fact]
    public async Task OverlappingServicesCannotReuseAReservedNumberEvenWhenSendIsAmbiguous()
    {
        var store = new MemoryGazetteIssueStore();
        var first = new GazetteIssueService(store, TimeProvider.System);
        var second = new GazetteIssueService(store, TimeProvider.System);
        var cancellation = TestContext.Current.CancellationToken;
        var results = await Task.WhenAll(first.TryReserveAsync(1, "one", 8, cancellation), second.TryReserveAsync(1, "two", 8, cancellation));
        Assert.Equal(1, results.Count(success => success));
        Assert.Equal(2, await second.GetNextAsync(cancellation));
        Assert.Null(Assert.Single(store.State.Publications).MessageId);
    }

    [Fact]
    public async Task IllustrationAttemptsHaveADurableRollingBudgetButDoNotConsumeIssueNumbers()
    {
        var store = new MemoryGazetteIssueStore();
        var clock = new Clock();
        var service = new GazetteIssueService(store, clock);
        var cancellation = TestContext.Current.CancellationToken;
        Assert.True(await service.TryReserveIllustrationAsync(1, cancellation));
        Assert.False(await new GazetteIssueService(store, clock).TryReserveIllustrationAsync(1, cancellation));
        Assert.Equal(1, await service.GetNextAsync(cancellation));
        clock.Now = clock.Now.AddDays(1);
        Assert.True(await service.TryReserveIllustrationAsync(1, cancellation));
        Assert.Single(store.State.IllustrationAttempts);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
