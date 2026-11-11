using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Tests;

public class SpecialsDirectMessageCommandTests
{
    [Fact]
    public async Task DraftPrivatelyShowsExactMenuAndSelectedSourcesThenApprovalPublishesOnlyTheMenu()
    {
        var fixture = new Fixture();
        await fixture.Run("specials draft");
        var pending = Assert.IsType<SpecialsPendingDraft>(fixture.Session.Pending);
        Assert.Empty(fixture.Gateway.Publications);
        Assert.Equal(fixture.Clock.Now.AddDays(-7), fixture.Gateway.Since);
        Assert.Contains(pending.Menu, fixture.Replies[1]);
        Assert.Contains("New Sign Supper", pending.Menu);
        Assert.DoesNotContain("source", pending.Menu);
        Assert.DoesNotContain("The guild was renamed", pending.Menu);
        var review = fixture.Replies[^1];
        Assert.Contains("The guild was renamed", review);
        Assert.Contains(Gateway.Url, review);
        Assert.Contains($"specials approve {pending.Token}", review);
        Assert.Contains("`specials discard`", review);
        Assert.Contains("Coverage:", review);
        Assert.DoesNotContain("lore", string.Join('\n', fixture.Replies));
        await fixture.Run($"specials approve {pending.Token}");
        Assert.Equal(pending.Menu, Assert.Single(fixture.Gateway.Publications));
        Assert.Null(fixture.Session.Pending);
        Assert.Equal(1, fixture.Writer.WriteCalls);
    }

    [Fact]
    public async Task DiscoveryReceivesTheWeekAndWritingReceivesResolvedNeighbouringContext()
    {
        var fixture = new Fixture();
        fixture.Gateway.Sources = Enumerable
            .Range(1, 500)
            .Select(index => SpecialsDiscoveryTests.Source(index + 1000, 3, fixture.Clock.Now.AddDays(-2).AddSeconds(index), $"Chat {index}"))
            .ToArray();
        fixture.Gateway.Sources[199] = fixture.Gateway.Sources[199] with { Url = Gateway.Url, Content = "Rename announcement" };
        await fixture.Run("specials draft");
        Assert.Equal(500, fixture.Writer.Discovered.SelectMany(batch => batch).DistinctBy(source => source.Url).Count());
        Assert.True(fixture.Writer.Discovered.Count > 1);
        var context = Assert.Single(fixture.Writer.Contexts!);
        Assert.Contains(context.Sources, source => source.Content == "Chat 199");
        Assert.Contains(context.Sources, source => source.Content == "Chat 201");
        Assert.All(context.Sources, source => Assert.Equal("Server display name", source.AuthorName));
        Assert.Single(fixture.Gateway.VerifiedSources!);
    }

    [Fact]
    public async Task MissingReadAndDiscoveryCoverageIsVisibleRatherThanImplyingACompleteWeek()
    {
        var fixture = new Fixture();
        fixture.Gateway.Incomplete = true;
        await fixture.Run("specials draft");
        Assert.Contains("1 failed, 2 limited", fixture.Session.Pending!.Coverage);
        Assert.Contains("Weekly coverage is incomplete", fixture.Replies[^1]);
    }

    [Fact]
    public async Task ShowReusesAssetsAndDoesNotExtendExpiryAndDiscardClearsApproval()
    {
        var fixture = new Fixture();
        await fixture.Run("specials draft");
        var pending = fixture.Session.Pending!;
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(8);
        await fixture.Run("specials show");
        Assert.Same(pending, fixture.Session.Pending);
        Assert.Contains("within 22 minutes", fixture.Replies[^1]);
        Assert.Equal(1, fixture.Writer.WriteCalls);
        await fixture.Run("specials discard");
        await fixture.Run($"specials approve {pending.Token}");
        Assert.Null(fixture.Session.Pending);
        Assert.Empty(fixture.Gateway.Publications);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("changed-evidence")]
    [InlineData("changed-destination")]
    [InlineData("permissions")]
    public async Task StaleOrUnavailableApprovalCannotPublish(string reason)
    {
        var fixture = new Fixture();
        await fixture.Run("specials draft");
        var token = fixture.Session.Pending!.Token;
        if (reason == "expired")
            fixture.Clock.Now = fixture.Clock.Now.AddMinutes(30);
        if (reason == "changed-evidence")
            fixture.Gateway.Valid = false;
        if (reason == "changed-destination")
            fixture.Gateway.Destination = new(99, "other");
        if (reason == "permissions")
            fixture.Gateway.PublicationError = "The bot needs Send Messages.";
        await fixture.Run($"specials approve {token}");
        Assert.Null(fixture.Session.Pending);
        Assert.Empty(fixture.Gateway.Publications);
    }

