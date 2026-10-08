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
    public async Task EmbeddingRegenerationCreatesWithHistoryInsteadOfAnUnconditionalUpsert()
    {
        var fixture = new MongoLoreStoreFixture();

        await fixture.Store.UpsertLoreAsync(new GuildLore("Guild", "Content"));

        Assert.NotNull(fixture.Inserted);
        Assert.Single(fixture.Inserted.History);
        Assert.NotNull(fixture.Inserted.Embedding);
        Assert.Null(fixture.Replacement);
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

    [Fact]
    public async Task EditingLegacyLoreRecordsOriginalSnapshotAndUsesNullRevisionCompareExchange()
    {
        var original = new LoreDocument("Guild", "Original");
        var fixture = new MongoLoreStoreFixture { ReadResult = original };
        Assert.True(await fixture.Store.UpdateLoreAsync(new GuildLore("guild", "Updated"), TestContext.Current.CancellationToken));

        var document = fixture.Replacement!;
        Assert.Equal("Guild", document.Name);
        var revision = Assert.Single(document.History);
        Assert.Equal("Original", revision.Before!.Content);
        Assert.Equal("Updated", revision.After!.Content);
        Assert.Equal(document.Revision, revision.Id);
        Assert.Equal("lore-store", revision.Source);
        Assert.Null(revision.ActorId);
        var filter = Render(fixture.ReplaceFilter!);
        var revisionField = BsonClassMap.LookupClassMap(typeof(LoreDocument)).GetMemberMap(nameof(LoreDocument.Revision)).ElementName;
        Assert.Equal(BsonNull.Value, filter[revisionField]);
    }

    [Fact]
    public async Task EditorWritesBothLoreAndAuditHistoryWithExpectedRevisionAndEmbeddings()
    {
        var original = new LoreDocument("Guild", "Original") { Revision = "version-one" };
        var fixture = new MongoLoreStoreFixture { ReadResult = original };
        var state = await fixture.Store.GetEntryAsync("Guild", TestContext.Current.CancellationToken);
        var revision = Revision(state, state.Entry! with { Content = "Updated" });

        Assert.True(await fixture.Store.TrySaveAsync(state, revision, TestContext.Current.CancellationToken));
        var replacement = fixture.Replacement!;
        Assert.Equal("Updated", replacement.Content);
        Assert.Equal(revision.Id, replacement.Revision);
        Assert.Equal(revision, Assert.Single(replacement.History));
        Assert.NotNull(replacement.Embedding);
        Assert.Equal([0.1f, 0.2f], replacement.Embedding);
        var revisionField = BsonClassMap.LookupClassMap(typeof(LoreDocument)).GetMemberMap(nameof(LoreDocument.Revision)).ElementName;
        Assert.Equal("version-one", Render(fixture.ReplaceFilter!)[revisionField].AsString);
        Assert.False(fixture.ReplaceOptions!.IsUpsert);
    }

    [Fact]
    public async Task DeletionIsAnAuditedTombstoneWithoutEmbeddingGeneration()
    {
        var fixture = new MongoLoreStoreFixture
        {
            ReadResult = new LoreDocument("Guild", "Original") { Revision = "current" },
            EmbeddingFailure = new InvalidOperationException("Should not embed a deletion"),
        };
        await fixture.Store.DeleteLoreAsync("Guild");

        var document = fixture.Replacement!;
        Assert.True(document.Deleted);
        Assert.Null(document.Embedding);
        Assert.Equal("", document.Content);
        Assert.Equal(0, fixture.EmbeddingCalls);
        var revision = Assert.Single(document.History);
        Assert.Null(revision.After);
        Assert.Equal("Original", revision.Before!.Content);
    }

    [Fact]
    public async Task RecreatingDeletedLoreKeepsItsNameAndHistory()
    {
        var old = new LoreEntrySnapshot("Guild", "guild") { Content = "Old" };
        var deletion = new LoreRevision("deleted", DateTime.UtcNow, "admin-dm", 1, "delete", old, null);
        var fixture = new MongoLoreStoreFixture
        {
            ReadResult = new LoreDocument("Guild", "")
            {
                Deleted = true,
                Revision = deletion.Id,
                History = [deletion],
            },
        };
        Assert.True(await fixture.Store.CreateLoreAsync(new GuildLore("guild", "New"), TestContext.Current.CancellationToken));
        Assert.Null(fixture.Inserted);
        Assert.False(fixture.Replacement!.Deleted);
        Assert.Equal("Guild", fixture.Replacement.Name);
        Assert.Equal(deletion, fixture.Replacement.History[0]);
        Assert.Null(fixture.Replacement.History[^1].Before);
        Assert.Equal("New", fixture.Replacement.History[^1].After!.Content);
    }

    [Fact]
    public async Task HistoryIsBoundedAndRestoreRegeneratesEmbedding()
    {
        var restored = new LoreEntrySnapshot("Guild", "guild") { Content = "Original" };
        var history = Enumerable
            .Range(0, 30)
            .Select(i => new LoreRevision(i.ToString(), DateTime.UtcNow, "admin-dm", 1, "save", restored, restored))
            .ToArray();
        var fixture = new MongoLoreStoreFixture
        {
            ReadResult = new LoreDocument("Guild", "")
            {
                Deleted = true,
                Revision = "29",
                History = history,
            },
        };
        var state = await fixture.Store.GetEntryAsync("Guild", TestContext.Current.CancellationToken);
        var revision = Revision(state, restored) with { Action = "undo" };
        Assert.True(await fixture.Store.TrySaveAsync(state, revision, TestContext.Current.CancellationToken));
        Assert.Equal(20, fixture.Replacement!.History.Count);
        Assert.Equal("11", fixture.Replacement.History[0].Id);
        Assert.Equal(revision, fixture.Replacement.History[^1]);
        Assert.False(fixture.Replacement.Deleted);
        Assert.NotNull(fixture.Replacement.Embedding);
        Assert.Equal([0.1f, 0.2f], fixture.Replacement.Embedding);
    }

    [Fact]
    public async Task EmbeddingFailureNeverCommitsLoreOrHistory()
    {
        var fixture = new MongoLoreStoreFixture
        {
            ReadResult = new LoreDocument("Guild", "Original"),
            EmbeddingFailure = new InvalidOperationException("Embedding failed"),
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Store.UpdateLoreAsync(new GuildLore("Guild", "Updated"), TestContext.Current.CancellationToken)
        );
        Assert.Null(fixture.Replacement);
        Assert.Null(fixture.Inserted);
        Assert.Equal("Original", fixture.ReadResult.Content);
    }

    [Fact]
    public async Task ActiveListAndLookupExcludeDeletedDocumentsWhileEditorCanReadTheirHistory()
    {
        var fixture = new MongoLoreStoreFixture();
        var token = TestContext.Current.CancellationToken;
        var field = BsonClassMap.LookupClassMap(typeof(LoreDocument)).GetMemberMap(nameof(LoreDocument.Deleted)).ElementName;
        await fixture.Store.ReadLoreAsync(token);
        Assert.True(Render(fixture.ReadFilter!)[field]["$ne"].AsBoolean);
        await fixture.Store.ReadLoreAsync("Guild", token);
        Assert.True(Render(fixture.ReadFilter!)[field]["$ne"].AsBoolean);
        await fixture.Store.GetEntriesAsync(token);
        Assert.True(Render(fixture.ReadFilter!)[field]["$ne"].AsBoolean);
        await fixture.Store.GetEntryAsync("Guild", token);
        Assert.False(Render(fixture.ReadFilter!).Contains(field));
    }

    [Fact]
    public void RevisionsAndPartialMembersRoundTripThroughMongoSerialization()
    {
        var snapshot = new LoreEntrySnapshot("Grim", "member") { Aliases = ["Grimbles"], Biography = "Elevator joke" };
        var revision = new LoreRevision("version", new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc), "admin-dm", 1, "create", null, snapshot);
        var document = LoreDocument.FromSnapshot(snapshot) with { Revision = revision.Id, History = [revision] };
        var restored = BsonSerializer.Deserialize<LoreDocument>(document.ToBsonDocument());
        Assert.Equivalent(document, restored);
        var member = Assert.IsType<MemberLore>(restored.ToModel());
        Assert.Equal("Unknown", member.MainCharacter);
        Assert.Equal("Unknown", member.Nationality);
        Assert.Equal("Unknown", member.Pronouns);
        Assert.Equal(DateTimeKind.Utc, restored.History[0].CreatedAtUtc.Kind);

        // Use class-map field names because production registers camel-case conventions.
        var legacyBson = new LoreDocument("Guild", "Old lore").ToBsonDocument();
        foreach (var property in new[] { nameof(LoreDocument.Revision), nameof(LoreDocument.History), nameof(LoreDocument.Deleted) })
            legacyBson.Remove(BsonClassMap.LookupClassMap(typeof(LoreDocument)).GetMemberMap(property).ElementName);
        var legacy = BsonSerializer.Deserialize<LoreDocument>(legacyBson);
        Assert.Empty(legacy.History);
        Assert.Null(legacy.Revision);
        Assert.False(legacy.Deleted);
        Assert.Equal("Old lore", legacy.ToEditState().Entry!.Content);
    }

    [Fact]
    public async Task VectorSearchExcludesTombstonesAndProjectsOutAuditHistory()
    {
        var fixture = new MongoLoreStoreFixture();
        await fixture.Store.SearchLoreAsync([0.1f, 0.2f], 5, TestContext.Current.CancellationToken);
        var stages = fixture
            .SearchPipeline!.Render(
                new RenderArgs<LoreDocument>(BsonSerializer.SerializerRegistry.GetSerializer<LoreDocument>(), BsonSerializer.SerializerRegistry)
            )
            .Documents;
        var map = BsonClassMap.LookupClassMap(typeof(LoreDocument));
        var deleted = map.GetMemberMap(nameof(LoreDocument.Deleted)).ElementName;
        var history = map.GetMemberMap(nameof(LoreDocument.History)).ElementName;
        Assert.True(stages.Single(stage => stage.Contains("$match"))["$match"][deleted]["$ne"].AsBoolean);
        Assert.Equal(0, stages.Single(stage => stage.Contains("$project"))["$project"][history].AsInt32);
    }

    private static LoreRevision Revision(LoreEditState state, LoreEntrySnapshot? after) =>
        new(Guid.NewGuid().ToString("N"), DateTime.UtcNow, "admin-dm", 1, "update", state.Entry, after);

    private static BsonDocument Render(FilterDefinition<LoreDocument> filter) =>
        filter.Render(
            new RenderArgs<LoreDocument>(BsonSerializer.SerializerRegistry.GetSerializer<LoreDocument>(), BsonSerializer.SerializerRegistry)
        );

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
