using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class GrubPreviewPersonalityTests
{
    [Fact]
    public async Task PreviewUsesGrubVoiceWithReadOnlyToolsAndPreservesConversation()
    {
        var chat = new CapturingClient();
        var tools = new PersonalityTools(null!, null!, null!, null!, NullLogger<PersonalityTools>.Instance, new EmptyLoreGetter());
        var personality = new GrubPreviewPersonality(new EmojiHandler(), chat, tools);
        var history = new ChatHistory
        {
            new(ChatRole.Assistant, "An earlier friendly dwarf reply") { AuthorName = "Hammerstone" },
            new(ChatRole.User, "Why do I keep dying to lifts?"),
        };

        var reply = await personality.InvokeAsync(history, new MessageContext(), TestContext.Current.CancellationToken);

        Assert.Equal("Mind the lift, you muppet.", reply);
        Assert.Equal(history, chat.Messages.Skip(1));
        var prompt = chat.Messages[0].Text!;
        Assert.Contains(GrubPreviewPersonality.Prompt.Replace("{{$userName}}", "Tester"), prompt);
        Assert.Contains(GuildPromptContext.Identity, prompt);
        Assert.DoesNotContain("{{$", prompt);
        Assert.Contains("not evidence that the guild has changed its name", prompt);
        Assert.Contains("cannot save notes, edit lore, generate images, or issue strikes or pardons", prompt);
        Assert.Contains(chat.Tools, tool => tool is HostedWebSearchTool);
        Assert.Equal(
            new[]
            {
                "find_discord_channels",
                "read_discord_messages",
                "search_generated_images",
                "search_lore",
                "search_notebook",
                "search_discord_messages",
            }.Order(),
            chat.Tools.OfType<AIFunction>().Select(tool => tool.Name).Order()
        );
        Assert.Equal(ReasoningEffort.Medium, chat.Options!.Reasoning?.Effort);
    }

    [Fact]
    public async Task PreviewRetainsLinkedMemberRecallWithoutAnyNotebookWrites()
    {
        var chat = new CapturingClient();
        var source = new NotebookSource("https://discord.com/channels/1/3/4", 99, "Witness", DateTime.UtcNow, "Leaf fell off the lift")
        {
            ContentHash = "source-hash",
            ViewHash = "view-hash",
        };
        var store = new ReadOnlyNotebookStore
        {
            State = new NotebookState
            {
                Notes =
                [
                    new NotebookNote(
                        "leaf-lift",
                        99,
                        "Leaf's lift mishap",
                        "joke",
                        "Witness reported Leaf falling off the lift",
                        "",
                        [source],
                        DateTime.UtcNow,
                        DateTime.UtcNow.AddDays(1)
                    )
                    {
                        ReviewDelivered = true,
                    },
                ],
            },
        };
        var notebook = new NotebookService(store, new SourceReader(source), null!, null!, TimeProvider.System, NullLogger<NotebookService>.Instance);
        var tools = new PersonalityTools(null!, null!, null!, notebook, NullLogger<PersonalityTools>.Instance, new LinkedLoreGetter());

        await new GrubPreviewPersonality(new EmojiHandler(), chat, tools).InvokeAsync(
            new ChatHistory(),
            new MessageContext(),
            TestContext.Current.CancellationToken
        );

        Assert.Contains("Restoration shaman", chat.Messages[0].Text);
        Assert.Contains("Witness reported Leaf", chat.Messages[0].Text);
        Assert.Contains(source.Url, chat.Messages[0].Text);
        Assert.Contains("NOT canon", chat.Messages[0].Text);
        Assert.Equal(2, store.Reads);
        var search = Assert.IsAssignableFrom<AIFunction>(chat.Tools.Single(tool => tool.Name == "search_notebook"));
        var arguments = new AIFunctionArguments { ["query"] = "lifts" };
        for (var index = 0; index < 2; index++)
            Assert.Contains("No accessible", (await search.InvokeAsync(arguments, TestContext.Current.CancellationToken))!.ToString());
        Assert.Contains("Only three", (await search.InvokeAsync(arguments, TestContext.Current.CancellationToken))!.ToString());
        Assert.Equal(6, store.Reads);
        Assert.Equal(0, store.SaveAttempts);
    }

    [Fact]
    public async Task ReadOnlyPreviewDoesNotRemoveNormalLorekeeperCapabilitiesOnTheSameToolsInstance()
    {
        var tools = new PersonalityTools(null!, null!, null!, null!, NullLogger<PersonalityTools>.Instance, new EmptyLoreGetter());
        var context = new MessageContext();
        var preview = tools.Create(context, true, allowWrites: false);
        Assert.DoesNotContain(preview, tool => tool.Name is "remember_note" or "generate_image");

        var normal = tools.Create(context, true);

        Assert.Contains(normal, tool => tool.Name == "remember_note");
        Assert.Contains(normal, tool => tool.Name == "generate_image");
        Assert.Equal("Lorekeeper Hammerstone", Constants.LorekeeperName);
        Assert.Equal("Hammerstone", Constants.OpenAiFriendlyLorekeeperName);
    }

    private sealed class MessageContext : IMessageContext
    {
        public ulong UserId => 42;
        public string UserName => "Tester";
        public ulong? ChannelId => 3;

        public Task Reply(string message) => throw new InvalidOperationException("The preview must not send tool-side confirmations.");
    }

    private sealed class EmojiHandler : IEmojiHandler
    {
        public IEnumerable<string> GetEmojis() => [];

        public string ProcessMessage(string message) => message;
    }

    private sealed record Member(string Name) : IMemberLore
    {
        public ulong? DiscordUserId => 42;
        public string Pronouns => "Unknown";
        public string Nationality => "Unknown";
        public string MainCharacter => "Restoration shaman";
        public string? Biography => null;
        public IReadOnlyList<string> Aliases => [];
        public IReadOnlyList<string> Roles => [];
    }

    private sealed class LinkedLoreGetter : IGetLore
    {
        public Task<IReadOnlyList<ILore>> GetLoreAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ILore?> GetLoreAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IMemberLore?> GetMemberLoreAsync(ulong discordUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IMemberLore?>(new Member("Leaf"));
    }

    private sealed class ReadOnlyNotebookStore : INotebookStore
    {
        public int Reads { get; private set; }
        public int SaveAttempts { get; private set; }
        public NotebookState State { get; init; } = new();

        public Task<NotebookState> GetAsync(CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(State);
        }

        public Task<bool> TrySaveAsync(NotebookState state, CancellationToken cancellationToken)
        {
            SaveAttempts++;
            throw new InvalidOperationException("Preview recall must not write.");
        }
    }

    private sealed class SourceReader(NotebookSource source) : INotebookSourceReader
    {
        public IReadOnlySet<string> GetAccessibleSourceUrls(IReadOnlyList<string> urls, IMessageContext messageContext) => urls.ToHashSet();

        public Task<IReadOnlyList<NotebookSource>?> ReadSourcesAsync(
            IReadOnlyList<string> urls,
            IMessageContext messageContext,
            CancellationToken cancellationToken
        ) => Task.FromResult<IReadOnlyList<NotebookSource>?>([source]);
    }

    private sealed class CapturingClient : IChatClient
    {
        public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];
        public IReadOnlyList<AITool> Tools { get; private set; } = [];
        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            Messages = messages.ToArray();
            Options = options;
            Tools = options!.Tools!.ToArray();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Mind the lift, you muppet.")));
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