    [Fact]
    public async Task PostingPermissionsAreOnlyCheckedAtApprovalAndBadTokensDoNotConsumeTheDraft()
    {
        var fixture = new Fixture();
        fixture.Gateway.PublicationError = "The bot needs Send Messages.";
        await fixture.Run("specials draft");
        Assert.NotNull(fixture.Session.Pending);
        Assert.Equal(0, fixture.Gateway.PublicationChecks);
        await fixture.Run("specials approve wrong");
        Assert.NotNull(fixture.Session.Pending);
        Assert.Empty(fixture.Gateway.Publications);
    }

    [Fact]
    public async Task ReplacementInvalidatesOldTokenAndConcurrentApprovalsSendOnce()
    {
        var fixture = new Fixture();
        await fixture.Run("specials draft");
        var old = fixture.Session.Pending!.Token;
        await fixture.Run("specials draft");
        var fresh = fixture.Session.Pending!.Token;
        Assert.NotEqual(old, fresh);
        await fixture.Run($"specials approve {old}");
        Assert.Empty(fixture.Gateway.Publications);
        await Task.WhenAll(fixture.Run($"specials approve {fresh}"), fixture.Run($"specials approve {fresh}"));
        Assert.Single(fixture.Gateway.Publications);
    }

    [Fact]
    public async Task FailedPreviewDeliveryCannotActivateApproval()
    {
        var fixture = new Fixture();
        var sends = 0;
        await fixture.Command.TryExecuteAsync(
            1,
            "specials draft",
            (_, _) => ++sends == 3 ? Task.FromException(new InvalidOperationException("SECRET DM content")) : Task.CompletedTask,
            TestContext.Current.CancellationToken
        );
        Assert.Null(fixture.Session.Pending);
        Assert.Empty(fixture.Gateway.Publications);
    }

