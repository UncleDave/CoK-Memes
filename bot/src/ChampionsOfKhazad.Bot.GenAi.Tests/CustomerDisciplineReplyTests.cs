using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class CustomerDisciplineReplyTests
{
    [Fact]
    public async Task OrdinaryRequestGetsNormalToolsWithoutChangingLedger()
    {
        using var fixture = new Fixture();

        Assert.Equal("Normal answer", await fixture.InvokeAsync());

        Assert.Equal(1, fixture.Chat.Decisions);
        Assert.Equal(1, fixture.Chat.NormalReplies);
        Assert.Equal(new[] { "issue_customer_strike" }, fixture.Chat.DecisionTools);
        Assert.Contains("generate_image", fixture.Chat.NormalTools);
        Assert.Empty((await fixture.GetAsync()).Strikes);
    }

    [Fact]
    public async Task FullGrubKeepsLinkedMemberContextAndNotebookRecallAfterTheStrikeDecision()
    {
        using var fixture = new Fixture();
        fixture.Lore.Member = new LinkedMember();

        Assert.Equal("Normal answer", await fixture.InvokeAsync());

        Assert.Contains("Restoration shaman", fixture.Chat.NormalPrompt);
        Assert.Contains("RECENT MEMBER ANECDOTES", fixture.Chat.NormalPrompt);
        Assert.Contains(GuildPromptContext.Identity, fixture.Chat.NormalPrompt);
        Assert.Equal(2, fixture.Notes.Reads);
        Assert.Contains("remember_note", fixture.Chat.NormalTools);
        Assert.Contains("generate_image", fixture.Chat.NormalTools);
    }

    [Fact]
    public async Task SavedStrikesDeliverReasonAndThirdStrikeBlocksNormalService()
    {
        using var fixture = new Fixture();
        fixture.Chat.Action = "issue_customer_strike";

        for (var count = 1; count <= 3; count++)
        {
            fixture.Context.MessageId = (ulong)count;
            var reply = await fixture.InvokeAsync("Your chips are rubbish");
            Assert.Contains($"Strike {count}/3 — Insulting the chips", reply);
            if (count < 3)
                Assert.Contains("Normal answer", reply);
            else
            {
                Assert.Contains("You're barred", reply);
                Assert.DoesNotContain("Normal answer", reply);
                Assert.Contains("Ask me nicely", reply);
            }
        }

        Assert.True((await fixture.GetAsync()).IsBarred);
        Assert.Equal(2, fixture.Chat.NormalReplies);
    }

    [Fact]
    public async Task BarredCustomerGetsOnlyPardonToolAndCannotReachNormalService()
    {
        using var fixture = new Fixture();
        await fixture.BarAsync();

        var reply = await fixture.InvokeAsync("Generate an image");

        Assert.Contains("You're barred", reply);
        Assert.Equal(new[] { "pardon_customer" }, fixture.Chat.DecisionTools);
        Assert.Equal(0, fixture.Chat.NormalReplies);
        Assert.Empty(fixture.Chat.NormalTools);
    }

    [Fact]
    public async Task GrubCanPardonABarredCustomerButDoesNotRunTheirOriginalRequest()
    {
        using var fixture = new Fixture();
        await fixture.BarAsync();
        fixture.Chat.Action = "pardon_customer";

        var reply = await fixture.InvokeAsync("Sorry Grub, let me back in?");

        Assert.Contains("strikes are cleared", reply);
        Assert.Empty((await fixture.GetAsync()).Strikes);
        Assert.Equal(0, fixture.Chat.NormalReplies);
        fixture.Chat.Action = null;
        fixture.Context.MessageId++;
        Assert.Equal("Normal answer", await fixture.InvokeAsync());
        Assert.Equal(1, fixture.Chat.NormalReplies);
    }

    [Fact]
    public async Task PardonBeforeBarringClearsTheRecordAndStillAnswers()
    {
        using var fixture = new Fixture();
        await fixture.Service.IssueStrikeAsync(2, 1, 1, "Cheek", TestContext.Current.CancellationToken);
        fixture.Chat.Action = "pardon_customer";

        Assert.Equal("Your strikes are cleared.\n\nNormal answer", await fixture.InvokeAsync("Sorry"));

        Assert.Empty((await fixture.GetAsync()).Strikes);
        Assert.Equal(1, fixture.Chat.NormalReplies);
    }

    [Fact]
    public async Task MultipleModelCallsCannotGiveMultipleStrikesOrPardonInTheSameRequest()
    {
        using var fixture = new Fixture();
        await fixture.Service.IssueStrikeAsync(2, 1, 1, "Cheek", TestContext.Current.CancellationToken);
        fixture.Chat.Action = "issue_customer_strike";
        fixture.Chat.SecondAction = "pardon_customer";

        var reply = await fixture.InvokeAsync();

        Assert.Equal(2, (await fixture.GetAsync()).Strikes.Length);
        Assert.Contains("Strike 2/3", reply);
        Assert.Contains(fixture.Chat.Results, result => result.Contains("Only one", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DuplicateDiscordMessageDoesNotAddOrAnnounceAnotherStrike()
    {
        using var fixture = new Fixture();
        fixture.Chat.Action = "issue_customer_strike";
        await fixture.InvokeAsync();

        Assert.Equal("Normal answer", await fixture.InvokeAsync());

        Assert.Single((await fixture.GetAsync()).Strikes);
    }

    [Fact]
    public async Task FailedWriteDoesNotAnnounceAVerdictOrServeAnAppeal()
    {
        using var fixture = new Fixture();
        await fixture.BarAsync();
        fixture.Chat.Action = "pardon_customer";
        fixture.Store.FailSave = true;

        var reply = await fixture.InvokeAsync("Please forgive me");

        Assert.Contains("Couldn't confirm", reply);
        Assert.DoesNotContain("cleared", reply);
        Assert.True((await fixture.GetAsync()).IsBarred);
        Assert.Equal(0, fixture.Chat.NormalReplies);
    }

    [Fact]
    public async Task UnavailableLedgerFailsClosedBeforeModelOrNormalTools()
    {
        using var fixture = new Fixture();
        fixture.Store.FailRead = true;

        Assert.Contains("unavailable", await fixture.InvokeAsync());

        Assert.Equal(0, fixture.Chat.Decisions);
        Assert.Equal(0, fixture.Chat.NormalReplies);
    }

    [Fact]
    public async Task BarringDuringDecisionIsRecheckedBeforeNormalTools()
    {
        using var fixture = new Fixture();
        fixture.Chat.BeforeDecision = fixture.BarAsync;

        Assert.Contains("You're barred", await fixture.InvokeAsync());

        Assert.Equal(0, fixture.Chat.NormalReplies);
    }

    [Fact]
    public async Task UnknownWriteOutcomeDoesNotPretendThePardonFailedOrServeTheAppeal()
    {
        using var fixture = new Fixture();
        await fixture.BarAsync();
        fixture.Chat.Action = "pardon_customer";
        fixture.Store.FailAfterSave = true;

        var reply = await fixture.InvokeAsync("Sorry");

        Assert.Contains("Couldn't confirm", reply);
        Assert.DoesNotContain("cleared", reply);
        Assert.Empty((await fixture.GetAsync()).Strikes);
        Assert.Equal(0, fixture.Chat.NormalReplies);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavedStrikeIsStillDeliveredWhenDecisionFollowUpOrNormalResponseFails(bool failDecisionFollowUp)
    {
        using var fixture = new Fixture();
        fixture.Chat.Action = "issue_customer_strike";
        fixture.Chat.FailDecisionFollowUp = failDecisionFollowUp;
        fixture.Chat.FailNormalResponse = !failDecisionFollowUp;

        var reply = await fixture.InvokeAsync("Rubbish chips");

        Assert.Contains("Strike 1/3 — Insulting the chips", reply);
        Assert.Contains("Couldn't answer", reply);
        Assert.Single((await fixture.GetAsync()).Strikes);
    }

    [Fact]
    public async Task ThirdStrikeIsStillDeliveredWhenDecisionFollowUpFails()
    {
        using var fixture = new Fixture();
        await fixture.Service.IssueStrikeAsync(2, 1, 1, "Cheek", TestContext.Current.CancellationToken);
        await fixture.Service.IssueStrikeAsync(2, 1, 2, "More cheek", TestContext.Current.CancellationToken);
        fixture.Chat.Action = "issue_customer_strike";
        fixture.Chat.FailDecisionFollowUp = true;

        var reply = await fixture.InvokeAsync("Rubbish chips");

        Assert.Contains("Strike 3/3", reply);
        Assert.Contains("You're barred", reply);
        Assert.True((await fixture.GetAsync()).IsBarred);
        Assert.Equal(0, fixture.Chat.NormalReplies);
    }

    [Fact]
    public async Task SavedPardonIsStillDeliveredWhenDecisionFollowUpFails()
    {
        using var fixture = new Fixture();
        await fixture.BarAsync();
        fixture.Chat.Action = "pardon_customer";
        fixture.Chat.FailDecisionFollowUp = true;

        var reply = await fixture.InvokeAsync("Sorry");

        Assert.Contains("strikes are cleared", reply);
        Assert.Empty((await fixture.GetAsync()).Strikes);
        Assert.Equal(0, fixture.Chat.NormalReplies);
    }

    [Theory]
    [InlineData("issue_customer_strike", "Strike 1/3")]
    [InlineData("pardon_customer", "Your strikes are cleared")]
    public async Task ConfirmedVerdictIsStillDeliveredWhenSubsequentLedgerReadFails(string action, string expectedVerdict)
    {
        using var fixture = new Fixture();
        if (action == "pardon_customer")
            await fixture.BarAsync();
        fixture.Chat.Action = action;
        fixture.Store.FailReadAfterSave = true;

        var reply = await fixture.InvokeAsync();

        Assert.Contains(expectedVerdict, reply);
        Assert.Contains("unavailable", reply);
        Assert.Equal(0, fixture.Chat.NormalReplies);
    }

    private sealed class Fixture : IDisposable
    {
        public MemoryStore Store { get; } = new();
        public CustomerDisciplineService Service { get; }
        public DecisionChatClient Chat { get; } = new();
        public MessageContext Context { get; } = new();
        public LoreGetter Lore { get; } = new();
        public NotebookStore Notes { get; } = new();
        private readonly IPersonality _personality;
        private readonly IChatClient _client;

        public Fixture()
        {
            Service = new CustomerDisciplineService(Store, TimeProvider.System);
            _client = new ChatClientBuilder(Chat).UseFunctionInvocation().Build();
            _personality = new LorekeeperPersonality(
                new EmojiHandler(),
                _client,
                new PersonalityTools(
                    null!,
                    null!,
                    null!,
                    new NotebookService(Notes, new SourceReader(), null!, null!, TimeProvider.System, NullLogger<NotebookService>.Instance),
                    NullLogger<PersonalityTools>.Instance,
                    Lore
                ),
                new LorekeeperPersonalityService(new PersonalityStore(), TimeProvider.System, NullLogger<LorekeeperPersonalityService>.Instance),
                Service,
                NullLogger<LorekeeperPersonality>.Instance
            );
        }

        public Task<string> InvokeAsync(string content = "Hello") =>
            _personality.InvokeAsync(new ChatHistory([new ChatMessage(ChatRole.User, content)]), Context, TestContext.Current.CancellationToken);

        public Task<CustomerDisciplineState> GetAsync() => Service.GetAsync(2, 1, TestContext.Current.CancellationToken);

        public async Task BarAsync()
        {
            for (ulong id = 1; id <= 3; id++)
                await Service.IssueStrikeAsync(2, 1, id, "Cheek", TestContext.Current.CancellationToken);
        }

        public void Dispose() => _client.Dispose();
    }

    private sealed class DecisionChatClient : IChatClient
    {
        public string? Action { get; set; }
        public string? SecondAction { get; set; }
        public Func<Task>? BeforeDecision { get; set; }
        public bool FailDecisionFollowUp { get; set; }
        public bool FailNormalResponse { get; set; }
        public int Decisions { get; private set; }
        public int NormalReplies { get; private set; }
        public string[] DecisionTools { get; private set; } = [];
        public string[] NormalTools { get; private set; } = [];
        public List<string> Results { get; } = [];
        public string? NormalPrompt { get; private set; }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            var history = messages.ToList();
            if (!history[0].Text!.Contains("Decide only", StringComparison.Ordinal))
            {
                if (FailNormalResponse)
                    throw new HttpRequestException("Controlled normal response failure");
                NormalReplies++;
                NormalPrompt = history[0].Text;
                NormalTools = options!.Tools!.Select(tool => tool.Name).ToArray();
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, "Normal answer"));
            }

            var results = history
                .Where(message => message.Role == ChatRole.Tool)
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .ToArray();
            if (results.Length > 0 && FailDecisionFollowUp)
                throw new HttpRequestException("Controlled decision follow-up failure");
            if (results.Length == 0)
            {
                Decisions++;
                DecisionTools = options!.Tools!.Select(tool => tool.Name).ToArray();
                if (BeforeDecision is not null)
                    await BeforeDecision();
            }
            Results.AddRange(results.Select(result => result.Result!.ToString()!));
            var action =
                results.Length == 0 ? Action
                : results.Length == 1 ? SecondAction
                : null;
            if (action is null)
                return new ChatResponse(new ChatMessage(ChatRole.Assistant, "Private decision"));

            return new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    [
                        new FunctionCallContent(
                            "call-" + results.Length,
                            action,
                            action == "issue_customer_strike" ? new Dictionary<string, object?> { ["reason"] = "Insulting the chips" } : []
                        ),
                    ]
                )
            )
            {
                FinishReason = ChatFinishReason.ToolCalls,
            };
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class MessageContext : IMessageContext
    {
        public ulong UserId => 1;
        public string UserName => "Tester";
        public ulong? ChannelId => 3;
        public ulong? GuildId => 2;
        public ulong? MessageId { get; set; } = 4;

        public Task Reply(string message) => Task.CompletedTask;
    }

    private sealed class EmojiHandler : IEmojiHandler
    {
        public IEnumerable<string> GetEmojis() => [];

        public string ProcessMessage(string message) => message;
    }

    private sealed class PersonalityStore : ILorekeeperPersonalityStore
    {
        public Task<LorekeeperPersonalitySetting?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<LorekeeperPersonalitySetting?>(null);

        public Task SaveAsync(LorekeeperPersonalitySetting setting, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed record LinkedMember : IMemberLore
    {
        public string Name => "Leaf";
        public ulong? DiscordUserId => 1;
        public string Pronouns => "Unknown";
        public string Nationality => "Unknown";
        public string MainCharacter => "Restoration shaman";
        public string? Biography => null;
        public IReadOnlyList<string> Aliases => [];
        public IReadOnlyList<string> Roles => [];
    }

    private sealed class LoreGetter : IGetLore
    {
        public IMemberLore? Member { get; set; }

        public Task<IReadOnlyList<ILore>> GetLoreAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ILore?> GetLoreAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IMemberLore?> GetMemberLoreAsync(ulong discordUserId, CancellationToken cancellationToken = default) => Task.FromResult(Member);
    }

    private sealed class NotebookStore : INotebookStore
    {
        public int Reads { get; private set; }

        public Task<NotebookState> GetAsync(CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(new NotebookState());
        }

        public Task<bool> TrySaveAsync(NotebookState state, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class SourceReader : INotebookSourceReader
    {
        public IReadOnlySet<string> GetAccessibleSourceUrls(IReadOnlyList<string> urls, IMessageContext messageContext) => urls.ToHashSet();

        public Task<IReadOnlyList<NotebookSource>?> ReadSourcesAsync(
            IReadOnlyList<string> urls,
            IMessageContext messageContext,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();
    }

    private sealed class MemoryStore : ICustomerDisciplineStore
    {
        private CustomerDisciplineState? _state;
        public bool FailRead { get; set; }
        public bool FailSave { get; set; }
        public bool FailAfterSave { get; set; }
        public bool FailReadAfterSave { get; set; }

        public Task<CustomerDisciplineState?> GetAsync(string id, CancellationToken cancellationToken) =>
            FailRead ? throw new InvalidOperationException("Controlled read failure") : Task.FromResult(_state);

        public Task<bool> TrySaveAsync(CustomerDisciplineState state, CancellationToken cancellationToken)
        {
            if (FailSave)
                throw new InvalidOperationException("Controlled write failure");
            if ((_state?.Revision ?? 0) != state.Revision)
                return Task.FromResult(false);
            _state = state with { Revision = state.Revision + 1 };
            FailRead = FailReadAfterSave;
            if (FailAfterSave)
                throw new InvalidOperationException("Controlled acknowledgement failure");
            return Task.FromResult(true);
        }
    }
}
