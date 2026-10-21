using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class CustomerLedgerLookupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToolListsOtherCustomersInTheCurrentGuildWithoutExposingReasonsOrWriteOperations(bool barredOnly)
    {
        var store = new ListStore { Results = barredOnly ? [State(1234567890123456789, 3)] : [State(1234567890123456789, 3), State(3, 1)] };
        var tool = CreateTool(store);

        var response = await tool.InvokeAsync(new AIFunctionArguments { ["barredOnly"] = barredOnly }, TestContext.Current.CancellationToken);

        using var data = JsonDocument.Parse(response!.ToString()!);
        Assert.Equal((9UL, barredOnly, 0, CustomerDisciplineService.ListPageSize + 1), Assert.Single(store.Calls));
        var customers = data.RootElement.GetProperty("customers").EnumerateArray().ToArray();
        Assert.Equal(barredOnly ? 1 : 2, customers.Length);
        Assert.Equal("1234567890123456789", customers[0].GetProperty("userId").GetString());
        Assert.Equal("<@1234567890123456789>", customers[0].GetProperty("mention").GetString());
        Assert.Equal(3, customers[0].GetProperty("strikeCount").GetInt32());
        Assert.True(customers[0].GetProperty("isBarred").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("nextOffset").ValueKind);
        Assert.DoesNotContain("PRIVATE reason", response.ToString());
        Assert.Equal(new[] { "barredOnly", "offset" }, tool.JsonSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name));
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task PaginationReportsTheNextOffsetInsteadOfSilentlyTruncatingTheGuildLedger()
    {
        var store = new ListStore
        {
            Results = Enumerable.Range(1, CustomerDisciplineService.ListPageSize + 1).Select(id => State((ulong)id, 1)).ToArray(),
        };
        var tool = CreateTool(store);

        var first = await tool.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        using var page = JsonDocument.Parse(first!.ToString()!);
        Assert.Equal(CustomerDisciplineService.ListPageSize, page.RootElement.GetProperty("customers").GetArrayLength());
        var next = page.RootElement.GetProperty("nextOffset").GetInt32();
        Assert.Equal(CustomerDisciplineService.ListPageSize, next);
        store.Results = [State(26, 1)];

        var second = await tool.InvokeAsync(new AIFunctionArguments { ["offset"] = next }, TestContext.Current.CancellationToken);

        using var last = JsonDocument.Parse(second!.ToString()!);
        Assert.Single(last.RootElement.GetProperty("customers").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, last.RootElement.GetProperty("nextOffset").ValueKind);
        Assert.Equal(next, store.Calls[^1].Offset);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task EmptyLedgerIsAnExplicitSuccessfulEmptyList()
    {
        var response = await CreateTool(new ListStore()).InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        using var data = JsonDocument.Parse(response!.ToString()!);
        Assert.Empty(data.RootElement.GetProperty("customers").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("nextOffset").ValueKind);
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("other-guild")]
    [InlineData("cleared")]
    [InlineData("not-barred")]
    [InlineData("invalid-id")]
    public async Task LookupFailuresAndOutOfScopeRecordsDoNotBecomeAnEmptyListOrLeakDetails(string failure)
    {
        var store = new ListStore
        {
            Failure = failure == "failure" ? new InvalidOperationException("SECRET database details") : null,
            Results = failure switch
            {
                "other-guild" => [State(2, 3) with { Id = "99:2" }],
                "cleared" => [State(2, 0)],
                "not-barred" => [State(2, 1)],
                "invalid-id" => [State(2, 3) with { Id = "9:not-a-user" }],
                _ => [],
            },
        };

        var response = await CreateTool(store).InvokeAsync(new AIFunctionArguments { ["barredOnly"] = true }, TestContext.Current.CancellationToken);

        Assert.Contains("lookup failed", response!.ToString());
        Assert.DoesNotContain("SECRET", response.ToString());
        Assert.DoesNotContain("PRIVATE reason", response.ToString());
        Assert.DoesNotContain("\"customers\":[]", response.ToString());
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(null, 4UL)]
    [InlineData(0UL, 4UL)]
    [InlineData(9UL, null)]
    [InlineData(9UL, 0UL)]
    public async Task MissingGuildContextNeverQueriesTheLedger(ulong? guildId, ulong? channelId)
    {
        var store = new ListStore();

        var response = await CreateTool(store, new MessageContext(guildId, channelId))
            .InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        Assert.Contains("guild chat", response!.ToString());
        Assert.Empty(store.Calls);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task InvalidOffsetsDoNotReachPersistence(int offset)
    {
        var store = new ListStore();

        var response = await CreateTool(store).InvokeAsync(new AIFunctionArguments { ["offset"] = offset }, TestContext.Current.CancellationToken);

        Assert.Contains("offset", response!.ToString());
        Assert.Empty(store.Calls);
    }

    [Fact]
    public async Task RequestedCancellationIsPropagatedRatherThanReportedAsAnEmptyLedger()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateTool(new ListStore()).InvokeAsync(new AIFunctionArguments(), cancellation.Token).AsTask()
        );
    }

    private static AIFunction CreateTool(ListStore store, MessageContext? context = null)
    {
        var tools = new PersonalityTools(
            null!,
            null!,
            null!,
            null!,
            NullLogger<PersonalityTools>.Instance,
            null!,
            new CustomerDisciplineService(store, TimeProvider.System)
        );
        return Assert.IsAssignableFrom<AIFunction>(
            tools.Create(context ?? new MessageContext(9, 4), true).Single(tool => tool.Name == "list_customer_strikes")
        );
    }

    private static CustomerDisciplineState State(ulong userId, int count) =>
        CustomerDisciplineState.Create(9, userId) with
        {
            Strikes = Enumerable.Range(1, count).Select(id => new CustomerStrike((ulong)id, "PRIVATE reason", DateTime.UnixEpoch)).ToArray(),
        };

    private sealed class MessageContext(ulong? guildId, ulong? channelId) : IMessageContext
    {
        public ulong UserId => 42;
        public string UserName => "Tester";
        public ulong? ChannelId => channelId;
        public ulong? GuildId => guildId;
        public ulong? MessageId => 100;

        public Task Reply(string message) => throw new NotSupportedException();
    }

    private sealed class ListStore : ICustomerDisciplineStore
    {
        public IReadOnlyList<CustomerDisciplineState> Results { get; set; } = [];
        public Exception? Failure { get; set; }
        public List<(ulong GuildId, bool BarredOnly, int Offset, int Limit)> Calls { get; } = [];
        public int Writes { get; private set; }

        public Task<CustomerDisciplineState?> GetAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<CustomerDisciplineState>> ListAsync(
            ulong guildId,
            bool barredOnly,
            int offset,
            int limit,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add((guildId, barredOnly, offset, limit));
            return Failure is null ? Task.FromResult(Results) : Task.FromException<IReadOnlyList<CustomerDisciplineState>>(Failure);
        }

        public Task<bool> TrySaveAsync(CustomerDisciplineState state, CancellationToken cancellationToken)
        {
            Writes++;
            throw new NotSupportedException();
        }
    }
}
