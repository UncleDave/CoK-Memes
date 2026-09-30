using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace ChampionsOfKhazad.Bot.Tests;

public class NotebookReviewTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ReviewCardFitsDiscordLimitsWithMaximumLengthInputsAndThreeSources()
    {
        var note = CreateNote() with
        {
            Subject = new string('s', 80),
            Content = new string('c', 400),
            Reason = new string('r', 300),
            ReviewReason = new string('a', 300),
            Sources = Enumerable
                .Range(1, 3)
                .Select(i => new NotebookSource(
                    $"https://discord.com/channels/123456789012345678/123456789012345679/12345678901234568{i}",
                    (ulong)i,
                    new string('n', 80),
                    Now,
                    new string('x', 1000)
                ))
                .ToArray(),
        };
        var embed = NotebookReview.CreateEmbed(note);
        Assert.True(embed.Title.Length <= 256);
        Assert.All(embed.Fields, field => Assert.True(field.Value.Length <= 1024));
        Assert.True(
            embed.Title.Length
                + embed.Description.Length
                + embed.Footer!.Value.Text.Length
                + embed.Fields.Sum(field => field.Name.Length + field.Value.Length)
                <= 6000
        );
        Assert.Equal(3, embed.Fields.Count(field => field.Name == "Source excerpt (quoted data)"));
        Assert.Contains(embed.Fields, field => field.Value.Contains($"notebook discard {note.Id}", StringComparison.Ordinal));
        Assert.Contains(embed.Fields, field => field.Name == "Why he chose to remember it" && field.Value == note.Reason);
    }

    [Fact]
    public void NotebookStateRoundTripsThroughMongoSerialization()
    {
        var state = new NotebookState
        {
            Revision = 7,
            ReviewRevision = 3,
            Paused = true,
            Notes = [CreateNote() with { ReviewDelivered = true, DiscardedAtUtc = Now }],
            EvaluationAttempts = [new NotebookEvaluationAttempt(42, Now)],
        };
        var bson = state.ToBsonDocument();
        Assert.Equal("lorekeeper", bson["_id"].AsString);
        var restored = BsonSerializer.Deserialize<NotebookState>(bson);
        Assert.Equal(state.Revision, restored.Revision);
        Assert.Equal(state.ReviewRevision, restored.ReviewRevision);
        Assert.True(restored.Paused);
        Assert.Equal(new NotebookEvaluationAttempt(42, Now), Assert.Single(restored.EvaluationAttempts));
        var note = Assert.Single(restored.Notes);
        Assert.True(note.ReviewDelivered);
        Assert.Equal(Now, note.DiscardedAtUtc);
        Assert.Equal(DateTimeKind.Utc, note.CreatedAtUtc.Kind);
        Assert.Equal("Independent explanation", note.ReviewReason);
        var source = Assert.Single(note.Sources);
        Assert.Equivalent(state.Notes[0].Sources[0], source);
    }

    [Fact]
    public void MissingReviewRevisionInExistingDocumentDefaultsToZero()
    {
        var bson = new NotebookState { Revision = 7, Notes = [CreateNote()] }.ToBsonDocument();
        var member = BsonClassMap.LookupClassMap(typeof(NotebookState)).GetMemberMap(nameof(NotebookState.ReviewRevision));
        bson.Remove(member.ElementName);
        var restored = BsonSerializer.Deserialize<NotebookState>(bson);
        Assert.Equal(0, restored.ReviewRevision);
        Assert.Equal(7, restored.Revision);
        Assert.Single(restored.Notes);
    }

    [Fact]
    public void ReviewExcerptsDoNotSplitEmojiSurrogatePairs()
    {
        var text = new string('x', 159) + "😀 correction";
        var excerpt = NotebookReview.Excerpt(text);
        Assert.Equal(new string('x', 159) + "…", excerpt);
        Assert.DoesNotContain(excerpt, char.IsSurrogate);
    }

    [Fact]
    public void ReviewDisplayEscapesUntrustedFormattingAndPreservesSourceIdentityLabels()
    {
        var note = CreateNote() with
        {
            Subject = "**Official approval**\n@everyone",
            Content = "[approved](https://example.invalid) **canon**",
            Sources = [CreateNote().Sources[0] with { MentionedUsers = [new NotebookMentionedUser(123, "**Alice**")] }],
        };
        var embed = NotebookReview.CreateEmbed(note);
        Assert.Contains("\\*", embed.Title);
        Assert.DoesNotContain("\n", embed.Title);
        Assert.DoesNotContain("@everyone", embed.Title, StringComparison.Ordinal);
        Assert.Contains("\\[", embed.Description);
        Assert.Contains(
            embed.Fields,
            field => field.Name == "Mentioned users (source labels)" && field.Value.Contains("Discord user 123", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task CommandsCanInspectDiscardPauseResumeAndReviewHistory()
    {
        var store = new MemoryStore { State = new NotebookState { Notes = [CreateNote() with { ReviewDelivered = true }] } };
        var clock = new TestClock();
        var service = new NotebookService(store, null!, null!, null!, clock, NullLogger<NotebookService>.Instance);
        var command = new NotebookDirectMessageCommand(service, clock, NullLogger<NotebookDirectMessageCommand>.Instance);
        var token = TestContext.Current.CancellationToken;
        Assert.Contains("1/100", await command.ExecuteAsync("notebook", token));
        Assert.Contains("Raid anecdote", await command.ExecuteAsync("  NOTEBOOK LIST  ", token));
        Assert.Contains("Why he chose it: Useful", await command.ExecuteAsync("notebook show id", token));
        Assert.Contains("discarded", await command.ExecuteAsync("notebook discard ID", token));
        Assert.Contains("No notes", await command.ExecuteAsync("notebook list", token));
        Assert.Contains("discarded", await command.ExecuteAsync("notebook history", token));
        Assert.Contains("paused", await command.ExecuteAsync("notebook pause", token));
        Assert.True(store.State.Paused);
        Assert.Contains("resumed", await command.ExecuteAsync("notebook resume", token));
        Assert.False(store.State.Paused);
        Assert.Null(await command.ExecuteAsync("personality", token));
    }

    [Theory]
    [InlineData("notebook list 0")]
    [InlineData("notebook list -1")]
    [InlineData("notebook list 9999999999")]
    [InlineData("notebook discard")]
    [InlineData("notebook pause extra")]
    public async Task InvalidCommandsDoNotChangeState(string input)
    {
        var store = new MemoryStore();
        var service = new NotebookService(store, null!, null!, null!, new TestClock(), NullLogger<NotebookService>.Instance);
        var command = new NotebookDirectMessageCommand(service, new TestClock(), NullLogger<NotebookDirectMessageCommand>.Instance);
        Assert.Contains("Notebook commands", await command.ExecuteAsync(input, TestContext.Current.CancellationToken));
        Assert.Equal(0, store.State.Revision);
    }

    [Theory]
    [InlineData("notebook")]
    [InlineData("notebook pause")]
    [InlineData("notebook discard id")]
    public async Task PersistenceFailureIsReportedWithoutClaimingAControlChange(string input)
    {
        var service = new NotebookService(new FailingStore(), null!, null!, null!, new TestClock(), NullLogger<NotebookService>.Instance);
        var command = new NotebookDirectMessageCommand(service, new TestClock(), NullLogger<NotebookDirectMessageCommand>.Instance);
        var result = await command.ExecuteAsync(input, TestContext.Current.CancellationToken);
        Assert.Contains("effect could not be confirmed", result);
        Assert.Contains("Do not assume", result);
    }

    [Fact]
    public async Task AdminCommandCancellationIsNotConvertedIntoFailureConfirmation()
    {
        var service = new NotebookService(new MemoryStore(), null!, null!, null!, new TestClock(), NullLogger<NotebookService>.Instance);
        var command = new NotebookDirectMessageCommand(service, new TestClock(), NullLogger<NotebookDirectMessageCommand>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.ExecuteAsync("notebook", cancellation.Token));
    }

    private static NotebookNote CreateNote() =>
        new(
            "id",
            1,
            "Raid anecdote",
            "observation",
            "A specific observation",
            "Useful",
            [new NotebookSource("https://discord.com/channels/1/2/3", 42, "Member", Now, "Quoted source")],
            Now,
            Now.AddDays(30)
        )
        {
            ReviewReason = "Independent explanation",
        };

    private sealed class TestClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class MemoryStore : INotebookStore
    {
        public NotebookState State { get; set; } = new();

        public Task<NotebookState> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(State);
        }

        public Task<bool> TrySaveAsync(NotebookState state, CancellationToken cancellationToken)
        {
            State = state with { Revision = state.Revision + 1 };
            return Task.FromResult(true);
        }
    }

    private sealed class FailingStore : INotebookStore
    {
        public Task<NotebookState> GetAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Database unavailable");

        public Task<bool> TrySaveAsync(NotebookState state, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Database unavailable");
    }
}