    [Fact]
    public async Task AmbiguousSendConsumesApprovalAndWarnsWithoutExposingProviderData()
    {
        var fixture = new Fixture();
        await fixture.Run("specials draft");
        var token = fixture.Session.Pending!.Token;
        fixture.Gateway.SendFails = true;
        await fixture.Run($"specials approve {token}");
        Assert.Contains("Publication could not be confirmed", fixture.Replies[^1]);
        Assert.DoesNotContain("SECRET", fixture.Replies[^1]);
        await fixture.Run($"specials approve {token}");
        Assert.Single(fixture.Gateway.Publications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuietOrUnsuitableWeeksDoNotCreateFiller(bool noMessages)
    {
        var fixture = new Fixture();
        if (noMessages)
            fixture.Gateway.Sources = [];
        else
            fixture.Writer.NoEvents = true;
        await fixture.Run("specials draft");
        Assert.Null(fixture.Session.Pending);
        Assert.Contains("No suitable moments", fixture.Replies[^1]);
        Assert.Empty(fixture.Gateway.Publications);
    }

    [Fact]
    public async Task GenerationFailuresClearPreviousApprovalsAndDoNotExposeRawErrors()
    {
        var fixture = new Fixture();
        await fixture.Run("specials draft");
        fixture.Writer.Fail = true;
        await fixture.Run("specials draft");
        Assert.Null(fixture.Session.Pending);
        Assert.Contains("discovering weekly events", fixture.Replies[^1]);
        Assert.DoesNotContain("SECRET", string.Join('\n', fixture.Replies));
        Assert.DoesNotContain("SECRET", string.Join('\n', fixture.Logger.Messages));
        Assert.Equal(1, fixture.Session.Gate.CurrentCount);
    }

    [Theory]
    [InlineData("TextTooLong", "301/240 characters")]
    [InlineData("MultilineText", "multiple lines for Summary")]
    [InlineData("MissingText", "empty Summary")]
    public async Task DiscoveryFailureReportsBatchAndSafeFieldDetailsWithoutRetryingOrPublishing(string category, string expected)
    {
        var fixture = new Fixture();
        fixture.Gateway.Sources = Enumerable
            .Range(1, 250)
            .Select(index => SpecialsDiscoveryTests.Source(index, 3, fixture.Clock.Now.AddDays(-2).AddSeconds(index), "SECRET weekly chat"))
            .ToArray();
        fixture.Writer.Failure = new SpecialsValidationException(category, SpecialsValidationField.Summary, 301, 240);
        fixture.Writer.FailAtBatch = 2;
        await fixture.Run("specials draft");
        Assert.Contains("discovering weekly events (batch 2/3)", fixture.Replies[^1]);
        Assert.Contains(expected, fixture.Replies[^1]);
        Assert.Contains("Pending approval was cleared", fixture.Replies[^1]);
        var log = Assert.Single(fixture.Logger.Messages);
        Assert.Contains("batch 2/3", log);
        Assert.Contains("Summary", log);
        Assert.Contains("301", log);
        Assert.Contains("240", log);
        Assert.DoesNotContain("SECRET", string.Join('\n', fixture.Replies.Concat(fixture.Logger.Messages)));
        Assert.Equal(2, fixture.Writer.Discovered.Count);
        Assert.Equal(0, fixture.Writer.WriteCalls);
        Assert.Empty(fixture.Gateway.Publications);
        Assert.Null(fixture.Session.Pending);
    }

    [Fact]
    public async Task CancellationClearsTheDraftAndReleasesTheGate()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Command.TryExecuteAsync(
                1,
                "specials draft",
                async (_, _) =>
                {
                    await cancellation.CancelAsync();
                    cancellation.Token.ThrowIfCancellationRequested();
                },
                cancellation.Token
            )
        );
        Assert.Null(fixture.Session.Pending);
        Assert.Equal(1, fixture.Session.Gate.CurrentCount);
    }

    [Theory]
    [InlineData(2UL, "specials draft")]
    [InlineData(2UL, "specials approve token")]
    [InlineData(1UL, "gazette draft")]
    [InlineData(1UL, "specialsboard")]
    public async Task UnauthorizedOrUnrelatedCommandsDoNothing(ulong actor, string content)
    {
        var fixture = new Fixture();
        Assert.False(
            await fixture.Command.TryExecuteAsync(
                actor,
                content,
                (_, _) => throw new InvalidOperationException(),
                TestContext.Current.CancellationToken
            )
        );
        Assert.Empty(fixture.Writer.Discovered);
    }

    private sealed class Fixture
    {
        public Clock Clock { get; } = new();
        public Gateway Gateway { get; } = new();
        public Writer Writer { get; } = new();
        public SpecialsSession Session { get; } = new();
        public List<string> Replies { get; } = [];
        public CapturingLogger Logger { get; } = new();
        public SpecialsDirectMessageCommand Command { get; }

        public Fixture() =>
            Command = new(Gateway, Writer, Session, Options.Create(new DirectMessageHandlerOptions { AdminUserId = 1 }), Clock, Logger);

        public Task Run(string content) =>
            Command.TryExecuteAsync(
                1,
                content,
                (text, _) =>
                {
                    Replies.Add(text);
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken
            );
    }

