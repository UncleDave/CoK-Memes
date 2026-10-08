using System.Reflection;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Microsoft.Extensions.Options;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class GazettePublicationDeliveryTests
{
    [Fact]
    public async Task PublicMessageContainsOnlyPageAndButtonWithSnapshotSavedBeforeSend()
    {
        var fixture = new Fixture();
        await fixture.Run();
        Assert.Equal(new[] { "archive", "send", "confirm" }, fixture.Store.Stages);
        Assert.Equal("Exact approved text", fixture.Store.Edition!.Text);
        Assert.Equal(99UL, fixture.Store.Edition.MessageId);
    }

    [Fact]
    public async Task PersistenceFailurePreventsSendingAPublicationWithABrokenButton()
    {
        var fixture = new Fixture();
        fixture.Store.FailSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(fixture.Run);
        Assert.DoesNotContain("send", fixture.Store.Stages);
    }

    [Fact]
    public async Task PermissionsRevokedWhileArchivingPreventSend()
    {
        var fixture = new Fixture { CanSend = false };
        await Assert.ThrowsAsync<InvalidOperationException>(fixture.Run);
        Assert.Equal(new[] { "archive" }, fixture.Store.Stages);
    }

    [Fact]
    public async Task AmbiguousSendKeepsApprovedSnapshotAndDoesNotAutomaticallyRetry()
    {
        var fixture = new Fixture { SendFails = true };
        await Assert.ThrowsAsync<InvalidOperationException>(fixture.Run);
        Assert.Equal(new[] { "archive", "send" }, fixture.Store.Stages);
        Assert.NotNull(fixture.Store.Edition);
        Assert.Null(fixture.Store.Edition.MessageId);
    }

    private sealed class Fixture
    {
        public Store Store { get; } = new();
        public bool CanSend { get; set; } = true;
        public bool SendFails { get; set; }

        public Task Run()
        {
            var page = new GazettePage("issue.png", [1, 2, 3]);
            var channel = Stub<ITextChannel>(
                (method, args) =>
                    method.Name switch
                    {
                        "get_GuildId" => 1UL,
                        "get_Id" => 8UL,
                        "SendFileAsync" => Send(method, args!, page),
                        _ => throw new NotSupportedException(method.Name),
                    }
            );
            var gateway = new DiscordGazetteGateway(
                new(),
                new(null!),
                Options.Create(new GazetteOptions()),
                Options.Create(new DiscordMessageToolsOptions()),
                Store,
                TimeProvider.System
            );
            return gateway.SendPublicationAsync(
                channel,
                "Exact approved text",
                page,
                "012345abcdef",
                () => CanSend,
                TestContext.Current.CancellationToken
            );
        }

        private Task<IUserMessage> Send(MethodInfo method, object?[] args, GazettePage page)
        {
            Store.Stages.Add("send");
            Assert.NotNull(Store.Edition);
            object? Argument(string name) => args[Array.FindIndex(method.GetParameters(), parameter => parameter.Name == name)];
            Assert.Equal(page.FileName, Argument("filename"));
            var embed = Assert.IsType<Embed>(Argument("embed"));
            Assert.Null(embed.Description);
            Assert.Equal("attachment://issue.png", embed.Image!.Value.Url);
            var embeds = Argument("embeds") as Embed[];
            Assert.True(embeds is null || embeds.Length == 0);
            var components = Assert.IsType<MessageComponent>(Argument("components"));
            Assert.Single(components.Components);
            Assert.Same(AllowedMentions.None, Argument("allowedMentions"));
            return SendFails
                ? Task.FromException<IUserMessage>(new InvalidOperationException("Unknown send outcome"))
                : Task.FromResult(Stub<IUserMessage>((method, _) => method.Name == "get_Id" ? 99UL : throw new NotSupportedException()));
        }
    }

    private sealed class Store : IGazettePublishedEditionStore
    {
        public List<string> Stages { get; } = [];
        public bool FailSave { get; set; }
        public GazettePublishedEdition? Edition { get; private set; }

        public Task<GazettePublishedEdition?> GetAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Edition);

        public Task SaveAsync(GazettePublishedEdition edition, CancellationToken cancellationToken)
        {
            Stages.Add("archive");
            if (FailSave)
                throw new InvalidOperationException("Archive unavailable");
            Edition = edition;
            return Task.CompletedTask;
        }

        public Task ConfirmMessageAsync(string id, ulong messageId, CancellationToken cancellationToken)
        {
            Stages.Add("confirm");
            Assert.Equal(id, Edition!.Id);
            Edition = Edition with { MessageId = messageId };
            return Task.CompletedTask;
        }
    }
}
