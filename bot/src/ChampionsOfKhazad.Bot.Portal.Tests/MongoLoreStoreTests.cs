using System.Net;
using System.Reflection;
using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using ChampionsOfKhazad.Bot.Lore.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace ChampionsOfKhazad.Bot.Portal.Tests;

public class MongoLoreStoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreatingLoreSendsAnInsertWithEmbeddingsAndCancellation(bool member)
    {
        var fixture = new MongoLoreStoreFixture();
        var token = TestContext.Current.CancellationToken;
        ILore lore = member ? new MemberLore("Member", "they", "UK", "Character", "Biography") : new GuildLore("Guild", "Content");
        var created = await fixture.Store.CreateLoreAsync(lore, token);

        Assert.True(created);
        Assert.NotNull(fixture.Inserted);
        Assert.Null(fixture.Replacement);
        Assert.Equal(token, fixture.WriteToken);
        Assert.Equal(token, fixture.EmbeddingToken);
        Assert.NotNull(fixture.Inserted.Embedding);
        Assert.Equal([0.1f, 0.2f], fixture.Inserted.Embedding);
        Assert.Equal(lore.Name, GetFilterName(fixture.ReadFilter!));
        Assert.Equal(Collections.Lore.UniqueIndex.Collation, fixture.ReadOptions!.Collation);
        Assert.Equal(CollationStrength.Primary, fixture.ReadOptions.Collation.Strength);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, true)]
    public async Task UpdatingLoreRequiresAnExistingMatchWithoutUpserting(long matched, long modified, bool expected)
    {
        var fixture = new MongoLoreStoreFixture { ReplaceResult = new ReplaceOneResult.Acknowledged(matched, modified, null) };
        fixture.ReadResult = new LoreDocument("Guild", "Original");
        var updated = await fixture.Store.UpdateLoreAsync(new GuildLore("Guild", "Content"), TestContext.Current.CancellationToken);

        Assert.Equal(expected, updated);
        Assert.Null(fixture.Inserted);
        Assert.NotNull(fixture.Replacement);
        Assert.False(fixture.ReplaceOptions!.IsUpsert);
        Assert.Equal(Collections.Lore.UniqueIndex.Collation, fixture.ReplaceOptions.Collation);
        Assert.Equal(CollationStrength.Primary, fixture.ReplaceOptions.Collation.Strength);
        Assert.Equal(TestContext.Current.CancellationToken, fixture.WriteToken);
        Assert.Equal("Guild", GetFilterName(fixture.ReplaceFilter!));
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
    public async Task ExistingNameIsCheckedWithCollationAndRejectedBeforeAnyWrite()
    {
        var original = new LoreDocument("Guild", "Original");
        var fixture = new MongoLoreStoreFixture { ReadResult = original, EmbeddingFailure = new InvalidOperationException("OpenAI unavailable") };

        Assert.False(await fixture.Store.CreateLoreAsync(new GuildLore("gUiLd", "Replacement"), TestContext.Current.CancellationToken));

        Assert.Equal(0, fixture.EmbeddingCalls);
        Assert.Null(fixture.Inserted);
        Assert.Null(fixture.Replacement);
        Assert.Equal("gUiLd", GetFilterName(fixture.ReadFilter!));
        Assert.Equal(Collections.Lore.UniqueIndex.Collation, fixture.ReadOptions!.Collation);
        Assert.Equal(CollationStrength.Primary, fixture.ReadOptions.Collation.Strength);
    }

    [Fact]
    public async Task MissingUpdateIsRejectedWithoutEmbeddings()
    {
        var fixture = new MongoLoreStoreFixture { EmbeddingFailure = new InvalidOperationException("OpenAI unavailable") };

        Assert.False(await fixture.Store.UpdateLoreAsync(new GuildLore("Missing", "Content"), TestContext.Current.CancellationToken));

        Assert.Equal(0, fixture.EmbeddingCalls);
        Assert.Null(fixture.Replacement);
        Assert.Null(fixture.Inserted);
    }

    [Fact]
    public async Task DuplicateKeyFromTheDatabaseIsReportedAsARejectedCreate()
    {
        var fixture = new MongoLoreStoreFixture { InsertFailure = DuplicateNameException() };

        Assert.False(await fixture.Store.CreateLoreAsync(new GuildLore("Guild", "Content"), TestContext.Current.CancellationToken));
        Assert.NotNull(fixture.Inserted);
        Assert.Null(fixture.Replacement);
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
        Assert.Null(fixture.Replacement);
    }

    private static string GetFilterName(FilterDefinition<LoreDocument> filter) =>
        filter
            .Render(new RenderArgs<LoreDocument>(BsonSerializer.SerializerRegistry.GetSerializer<LoreDocument>(), BsonSerializer.SerializerRegistry))
            .GetElement(0)
            .Value.AsString;

    private static MongoWriteException DuplicateNameException()
    {
        // Only this catch-path test needs the driver's internal WriteError constructor.
        var error = (WriteError)
            Activator.CreateInstance(
                typeof(WriteError),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                [ServerErrorCategory.DuplicateKey, 11000, "Duplicate lore name", new BsonDocument()],
                null
            )!;
        return new MongoWriteException(new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017))), error, null, null);
    }
}
