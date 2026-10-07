using System.Net;
using System.Security.Cryptography;
using System.Text;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Discord.Net;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class DiscordNotebookSourceReaderTests
{
    [Fact]
    public void ChannelEnumerationUsesFreshAccessAndDoesNotReadOrDisposeRest()
    {
        var fixture = new ReaderFixture();
        fixture.Access[7] = "members";
        var reader = fixture.CreateReader();

        Assert.Equal(new ulong[] { 3, 7 }, reader.GetChannelIds());
        fixture.Access.Remove(3);
        Assert.Equal(7UL, Assert.Single(reader.GetChannelIds()));
        Assert.Empty(fixture.Requests);
        Assert.Equal(0, fixture.Disposals);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(25, 25)]
    [InlineData(101, 100)]
    public async Task BatchUsesBoundedRestHistoryAfterTheCursorAndForwardsCancellation(int limit, int expectedLimit)
    {
        var fixture = new ReaderFixture();
        using var cancellation = new CancellationTokenSource();

        var batch = await fixture.CreateReader().ReadBatchAsync(3, 40, limit, cancellation.Token);

        Assert.NotNull(batch);
        Assert.Equal(40UL, batch.LastMessageId);
        Assert.Empty(batch.Messages);
        Assert.Equal(40UL, fixture.AfterMessageId);
        Assert.Equal(Direction.After, fixture.Direction);
        Assert.Equal(expectedLimit, fixture.Limit);
        Assert.Equal(new[] { "channel", "history" }, fixture.Requests);
        Assert.All(fixture.Tokens, token => Assert.Equal(cancellation.Token, token));
        Assert.Equal(0, fixture.Disposals);
    }

    [Fact]
    public async Task BatchFiltersDeduplicatesAndOrdersEvidenceButAdvancesPastAllConsumedMessages()
    {
        var fixture = new ReaderFixture();
        var first = fixture.Add(41, "First observation");
        var second = fixture.Add(42, "Second observation");
        var bot = fixture.Add(43, "Bot", bot: true);
        var webhook = fixture.Add(44, "Webhook", source: MessageSource.Webhook);
        var system = fixture.Add(45, "System", source: MessageSource.System);
        var whitespace = fixture.Add(46, " ");
        var oversized = fixture.Add(47, new string('x', 4001));
        var wrongChannel = fixture.Add(99, "Different channel", channelId: 4);
        var old = fixture.Add(40, "Already consumed");
        fixture.History = [oversized, second, wrongChannel, bot, second, old, whitespace, first, system, webhook];

        var batch = await fixture.CreateReader().ReadBatchAsync(3, 40, 100, CancellationToken.None);

        Assert.NotNull(batch);
        Assert.Equal(3UL, batch.ChannelId);
        Assert.Equal(47UL, batch.LastMessageId);
        Assert.Equal(new ulong[] { 41, 42 }, batch.Messages.Select(message => message.MessageId));
        Assert.Equal(new[] { "First observation", "Second observation" }, batch.Messages.Select(message => message.Source.Content));
    }

    [Fact]
    public async Task BatchContainingOnlyIneligibleMessagesStillAdvancesTheCursor()
    {
        var fixture = new ReaderFixture();
        fixture.Add(45, "Bot", bot: true);
        fixture.Add(46, "Webhook", source: MessageSource.Webhook);
        fixture.Add(47, new string('x', 4001));

        var batch = await fixture.CreateReader().ReadBatchAsync(3, 40, 25, CancellationToken.None);

        Assert.NotNull(batch);
        Assert.Equal(47UL, batch.LastMessageId);
        Assert.Empty(batch.Messages);
    }

    [Fact]
    public async Task SourcesAndBatchUseCompleteSanitizedEvidenceWithIdenticalHashesAndMetadata()
    {
        var fixture = new ReaderFixture();
        fixture.Access[4] = "members";
        var raw = "<#4> <#5> <@2> <@!2> <@&8> @everyone @here " + new string('x', 1100) + " Correction: fictional.";
        fixture.Add(42, raw, authorName: new string('n', 100), mentions: [2, 2, 6]);
        var reader = fixture.CreateReader();

        var batch = await reader.ReadBatchAsync(3, 40, 25, CancellationToken.None);
        var sources = await reader.ReadSourcesAsync([ReaderFixture.Url, ReaderFixture.Url], CancellationToken.None);

        var observed = Assert.Single(Assert.IsType<NotebookObservationBatch>(batch).Messages).Source;
        var verified = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<NotebookSource>>(sources));
        Assert.Equal(ReaderFixture.Url, observed.Url);
        Assert.Equal(9UL, observed.AuthorId);
        Assert.Equal(new string('n', 80), observed.AuthorName);
        Assert.Equal(ReaderFixture.Timestamp.UtcDateTime, observed.TimestampUtc);
        Assert.StartsWith(
            "#members [unavailable channel] [Discord user 2] [Discord user 2] [role mention] @\u200beveryone @\u200bhere",
            observed.Content
        );
        Assert.EndsWith("Correction: fictional.", observed.Content);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))), observed.ContentHash);
        Assert.Equal(new[] { new NotebookMentionedUser(2, "member-2"), new NotebookMentionedUser(6, "member-6") }, observed.MentionedUsers);
        Assert.Equal(NotebookSource.CalculateViewHash(observed.Content, observed.AuthorName, observed.MentionedUsers), observed.ViewHash);
        Assert.Equal(observed.Content, verified.Content);
        Assert.Equal(observed.ContentHash, verified.ContentHash);
        Assert.Equal(observed.ViewHash, verified.ViewHash);
        Assert.Equal(observed.MentionedUsers, verified.MentionedUsers);
        Assert.Equal(1, fixture.Requests.Count(request => request == "message"));
    }

    [Fact]
    public async Task SourceVerificationRefetchesEditsAndDeletionsInsteadOfReusingObservedEvidence()
    {
        var fixture = new ReaderFixture();
        fixture.Add(42, "Original observation");
        var reader = fixture.CreateReader();
        var observed = Assert.Single((await reader.ReadBatchAsync(3, 40, 25, CancellationToken.None))!.Messages).Source;
        fixture.Add(42, "Edited observation");

        var verified = Assert.Single((await reader.ReadSourcesAsync([ReaderFixture.Url], CancellationToken.None))!);

        Assert.Equal("Edited observation", verified.Content);
        Assert.NotEqual(observed.ContentHash, verified.ContentHash);
        Assert.NotEqual(observed.ViewHash, verified.ViewHash);
        fixture.Messages.Clear();
        Assert.Null(await reader.ReadSourcesAsync([ReaderFixture.Url], CancellationToken.None));
        Assert.Equal(2, fixture.Requests.Count(request => request == "message"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadRejectsAccessRevokedDuringNetworkOrMetadata(bool sourceRead)
    {
        foreach (var revokeDuringMetadata in new[] { false, true })
        {
            var fixture = new ReaderFixture();
            fixture.Add(42, "Observation <@2>", mentions: [2]);
            if (revokeDuringMetadata)
                fixture.MetadataRead = fixture.Access.Clear;
            else
                fixture.MessagesRead = fixture.Access.Clear;
            var reader = fixture.CreateReader();

            if (sourceRead)
                Assert.Null(await reader.ReadSourcesAsync([ReaderFixture.Url], CancellationToken.None));
            else
                Assert.Null(await reader.ReadBatchAsync(3, 40, 25, CancellationToken.None));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceRedactionUsesAccessRecheckedAfterTheNetwork(bool sourceRead)
    {
        var fixture = new ReaderFixture();
        fixture.Access[4] = "members";
        fixture.Add(42, "See <#4>");
        fixture.MessagesRead = () => fixture.Access.Remove(4);
        var reader = fixture.CreateReader();

        var source = sourceRead
            ? Assert.Single((await reader.ReadSourcesAsync([ReaderFixture.Url], CancellationToken.None))!)
            : Assert.Single((await reader.ReadBatchAsync(3, 40, 25, CancellationToken.None))!.Messages).Source;

        Assert.Equal("See [unavailable channel]", source.Content);
    }

    [Theory]
    [InlineData("wrong-guild")]
    [InlineData("wrong-channel")]
    [InlineData("missing")]
    [InlineData("thread")]
    [InlineData("voice")]
    public async Task RestChannelMustBeTextInTheConfiguredGuildAndNotAThreadOrVoiceChannel(string mismatch)
    {
        var fixture = new ReaderFixture();
        fixture.RestChannel = mismatch switch
        {
            "wrong-guild" => fixture.TextChannel(guildId: 2),
            "wrong-channel" => fixture.TextChannel(channelId: 4),
            "thread" => Stub<IThreadChannel>((method, _) => throw new InvalidOperationException(method.Name)),
            "voice" => Stub<IVoiceChannel>((method, _) => throw new InvalidOperationException(method.Name)),
            _ => null,
        };
        var reader = fixture.CreateReader();

        Assert.Null(await reader.ReadBatchAsync(3, 40, 25, CancellationToken.None));
        Assert.Null(await reader.ReadSourcesAsync([ReaderFixture.Url], CancellationToken.None));
        Assert.DoesNotContain("history", fixture.Requests);
        Assert.DoesNotContain("message", fixture.Requests);
    }

    [Fact]
    public async Task DeniedAndInvalidSourceRequestsDoNotReadRest()
    {
        var fixture = new ReaderFixture();
        var reader = fixture.CreateReader();
        Assert.Null(await reader.ReadSourcesAsync([], CancellationToken.None));
        Assert.Null(await reader.ReadSourcesAsync(Enumerable.Repeat(ReaderFixture.Url, 4).ToArray(), CancellationToken.None));
        Assert.Null(await reader.ReadSourcesAsync(["https://discord.com/channels/2/3/42"], CancellationToken.None));
        Assert.Null(await reader.ReadSourcesAsync(["https://discord.com/channels/1/4/42"], CancellationToken.None));
        fixture.Access.Clear();
        Assert.Null(await reader.ReadSourcesAsync([ReaderFixture.Url], CancellationToken.None));
        Assert.Null(await reader.ReadBatchAsync(3, 40, 25, CancellationToken.None));
        Assert.Empty(fixture.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "channel")]
    [InlineData(HttpStatusCode.Forbidden, "channel")]
    [InlineData(HttpStatusCode.NotFound, "messages")]
    [InlineData(HttpStatusCode.Forbidden, "messages")]
    public async Task NotFoundAndForbiddenReadsFailClosed(HttpStatusCode status, string stage)
    {
        var fixture = new ReaderFixture { Error = new HttpException(status, null!, null, "Unavailable", []), FailureStage = stage };
        var reader = fixture.CreateReader();

        Assert.Null(await reader.ReadBatchAsync(3, 40, 25, CancellationToken.None));
        Assert.Null(await reader.ReadSourcesAsync([ReaderFixture.Url], CancellationToken.None));
    }

    [Fact]
    public async Task UnexpectedFailuresAndCancellationPropagateWithoutDisposingTheSharedClient()
    {
        var error = new HttpException(HttpStatusCode.InternalServerError, null!, null, "Unavailable", []);
        var fixture = new ReaderFixture { Error = error, FailureStage = "messages" };
        var reader = fixture.CreateReader();
        Assert.Same(error, await Assert.ThrowsAsync<HttpException>(() => reader.ReadBatchAsync(3, 40, 25, CancellationToken.None)));
        Assert.Same(error, await Assert.ThrowsAsync<HttpException>(() => reader.ReadSourcesAsync([ReaderFixture.Url], CancellationToken.None)));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadBatchAsync(3, 40, 25, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadSourcesAsync([ReaderFixture.Url], cancellation.Token));
        Assert.Equal(0, fixture.Disposals);
    }

    private sealed class ReaderFixture
    {
        public const string Url = "https://discord.com/channels/1/3/42";
        public static readonly DateTimeOffset Timestamp = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
        public Dictionary<ulong, string> Access { get; } = new() { [3] = "general" };
        public Dictionary<ulong, IMessage> Messages { get; } = [];
        public List<IMessage> History { get; set; } = [];
        public List<string> Requests { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public IChannel? RestChannel { get; set; }
        public Action? MessagesRead { get; set; }
        public Action? MetadataRead { get; set; }
        public Exception? Error { get; set; }
        public string? FailureStage { get; set; }
        public ulong AfterMessageId { get; private set; }
        public Direction Direction { get; private set; }
        public int Limit { get; private set; }
        public int Disposals { get; private set; }

        public ReaderFixture() => RestChannel = TextChannel();

        public DiscordNotebookSourceReader CreateReader()
        {
            var client = Stub<IDiscordClient>(
                (method, arguments) =>
                {
                    if (method.Name == "Dispose")
                    {
                        Disposals++;
                        return null;
                    }
                    if (method.Name != "GetChannelAsync")
                        throw new NotSupportedException(method.Name);
                    Assert.Equal(3UL, arguments![0]);
                    Requests.Add("channel");
                    Tokens.Add(((RequestOptions)arguments[^1]!).CancelToken);
                    return FailureStage == "channel" ? Task.FromException<IChannel>(Error!) : Task.FromResult(RestChannel!);
                }
            );
            return new DiscordNotebookSourceReader(
                new SharedDiscordRestClient(client),
                1,
                () => new Dictionary<ulong, string>(Access),
                id =>
                {
                    MetadataRead?.Invoke();
                    return $"member-{id}";
                }
            );
        }

        public ITextChannel TextChannel(ulong guildId = 1, ulong channelId = 3) =>
            Stub<ITextChannel>(
                (method, arguments) =>
                    method.Name switch
                    {
                        "get_Id" => channelId,
                        "get_GuildId" => guildId,
                        "GetMessagesAsync" => ReadHistory(
                            (ulong)arguments![0]!,
                            (Direction)arguments[1]!,
                            (int)arguments[2]!,
                            (RequestOptions)arguments[^1]!
                        ),
                        "GetMessageAsync" => ReadMessage((ulong)arguments![0]!, (RequestOptions)arguments[^1]!),
                        _ => throw new NotSupportedException(method.Name),
                    }
            );

        public IMessage Add(
            ulong id,
            string content,
            bool bot = false,
            MessageSource source = MessageSource.User,
            ulong channelId = 3,
            string authorName = "Raider",
            IReadOnlyCollection<ulong>? mentions = null
        )
        {
            var author = Stub<IUser>(
                (method, _) =>
                    method.Name switch
                    {
                        "get_Id" => 9UL,
                        "get_IsBot" => bot,
                        "get_GlobalName" => authorName,
                        _ => throw new NotSupportedException(method.Name),
                    }
            );
            var channel = Stub<IMessageChannel>((method, _) => method.Name == "get_Id" ? channelId : throw new NotSupportedException(method.Name));
            var message = Stub<IMessage>(
                (method, _) =>
                    method.Name switch
                    {
                        "get_Id" => id,
                        "get_Channel" => channel,
                        "get_Author" => author,
                        "get_Source" => source,
                        "get_Content" => content,
                        "get_Timestamp" => Timestamp,
                        "get_MentionedUserIds" => mentions ?? Array.Empty<ulong>(),
                        _ => throw new NotSupportedException(method.Name),
                    }
            );
            Messages[id] = message;
            History.Add(message);
            return message;
        }

        private async IAsyncEnumerable<IReadOnlyCollection<IMessage>> ReadHistory(
            ulong afterMessageId,
            Direction direction,
            int limit,
            RequestOptions options
        )
        {
            Requests.Add("history");
            Tokens.Add(options.CancelToken);
            AfterMessageId = afterMessageId;
            Direction = direction;
            Limit = limit;
            MessagesRead?.Invoke();
            if (FailureStage == "messages")
                throw Error!;
            await Task.CompletedTask;
            yield return History.Take(2).ToArray();
            yield return History.Skip(2).ToArray();
        }

        private Task<IMessage> ReadMessage(ulong id, RequestOptions options)
        {
            Requests.Add("message");
            Tokens.Add(options.CancelToken);
            MessagesRead?.Invoke();
            return FailureStage == "messages" ? Task.FromException<IMessage>(Error!) : Task.FromResult(Messages.GetValueOrDefault(id)!);
        }
    }
}
