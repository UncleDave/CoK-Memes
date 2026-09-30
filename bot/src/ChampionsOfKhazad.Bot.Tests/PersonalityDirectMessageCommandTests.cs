using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChampionsOfKhazad.Bot.Tests;

public class PersonalityDirectMessageCommandTests
{
    [Theory]
    [InlineData("personality baseline", LorekeeperTemperament.Baseline, 0)]
    [InlineData("PERSONALITY GrOuChY", LorekeeperTemperament.Grouchy, 0)]
    [InlineData("  personality   furious  2h  ", LorekeeperTemperament.Furious, 120)]
    [InlineData("personality grouchy 30m", LorekeeperTemperament.Grouchy, 30)]
    [InlineData("personality furious 30d", LorekeeperTemperament.Furious, 43200)]
    [InlineData("personality reset", LorekeeperTemperament.Baseline, 0)]
    public async Task ValidCommandsSaveAndConfirm(string command, LorekeeperTemperament temperament, int minutes)
    {
        var store = new MemoryStore();
        var handler = CreateCommand(store);
        var response = await handler.ExecuteAsync(command, TestContext.Current.CancellationToken);
        Assert.Equal(temperament, store.Setting!.Temperament);
        Assert.Equal(minutes == 0 ? null : new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc).AddMinutes(minutes), store.Setting.ExpiresAtUtc);
        Assert.Contains(temperament.ToString().ToLowerInvariant(), response!);
        Assert.Contains(minutes == 0 ? "Active until changed" : "Returns to baseline", response!);
    }

    [Theory]
    [InlineData("personality angry")]
    [InlineData("personality 2")]
    [InlineData("personality furious 0h")]
    [InlineData("personality furious -1h")]
    [InlineData("personality furious 1.5h")]
    [InlineData("personality furious 31d")]
    [InlineData("personality furious 999999999999999d")]
    [InlineData("personality furious 1s")]
    [InlineData("personality furious 1h extra")]
    [InlineData("personality reset 1h")]
    public async Task InvalidCommandsReturnHelpWithoutWriting(string command)
    {
        var store = new MemoryStore();
        var response = await CreateCommand(store).ExecuteAsync(command, TestContext.Current.CancellationToken);
        Assert.Contains("Commands:", response!);
        Assert.Null(store.Setting);
    }

    [Fact]
    public async Task StatusAndListDoNotWriteAndUnrelatedMessagesAreIgnored()
    {
        var store = new MemoryStore();
        var handler = CreateCommand(store);
        Assert.Contains("baseline", (await handler.ExecuteAsync("personality", TestContext.Current.CancellationToken))!);
        var list = await handler.ExecuteAsync("personality list", TestContext.Current.CancellationToken);
        Assert.Contains("grouchy", list!);
        Assert.Contains("furious", list!);
        Assert.DoesNotContain("Commands:", list!);
        Assert.DoesNotContain("Duration:", list!);
        Assert.Null(await handler.ExecuteAsync("word", TestContext.Current.CancellationToken));
        Assert.Null(await handler.ExecuteAsync("", TestContext.Current.CancellationToken));
        Assert.Null(store.Setting);
    }

    private static PersonalityDirectMessageCommand CreateCommand(MemoryStore store) =>
        new(new LorekeeperPersonalityService(store, new TestClock(), NullLogger<LorekeeperPersonalityService>.Instance));

    private sealed class MemoryStore : ILorekeeperPersonalityStore
    {
        public LorekeeperPersonalitySetting? Setting { get; private set; }

        public Task<LorekeeperPersonalitySetting?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Setting);

        public Task SaveAsync(LorekeeperPersonalitySetting setting, CancellationToken cancellationToken = default)
        {
            Setting = setting;
            return Task.CompletedTask;
        }
    }

    private sealed class TestClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    }
}
