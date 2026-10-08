using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Tests;

public class GazetteDirectMessageCommandTests
{
    [Fact]
    public void ReadableEditionKeepsOriginalUnicodeNamesAndParagraphsWhilePrintUsesItsOwnGlyphCleanup()
    {
        var body = "Beaverhausen🦫 supplied the proposal.\n\nAlexie attributed it to the code-merge agent.";
        var date = DateTimeOffset.UtcNow;
        var text = GazetteDirectMessageCommand.Render(new([new("Headline", body, ["source"])], "Ad"), date.AddDays(-7), date);
        Assert.Contains("Beaverhausen🦫", text);
        Assert.Contains("proposal.\n\nAlexie", text);
    }

    [Fact]
    public async Task RenderingFailureIsClearlyDistinguishedFromModelValidation()
    {
        var fixture = new Fixture();
        fixture.Renderer.Failure = new InvalidOperationException("SECRET renderer data");
        await fixture.Run("gazette draft");
        Assert.Contains("rendering newspaper pages", Assert.Single(fixture.Logger.Messages));
        Assert.Contains(fixture.Replies, reply => reply.Contains("rendering newspaper pages"));
        Assert.DoesNotContain("SECRET", string.Join('\n', fixture.Logger.Messages.Concat(fixture.Replies)));
        Assert.Null(fixture.Session.Pending);
        Assert.Empty(fixture.Pages);
    }

    [Fact]
    public async Task RejectedModelFieldsIdentifyStageAndBoundsWithoutExposingContentInLogsOrDms()
    {
        var fixture = new Fixture();
        fixture.Writer.Failure = new GazetteDraftValidationException(GazetteValidationFailure.FieldTooLong, GazetteValidationField.Body, 701, 650);
        await fixture.Run("gazette draft");
        var log = Assert.Single(fixture.Logger.Messages);
        Assert.Contains("writing stories", log);
        Assert.Contains("FieldTooLong", log);
        Assert.Contains("Body", log);
        Assert.Contains("701", log);
        Assert.Contains("650", log);
        Assert.Contains(fixture.Replies, reply => reply.Contains("writing stories") && reply.Contains("701/650"));
        Assert.Null(fixture.Session.Pending);
        Assert.Empty(fixture.Gateway.Publications);
    }

    [Fact]
    public async Task GenericProviderFailuresExposeOnlyStageTypeAndStaticFailureSiteNotRawExceptionMessages()
    {
        var fixture = new Fixture();
        fixture.Writer.Failure = new InvalidOperationException("SECRET raw guild conversation and credential");
        await fixture.Run("gazette draft");
        Assert.Contains("writing stories", Assert.Single(fixture.Logger.Messages));
        Assert.DoesNotContain("SECRET", string.Join('\n', fixture.Logger.Messages.Concat(fixture.Replies)));
        Assert.Contains(fixture.Replies, reply => reply.Contains("writing stories"));
        Assert.Null(fixture.Session.Pending);
    }

