using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Responses;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class PersonalityBaseTests
{
    [Fact]
    public async Task LorekeeperReceivesWebSearchToolAndPolicy()
    {
        var chatClient = new CapturingChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Answer")));
        var personality = CreatePersonality(chatClient, includeLorekeeperTools: true);

        await personality.InvokeAsync(new ChatHistory(), new TestMessageContext(), TestContext.Current.CancellationToken);

        Assert.Contains(chatClient.Options!.Tools!, tool => tool is HostedWebSearchTool);
        Assert.Contains("You have public-web access through web_search.", chatClient.Messages![0].Text);
    }

    [Fact]
    public async Task OtherPersonalitiesDoNotReceiveWebSearchTool()
    {
        var chatClient = new CapturingChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Answer")));
        var personality = CreatePersonality(chatClient, includeLorekeeperTools: false);

        await personality.InvokeAsync(new ChatHistory(), new TestMessageContext(), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(chatClient.Options!.Tools!, tool => tool is HostedWebSearchTool);
        Assert.Contains("You do not have public-web access.", chatClient.Messages![0].Text);
    }

    [Fact]
    public async Task WebCitationsAreAppendedAsDistinctDiscordLinks()
    {
        var textContent = new TextContent("Answer")
        {
            Annotations =
            [
                new CitationAnnotation { Url = new Uri("https://example.com/article") },
                new CitationAnnotation { Url = new Uri("https://example.com/article") },
            ],
        };
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, [textContent]));
        var personality = CreatePersonality(new CapturingChatClient(response), includeLorekeeperTools: true);

        var result = await personality.InvokeAsync(new ChatHistory(), new TestMessageContext(), TestContext.Current.CancellationToken);

        Assert.Equal("Answer\n\nSources:\n- <https://example.com/article>", result);
    }

    [Fact]
    public void HostedWebSearchToolMapsToOpenAiWebSearchTool()
    {
#pragma warning disable MEAI001
#pragma warning disable OPENAI001
        var openAiTool = new HostedWebSearchTool().AsOpenAIResponseTool();
        Assert.IsType<WebSearchTool>(openAiTool);
#pragma warning restore OPENAI001
#pragma warning restore MEAI001
    }

    private static TestPersonality CreatePersonality(IChatClient chatClient, bool includeLorekeeperTools)
    {
        var personalityTools = new PersonalityTools(new EmptyRelatedLore(), null!, NullLogger<PersonalityTools>.Instance);
        return new TestPersonality(includeLorekeeperTools, new PassThroughEmojiHandler(), chatClient, personalityTools);
    }

    private sealed class TestPersonality(
        bool includeLorekeeperTools,
        IEmojiHandler emojiHandler,
        IChatClient chatClient,
        PersonalityTools personalityTools
    ) : PersonalityBase("Test personality", includeLorekeeperTools, emojiHandler, chatClient, personalityTools);

    private sealed class CapturingChatClient(ChatResponse response) : IChatClient
    {
        public IList<ChatMessage>? Messages { get; private set; }
        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            Messages = messages.ToList();
            Options = options;
            return Task.FromResult(response);
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

    private sealed class PassThroughEmojiHandler : IEmojiHandler
    {
        public IEnumerable<string> GetEmojis() => [];

        public string ProcessMessage(string message) => message;
    }

    private sealed class TestMessageContext : IMessageContext
    {
        public ulong UserId => 1;
        public string UserName => "Tester";

        public Task Reply(string message) => Task.CompletedTask;
    }

    private sealed class EmptyRelatedLore : IGetRelatedLore
    {
        public Task<IReadOnlyList<ILore>> GetRelatedLoreAsync(string text, uint max = 10) => Task.FromResult<IReadOnlyList<ILore>>([]);
    }
}
