using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using ChampionsOfKhazad.Bot.Lore.Mongo;
using ChampionsOfKhazad.Bot.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Tests;

public class MemberLoreMongoIntegrationTests
{
    private const ulong DiscordUserId = 1234567890123456789;
    public static bool HasLocalMongo => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COK_MEMBER_LORE_MONGO_TEST_CONNECTION"));

    [Fact(Skip = "Set COK_MEMBER_LORE_MONGO_TEST_CONNECTION to a disposable local Mongo instance.", SkipUnless = nameof(HasLocalMongo))]
    public async Task RealMongoLookupRequiresExactBindingOnAnActiveMemberAndRejectsLegacyDuplicates()
    {
        var client = CreateLocalClient();
        var databaseName = "member_lore_validation_" + Guid.NewGuid().ToString("N");
        var database = client.GetDatabase(databaseName);
        var collection = database.GetCollection<LoreDocument>("lore");
        var raw = database.GetCollection<BsonDocument>("lore");
        var token = TestContext.Current.CancellationToken;
        try
        {
            await collection.InsertManyAsync(
                [
                    new LoreDocument(Member(DiscordUserId.ToString(), null)) { Aliases = ["Bound"] },
                    new LoreDocument(Member("Deleted", DiscordUserId)) { Deleted = true },
                    new LoreDocument("Guild", "Not a member") { DiscordUserId = DiscordUserId },
                    new LoreDocument(Member("Different account", DiscordUserId + 1)),
                ],
                cancellationToken: token
            );
            var store = new MongoLoreStore(collection, MemberLoreStoreFixture.CreateEmbeddings());
            Assert.Null(await store.ReadMemberLoreAsync(DiscordUserId, token));

            var legacy = new LoreDocument(Member("Bound", DiscordUserId)).ToBsonDocument();
            legacy.Remove(Field(nameof(LoreDocument.Deleted)));
            await raw.InsertOneAsync(legacy, cancellationToken: token);
            Assert.Equivalent(Member("Bound", DiscordUserId), await store.ReadMemberLoreAsync(DiscordUserId, token));

            await collection.InsertOneAsync(new LoreDocument(Member("Conflicting legacy binding", DiscordUserId)), cancellationToken: token);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadMemberLoreAsync(DiscordUserId, token));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName, CancellationToken.None);
        }
    }

    [Fact(Skip = "Set COK_MEMBER_LORE_MONGO_TEST_CONNECTION to a disposable local Mongo instance.", SkipUnless = nameof(HasLocalMongo))]
    public async Task RealMongoUniqueIndexSerializesConcurrentBindingsRejectsReplacementAndReleasesDeletedBindings()
    {
        var client = CreateLocalClient();
        var databaseName = "member_lore_validation_" + Guid.NewGuid().ToString("N");
        var database = client.GetDatabase(databaseName);
        var collection = database.GetCollection<LoreDocument>("lore");
        var raw = database.GetCollection<BsonDocument>("lore");
        var token = TestContext.Current.CancellationToken;
        try
        {
            foreach (var name in new[] { "Legacy one", "Legacy two" })
            {
                var legacy = new LoreDocument(Member(name, null)).ToBsonDocument();
                legacy.Remove(Field(nameof(LoreDocument.DiscordUserId)));
                legacy.Remove(Field(nameof(LoreDocument.Deleted)));
                await raw.InsertOneAsync(legacy, cancellationToken: token);
            }
            collection.CreateUniqueIndex(Collections.Lore.UniqueIndex.Field, Collections.Lore.UniqueIndex.Collation);
            await collection.Indexes.CreateOneAsync(Collections.MemberDiscordUserIdIndex, cancellationToken: token);
            var legacyBound = new LoreDocument(Member("Legacy bound", DiscordUserId + 1)).ToBsonDocument();
            legacyBound.Remove(Field(nameof(LoreDocument.Deleted)));
            await raw.InsertOneAsync(legacyBound, cancellationToken: token);
            var store = new MongoLoreStore(collection, MemberLoreStoreFixture.CreateEmbeddings());
            Assert.False(await store.CreateLoreAsync(Member("Duplicate legacy account", DiscordUserId + 1), token));
            Assert.Equal("Legacy bound", (await store.ReadMemberLoreAsync(DiscordUserId + 1, token))!.Name);
            Assert.True(await store.CreateLoreAsync(Member("Unbound", null), token));
            var names = Enumerable.Range(0, 8).Select(index => "Claim " + index).ToArray();
            var claims = await Task.WhenAll(names.Select(name => store.CreateLoreAsync(Member(name, DiscordUserId), token)));
            Assert.Equal(1, claims.Count(success => success));
            var winner = Assert.IsType<MemberLore>(await store.ReadMemberLoreAsync(DiscordUserId, token));
            Assert.Equal(names[Array.FindIndex(claims, success => success)], winner.Name);

            var unbound = (await store.GetEntryAsync("Unbound", token)).Entry!;
            Assert.False(await store.UpdateLoreAsync(Member("Unbound", DiscordUserId) with { Biography = "Must not be saved" }, token));
            var rejected = await store.GetEntryAsync("Unbound", token);
            Assert.Equivalent(unbound, rejected.Entry);
            Assert.Single(rejected.History);
            Assert.Equivalent(winner, await store.ReadMemberLoreAsync(DiscordUserId, token));

            await store.DeleteLoreAsync(winner.Name);
            Assert.Null(await store.ReadMemberLoreAsync(DiscordUserId, token));
            Assert.True(await store.UpdateLoreAsync(Member("Unbound", DiscordUserId), token));
            Assert.Equivalent(Member("Unbound", DiscordUserId), await store.ReadMemberLoreAsync(DiscordUserId, token));

            var deleted = await store.GetEntryAsync(winner.Name, token);
            var original = deleted.History[^1].Before!;
            var restore = new LoreRevision(Guid.NewGuid().ToString("N"), DateTime.UtcNow, "admin-dm", 1, "undo", null, original);
            Assert.False(await store.TrySaveAsync(deleted, restore, token));
            var stillDeleted = await store.GetEntryAsync(winner.Name, token);
            Assert.Null(stillDeleted.Entry);
            Assert.Equal(deleted.Revision, stillDeleted.Revision);
            Assert.Equivalent(deleted.History, stillDeleted.History);
            var restarted = new MongoLoreStore(
                new MongoClient(Environment.GetEnvironmentVariable("COK_MEMBER_LORE_MONGO_TEST_CONNECTION")!)
                    .GetDatabase(databaseName)
                    .GetCollection<LoreDocument>("lore"),
                MemberLoreStoreFixture.CreateEmbeddings()
            );
            Assert.Equivalent(Member("Unbound", DiscordUserId), await restarted.ReadMemberLoreAsync(DiscordUserId, token));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName, CancellationToken.None);
        }
    }

    private static MongoClient CreateLocalClient()
    {
        var connection = Environment.GetEnvironmentVariable("COK_MEMBER_LORE_MONGO_TEST_CONNECTION")!;
        Assert.All(MongoUrl.Create(connection).Servers, server => Assert.Contains(server.Host, new[] { "localhost", "127.0.0.1", "::1" }));
        return new MongoClient(connection);
    }

    private static MemberLore Member(string name, ulong? discordUserId) =>
        new(name, "they", "UK", "Shaman", "Elevator joke")
        {
            DiscordUserId = discordUserId,
            Aliases = ["Grimbles"],
            Roles = ["Raider"],
        };

    private static string Field(string property) => BsonClassMap.LookupClassMap(typeof(LoreDocument)).GetMemberMap(property).ElementName;
}
