using System.Reflection;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Microsoft.Extensions.Logging.Abstractions;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class GazetteReadInteractionHandlerTests
{
    private const string Id = "012345abcdef";

    [Fact]
    public async Task ButtonAcknowledgesPrivatelyBeforeAnyDatabaseWork()
    {
        var called = false;
        var interaction = Stub<IComponentInteraction>(
            (method, args) =>
            {
                Assert.Equal("DeferLoadingAsync", method.Name);
                Assert.True((bool)args![Array.FindIndex(method.GetParameters(), parameter => parameter.Name == "ephemeral")]!);
                called = true;
                return Task.CompletedTask;
            }
        );
        await GazetteReadButton.AcknowledgeAsync(interaction);
        Assert.True(called);
    }

    [Fact]
    public void ButtonHasNoUrlOrPublicTextAndUsesAStrictPublicationId()
    {
        var component = GazetteReadButton.Build(Id);
        var row = Assert.IsType<ActionRowComponent>(Assert.Single(component.Components));
        var button = Assert.IsType<ButtonComponent>(Assert.Single(row.Components));
        Assert.Equal("Read text & sources", button.Label);
        Assert.Equal(ButtonStyle.Secondary, button.Style);
        Assert.Null(button.Url);
        Assert.True(GazetteReadButton.TryParse(button.CustomId, out var parsed));
        Assert.Equal(Id, parsed);
    }

    [Theory]
    [InlineData("gazette:read:")]
    [InlineData("gazette:read:012345abcdefextra")]
    [InlineData("gazette:read:012345ABCDEF")]
    [InlineData("gazette:read:012345abcdez")]
    [InlineData("other:012345abcdef")]
    public void OtherOrMalformedButtonsAreNotClaimed(string customId) => Assert.False(GazetteReadButton.TryParse(customId, out _));

    [Fact]
    public async Task ReaderReturnsStoredApprovedTextPrivatelyWithoutDraftStateOrModelCalls()
    {
        var fixture = new Fixture();
        await fixture.Run();
        Assert.Equal(1, fixture.Store.Reads);
        Assert.Equal(2, fixture.Access.Checks);
        Assert.Equal(fixture.Store.Edition!.Text, Assert.Single(fixture.Replies).Description);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("guild")]
    [InlineData("channel")]
    [InlineData("message")]
    [InlineData("empty")]
    [InlineData("oversized")]
    [InlineData("revoked-access")]
    [InlineData("database-failure")]
    public async Task MissingMismatchedOrUnavailableTextNeverLeaksIntoTheReader(string failure)
    {
        var fixture = new Fixture();
        fixture.Store.Edition = failure switch
        {
            "missing" => null,
            "guild" => fixture.Store.Edition! with { GuildId = 2 },
            "channel" => fixture.Store.Edition! with { ChannelId = 9 },
            "message" => fixture.Store.Edition! with { MessageId = 101 },
            "empty" => fixture.Store.Edition! with { Text = " " },
            "oversized" => fixture.Store.Edition! with { Text = new string('x', 4001) },
            _ => fixture.Store.Edition,
        };
        if (failure == "revoked-access")
            fixture.Access.RevokeAfterFirstCheck = true;
        if (failure == "database-failure")
            fixture.Store.Fails = true;
        await fixture.Run();
        Assert.Null(Assert.Single(fixture.Replies).Description);
        Assert.Contains("unavailable", Assert.Single(fixture.Replies).Text);
    }

    [Fact]
    public async Task UnreadableOrNonBotPublicationDoesNotEvenReadTheArchive()
    {
        var fixture = new Fixture();
        fixture.Access.Allowed = false;
        await fixture.Run();
        Assert.Equal(0, fixture.Store.Reads);
        Assert.Null(Assert.Single(fixture.Replies).Description);
    }

    [Fact]
    public async Task AmbiguousSendCanStillBeReadFromTheActualBotMessageWhileAcknowledgementIsPending()
    {
        var fixture = new Fixture();
        fixture.Store.Edition = fixture.Store.Edition! with { MessageId = null };
        await fixture.Run();
        Assert.Equal(fixture.Store.Edition.Text, Assert.Single(fixture.Replies).Description);
    }

    [Theory]
    [InlineData(9UL, 9UL, 8UL, 8UL, true, true, true)]
    [InlineData(2UL, 9UL, 8UL, 8UL, true, true, false)]
    [InlineData(9UL, 9UL, 7UL, 8UL, true, true, false)]
    [InlineData(9UL, 9UL, 8UL, 8UL, false, true, false)]
    [InlineData(9UL, 9UL, 8UL, 8UL, true, false, false)]
    public void ReaderRequiresBotMessageIdentityAndCurrentViewAndHistoryPermissions(
        ulong author,
        ulong bot,
        ulong messageChannel,
        ulong channel,
        bool view,
        bool history,
        bool allowed
    )
    {
        Assert.Equal(
            allowed,
            DiscordGazetteReaderAccess.CanReadMessage(
                author,
                bot,
                messageChannel,
                channel,
                new ChannelPermissions(viewChannel: view, readMessageHistory: history)
            )
        );
    }

    private sealed class Fixture
    {
        public Store Store { get; } = new();
        public Access Access { get; } = new();
        public List<(string? Text, string? Description)> Replies { get; } = [];

        public Task Run()
        {
            var data = Stub<IComponentInteractionData>(
                (method, _) => method.Name == "get_CustomId" ? "gazette:read:" + Id : throw new NotSupportedException()
            );
            var message = Stub<IUserMessage>((method, _) => method.Name == "get_Id" ? 100UL : throw new NotSupportedException());
            var interaction = Stub<IComponentInteraction>(
                (method, args) =>
                    method.Name switch
                    {
                        "get_Data" => data,
                        "get_GuildId" => (ulong?)1,
                        "get_ChannelId" => (ulong?)8,
                        "get_Message" => message,
                        "FollowupAsync" => Reply(method, args!),
                        _ => throw new NotSupportedException(method.Name),
                    }
            );
            return new GazetteReadInteractionHandler(Store, Access, NullLogger<GazetteReadInteractionHandler>.Instance).Handle(
                new(interaction),
                TestContext.Current.CancellationToken
            );
        }

        private Task<IUserMessage> Reply(MethodInfo method, object?[] args)
        {
            object? Argument(string name) => args[Array.FindIndex(method.GetParameters(), parameter => parameter.Name == name)];
            Assert.True((bool)Argument("ephemeral")!);
            Assert.Same(AllowedMentions.None, Argument("allowedMentions"));
            var embed = Argument("embed") as Embed;
            Replies.Add((Argument("text") as string, embed?.Description));
            return Task.FromResult<IUserMessage>(null!);
        }
    }

    private sealed class Access : IGazetteReaderAccess
    {
        public int Checks { get; private set; }
        public bool Allowed { get; set; } = true;
        public bool RevokeAfterFirstCheck { get; set; }

        public bool CanRead(IComponentInteraction interaction) => Allowed && (++Checks == 1 || !RevokeAfterFirstCheck);
    }

    private sealed class Store : IGazettePublishedEditionStore
    {
        public int Reads { get; private set; }
        public bool Fails { get; set; }
        public GazettePublishedEdition? Edition { get; set; } =
            new(Id, 1, 8, "Exact approved edition with [source](https://discord.com/channels/1/3/42)", DateTime.UtcNow) { MessageId = 100 };

        public Task<GazettePublishedEdition?> GetAsync(string id, CancellationToken cancellationToken)
        {
            Reads++;
            Assert.Equal(Id, id);
            return Fails
                ? Task.FromException<GazettePublishedEdition?>(new InvalidOperationException("Persistence unavailable"))
                : Task.FromResult(Edition);
        }

        public Task SaveAsync(GazettePublishedEdition edition, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ConfirmMessageAsync(string id, ulong messageId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