    [Fact]
    public async Task TruncatedModelResponseIsExplainedAsAnOutputLimitNotAPermissionProblem()
    {
        var fixture = new Fixture();
        fixture.Writer.Failure = new GazetteDraftValidationException(GazetteValidationFailure.IncompleteResponse, GazetteValidationField.Response);
        await fixture.Run("gazette draft");
        Assert.Contains(fixture.Replies, reply => reply.Contains("cut short at its output limit"));
        Assert.DoesNotContain(fixture.Replies, reply => reply.Contains("permission", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AllPagesArePrivatelyPreviewedAndPublishedUnchangedWithOneApproval()
    {
        var fixture = new Fixture();
        fixture.Writer.Edition = new(
            [
                new("Lead story", "Lead body", [Fixture.Url]),
                new("Small dispatch", "Inside story body", [Fixture.Url]) { Teaser = "Inside story preview" },
            ],
            "Wanted: a clock."
        );
        await fixture.Run("gazette draft");
        var pending = Assert.IsType<GazettePendingDraft>(fixture.Session.Pending);
        Assert.Equal(2, fixture.Pages.Count);
        Assert.Equal(pending.PrintEdition.Pages, fixture.Pages);
        Assert.Contains(fixture.Replies, reply => reply.Contains("2 newspaper pages") && reply.Contains("Review all pages"));
        await fixture.Run($"gazette approve {pending.Token}");
        Assert.Single(fixture.Gateway.Publications);
        Assert.Equal(pending.PrintEdition.Pages, fixture.Gateway.PublishedPages);
        Assert.Contains("Inside story body", Assert.Single(fixture.Gateway.Publications).Text);
        Assert.Equal(1, fixture.Writer.Calls);
    }

    [Fact]
    public async Task FailedInsidePageDeliveryLeavesNoApprovalAndNoPublication()
    {
        var fixture = new Fixture();
        fixture.Writer.Edition = new([new("Lead", "Body", [Fixture.Url]), new("Inside", "Body", [Fixture.Url])], "Ad");
        var pageCalls = 0;
        await fixture.Command.TryExecuteAsync(
            1,
            "gazette draft",
            (_, _) => Task.CompletedTask,
            TestContext.Current.CancellationToken,
            sendPage: (_, _) =>
                ++pageCalls == 2 ? Task.FromException(new InvalidOperationException("Second page delivery failed")) : Task.CompletedTask
        );
        Assert.Equal(2, pageCalls);
        Assert.Null(fixture.Session.Pending);
        Assert.Empty(fixture.Gateway.Publications);
    }

    [Fact]
    public async Task UnavailableArtworkBudgetDoesNotSpendAnUnreservedImageCallOrPreventThePrivatePage()
    {
        var fixture = new Fixture();
        fixture.Writer.Edition = fixture.Writer.Edition with { IllustrationPrompt = "A woodcut phone." };
        fixture.IssueStore.FailWrites = true;
        await fixture.Run("gazette draft");
        Assert.NotNull(fixture.Session.Pending);
        Assert.Equal(0, fixture.Illustrator.Calls);
        Assert.Contains(fixture.Replies, reply => reply.Contains("Illustration was unavailable"));
    }

    [Fact]
    public async Task DraftHasIssueNumberAndHumanApprovalWindowWithoutUtc()
    {
        var fixture = new Fixture();
        await fixture.Run("gazette draft");
        Assert.Contains(fixture.Replies, reply => reply.Contains("Issue No. 1") && reply.Contains("within 30 minutes"));
        Assert.DoesNotContain(fixture.Replies, reply => reply.Contains("UTC"));
        Assert.Equal(0, fixture.IssueStore.State.LastReservedIssue);
        await fixture.Run("gazette discard");
        Assert.Equal(0, fixture.IssueStore.State.LastReservedIssue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalArtIsGeneratedOnceBeforePreviewAndFailureStillAllowsANewspaper(bool fails)
    {
        var fixture = new Fixture();
        fixture.Writer.Edition = fixture.Writer.Edition with { IllustrationPrompt = "A woodcut phone resurrection." };
        fixture.Illustrator.Fails = fails;
        await fixture.Run("gazette draft");
        Assert.NotNull(fixture.Session.Pending);
        Assert.Single(fixture.Pages);
        Assert.Equal(1, fixture.Illustrator.Calls);
        Assert.Single(fixture.IssueStore.State.IllustrationAttempts);
        var pending = fixture.Session.Pending!;
        await fixture.Run("gazette show");
        await fixture.Run($"gazette approve {pending.Token}");
        Assert.Equal(1, fixture.Illustrator.Calls);
        Assert.Same(Assert.Single(pending.PrintEdition.Pages), Assert.Single(fixture.Gateway.PublishedPages));
    }

    [Fact]
    public async Task DraftDoesNotCheckPostingPermissionsAndCanBeReviewedWhileBotCannotPublish()
    {
        var fixture = new Fixture();
        fixture.Gateway.PublicationError = "The bot needs Embed Links in #ai-tavern.";
        await fixture.Run("gazette draft");
        Assert.NotNull(fixture.Session.Pending);
        Assert.Equal(1, fixture.Writer.Calls);
        Assert.Equal(0, fixture.Gateway.PublicationChecks);
        Assert.Empty(fixture.Gateway.Publications);
    }

    [Fact]
    public async Task ApprovalChecksBotPostingPermissionsAndReportsKnownFailureWithoutAnAmbiguousSendWarning()
    {
        var fixture = new Fixture();
        fixture.Gateway.PublicationError = "The bot needs Embed Links in #ai-tavern.";
        await fixture.Run("gazette draft");
        await fixture.Run($"gazette approve {fixture.Session.Pending!.Token}");
        Assert.Equal(1, fixture.Gateway.PublicationChecks);
        Assert.Empty(fixture.Gateway.Publications);
        Assert.Null(fixture.Session.Pending);
        Assert.Contains(fixture.Replies, reply => reply.Contains(fixture.Gateway.PublicationError));
        Assert.DoesNotContain(fixture.Replies, reply => reply.Contains("Publication could not be confirmed"));
    }

    [Theory]
    [InlineData("destination")]
    [InlineData("no-messages")]
    [InlineData("no-stories")]
    [InlineData("source-changed")]
    public async Task DraftFailuresDescribeDraftingRatherThanPublication(string failure)
    {
        var fixture = new Fixture();
        switch (failure)
        {
            case "destination":
                fixture.Gateway.Destination = null;
                break;
            case "no-messages":
                fixture.Gateway.Sources = [];
                break;
            case "no-stories":
                fixture.Writer.Edition = new([], "");
                break;
            case "source-changed":
                fixture.Gateway.Valid = false;
                break;
        }
        await fixture.Run("gazette draft");
        Assert.Null(fixture.Session.Pending);
        Assert.DoesNotContain(fixture.Replies, reply => reply.Contains("published", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, fixture.Gateway.PublicationChecks);
    }

    [Fact]
    public async Task DraftIsPrivateAndOnlyBecomesApprovableAfterCompletePreviewDelivery()
    {
        var fixture = new Fixture();
        string? preview = null;
        await fixture.Command.TryExecuteAsync(
            1,
            "gazette draft",
            (text, _) =>
            {
                Assert.Null(fixture.Session.Pending);
                Assert.Empty(fixture.Gateway.Publications);
                preview = (preview ?? "") + text;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken,
            sendPage: (page, _) =>
            {
                fixture.Pages.Add(page);
                Assert.Null(fixture.Session.Pending);
                return Task.CompletedTask;
            }
        );

        var pending = Assert.IsType<GazettePendingDraft>(fixture.Session.Pending);
        Assert.Contains(pending.Edition, preview);
        Assert.Contains($"gazette approve {pending.Token}", preview);
        Assert.Contains("PRIVATE DRAFT", preview);
        Assert.Contains("1/2 eligible channels", preview);
        Assert.Equal(1, fixture.Writer.Calls);
        Assert.Equal(fixture.Clock.Now.AddDays(-7), fixture.Writer.Since);
        Assert.Equal(fixture.Clock.Now.AddMinutes(30), pending.ExpiresAtUtc);
    }

    [Fact]
    public async Task ApprovalPublishesExactPreviewOnceWithoutRegenerating()
    {
        var fixture = new Fixture();
        await fixture.Run("gazette draft");
        var pending = fixture.Session.Pending!;
        fixture.Replies.Clear();
        await fixture.Run($"gazette approve {pending.Token}");
        await fixture.Run($"gazette approve {pending.Token}");

        var publication = Assert.Single(fixture.Gateway.Publications);
        Assert.Equal(pending.Destination.Id, publication.Destination);
        Assert.Equal(pending.Edition, publication.Text);
        Assert.Same(Assert.Single(pending.PrintEdition.Pages), Assert.Single(fixture.Gateway.PublishedPages));
        Assert.Equal(1, fixture.Writer.Calls);
        Assert.Equal(3, fixture.Gateway.Verifications);
        Assert.Null(fixture.Session.Pending);
        Assert.Contains(fixture.Replies, text => text.Contains("No live Gazette draft"));
    }

    [Theory]
    [InlineData("gazette approve wrong")]
    [InlineData("gazette approve")]
    [InlineData("gazette approve wrong extra")]
    [InlineData("gazette publish")]
    [InlineData("gazette show")]
    public async Task OtherCommandsDoNotPublishOrConsumeAValidPreview(string command)
    {
        var fixture = new Fixture();
        await fixture.Run("gazette draft");
        var pending = fixture.Session.Pending;
        await fixture.Run(command);
        Assert.Same(pending, fixture.Session.Pending);
        Assert.Empty(fixture.Gateway.Publications);
    }

    [Theory]
    [InlineData("discard")]
    [InlineData("expiry")]
    [InlineData("changed-source")]
    [InlineData("changed-destination")]
    public async Task DiscardExpiryOrChangedAccessAndEvidenceBlockPublication(string change)
    {
        var fixture = new Fixture();
        await fixture.Run("gazette draft");
        var token = fixture.Session.Pending!.Token;
        switch (change)
        {
            case "discard":
                await fixture.Run("gazette discard");
                break;
            case "expiry":
                fixture.Clock.Now = fixture.Clock.Now.AddMinutes(30);
                break;
            case "changed-source":
                fixture.Gateway.Valid = false;
                break;
            case "changed-destination":
                fixture.Gateway.Destination = new(99, "ai-tavern");
                break;
        }
        await fixture.Run($"gazette approve {token}");
        Assert.Empty(fixture.Gateway.Publications);
        Assert.Null(fixture.Session.Pending);
    }

    [Fact]
    public async Task NewDraftInvalidatesTheEarlierApproval()
    {
        var fixture = new Fixture();
        await fixture.Run("gazette draft");
        var token = fixture.Session.Pending!.Token;
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
        await fixture.Run("gazette draft");
        Assert.NotEqual(token, fixture.Session.Pending!.Token);
        await fixture.Run($"gazette approve {token}");
        Assert.Empty(fixture.Gateway.Publications);
    }

    [Fact]
    public async Task ConcurrentApprovalsCannotDuplicatePublication()
    {
        var fixture = new Fixture();
        await fixture.Run("gazette draft");
        var command = $"gazette approve {fixture.Session.Pending!.Token}";
        await Task.WhenAll(fixture.Run(command), fixture.Run(command));
        Assert.Single(fixture.Gateway.Publications);
    }

    [Fact]
    public async Task AmbiguousSendConsumesApprovalAndWarnsAgainstRetry()
    {
        var fixture = new Fixture();
        await fixture.Run("gazette draft");
        var token = fixture.Session.Pending!.Token;
        fixture.Gateway.SendFails = true;
        await fixture.Run($"gazette approve {token}");
        await fixture.Run($"gazette approve {token}");
        Assert.Single(fixture.Gateway.Publications);
        Assert.Null(fixture.Session.Pending);
        Assert.Contains(fixture.Replies, text => text.Contains("check #ai-tavern"));
    }

    [Fact]
    public async Task FailedPrivateDeliveryLeavesNoApprovableDraft()
    {
        var fixture = new Fixture();
        var calls = 0;
        await fixture.Command.TryExecuteAsync(
            1,
            "gazette draft",
            (_, _) =>
            {
                calls++;
                return calls == 1 ? Task.FromException(new InvalidOperationException("DM unavailable")) : Task.CompletedTask;
            },
            TestContext.Current.CancellationToken,
            sendPage: (_, _) => Task.CompletedTask
        );
        Assert.Null(fixture.Session.Pending);
        Assert.Empty(fixture.Gateway.Publications);
    }

    [Theory]
    [InlineData("no-messages")]
    [InlineData("no-stories")]
    [InlineData("no-destination")]
    [InlineData("invalid-citation")]
    [InlineData("evidence-changed")]
    public async Task NoMaterialOrInvalidEvidenceDoesNotCreateApproval(string condition)
    {
        var fixture = new Fixture();
        switch (condition)
        {
            case "no-messages":
                fixture.Gateway.Sources = [];
                break;
            case "no-stories":
                fixture.Writer.Edition = new([], "");
                break;
            case "no-destination":
                fixture.Gateway.Destination = null;
                break;
            case "invalid-citation":
                fixture.Writer.Edition = new([new("Headline", "Body", ["https://discord.com/channels/1/3/99"])], "");
                break;
            case "evidence-changed":
                fixture.Gateway.Valid = false;
                break;
        }
        await fixture.Run("gazette draft");
        Assert.Null(fixture.Session.Pending);
        Assert.Empty(fixture.Gateway.Publications);
        if (condition is "no-messages" or "no-destination")
            Assert.Equal(0, fixture.Writer.Calls);
        if (condition == "no-destination")
            Assert.Contains(fixture.Replies, text => text.Contains(fixture.Gateway.DestinationError));
    }

    [Fact]
    public async Task RapidDraftRequestsDoNotSpendMoreModelCallsOrReplacePreview()
    {
        var fixture = new Fixture();
        await fixture.Run("gazette draft");
        var pending = fixture.Session.Pending;
        await fixture.Run("gazette draft");
        Assert.Same(pending, fixture.Session.Pending);
        Assert.Equal(1, fixture.Writer.Calls);
    }

    [Theory]
    [InlineData(2UL, "gazette draft")]
    [InlineData(2UL, "gazette approve token")]
    [InlineData(1UL, "lore list")]
    [InlineData(1UL, "gazetteer")]
    public async Task UnauthorizedOrUnrelatedMessagesAreNotHandled(ulong actor, string content)
    {
        var fixture = new Fixture();
        var handled = await fixture.Command.TryExecuteAsync(
            actor,
            content,
            (_, _) => throw new InvalidOperationException(),
            TestContext.Current.CancellationToken
        );
        Assert.False(handled);
        Assert.Equal(0, fixture.Writer.Calls);
        Assert.Equal(0, fixture.Gateway.Reads);
    }

    [Fact]
    public void RendererEscapesModelFormattingAndLimitsTheEditionToOneEmbed()
    {
        var now = DateTimeOffset.UtcNow;
        var edition = new GazetteEdition([new("**Headline**", "[Body] <@123>", [Fixture.Url])], "A fictional advert.");
        var rendered = GazetteDirectMessageCommand.Render(edition, now.AddDays(-7), now);
        Assert.Contains(@"\*\*Headline\*\*", rendered);
        Assert.Contains(@"\[Body\] \<@123\>", rendered);
        Assert.Contains("**Classifieds**", rendered);
        Assert.DoesNotContain("fictional satire", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("satirical classified", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() =>
            GazetteDirectMessageCommand.Render(new([new("Headline", new string('x', 4100), [Fixture.Url])], ""), now, now)
        );
    }

    private sealed class Fixture
    {
        public const string Url = "https://discord.com/channels/1/3/42";
        public Clock Clock { get; } = new();
        public Gateway Gateway { get; } = new();
        public Writer Writer { get; } = new();
        public GazetteSession Session { get; } = new();
        public List<string> Replies { get; } = [];
        public List<GazettePage> Pages { get; } = [];
        public MemoryGazetteIssueStore IssueStore { get; } = new();
        public StubGazetteIllustrator Illustrator { get; } = new();
        public CapturingLogger Logger { get; } = new();
        public StubGazettePageRenderer Renderer { get; } = new();
        public GazetteDirectMessageCommand Command { get; }

        public Fixture() =>
            Command = new(
                Gateway,
                Writer,
                Session,
                Options.Create(new DirectMessageHandlerOptions { AdminUserId = 1 }),
                Clock,
                Logger,
                new GazetteIssueService(IssueStore, Clock),
                Renderer,
                Illustrator,
                Options.Create(new GazetteOptions())
            );

        public Task Run(string content) =>
            Command.TryExecuteAsync(
                1,
                content,
                (text, _) =>
                {
                    Replies.Add(text);
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken,
                sendPage: (page, _) =>
                {
                    Pages.Add(page);
                    return Task.CompletedTask;
                }
            );
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Gateway : IGazetteGateway
    {
        public string DestinationError => "The Gazette channel could not be resolved.";

        public string? PublicationError { get; set; }
        public int PublicationChecks { get; private set; }

        public GazetteDestination? Destination { get; set; } = new(8, "ai-tavern");
        public bool Valid { get; set; } = true;
        public bool SendFails { get; set; }
        public int Reads { get; private set; }
        public int Verifications { get; private set; }
        public IReadOnlyList<NotebookSource> Sources { get; set; } =
        [new(Fixture.Url, 9, "Raider", new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc), "Dinner debate.")];
        public List<(ulong Destination, string Text)> Publications { get; } = [];
        public List<GazettePage> PublishedPages { get; } = [];

        public GazetteDestination? GetDestination() => Destination;

        public string? GetPublicationError(ulong destinationId)
        {
            PublicationChecks++;
            return PublicationError;
        }

        public Task<GazetteChatBatch> ReadRecentAsync(
            ulong destinationId,
            DateTimeOffset since,
            DateTimeOffset until,
            CancellationToken cancellationToken
        )
        {
            Reads++;
            return Task.FromResult(new GazetteChatBatch(Sources, 1, 2, 1));
        }

        public Task<bool> VerifyAsync(ulong destinationId, IReadOnlyList<NotebookSource> sources, CancellationToken cancellationToken)
        {
            Verifications++;
            return Task.FromResult(Valid);
        }

        public Task<ulong> PublishAsync(
            ulong destinationId,
            string edition,
            GazettePrintEdition printEdition,
            string publicationId,
            CancellationToken cancellationToken
        )
        {
            Publications.Add((destinationId, edition));
            PublishedPages.AddRange(printEdition.Pages);
            return SendFails ? Task.FromException<ulong>(new InvalidOperationException("Unknown send outcome")) : Task.FromResult(100UL);
        }
    }

    private sealed class Writer : IGazetteWriter
    {
        public Exception? Failure { get; set; }
        public int Calls { get; private set; }
        public DateTimeOffset Since { get; private set; }
        public GazetteEdition Edition { get; set; } =
            new(
                [new("DINNER DEBATE CONTINUES", "A modest discussion acquired considerable importance.", [Fixture.Url])],
                "Wanted: a competent clock."
            );

        public Task<GazetteEdition> WriteAsync(
            IReadOnlyList<NotebookSource> sources,
            DateTimeOffset since,
            DateTimeOffset until,
            CancellationToken cancellationToken
        )
        {
            Calls++;
            if (Failure is { } error)
                return Task.FromException<GazetteEdition>(error);
            Since = since;
            return Task.FromResult(Edition);
        }
    }

    private sealed class CapturingLogger : ILogger<GazetteDirectMessageCommand>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.Null(exception);
            Messages.Add(formatter(state, exception));
        }
    }
}
