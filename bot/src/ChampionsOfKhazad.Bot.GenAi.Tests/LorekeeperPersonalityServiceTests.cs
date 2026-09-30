using Microsoft.Extensions.Logging.Abstractions;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class LorekeeperPersonalityServiceTests
{
    [Fact]
    public async Task MissingSettingDefaultsToBaseline()
    {
        var service = CreateService(new MemoryStore(), new TestClock());
        Assert.Equal(LorekeeperTemperament.Baseline, (await service.GetAsync(TestContext.Current.CancellationToken)).Temperament);
    }

    [Fact]
    public async Task TemporarySettingExpiresAtTheExactDeadlineWithoutWriting()
    {
        var store = new MemoryStore();
        var clock = new TestClock();
        var service = CreateService(store, clock);
        await service.SetAsync(LorekeeperTemperament.Furious, TimeSpan.FromHours(2), TestContext.Current.CancellationToken);

        clock.Now = clock.Now.AddHours(2).AddTicks(-1);
        Assert.Equal(LorekeeperTemperament.Furious, (await service.GetAsync(TestContext.Current.CancellationToken)).Temperament);
        clock.Now = clock.Now.AddTicks(1);
        var expired = await service.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LorekeeperTemperament.Baseline, expired.Temperament);
        Assert.Null(expired.ExpiresAtUtc);
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public async Task NewServiceReadsPersistedSelectionAndUntimedChangeClearsExpiry()
    {
        var store = new MemoryStore();
        var clock = new TestClock();
        var service = CreateService(store, clock);
        await service.SetAsync(LorekeeperTemperament.Furious, TimeSpan.FromHours(2), TestContext.Current.CancellationToken);
        var restartedService = CreateService(store, clock);
        Assert.Equal(LorekeeperTemperament.Furious, (await restartedService.GetAsync(TestContext.Current.CancellationToken)).Temperament);

        await restartedService.SetAsync(LorekeeperTemperament.Grouchy, cancellationToken: TestContext.Current.CancellationToken);
        clock.Now = clock.Now.AddYears(1);
        var setting = await service.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LorekeeperTemperament.Grouchy, setting.Temperament);
        Assert.Null(setting.ExpiresAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(43201)]
    public async Task InvalidDurationCannotChangeState(int minutes)
    {
        var store = new MemoryStore();
        var service = CreateService(store, new TestClock());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SetAsync(LorekeeperTemperament.Furious, TimeSpan.FromMinutes(minutes), TestContext.Current.CancellationToken)
        );
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(LorekeeperTemperament.Baseline)]
    [InlineData(LorekeeperTemperament.Grouchy)]
    [InlineData(LorekeeperTemperament.Furious)]
    public void PresetsRetainCapabilitiesAndOverrideHistoryTone(LorekeeperTemperament temperament)
    {
        var prompt = LorekeeperPersonality.GetPrompt(temperament);
        Assert.Contains("Follow the Guild Lore Lookup Policy", prompt);
        Assert.Contains("Use generate_image", prompt);
        Assert.Contains("Do not make decisions about image allowances yourself", prompt);
        Assert.Contains("Do not imitate their tone", prompt);
        if (temperament == LorekeeperTemperament.Baseline)
        {
            Assert.Contains("wise Dwarf Lorekeeper", prompt);
            Assert.DoesNotContain("rude", prompt);
            Assert.DoesNotContain("furious", prompt);
        }
        else
            Assert.Contains("drop the act for sensitive topics or when asked to stop", prompt);
    }

    [Fact]
    public async Task FailedReadFallsBackToBaselineWithoutChangingSavedSettingAndRecovers()
    {
        var store = new MemoryStore();
        var service = CreateService(store, new TestClock());
        await service.SetAsync(LorekeeperTemperament.Furious, cancellationToken: TestContext.Current.CancellationToken);
        store.ReadException = new InvalidOperationException("Database unavailable");

        var fallback = await service.GetAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LorekeeperTemperament.Baseline, fallback.Temperament);
        Assert.Null(fallback.ExpiresAtUtc);
        Assert.Equal(1, store.Writes);

        store.ReadException = null;
        Assert.Equal(LorekeeperTemperament.Furious, (await service.GetAsync(TestContext.Current.CancellationToken)).Temperament);
    }

    [Fact]
    public async Task CanceledReadDoesNotFallBackToBaseline()
    {
        var store = new MemoryStore { ReadException = new OperationCanceledException() };
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateService(store, new TestClock()).GetAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task FailedSaveIsNotSuppressed()
    {
        var store = new MemoryStore { WriteException = new InvalidOperationException("Database unavailable") };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(store, new TestClock()).SetAsync(LorekeeperTemperament.Furious, cancellationToken: TestContext.Current.CancellationToken)
        );
        Assert.Equal(0, store.Writes);
    }

    private static LorekeeperPersonalityService CreateService(ILorekeeperPersonalityStore store, TimeProvider clock) =>
        new(store, clock, NullLogger<LorekeeperPersonalityService>.Instance);

    private sealed class MemoryStore : ILorekeeperPersonalityStore
    {
        private LorekeeperPersonalitySetting? _setting;
        public int Writes { get; private set; }
        public Exception? ReadException { get; set; }
        public Exception? WriteException { get; set; }

        public Task<LorekeeperPersonalitySetting?> GetAsync(CancellationToken cancellationToken = default) =>
            ReadException is null ? Task.FromResult(_setting) : Task.FromException<LorekeeperPersonalitySetting?>(ReadException);

        public Task SaveAsync(LorekeeperPersonalitySetting setting, CancellationToken cancellationToken = default)
        {
            if (WriteException is not null)
                return Task.FromException(WriteException);
            _setting = setting;
            Writes++;
            return Task.CompletedTask;
        }
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
