using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using ChampionsOfKhazad.Bot.Lore.Mongo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Portal.Tests;

public class MongoLoreStoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreatingLoreInsertsRatherThanReplacingAnExistingEntry(bool member)
    {
        var fixture = new MongoLoreStoreFixture();
        using var services = fixture.CreateServices();
        var creator = services.GetRequiredService<ICreateLore>();
        var token = TestContext.Current.CancellationToken;
        var created = member
            ? await creator.CreateLoreAsync(new MemberLore("Member", "they", "UK", "Character", "Biography"), token)
            : await creator.CreateLoreAsync(new GuildLore("Guild", "Content"), token);

        Assert.True(created);
        Assert.NotNull(fixture.Inserted);
        Assert.Null(fixture.Replacement);
        Assert.Equal(token, fixture.WriteToken);
        Assert.Equal(token, fixture.EmbeddingToken);
        Assert.NotNull(fixture.Inserted.Embedding);
        Assert.Equal([0.1f, 0.2f], fixture.Inserted.Embedding);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, true)]
    public async Task UpdatingLoreRequiresAnExistingMatchWithoutUpserting(long matched, long modified, bool expected)
    {
        var fixture = new MongoLoreStoreFixture { ReplaceResult = new ReplaceOneResult.Acknowledged(matched, modified, null) };
        fixture.Seed(new LoreDocument("Guild", "Original"));
        using var services = fixture.CreateServices();
        var updated = await services
            .GetRequiredService<IUpdateLore>()
            .UpdateLoreAsync(new GuildLore("Guild", "Content"), TestContext.Current.CancellationToken);

        Assert.Equal(expected, updated);
        Assert.Null(fixture.Inserted);
        Assert.NotNull(fixture.Replacement);
        Assert.False(fixture.ReplaceOptions!.IsUpsert);
        Assert.Equal(Collections.Lore.UniqueIndex.Collation, fixture.ReplaceOptions.Collation);
        Assert.Equal(TestContext.Current.CancellationToken, fixture.WriteToken);
    }

    [Fact]
    public async Task EmbeddingRegenerationStillSupportsExplicitUpserts()
    {
        var fixture = new MongoLoreStoreFixture();

        await fixture.Store.UpsertLoreAsync(new GuildLore("Guild", "Content"));

        Assert.NotNull(fixture.Replacement);
        Assert.True(fixture.ReplaceOptions!.IsUpsert);
    }

    [Fact]
    public async Task CancelledRequestDoesNotWriteLore()
    {
        var fixture = new MongoLoreStoreFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Store.CreateLoreAsync(new GuildLore("Guild", "Content"), cancellation.Token)
        );

        Assert.Null(fixture.Inserted);
        Assert.Null(fixture.Replacement);
    }

    [Fact]
    public async Task CaseInsensitiveDuplicateIsRejectedWithoutEmbeddingsOrChangingTheExistingEntry()
    {
        var original = new LoreDocument("Guild", "Original");
        var fixture = new MongoLoreStoreFixture { EmbeddingFailure = new InvalidOperationException("OpenAI unavailable") };
        fixture.Seed(original);

        Assert.False(await fixture.Store.CreateLoreAsync(new GuildLore("gUiLd", "Replacement"), TestContext.Current.CancellationToken));

        Assert.Same(original, Assert.Single(fixture.Documents));
        Assert.Equal(0, fixture.EmbeddingCalls);
        Assert.Null(fixture.Inserted);
        Assert.Equal(Collections.Lore.UniqueIndex.Collation, fixture.ReadOptions!.Collation);
    }

    [Fact]
    public async Task MissingUpdateIsRejectedWithoutEmbeddings()
    {
        var fixture = new MongoLoreStoreFixture { EmbeddingFailure = new InvalidOperationException("OpenAI unavailable") };

        Assert.False(await fixture.Store.UpdateLoreAsync(new GuildLore("Missing", "Content"), TestContext.Current.CancellationToken));

        Assert.Equal(0, fixture.EmbeddingCalls);
        Assert.Null(fixture.Replacement);
        Assert.Empty(fixture.Documents);
    }

    [Fact]
    public async Task ConcurrentCaseInsensitiveCreatesLeaveExactlyOneEntry()
    {
        var bothInserting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;
        var fixture = new MongoLoreStoreFixture
        {
            BeforeInsert = async () =>
            {
                if (Interlocked.Increment(ref arrivals) == 2)
                    bothInserting.SetResult();
                await release.Task.WaitAsync(TestContext.Current.CancellationToken);
            },
        };
        var first = fixture.Store.CreateLoreAsync(new GuildLore("Guild", "First"), TestContext.Current.CancellationToken);
        var second = fixture.Store.CreateLoreAsync(new GuildLore("gUiLd", "Second"), TestContext.Current.CancellationToken);
        bool[] results;
        try
        {
            await bothInserting.Task.WaitAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            release.TrySetResult();
            results = await Task.WhenAll(first, second);
        }

        Assert.Equal([false, true], results.Order());
        Assert.Single(fixture.Documents);
        Assert.Equal(1, fixture.DuplicateKeyFailures);
    }

    [Fact]
    public async Task OtherDatabaseErrorsAreNotReportedAsDuplicateNames()
    {
        var failure = new InvalidOperationException("Database unavailable");
        var fixture = new MongoLoreStoreFixture { InsertFailure = failure };

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Store.CreateLoreAsync(new GuildLore("Guild", "Content"), TestContext.Current.CancellationToken)
        );

        Assert.Same(failure, actual);
        Assert.Empty(fixture.Documents);
    }
}
