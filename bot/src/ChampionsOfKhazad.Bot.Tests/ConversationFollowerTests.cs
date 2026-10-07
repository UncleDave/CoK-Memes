using System.ComponentModel.DataAnnotations;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Tests;

public class ConversationFollowerTests
{
    [Fact]
    public async Task AnyAuthorCanTriggerConsecutiveResponsesWithoutACooldown()
    {
        var fixture = new FollowerFixture();
        var follower = fixture.CreateFollower(100);

        await follower.Handle(new MessageReceived(fixture.CreateMessage(123)), TestContext.Current.CancellationToken);
        await follower.Handle(new MessageReceived(fixture.CreateMessage(456)), TestContext.Current.CancellationToken);

        Assert.Equal(2, fixture.Replies.Count);
        Assert.Equal(new ulong[] { 123, 456 }, fixture.RespondedTo);
        Assert.All(
            fixture.Replies,
            reply => Assert.Contains(reply, new[] { "Sycophant", "Contrarian", "DisappointedTeacher", "CondescendingTeacher", "StonerBro" })
        );
    }

    [Fact]
    public async Task SpontaneousInvocationContextKeepsEarlierConversationAsBackground()
    {
        var fixture = new FollowerFixture();
        fixture.History.Add(fixture.CreateMessage(FollowerFixture.BotId, content: "An earlier bot persona"));
        fixture.History.Add(fixture.CreateMessage(789, content: "An earlier question from someone else"));

        await fixture.CreateFollower(100).Handle(new MessageReceived(fixture.CreateMessage(456)), TestContext.Current.CancellationToken);

        var history = Assert.Single(fixture.ReceivedHistories);
        Assert.Equal(ChatRole.System, history[0].Role);
        Assert.Contains("spontaneously joining an ongoing Discord conversation", history[0].Text);
        Assert.Contains("Respond only to the final user message from the current author", history[0].Text);
        Assert.Contains("Earlier messages are background, not pending requests", history[0].Text);
        Assert.Contains("Use only the personality selected for this invocation", history[0].Text);
        Assert.Equal(
            new[] { "An earlier question from someone else", "An earlier bot persona", "A message from anyone" },
            history.Skip(1).Select(message => message.Text)
        );
        Assert.Equal(new[] { ChatRole.User, ChatRole.Assistant, ChatRole.User }, history.Skip(1).Select(message => message.Role));
        Assert.Equal(new ulong[] { 456 }, fixture.RespondedTo);
        Assert.Single(fixture.Replies);
    }

    [Fact]
    public async Task FileOnlyMessageRemainsTheCurrentMessageInsteadOfAnEarlierQuestion()
    {
        var fixture = new FollowerFixture();
        fixture.History.Add(fixture.CreateMessage(789, content: "An earlier question from someone else"));
        var attachment = DiscordConversationFixture.Stub<IAttachment>(
            (method, _) => method.Name == "get_Filename" ? "raid.txt" : throw new NotSupportedException(method.Name)
        );

        await fixture
            .CreateFollower(100)
            .Handle(
                new MessageReceived(fixture.CreateMessage(456, content: string.Empty, attachments: [attachment])),
                TestContext.Current.CancellationToken
            );

        var history = Assert.Single(fixture.ReceivedHistories);
        Assert.Equal("An earlier question from someone else", history[1].Text);
        Assert.Equal(ChatRole.User, history[^1].Role);
        Assert.Equal("The current Discord message contains no text or supported image content.", history[^1].Text);
        Assert.Equal(new ulong[] { 456 }, fixture.RespondedTo);
    }

    [Fact]
    public async Task SplitPersonalityStrategyWithoutInvocationContextKeepsPlainHistory()
    {
        var fixture = new FollowerFixture();
        var strategy = new SplitPersonalityFollowerResponseStrategy(
            [new CapturingPersonality("answer", fixture.RespondedTo, fixture.ReceivedHistories)],
            FollowerFixture.BotId
        );

        var response = await strategy.GetResponseAsync(new MessageReceived(fixture.CreateMessage(456)), TestContext.Current.CancellationToken);

        Assert.Equal("answer", response);
        var message = Assert.Single(Assert.Single(fixture.ReceivedHistories));
        Assert.Equal(ChatRole.User, message.Role);
        Assert.Equal("A message from anyone", message.Text);
    }

    [Fact]
    public async Task ZeroChanceDisablesResponses()
    {
        var fixture = new FollowerFixture();

        await fixture.CreateFollower(0).Handle(new MessageReceived(fixture.CreateMessage(123)), TestContext.Current.CancellationToken);

        Assert.Empty(fixture.Replies);
        Assert.Empty(fixture.RespondedTo);
    }

    [Fact]
    public async Task BotMentionInIgnoredChannelDoesNotTrigger()
    {
        var fixture = new FollowerFixture();

        await fixture
            .CreateFollower(100, ignoreBotMentionsInChannelId: FollowerFixture.ChannelId)
            .Handle(new MessageReceived(fixture.CreateMessage(123, mentionBot: true)), TestContext.Current.CancellationToken);

        Assert.Empty(fixture.Replies);
        Assert.Empty(fixture.RespondedTo);
    }

