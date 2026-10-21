using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Tests;

public class MentionHandlerTests
{
    [Fact]
    public async Task PreviewIsDisabledByDefaultAndOrdinaryRepliesRemainUnlabelled()
    {
        var fixture = new DiscordConversationFixture();
        var lorekeeper = new CapturingPersonality();
        var grub = new CapturingPersonality();
        fixture.AddMessage(1, "previous answer", DiscordConversationFixture.BotId);
        var message = fixture.AddMessage(2, "question");

        await CreateHandler(fixture, lorekeeper, grubPreview: grub).Handle(new MessageReceived(message), TestContext.Current.CancellationToken);

        Assert.NotNull(lorekeeper.History);
        Assert.Equal("Hammerstone", lorekeeper.History[0].AuthorName);
        Assert.Null(grub.History);
        Assert.Equal(["answer"], fixture.Replies);
    }

    [Theory]
    [InlineData(1UL, 100UL)]
    [InlineData(2UL, 200UL)]
    [InlineData(99UL, 200UL)]
    public async Task ExactPreviewChannelOverridesTheAllowlistForAnyHuman(ulong authorId, ulong allowedChannelId)
    {
        var fixture = new DiscordConversationFixture();
        var lorekeeper = new CapturingPersonality();
        var grub = new CapturingPersonality();
        fixture.AddMessage(1, "previous answer", DiscordConversationFixture.BotId);
        var message = fixture.AddMessage(2, "question", authorId);

        await CreateHandler(fixture, lorekeeper, allowedChannelId, DiscordConversationFixture.ChannelId, grub)
            .Handle(new MessageReceived(message), TestContext.Current.CancellationToken);

        Assert.Null(lorekeeper.History);
        Assert.NotNull(grub.History);
        Assert.Equal("Grub", grub.History[0].AuthorName);
        Assert.Equal(authorId, grub.Context!.UserId);
        Assert.Equal(["answer"], fixture.Replies);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviewDoesNotMatchACategoryAndNormalCategoryRoutingRemainsUnchanged(bool allowCategory)
    {
        var fixture = new DiscordConversationFixture(channelId: 200, categoryId: DiscordConversationFixture.ChannelId);
        var lorekeeper = new CapturingPersonality();
        var grub = new CapturingPersonality();
        var message = fixture.AddMessage(1, "question");

        await CreateHandler(
                fixture,
                lorekeeper,
                allowCategory ? DiscordConversationFixture.ChannelId : 300,
                DiscordConversationFixture.ChannelId,
                grub
            )
            .Handle(new MessageReceived(message), TestContext.Current.CancellationToken);

        Assert.Null(grub.History);
        if (allowCategory)
        {
            Assert.NotNull(lorekeeper.History);
            Assert.Equal(["answer"], fixture.Replies);
        }
        else
        {
            Assert.Null(lorekeeper.History);
            Assert.Equal(0, fixture.HistoryReads);
            Assert.Empty(fixture.Replies);
        }
    }

    [Fact]
    public async Task ConfiguredPreviewLeavesOtherAllowedChannelsOnLorekeeper()
    {
        var fixture = new DiscordConversationFixture();
        var lorekeeper = new CapturingPersonality();
        var grub = new CapturingPersonality();
        var message = fixture.AddMessage(1, "question");

        await CreateHandler(fixture, lorekeeper, grubPreviewChannelId: 200, grubPreview: grub)
            .Handle(new MessageReceived(message), TestContext.Current.CancellationToken);

        Assert.NotNull(lorekeeper.History);
        Assert.Null(grub.History);
        Assert.Equal(["answer"], fixture.Replies);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task PreviewStillRequiresAHumanMessageAndBotMention(bool authorIsBot, bool mentionBot)
    {
        var fixture = new DiscordConversationFixture();
        var lorekeeper = new CapturingPersonality();
        var grub = new CapturingPersonality();
        var message = fixture.AddMessage(1, "question", authorIsBot: authorIsBot, mentionBot: mentionBot);

        await CreateHandler(fixture, lorekeeper, grubPreviewChannelId: DiscordConversationFixture.ChannelId, grubPreview: grub)
            .Handle(new MessageReceived(message), TestContext.Current.CancellationToken);

        Assert.Null(lorekeeper.History);
        Assert.Null(grub.History);
        Assert.Equal(0, fixture.HistoryReads);
        Assert.Empty(fixture.Replies);
    }

    [Fact]
    public async Task PreviewAdminResetMarkerReactsWithoutInvokingEitherPersonality()
    {
        var fixture = new DiscordConversationFixture();
        var lorekeeper = new CapturingPersonality();
        var grub = new CapturingPersonality();
        var marker = fixture.AddMessage(1, "<@9> you've had a stroke.", DiscordConversationFixture.AdminId);

        await CreateHandler(fixture, lorekeeper, allowedChannelId: 200, grubPreviewChannelId: DiscordConversationFixture.ChannelId, grubPreview: grub)
            .Handle(new MessageReceived(marker), TestContext.Current.CancellationToken);

        Assert.Equal(["🧠"], fixture.Reactions);
        Assert.Equal(0, fixture.HistoryReads);
        Assert.Empty(fixture.Replies);
        Assert.Null(lorekeeper.History);
        Assert.Null(grub.History);
    }

    [Theory]
    [InlineData("<@9> you've had a stroke.")]
    [InlineData("<@!9> you've had a stroke")]
    [InlineData("  <@9> YOU’VE HAD A STROKE.  ")]
    public async Task AdminMarkerReactsWithoutReadingHistoryOrCallingTheModel(string content)
    {
        var fixture = new DiscordConversationFixture();
        var personality = new CapturingPersonality();
        var marker = fixture.AddMessage(10, content, DiscordConversationFixture.AdminId);
        await CreateHandler(fixture, personality).Handle(new MessageReceived(marker), TestContext.Current.CancellationToken);

        Assert.Equal(["🧠"], fixture.Reactions);
        Assert.Equal(0, fixture.HistoryReads);
        Assert.Empty(fixture.Replies);
        Assert.Null(personality.History);
    }

    [Theory]
    [InlineData("<@9> you've had a stroke.", 2)]
    [InlineData("<@9> you've had a stroke. Now answer me", 1)]
    [InlineData("Quoting: <@9> you've had a stroke.", 1)]
    [InlineData("<@99> you've had a stroke.", 1)]
    public async Task NonAdminOrNonExactMarkersAreOrdinaryRequests(string content, ulong authorId)
    {
        var fixture = new DiscordConversationFixture();
        var personality = new CapturingPersonality();
        var message = fixture.AddMessage(10, content, authorId);
        await CreateHandler(fixture, personality).Handle(new MessageReceived(message), TestContext.Current.CancellationToken);

        Assert.Empty(fixture.Reactions);
        Assert.NotNull(personality.History);
        Assert.Equal(["answer"], fixture.Replies);
    }

    [Fact]
    public async Task MarkerIsLimitedToConfiguredChannelsAndHumanAdmin()
    {
        var fixture = new DiscordConversationFixture();
        var personality = new CapturingPersonality();
        var marker = fixture.AddMessage(10, "<@9> you've had a stroke.", DiscordConversationFixture.AdminId);
        await CreateHandler(fixture, personality, allowedChannelId: 200).Handle(new MessageReceived(marker), TestContext.Current.CancellationToken);
        var botMarker = fixture.AddMessage(11, "<@9> you've had a stroke.", DiscordConversationFixture.AdminId, authorIsBot: true);
        await CreateHandler(fixture, personality).Handle(new MessageReceived(botMarker), TestContext.Current.CancellationToken);
        Assert.Empty(fixture.Reactions);
        Assert.Null(personality.History);
    }

    [Fact]
    public async Task NewBuilderAfterResetFindsTheMarkerAndCorrectCurrentAuthor()
    {
        var fixture = new DiscordConversationFixture();
        fixture.AddMessage(1, "stale");
        var personality = new CapturingPersonality();
        var marker = fixture.AddMessage(2, "<@9> you've had a stroke.", DiscordConversationFixture.AdminId);
        await CreateHandler(fixture, personality).Handle(new MessageReceived(marker), TestContext.Current.CancellationToken);
        var message = fixture.AddMessage(3, "new question", 7, "Leaf");
        await CreateHandler(fixture, personality).Handle(new MessageReceived(message), TestContext.Current.CancellationToken);
        Assert.Single(personality.History!);
        Assert.Equal(7UL, personality.Context!.UserId);
        Assert.Equal("Leaf", personality.Context.UserName);
        Assert.Equal(50UL, personality.Context.GuildId);
        Assert.Equal(3UL, personality.Context.MessageId);
        Assert.Contains("new question", personality.History![0].Text);
        Assert.DoesNotContain("stale", personality.History[0].Text);
    }

    [Fact]
    public async Task FailedConfirmationReactionDoesNotMakeTheMarkerIneffective()
    {
        var fixture = new DiscordConversationFixture { ReactionException = new InvalidOperationException("Missing Add Reactions permission") };
        fixture.AddMessage(1, "stale");
        var marker = fixture.AddMessage(2, "<@9> you've had a stroke.", DiscordConversationFixture.AdminId);
        var personality = new CapturingPersonality();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateHandler(fixture, personality).Handle(new MessageReceived(marker), TestContext.Current.CancellationToken)
        );
        Assert.Null(personality.History);

        var trigger = fixture.AddMessage(3, "new question");
        await CreateHandler(fixture, personality).Handle(new MessageReceived(trigger), TestContext.Current.CancellationToken);
        Assert.Single(personality.History!);
        Assert.DoesNotContain("stale", personality.History![0].Text);
    }

    private static MentionHandler CreateHandler(
        DiscordConversationFixture fixture,
        CapturingPersonality personality,
        ulong allowedChannelId = DiscordConversationFixture.ChannelId,
        ulong? grubPreviewChannelId = null,
        CapturingPersonality? grubPreview = null
    ) =>
        new(
            Options.Create(new MentionHandlerOptions { ChannelIds = [allowedChannelId], GrubPreviewChannelId = grubPreviewChannelId }),
            new BotContext(DiscordConversationFixture.BotId, null!, null!),
            DiscordConversationFixture.Stub<ICompletionService>(
                (method, _) =>
                    method.Name switch
                    {
                        "get_Lorekeeper" => personality,
                        "get_GrubPreview" => grubPreview ?? throw new InvalidOperationException("Preview should not be invoked"),
                        _ => throw new NotSupportedException(method.Name),
                    }
            ),
            Options.Create(new DirectMessageHandlerOptions { AdminUserId = DiscordConversationFixture.AdminId }),
            fixture.CreateBuilder()
        );

    private sealed class CapturingPersonality : IPersonality
    {
        public ChatHistory? History { get; private set; }
        public IMessageContext? Context { get; private set; }

        public Task<string> InvokeAsync(ChatHistory chatHistory, IMessageContext messageContext, CancellationToken cancellationToken = default)
        {
            History = chatHistory;
            Context = messageContext;
            return Task.FromResult("answer");
        }
    }
}
