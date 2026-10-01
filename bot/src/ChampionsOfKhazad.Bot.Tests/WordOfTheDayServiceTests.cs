using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChampionsOfKhazad.Bot.Tests;

public class WordOfTheDayServiceTests
{
    [Fact]
    public async Task AlreadyCancelledRequestDoesNotReleaseAnUnacquiredLock()
    {
        var store = new WordStore();
        var service = new WordOfTheDayService(store, NullLogger<WordOfTheDayService>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetWordOfTheDayAsync(cancellation.Token));

        Assert.Equal(0, store.ReadCount);
        Assert.Same(store.Word, await service.GetWordOfTheDayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancelledWaiterDoesNotReleaseAnotherRequestsLock()
    {
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new WordStore { BeforeRead = token => releaseRead.Task.WaitAsync(token) };
        var service = new WordOfTheDayService(store, NullLogger<WordOfTheDayService>.Instance);
        var owner = service.GetWordOfTheDayAsync(TestContext.Current.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var waiter = service.GetWordOfTheDayAsync(cancellation.Token);
        cancellation.Cancel();

        Task<WordOfTheDay>? next = null;
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
            next = service.GetWordOfTheDayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, store.ReadCount);
            Assert.False(next.IsCompleted);
        }
        finally
        {
            releaseRead.TrySetResult();
            await owner;
            if (next is not null)
                await next;
        }

        Assert.Equal(2, store.ReadCount);
    }

    [Fact]
    public async Task FailedReadReleasesAnAcquiredLock()
    {
        var store = new WordStore { BeforeRead = _ => throw new InvalidOperationException("Read failed") };
        var service = new WordOfTheDayService(store, NullLogger<WordOfTheDayService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetWordOfTheDayAsync(TestContext.Current.CancellationToken));
        store.BeforeRead = null;

        Assert.Same(store.Word, await service.GetWordOfTheDayAsync(TestContext.Current.CancellationToken));
    }

    private sealed class WordStore : IWordOfTheDayStore
    {
        private int _readCount;
        public int ReadCount => _readCount;
        public WordOfTheDay Word { get; } = new("testword", DateOnly.FromDateTime(DateTime.Now));
        public Func<CancellationToken, Task>? BeforeRead { get; set; }

        public async Task<WordOfTheDay?> GetWordOfTheDayAsync(DateOnly date, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _readCount);
            if (BeforeRead is not null)
                await BeforeRead(cancellationToken);
            return Word;
        }

        public Task<WordOfTheDay?> GetMostRecentlyWonWordOfTheDayAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<WordOfTheDay?>(Word);

        public Task<ushort> GetWinCountAsync(ulong userId, CancellationToken cancellationToken = default) => Task.FromResult((ushort)0);

        public Task UpsertWordOfTheDayAsync(WordOfTheDay wordOfTheDay) => Task.CompletedTask;
    }
}
