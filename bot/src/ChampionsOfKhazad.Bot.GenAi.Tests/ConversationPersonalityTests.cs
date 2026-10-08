using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class ConversationPersonalityTests
{
    [Theory]
    [InlineData("Sycophant")]
    [InlineData("Contrarian")]
    [InlineData("DisappointedTeacher")]
    [InlineData("CondescendingTeacher")]
    [InlineData("StonerBro")]
    public async Task PromptsTargetTheCurrentAuthorAndPreserveConversationContext(string personalityName)
    {
        var chatClient = new CapturingChatClient();
        var personality = CreatePersonality(personalityName, chatClient);
        var history = new ChatHistory
        {
            new(ChatRole.System, "Follower invocation context"),
            new(ChatRole.User, "An earlier question") { AuthorName = "OtherRaider" },
            new(ChatRole.Assistant, "A previous bot reply") { AuthorName = "You" },
            new(ChatRole.User, "Hello everyone") { AuthorName = "CurrentRaider" },
        };

        var response = await personality.InvokeAsync(history, new TestMessageContext(), TestContext.Current.CancellationToken);

        Assert.Equal("An in-character interjection", response);
        var messages = Assert.IsAssignableFrom<IReadOnlyList<ChatMessage>>(chatClient.Messages);
        Assert.Equal(ChatRole.System, messages[0].Role);
        Assert.Contains(GuildPromptContext.Identity, messages[0].Text);
        Assert.Contains(GuildPromptContext.Activity, messages[0].Text);
        Assert.Contains("You are responding to a Discord message from: CurrentRaider", messages[0].Text);
        Assert.Contains("The current author's Discord user ID is 456.", messages[0].Text);
        Assert.DoesNotContain("{{$", messages[0].Text);
        Assert.Equal(history.Select(message => message.Text), messages.Skip(1).Select(message => message.Text));
        Assert.Equal(history.Select(message => message.AuthorName), messages.Skip(1).Select(message => message.AuthorName));
        Assert.Equal(history.Select(message => message.Role), messages.Skip(1).Select(message => message.Role));
        Assert.Equal("search_lore", Assert.Single(chatClient.Options!.Tools!).Name);
    }

    [Theory]
    [InlineData("DisappointedTeacher")]
    [InlineData("CondescendingTeacher")]
    public async Task TeacherPromptsFrameTheActualMessageWithoutAssumingSubmittedWork(string personalityName)
    {
        var chatClient = new CapturingChatClient();

        await CreatePersonality(personalityName, chatClient)
            .InvokeAsync(new ChatHistory(), new TestMessageContext(), TestContext.Current.CancellationToken);

        var prompt = chatClient.Messages![0].Text;
        Assert.Contains("latest Discord message as if it were a classroom contribution", prompt);
        Assert.Contains("statement, question, greeting, or image", prompt);
        Assert.Contains("Do not invent submitted work", prompt);
        Assert.DoesNotContain("has just submitted work", prompt);
        Assert.DoesNotContain("has just demonstrated poor judgment", prompt);
    }

    [Fact]
    public async Task ContrarianPromptHandlesMessagesWithNoClaimToDispute()
    {
        var chatClient = new CapturingChatClient();

        await CreatePersonality("Contrarian", chatClient)
            .InvokeAsync(new ChatHistory(), new TestMessageContext(), TestContext.Current.CancellationToken);

        var prompt = chatClient.Messages![0].Text;
        Assert.Contains("If the latest message has no claim to dispute", prompt);
        Assert.Contains("greeting, question, or visible image content", prompt);
        Assert.Contains("Do not invent an opinion, assertion, or unseen image detail", prompt);
    }

    private static IPersonality CreatePersonality(string personalityName, IChatClient chatClient)
    {
        var emojiHandler = new PassThroughEmojiHandler();
        var tools = new PersonalityTools(null!, null!, null!, null!, NullLogger<PersonalityTools>.Instance);
        return personalityName switch
        {
            "Sycophant" => new SycophantPersonality(emojiHandler, chatClient, tools),
            "Contrarian" => new ContrarianPersonality(emojiHandler, chatClient, tools),
            "DisappointedTeacher" => new DisappointedTeacherPersonality(emojiHandler, chatClient, tools),
            "CondescendingTeacher" => new CondescendingTeacherPersonality(emojiHandler, chatClient, tools),
            "StonerBro" => new StonerBroPersonality(emojiHandler, chatClient, tools),
            _ => throw new ArgumentOutOfRangeException(nameof(personalityName)),
        };
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public IReadOnlyList<ChatMessage>? Messages { get; private set; }
        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            Messages = messages.ToArray();
            Options = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "An in-character interjection")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

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
        public ulong UserId => 456;
        public string UserName => "CurrentRaider";
        public ulong? ChannelId => 100;

        public Task Reply(string message) => Task.CompletedTask;
    }
}
