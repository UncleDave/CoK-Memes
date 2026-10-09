using System.Reflection;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Microsoft.Extensions.Options;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class GazettePublicationDeliveryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PublicMessageContainsOnlyOrderedPagesAndButtonWithSnapshotSavedBeforeSend(int pageCount)
    {
        var fixture = new Fixture { PageCount = pageCount };
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
        public int PageCount { get; set; } = 1;

        public Task Run()
        {
            var printEdition = new GazettePrintEdition(
                Enumerable.Range(1, PageCount).Select(page => new GazettePage($"issue-page-{page}.png", [1, 2, 3])).ToArray()
            );
            var channel = Stub<ITextChannel>(
                (method, args) =>
                    method.Name switch
                    {
                        "get_GuildId" => 1UL,
                        "get_Id" => 8UL,
                        "SendFilesAsync" => Send(method, args!, printEdition),
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
                printEdition,
                "012345abcdef",
                () => CanSend,
                TestContext.Current.CancellationToken
            );
        }

        private Task<IUserMessage> Send(MethodInfo method, object?[] args, GazettePrintEdition printEdition)
        {
            Store.Stages.Add("send");
            Assert.NotNull(Store.Edition);
            object? Argument(string name) => args[Array.FindIndex(method.GetParameters(), parameter => parameter.Name == name)];
            var files = Assert.IsAssignableFrom<IEnumerable<FileAttachment>>(Argument("attachments")).ToArray();
            Assert.Equal(printEdition.Pages.Select(page => page.FileName), files.Select(file => file.FileName));
            var embeds = Assert.IsType<Embed[]>(Argument("embeds"));
            Assert.Equal(printEdition.Pages.Count, embeds.Length);
            Assert.All(embeds, embed => Assert.Null(embed.Description));
            Assert.Equal(printEdition.Pages.Select(page => $"attachment://{page.FileName}"), embeds.Select(embed => embed.Image!.Value.Url));
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

        public Task<IReadOnlyList<GazettePublishedEdition>> GetRecentAsync(
            ulong guildId,
            ulong channelId,
            DateTime sinceUtc,
            DateTime untilUtc,
            int maximumEditions,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

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
