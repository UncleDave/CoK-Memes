using System.Net;
using ChampionsOfKhazad.Bot.Roster;
using ChampionsOfKhazad.Bot.Roster.Blizzard;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.Tests;

public class GuildRosterWorkerTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task WorkerDoesNoRosterWorkUntilDiscordIsReadyWithAContext(bool ready, bool hasContext)
    {
        await using var client = new DiscordSocketClient();
        using var fixture = new RosterFixture();
        var context = Context(client);
        context.IsReady = ready;
        if (!hasContext)
            context.BotContext = null;
        var sender = new Sender();
        var logger = new RecordingLogger();
        using var worker = new GuildRosterWorker(fixture.Service, sender, context, fixture.Clock, logger);
        await worker.RefreshAndDeliverAsync(fixture.Token);
        Assert.Equal(0, fixture.Source.RosterReads);
        Assert.Equal(0, fixture.Store.Reads);
        Assert.Empty(sender.Messages);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task MissingGuildRetriesWithoutErrorSinkAlertsAndItsFirstValidSnapshotIsSilent()
    {
        await using var client = new DiscordSocketClient();
        using var fixture = new RosterFixture();
        fixture.Source.Failure = new RosterSourceUnavailableException("Guild endpoint returned HTTP 404.");
        var sender = new Sender();
        var logger = new RecordingLogger();
        using var worker = new GuildRosterWorker(fixture.Service, sender, Context(client), fixture.Clock, logger);
        await worker.RefreshAndDeliverAsync(fixture.Token);
        fixture.Advance();
        await worker.RefreshAndDeliverAsync(fixture.Token);
        Assert.Equal(2, fixture.Source.RosterReads);
        Assert.Equal("refresh_failed", (await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token)).Status);
        Assert.Equal(2, logger.Entries.Count);
        Assert.All(
            logger.Entries,
            entry =>
            {
                Assert.Equal(LogLevel.Information, entry.Level);
                Assert.Null(entry.Exception);
            }
        );
        Assert.Empty(sender.Messages);
        Assert.Empty(fixture.Notices.Items);

        fixture.Source.Failure = null;
        fixture.Advance();
        fixture.SetMembers(RosterFixture.Character());
        await worker.RefreshAndDeliverAsync(fixture.Token);
        Assert.Empty(sender.Messages);
        Assert.Equal("ok", (await fixture.Queries.QueryAsync(50, cancellationToken: fixture.Token)).Status);
        fixture.Advance();
        fixture.SetMembers(RosterFixture.Character(level: 20));
        await worker.RefreshAndDeliverAsync(fixture.Token);
        Assert.Single(sender.Messages);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Theory]
    [InlineData("credentials")]
    [InlineData("schema")]
    [InlineData("other-resource-404")]
    public async Task UnexpectedFailuresRemainErrorsRatherThanBeingHiddenAsAMissingGuild(string failure)
    {
        await using var client = new DiscordSocketClient();
        using var fixture = new RosterFixture();
        fixture.Source.Failure = failure switch
        {
            "credentials" => new BlizzardApiException("OAuth rejected credentials.", HttpStatusCode.Unauthorized, "OAuth"),
            "other-resource-404" => new BlizzardApiException("Static resource missing.", HttpStatusCode.NotFound, "resource"),
            _ => new InvalidDataException("Unexpected roster schema."),
        };
        var logger = new RecordingLogger();
        using var worker = new GuildRosterWorker(fixture.Service, new Sender(), Context(client), fixture.Clock, logger);
        await worker.RefreshAndDeliverAsync(fixture.Token);
        var error = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Same(fixture.Source.Failure, error.Exception);
        Assert.True(fixture.Store.States[fixture.Service.ScopeId].LastRefreshFailed);
    }

    [Fact]
    public async Task DisconnectDuringRefreshDefersDeliveryUntilReadinessReturns()
    {
        await using var client = new DiscordSocketClient();
        using var fixture = new RosterFixture();
        var context = Context(client);
        var sender = new Sender();
        using var worker = new GuildRosterWorker(fixture.Service, sender, context, fixture.Clock, new RecordingLogger());
        await worker.RefreshAndDeliverAsync(fixture.Token);
        fixture.Advance();
        fixture.SetMembers(RosterFixture.Character(level: 20));
        fixture.Source.BeforeRead = () =>
        {
            context.IsReady = false;
            return Task.CompletedTask;
        };
        await worker.RefreshAndDeliverAsync(fixture.Token);
        Assert.Empty(sender.Messages);
        Assert.Null(Assert.Single(fixture.Notices.Items.Values).SentAtUtc);
        context.IsReady = true;
        await worker.RefreshAndDeliverAsync(fixture.Token);
        Assert.Single(sender.Messages);
    }

    private static BotContextProvider Context(DiscordSocketClient client) =>
        new()
        {
            IsReady = true,
            BotContext = new BotContext(
                42,
                DiscordConversationFixture.Stub<IGuild>((method, _) => method.Name == "get_Id" ? 50UL : throw new NotSupportedException(method.Name)),
                client
            ),
        };

    private sealed class Sender : IRosterNotificationSender
    {
        public List<string> Messages { get; } = [];

        public Task<ulong> SendAsync(ulong guildId, ulong channelId, RosterNoticeBatch batch, CancellationToken cancellationToken)
        {
            Messages.Add(batch.Text);
            return Task.FromResult(123UL);
        }
    }

    private sealed class RecordingLogger : ILogger<GuildRosterWorker>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception));
    }
}