    [Fact]
    public async Task DirectMessagesDoNotTrigger()
    {
        var fixture = new FollowerFixture();
        var directChannel = DiscordConversationFixture.Stub<IDMChannel>((_, _) => throw new InvalidOperationException("DM must not be accessed"));

        await fixture
            .CreateFollower(100)
            .Handle(new MessageReceived(fixture.CreateMessage(123, channel: directChannel)), TestContext.Current.CancellationToken);

        Assert.Empty(fixture.Replies);
        Assert.Empty(fixture.RespondedTo);
    }

    [Fact]
    public void FractionalChanceBindsWithoutATargetUser()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Followers:Conversation:Chance"] = "0.1" })
            .Build();

        var options = configuration.GetFollowerSection(ConversationFollowerOptions.Key).Get<ConversationFollowerOptions>();

        Assert.NotNull(options);
        Assert.Equal(0.1, options.Chance);
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public void ChanceMustBeAValidPercentage(double chance)
    {
        var options = new ConversationFollowerOptions { Chance = chance };

        Assert.Throws<ValidationException>(() => Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true));
    }

    private sealed class FollowerFixture
    {
        public const ulong ChannelId = 100;
        public const ulong BotId = 9;
        public List<string> Replies { get; } = [];
        public List<ulong> RespondedTo { get; } = [];
        public List<IMessage> History { get; } = [];
        public List<ChatHistory> ReceivedHistories { get; } = [];

        public ConversationFollower CreateFollower(double chance, ulong ignoreBotMentionsInChannelId = 0) =>
            new(
                Options.Create(new AllFollowersOptions { IgnoreBotMentionsInChannelId = ignoreBotMentionsInChannelId }),
                Options.Create(new ConversationFollowerOptions { Chance = chance }),
                DiscordConversationFixture.Stub<ICompletionService>(
                    (method, _) =>
                        method.Name switch
                        {
                            "get_Sycophant" => new CapturingPersonality("Sycophant", RespondedTo, ReceivedHistories),
                            "get_Contrarian" => new CapturingPersonality("Contrarian", RespondedTo, ReceivedHistories),
                            "get_DisappointedTeacher" => new CapturingPersonality("DisappointedTeacher", RespondedTo, ReceivedHistories),
                            "get_CondescendingTeacher" => new CapturingPersonality("CondescendingTeacher", RespondedTo, ReceivedHistories),
                            "get_StonerBro" => new CapturingPersonality("StonerBro", RespondedTo, ReceivedHistories),
                            _ => throw new NotSupportedException(method.Name),
                        }
                ),
                new BotContext(BotId, null!, null!),
                NullLogger<RandomChanceFollowerTriggerStrategy>.Instance
            );

        public IUserMessage CreateMessage(
            ulong authorId,
            bool mentionBot = false,
            IMessageChannel? channel = null,
            string content = "A message from anyone",
            IReadOnlyCollection<IAttachment>? attachments = null
        )
        {
            var author = DiscordConversationFixture.Stub<IUser>(
                (method, _) =>
                    method.Name switch
                    {
                        "get_Id" => authorId,
                        "get_GlobalName" or "get_Username" => "Raider",
                        _ => throw new NotSupportedException(method.Name),
                    }
            );
            channel ??= DiscordConversationFixture.Stub<ITextChannel>(
                (method, args) =>
                    method.Name switch
                    {
                        "get_Id" => ChannelId,
                        "EnterTypingState" => new NoopDisposable(),
                        "GetMessagesAsync" => GetHistory(),
                        "SendMessageAsync" => Send((string)args![0]!),
                        _ => throw new NotSupportedException(method.Name),
                    }
            );
            return DiscordConversationFixture.Stub<IUserMessage>(
                (method, _) =>
                    method.Name switch
                    {
                        "get_Channel" => channel,
                        "get_Author" => author,
                        "get_Content" or "get_CleanContent" => content,
                        "get_Attachments" => attachments ?? Array.Empty<IAttachment>(),
                        "get_MentionedUserIds" => mentionBot ? new[] { BotId } : Array.Empty<ulong>(),
                        _ => throw new NotSupportedException(method.Name),
                    }
            );
        }

        private Task<IUserMessage> Send(string content)
        {
            Replies.Add(content);
            return Task.FromResult<IUserMessage>(null!);
        }

        private async IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetHistory()
        {
            await Task.CompletedTask;
            yield return History.ToArray();
        }
    }

    private sealed class CapturingPersonality(string response, List<ulong> respondedTo, List<ChatHistory> histories) : IPersonality
    {
        public Task<string> InvokeAsync(ChatHistory chatHistory, IMessageContext messageContext, CancellationToken cancellationToken = default)
        {
            histories.Add(chatHistory);
            respondedTo.Add(messageContext.UserId);
            return Task.FromResult(response);
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