    private sealed class CapturingLogger : ILogger<SpecialsDirectMessageCommand>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    internal sealed class Gateway : ISpecialsGateway
    {
        public const string Url = "https://discord.com/channels/1/3/42";
        public string DestinationError => "Destination is unavailable.";
        public SpecialsDestination? Destination { get; set; } = new(8, "ai-tavern");
        public NotebookSource[] Sources { get; set; } =
        [new(Url, 9, "Global name", new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc), "The guild was renamed.")];
        public bool Valid { get; set; } = true;
        public bool Incomplete { get; set; }
        public bool SendFails { get; set; }
        public string? PublicationError { get; set; }
        public int PublicationChecks { get; private set; }
        public List<string> Publications { get; } = [];
        public IReadOnlyList<NotebookSource>? VerifiedSources { get; private set; }
        public DateTimeOffset Since { get; private set; }
        public bool DestinationCalled { get; private set; }

        public SpecialsDestination? GetDestination()
        {
            DestinationCalled = true;
            return Destination;
        }

        public string? GetPublicationError(ulong destinationId)
        {
            PublicationChecks++;
            return PublicationError;
        }

        public Task<SpecialsChatBatch> ReadWeekAsync(
            ulong destinationId,
            DateTimeOffset since,
            DateTimeOffset until,
            CancellationToken cancellationToken
        )
        {
            Since = since;
            return Task.FromResult(new SpecialsChatBatch(Sources, Incomplete ? 4 : 1, 1, Incomplete ? 1 : 0, Incomplete ? 2 : 0, 1, 0));
        }

        public Task<IReadOnlyList<NotebookSource>> ResolveNamesAsync(IReadOnlyList<NotebookSource> sources, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NotebookSource>>(sources.Select(source => source with { AuthorName = "Server display name" }).ToArray());

        public Task<bool> VerifyAsync(ulong destinationId, IReadOnlyList<NotebookSource> sources, CancellationToken cancellationToken)
        {
            VerifiedSources = sources;
            return Task.FromResult(Valid);
        }

        public Task<ulong> PublishAsync(ulong destinationId, string menu, CancellationToken cancellationToken)
        {
            Publications.Add(menu);
            return SendFails ? Task.FromException<ulong>(new InvalidOperationException("SECRET provider data")) : Task.FromResult(100UL);
        }
    }

    private sealed class Writer : ISpecialsWriter
    {
        public List<IReadOnlyList<NotebookSource>> Discovered { get; } = [];
        public IReadOnlyList<SpecialsContext>? Contexts { get; private set; }
        public int WriteCalls { get; private set; }
        public bool NoEvents { get; set; }
        public bool Fail { get; set; }
        public Exception? Failure { get; set; }
        public int FailAtBatch { get; set; } = 1;

        public Task<IReadOnlyList<SpecialsEvent>> DiscoverAsync(
            IReadOnlyList<NotebookSource> sources,
            DateTimeOffset until,
            CancellationToken cancellationToken
        )
        {
            if (Fail)
                throw new InvalidOperationException("SECRET provider data");
            Discovered.Add(sources);
            if (Failure is { } failure && Discovered.Count == FailAtBatch)
                throw failure;
            return Task.FromResult<IReadOnlyList<SpecialsEvent>>(
                NoEvents || sources.All(source => source.Url != Gateway.Url) ? [] : [new("Rename", [Gateway.Url])]
            );
        }

        public Task<IReadOnlyList<SpecialsEvent>> SelectAsync(
            IReadOnlyList<SpecialsEvent> candidates,
            DateTimeOffset until,
            CancellationToken cancellationToken
        ) => Task.FromResult<IReadOnlyList<SpecialsEvent>>(candidates.DistinctBy(item => item.Summary).Take(5).ToArray());

        public Task<IReadOnlyList<SpecialsDish>> WriteAsync(
            IReadOnlyList<SpecialsContext> events,
            DateTimeOffset until,
            CancellationToken cancellationToken
        )
        {
            WriteCalls++;
            Contexts = events;
            return Task.FromResult<IReadOnlyList<SpecialsDish>>(
                events.Count == 0
                    ? []
                    : [new(0, "New Sign Supper", "Same batter. Suspiciously fresh packaging.", "8g", "The guild was renamed.", [Gateway.Url])]
            );
        }
    }
}
