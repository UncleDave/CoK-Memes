using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChampionsOfKhazad.Bot.Tests;

public class CustomerDirectMessageCommandTests
{
    [Theory]
    [InlineData("customers")]
    [InlineData("CUSTOMERS help")]
    [InlineData("customers show")]
    [InlineData("customers pardon")]
    [InlineData("customers ban 42")]
    [InlineData("customers show 0")]
    [InlineData("customers show -42")]
    [InlineData("customers show +42")]
    [InlineData("customers show 18446744073709551616")]
    [InlineData("customers pardon someone")]
    [InlineData("customers pardon <@&42>")]
    [InlineData("customers pardon <#42>")]
    [InlineData("customers pardon <@42")]
    [InlineData("customers pardon <@!>")]
    [InlineData("customers pardon 42 extra")]
    public async Task HelpAndInvalidCommandsDoNotAccessPersistence(string content)
    {
        var command = new CustomerDirectMessageCommand(null!, null!, NullLogger<CustomerDirectMessageCommand>.Instance);
        var response = await command.ExecuteAsync(content, 100, TestContext.Current.CancellationToken);
        Assert.Contains("Commands: customers", response!);
        Assert.Contains("customers show <user-id>", response!);
        Assert.Contains("customers pardon <user-id>", response!);
        Assert.Contains("Three strikes: no service until pardoned.", response!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("word")]
    [InlineData("personality")]
    [InlineData("customer show 42")]
    public async Task UnrelatedMessagesAreIgnored(string content)
    {
        var command = new CustomerDirectMessageCommand(null!, null!, NullLogger<CustomerDirectMessageCommand>.Instance);
        Assert.Null(await command.ExecuteAsync(content, 100, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0, "Service available.")]
    [InlineData(1, "Service available.")]
    [InlineData(2, "Service available.")]
    [InlineData(3, "Service refused until pardoned.")]
    public async Task ShowReportsStrikesAndServiceStatusWithoutWriting(int strikes, string status)
    {
        var store = new MemoryStore { State = CreateState(strikes) };
        var response = await CreateCommand(store).ExecuteAsync("customers show 42", 100, TestContext.Current.CancellationToken);

        Assert.StartsWith($"Customer 42: {strikes}/3 strikes. {status}", response!);
        Assert.Equal(CustomerDisciplineState.Create(10, 42).Id, Assert.Single(store.ReadIds));
        Assert.Equal(0, store.Saves);
        for (var index = 0; index < strikes; index++)
            Assert.Contains($"2026-10-21 12:00 UTC — Strike {index + 1} (message {index + 1})", response!);
    }

    [Theory]
    [InlineData("customers show 42")]
    [InlineData("CUSTOMERS SHOW <@42>")]
    [InlineData("  customers   show   <@!42>  ")]
    public async Task ShowAcceptsNumericIdsAndDiscordUserMentions(string content)
    {
        var store = new MemoryStore();
        var response = await CreateCommand(store).ExecuteAsync(content, 100, TestContext.Current.CancellationToken);

        Assert.Equal("Customer 42: 0/3 strikes. Service available.", response);
        Assert.Equal(CustomerDisciplineState.Create(10, 42).Id, Assert.Single(store.ReadIds));
        Assert.Equal(0, store.Saves);
    }

    [Theory]
    [InlineData("customers pardon 42")]
    [InlineData("CUSTOMERS PARDON <@42>")]
    [InlineData("  customers   pardon   <@!42>  ")]
    public async Task PardonResetsStrikesAndRecordsTheAdminMessageId(string content)
    {
        var store = new MemoryStore { State = CreateState(3) };
        var response = await CreateCommand(store).ExecuteAsync(content, 123, TestContext.Current.CancellationToken);

        Assert.Equal("Customer 42 pardoned. 0/3 strikes. Service available.", response);
        Assert.Empty(store.State!.Strikes);
        Assert.Equal(123UL, store.State.LastPardonMessageId);
        Assert.Equal(CustomerDisciplineState.Create(10, 42).Id, store.State.Id);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task ReplayingAPardonDoesNotClearNewStrikesOrClaimTheCustomerWasPardoned()
    {
        var store = new MemoryStore { State = CreateState(3) };
        var command = CreateCommand(store);
        await command.ExecuteAsync("customers pardon 42", 123, TestContext.Current.CancellationToken);
        store.State = store.State! with
        {
            Strikes =
            [
                new(124, "New first strike", DateTime.UtcNow),
                new(125, "New second strike", DateTime.UtcNow),
                new(126, "New third strike", DateTime.UtcNow),
            ],
        };

        var response = await command.ExecuteAsync("customers pardon 42", 123, TestContext.Current.CancellationToken);

        Assert.StartsWith("Customer 42: 3/3 strikes. Service refused until pardoned.", response!);
        Assert.DoesNotContain("Customer 42 pardoned", response!);
        Assert.Equal(3, store.State.Strikes.Length);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task PardonWithoutStrikesReportsCurrentStatusAndSavesTheStaleStrikeBarrier()
    {
        var store = new MemoryStore();
        var response = await CreateCommand(store).ExecuteAsync("customers pardon 42", 123, TestContext.Current.CancellationToken);

        Assert.Equal("Customer 42: 0/3 strikes. Service available.", response);
        Assert.Equal(1, store.Saves);
        Assert.Equal(123UL, store.State!.LastPardonMessageId);
    }

    [Theory]
    [InlineData("customers show 42")]
    [InlineData("customers pardon 42")]
    public async Task PersistenceFailuresDoNotExposeDetailsOrClaimSuccess(string content)
    {
        var store = new MemoryStore { Failure = new InvalidOperationException("SECRET database details") };
        var response = await CreateCommand(store).ExecuteAsync(content, 123, TestContext.Current.CancellationToken);

        Assert.Contains("could not be confirmed", response!);
        Assert.Contains("customers show 42", response!);
        Assert.DoesNotContain("SECRET", response!);
        Assert.DoesNotContain("Customer 42 pardoned", response!);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task ConflictingWritesDoNotClaimAPardonSucceeded()
    {
        var store = new MemoryStore { State = CreateState(3), RejectSaves = true };
        var response = await CreateCommand(store).ExecuteAsync("customers pardon 42", 123, TestContext.Current.CancellationToken);

        Assert.Contains("Customer pardon could not be confirmed", response!);
        Assert.Contains("customers show 42", response!);
        Assert.Equal(3, store.State!.Strikes.Length);
        Assert.Equal(0, store.Saves);
    }

    [Fact]
    public async Task CancellationIsForwardedToPersistenceAndPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var store = new MemoryStore();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateCommand(store).ExecuteAsync("customers show 42", 123, cancellation.Token)
        );
        Assert.Equal(cancellation.Token, store.LastCancellationToken);
        Assert.Equal(0, store.Saves);
    }

    private static CustomerDirectMessageCommand CreateCommand(MemoryStore store)
    {
        var guild = DiscordConversationFixture.Stub<Discord.IGuild>(
            (method, _) => method.Name == "get_Id" ? 10UL : throw new NotSupportedException(method.Name)
        );
        return new(
            new CustomerDisciplineService(store, TimeProvider.System),
            new BotContext(9, guild, null!),
            NullLogger<CustomerDirectMessageCommand>.Instance
        );
    }

    private static CustomerDisciplineState CreateState(int strikes) =>
        CustomerDisciplineState.Create(10, 42) with
        {
            Strikes = Enumerable
                .Range(1, strikes)
                .Select(index => new CustomerStrike((ulong)index, $"Strike {index}", new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc)))
                .ToArray(),
        };

    private sealed class MemoryStore : ICustomerDisciplineStore
    {
        public CustomerDisciplineState? State { get; set; }
        public List<string> ReadIds { get; } = [];
        public int Saves { get; private set; }
        public bool RejectSaves { get; set; }
        public Exception? Failure { get; set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public Task<CustomerDisciplineState?> GetAsync(string id, CancellationToken cancellationToken)
        {
            LastCancellationToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null)
                throw Failure;
            ReadIds.Add(id);
            return Task.FromResult(State?.Id == id ? State : null);
        }

        public Task<bool> TrySaveAsync(CustomerDisciplineState state, CancellationToken cancellationToken)
        {
            LastCancellationToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null)
                throw Failure;
            if (RejectSaves || state.Revision != (State?.Revision ?? 0))
                return Task.FromResult(false);
            State = state with { Revision = state.Revision + 1 };
            Saves++;
            return Task.FromResult(true);
        }
    }
}
