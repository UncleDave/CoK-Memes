using Microsoft.Extensions.AI;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class CompletionServiceTests
{
    [Fact]
    public async Task ExplicitEffortIsSentWithHistoryAndCancellation()
    {
        var client = new CapturingClient();
        ICompletionService service = new CompletionService(client, null!, null!, null!, null!, null!, null!, null!);
        var history = new ChatHistory("Summarise these messages.");
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await service.InvokeAsync(history, ReasoningEffort.High, cancellationToken);

        Assert.Equal("Result", result);
        Assert.Equal(history, client.Messages);
        Assert.Equal(ReasoningEffort.High, client.Options?.Reasoning?.Effort);
        Assert.Equal(cancellationToken, client.CancellationToken);
    }

    [Fact]
    public async Task OrdinaryCompletionsKeepProviderDefaults()
    {
        var client = new CapturingClient();
        ICompletionService service = new CompletionService(client, null!, null!, null!, null!, null!, null!, null!);
        var history = new ChatHistory("Choose an emoji.");
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await service.InvokeAsync(history, cancellationToken);

        Assert.Equal("Result", result);
        Assert.Equal(history, client.Messages);
        Assert.Null(client.Options);
        Assert.Equal(cancellationToken, client.CancellationToken);
    }

    private sealed class CapturingClient : IChatClient
    {
        public IList<ChatMessage>? Messages { get; private set; }
        public ChatOptions? Options { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            Messages = messages.ToList();
            Options = options;
            CancellationToken = cancellationToken;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Result")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
