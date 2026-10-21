using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class CustomerStrikeReviewerTests
{
    [Fact]
    public async Task ReviewReceivesOriginalConversationAndProposalButNoMutationOrServiceTools()
    {
        var client = new ChatClient("{\"approved\":true,\"evidence\":\"kicking over the fryer\"}");
        var history = new ChatHistory([
            new ChatMessage(ChatRole.Assistant, "Previous reply"),
            new ChatMessage(ChatRole.User, "I'm kicking over the fryer"),
        ]);

        Assert.True(await ReviewAsync(client, history));

        Assert.Empty(client.Options!.Tools!);
        Assert.Equal(ChatResponseFormat.Json, client.Options.ResponseFormat);
        Assert.Equal(ReasoningEffort.Medium, client.Options.Reasoning!.Effort);
        using var input = JsonDocument.Parse(client.Messages[^1].Text!);
        Assert.Equal("1", input.RootElement.GetProperty("customerId").GetString());
        Assert.Equal("4", input.RootElement.GetProperty("requestMessageId").GetString());
        Assert.Equal("Proposed conduct", input.RootElement.GetProperty("proposedReason").GetString());
        Assert.Equal("I'm kicking over the fryer", input.RootElement.GetProperty("currentMessage").GetString());
        Assert.Equal("Previous reply", input.RootElement.GetProperty("conversation")[0].GetProperty("text").GetString());
    }

    [Theory]
    [InlineData("{\"approved\":false,\"evidence\":null}")]
    [InlineData("{\"approved\":true}")]
    [InlineData("{\"approved\":\"true\",\"evidence\":\"fryer\"}")]
    [InlineData("{\"approved\":true,\"evidence\":null}")]
    [InlineData("{\"approved\":true,\"evidence\":\" \"}")]
    [InlineData("{\"approved\":true,\"evidence\":\"invented conduct\"}")]
    [InlineData("{\"approved\":true,\"evidence\":\"fryer\",\"extra\":true}")]
    [InlineData("{\"approved\":true,\"approved\":true}")]
    [InlineData("{\"evidence\":\"fryer\",\"evidence\":\"fryer\"}")]
    [InlineData("[]")]
    [InlineData("APPROVE")]
    [InlineData("")]
    public async Task RejectionMalformedOutputAndMissingOrInventedEvidenceDoNotApproveAStrike(string output)
    {
        Assert.False(await ReviewAsync(new ChatClient(output), History("I'm kicking over the fryer")));
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("timeout")]
    [InlineData("truncated")]
    [InlineData("tool-call")]
    [InlineData("oversized")]
    [InlineData("long-evidence")]
    public async Task UnusableReviewFailsClosed(string mode)
    {
        var text = new string('x', mode == "long-evidence" ? 201 : 5);
        var client = new ChatClient(JsonSerializer.Serialize(new { approved = true, evidence = text }))
        {
            Failure = mode switch
            {
                "failure" => new HttpRequestException("Review unavailable"),
                "timeout" => new OperationCanceledException("Review deadline"),
                _ => null,
            },
            FinishReason = mode == "truncated" ? ChatFinishReason.Length : null,
            ToolCall = mode == "tool-call",
        };
        if (mode == "oversized")
            client.Output += new string(' ', 2001);

        Assert.False(await ReviewAsync(client, History(text)));
    }

    [Fact]
    public async Task EvidenceFromSomeoneElseOrAnEarlierMessageDoesNotApproveTheCurrentQuestion()
    {
        var history = new ChatHistory([
            new ChatMessage(ChatRole.User, "I'm kicking over the fryer"),
            new ChatMessage(ChatRole.User, "Who has strikes?"),
        ]);

        Assert.False(await ReviewAsync(new ChatClient("{\"approved\":true,\"evidence\":\"kicking over the fryer\"}"), history));
    }

    [Theory]
    [InlineData(1UL, 4UL, "currentRequest", "kicking over the fryer", true)]
    [InlineData(1UL, 4UL, "currentRequest", "Offensive display name", false)]
    [InlineData(2UL, 4UL, "currentRequest", "kicking over the fryer", false)]
    [InlineData(1UL, 5UL, "currentRequest", "kicking over the fryer", false)]
    [InlineData(1UL, 4UL, "recent", "kicking over the fryer", false)]
    public async Task DiscordMetadataMustMatchTheCurrentAuthorAndRequestAndCannotSupplyMisconductEvidence(
        ulong authorId,
        ulong messageId,
        string contextKind,
        string evidence,
        bool expected
    )
    {
        var metadata = JsonSerializer.Serialize(
            new
            {
                authorId,
                messageId,
                contextKind,
                authorName = "Offensive display name",
            }
        );
        var history = new ChatHistory([
            new ChatMessage(
                ChatRole.User,
                [new TextContent($"Discord message metadata: {metadata}\nMessage content follows:"), new TextContent("I'm kicking over the fryer")]
            ),
        ]);
        var client = new ChatClient(JsonSerializer.Serialize(new { approved = true, evidence }));

        Assert.Equal(expected, await ReviewAsync(client, history));
        if (authorId != 1 || messageId != 4 || contextKind != "currentRequest")
            Assert.Equal(0, client.Calls);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("assistant")]
    [InlineData("no-text")]
    [InlineData("oversized")]
    public async Task MissingOrOversizedCurrentRequestSkipsReview(string mode)
    {
        var history = mode switch
        {
            "empty" => new ChatHistory(),
            "assistant" => new ChatHistory([new ChatMessage(ChatRole.Assistant, "Assistant claim")]),
            "no-text" => History(" "),
            _ => History(new string('x', 32001)),
        };
        var client = new ChatClient("{\"approved\":true,\"evidence\":\"x\"}");

        Assert.False(await ReviewAsync(client, history));
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task RecentContextIsBoundedAndDoesNotIncludeTheCurrentRequestTwice()
    {
        var history = new ChatHistory(Enumerable.Range(1, 30).Select(id => new ChatMessage(ChatRole.User, $"Message {id}")));
        var client = new ChatClient("{\"approved\":false,\"evidence\":null}");

        Assert.False(await ReviewAsync(client, history));

        using var input = JsonDocument.Parse(client.Messages[^1].Text!);
        Assert.Equal(11, input.RootElement.GetProperty("conversation").GetArrayLength());
        Assert.Equal("Message 19", input.RootElement.GetProperty("conversation")[0].GetProperty("text").GetString());
        Assert.Equal("Message 30", input.RootElement.GetProperty("currentMessage").GetString());
    }

    [Fact]
    public async Task CallerCancellationPropagatesEvenIfTheClientReturnsAnApproval()
    {
        using var cancellation = new CancellationTokenSource();
        var client = new ChatClient("{\"approved\":true,\"evidence\":\"fryer\"}") { BeforeResponse = cancellation.Cancel };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CustomerStrikeReviewer(client, NullLogger.Instance).ReviewAsync(
                History("fryer"),
                new MessageContext(),
                "Proposed conduct",
                cancellation.Token
            )
        );
    }

    private static Task<bool> ReviewAsync(ChatClient client, ChatHistory history) =>
        new CustomerStrikeReviewer(client, NullLogger.Instance).ReviewAsync(
            history,
            new MessageContext(),
            "Proposed conduct",
            TestContext.Current.CancellationToken
        );

    private static ChatHistory History(string content) => new([new ChatMessage(ChatRole.User, content)]);

    private sealed class MessageContext : IMessageContext
    {
        public ulong UserId => 1;
        public string UserName => "Tester";
        public ulong? GuildId => 2;
        public ulong? ChannelId => 3;
        public ulong? MessageId => 4;

        public Task Reply(string message) => throw new NotSupportedException();
    }

    private sealed class ChatClient(string output) : IChatClient
    {
        public string Output { get; set; } = output;
        public Exception? Failure { get; set; }
        public ChatFinishReason? FinishReason { get; set; }
        public bool ToolCall { get; set; }
        public Action? BeforeResponse { get; set; }
        public int Calls { get; private set; }
        public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];
        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            Calls++;
            Messages = messages.ToArray();
            Options = options;
            BeforeResponse?.Invoke();
            if (Failure is not null)
                throw Failure;
            var message = new ChatMessage(ChatRole.Assistant, Output);
            if (ToolCall)
                message.Contents.Add(new FunctionCallContent("unexpected", "issue_customer_strike", new Dictionary<string, object?>()));
            return Task.FromResult(new ChatResponse(message) { FinishReason = FinishReason });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
