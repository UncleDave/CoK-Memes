using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;
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

    [Theory]
    [InlineData(0, 100, 123UL)]
    [InlineData(100, 0, 456UL)]
    public async Task TargetRoleChanceReplacesTheDefaultChanceWithoutAnExtraRoll(double chance, double targetRoleChance, ulong respondingAuthorId)
    {
        var fixture = new FollowerFixture();
        var follower = fixture.CreateFollower(chance, targetRoleId: FollowerFixture.TargetRoleId, targetRoleChance: targetRoleChance);

        await follower.Handle(
            new MessageReceived(fixture.CreateMessage(123, roleIds: [7, FollowerFixture.TargetRoleId, 8])),
            TestContext.Current.CancellationToken
        );
        await follower.Handle(new MessageReceived(fixture.CreateMessage(456, roleIds: [7, 8])), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { respondingAuthorId }, fixture.RespondedTo);
        Assert.Single(fixture.Replies);
    }

    [Theory]
    [InlineData(0, 100, false)]
    [InlineData(100, 0, true)]
    public async Task AuthorsWithoutGuildRoleMetadataUseTheDefaultChance(double chance, double targetRoleChance, bool shouldRespond)
    {
        var fixture = new FollowerFixture();
        var follower = fixture.CreateFollower(chance, targetRoleId: FollowerFixture.TargetRoleId, targetRoleChance: targetRoleChance);

        await follower.Handle(new MessageReceived(fixture.CreateMessage(123)), TestContext.Current.CancellationToken);

        Assert.Equal(shouldRespond ? 1 : 0, fixture.Replies.Count);
    }

    [Fact]
    public async Task AddingAndRemovingTheTargetRoleChangesEligibility()
    {
        var fixture = new FollowerFixture();
        var follower = fixture.CreateFollower(0, targetRoleId: FollowerFixture.TargetRoleId, targetRoleChance: 100);
        var roles = new List<ulong>();
        var notification = new MessageReceived(fixture.CreateMessage(123, roleIds: roles));

        await follower.Handle(notification, TestContext.Current.CancellationToken);
        Assert.Empty(fixture.Replies);

        roles.Add(FollowerFixture.TargetRoleId);
        await follower.Handle(notification, TestContext.Current.CancellationToken);
        Assert.Single(fixture.Replies);

        roles.Clear();
        await follower.Handle(notification, TestContext.Current.CancellationToken);
        Assert.Single(fixture.Replies);
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PreviewSuppressesBothAiFollowersWithAndWithoutBotMentions(bool ratFacts, bool mentionBot)
    {
        var fixture = new FollowerFixture();
        StrategyFollower follower = ratFacts
            ? fixture.CreateRatFactsFollower(FollowerFixture.ChannelId)
            : fixture.CreateFollower(100, grubPreviewChannelId: FollowerFixture.ChannelId);

        await follower.Handle(new MessageReceived(fixture.CreateMessage(123, mentionBot: mentionBot)), TestContext.Current.CancellationToken);

        Assert.Empty(fixture.Replies);
        Assert.Empty(fixture.RespondedTo);
        Assert.Empty(fixture.ReceivedHistories);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, 200UL)]
    [InlineData(true, 200UL)]
    public async Task AiFollowersRemainEnabledWhenPreviewIsDisabledOrInAnotherChannel(bool ratFacts, ulong? grubPreviewChannelId)
    {
        var fixture = new FollowerFixture();
        StrategyFollower follower = ratFacts
            ? fixture.CreateRatFactsFollower(grubPreviewChannelId)
            : fixture.CreateFollower(100, grubPreviewChannelId: grubPreviewChannelId);

        await follower.Handle(new MessageReceived(fixture.CreateMessage(123)), TestContext.Current.CancellationToken);

        Assert.Single(fixture.Replies);
        Assert.Equal(new ulong[] { 123 }, fixture.RespondedTo);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviewFollowerSuppressionDoesNotApplyToChannelsInAMatchingCategory(bool ratFacts)
    {
        var fixture = new FollowerFixture();
        StrategyFollower follower = ratFacts
            ? fixture.CreateRatFactsFollower(FollowerFixture.ChannelId)
            : fixture.CreateFollower(100, grubPreviewChannelId: FollowerFixture.ChannelId);

        await follower.Handle(
            new MessageReceived(fixture.CreateMessage(123, channelId: 200, categoryId: FollowerFixture.ChannelId)),
            TestContext.Current.CancellationToken
        );

        Assert.Single(fixture.Replies);
        Assert.Equal(new ulong[] { 123 }, fixture.RespondedTo);
    }

    [Fact]
    public async Task WordOfTheDayRemainsEnabledInThePreviewChannel()
    {
        var fixture = new FollowerFixture();
        ulong? winnerId = null;
        var follower = new WordOfTheDayFollower(
            new BotContext(FollowerFixture.BotId, null!, null!),
            DiscordConversationFixture.Stub<IGetTheWordOfTheDay>(
                (method, _) =>
                    method.Name == "GetWordOfTheDayAsync"
                        ? Task.FromResult(new WordOfTheDay("message", DateOnly.FromDateTime(DateTime.UtcNow)))
                        : throw new NotSupportedException(method.Name)
            ),
            DiscordConversationFixture.Stub<IWinTheWordOfTheDay>(
                (method, args) =>
                {
                    if (method.Name != "WinWordOfTheDayAsync")
                        throw new NotSupportedException(method.Name);
                    winnerId = (ulong)args![0]!;
                    return Task.FromResult((ushort)1);
                }
            )
        );
        var message = fixture.CreateMessage(123, mentionBot: true);

        await fixture
            .CreateFollower(100, grubPreviewChannelId: FollowerFixture.ChannelId)
            .Handle(new MessageReceived(message), TestContext.Current.CancellationToken);
        await follower.Handle(new MessageReceived(message), TestContext.Current.CancellationToken);

        Assert.Equal(123UL, winnerId);
        Assert.Contains("You found the word of the day!", Assert.Single(fixture.Replies));
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
    public void FractionalChanceBindsWithoutATargetRole()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Followers:Conversation:Chance"] = "0.1" })
            .Build();

        var options = configuration.GetFollowerSection(ConversationFollowerOptions.Key).Get<ConversationFollowerOptions>();

        Assert.NotNull(options);
        Assert.Equal(0.1, options.Chance);
        Assert.Null(options.TargetRoleId);
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
    }

    [Fact]
    public void TargetRoleOverrideBindsAlongsideTheDefaultChance()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Followers:Conversation:Chance"] = "0.1",
                    ["Followers:Conversation:TargetRoleId"] = "819778028576964618",
                    ["Followers:Conversation:TargetRoleChance"] = "1",
                }
            )
            .Build();

        var options = configuration.GetFollowerSection(ConversationFollowerOptions.Key).Get<ConversationFollowerOptions>();

        Assert.NotNull(options);
        Assert.Equal(0.1, options.Chance);
        Assert.Equal(819778028576964618UL, options.TargetRoleId);
        Assert.Equal(1, options.TargetRoleChance);
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

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public void TargetRoleChanceMustBeAValidPercentage(double chance)
    {
        var options = new ConversationFollowerOptions { TargetRoleChance = chance };

        Assert.Throws<ValidationException>(() => Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true));
    }

    private sealed class FollowerFixture
    {
        public const ulong ChannelId = 100;
        public const ulong BotId = 9;
        public const ulong TargetRoleId = 819778028576964618;
        public List<string> Replies { get; } = [];
        public List<ulong> RespondedTo { get; } = [];
        public List<IMessage> History { get; } = [];
        public List<ChatHistory> ReceivedHistories { get; } = [];

        public ConversationFollower CreateFollower(
            double chance,
            ulong ignoreBotMentionsInChannelId = 0,
            ulong? targetRoleId = null,
            double targetRoleChance = 1,
            ulong? grubPreviewChannelId = null
        ) =>
            new(
                Options.Create(new AllFollowersOptions { IgnoreBotMentionsInChannelId = ignoreBotMentionsInChannelId }),
                Options.Create(
                    new ConversationFollowerOptions
                    {
                        Chance = chance,
                        TargetRoleId = targetRoleId,
                        TargetRoleChance = targetRoleChance,
                    }
                ),
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
                NullLogger<RandomChanceFollowerTriggerStrategy>.Instance,
                Options.Create(new MentionHandlerOptions { ChannelIds = [], GrubPreviewChannelId = grubPreviewChannelId })
            );

        public RatFactsFollower CreateRatFactsFollower(ulong? grubPreviewChannelId) =>
            new(
                Options.Create(new AllFollowersOptions()),
                Options.Create(new RatFactsFollowerOptions { UserId = 123, Chance = 100 }),
                new BotContext(BotId, null!, null!),
                NullLogger<RandomChanceFollowerTriggerStrategy>.Instance,
                DiscordConversationFixture.Stub<ICompletionService>(
                    (method, _) =>
                        method.Name == "get_RatExpert"
                            ? new CapturingPersonality("RatExpert", RespondedTo, ReceivedHistories)
                            : throw new NotSupportedException(method.Name)
                ),
                Options.Create(new MentionHandlerOptions { ChannelIds = [], GrubPreviewChannelId = grubPreviewChannelId })
            );

        public IUserMessage CreateMessage(
            ulong authorId,
            bool mentionBot = false,
            IMessageChannel? channel = null,
            string content = "A message from anyone",
            IReadOnlyCollection<IAttachment>? attachments = null,
            IReadOnlyCollection<ulong>? roleIds = null,
            ulong channelId = ChannelId,
            ulong? categoryId = null
        )
        {
            object? AuthorCall(MethodInfo method, object?[]? _) =>
                method.Name switch
                {
                    "get_Id" => authorId,
                    "get_GlobalName" or "get_Username" or "get_DisplayName" => "Raider",
                    "get_Mention" => $"<@{authorId}>",
                    "get_RoleIds" => roleIds,
                    _ => throw new NotSupportedException(method.Name),
                };
            IUser author = roleIds is null
                ? DiscordConversationFixture.Stub<IUser>(AuthorCall)
                : DiscordConversationFixture.Stub<IGuildUser>(AuthorCall);
            channel ??= DiscordConversationFixture.Stub<ITextChannel>(
                (method, args) =>
                    method.Name switch
                    {
                        "get_Id" => channelId,
                        "get_CategoryId" => categoryId,
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
