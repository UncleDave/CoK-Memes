using ChampionsOfKhazad.Bot.Roster;
using ChampionsOfKhazad.Bot.Roster.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class MongoRosterStoreTests
{
    [Fact]
    public async Task SnapshotWritesUseRevisionCompareAndSwapAndIncrementRevision()
    {
        FilterDefinition<RosterState>? filter = null;
        RosterState? saved = null;
        var collection = Stub<IMongoCollection<RosterState>>(
            (method, args) =>
            {
                Assert.Equal("ReplaceOneAsync", method.Name);
                filter = (FilterDefinition<RosterState>)args![0]!;
                saved = (RosterState)args[1]!;
                Assert.False(((ReplaceOptions)args[2]!).IsUpsert);
                Assert.Equal(TestContext.Current.CancellationToken, args[^1]);
                return Task.FromResult<ReplaceOneResult>(new ReplaceOneResult.Acknowledged(1, 1, null));
            }
        );
        Assert.True(
            await new MongoRosterStore(collection).TrySaveAsync(new() { Id = "50:mock", Revision = 3 }, TestContext.Current.CancellationToken)
        );
        var rendered = Render(filter!);
        Assert.Equal("50:mock", rendered["_id"].AsString);
        Assert.Equal(3, rendered[Field<RosterState>(nameof(RosterState.Revision))].AsInt64);
        Assert.Equal(4, saved!.Revision);
    }

    [Fact]
    public async Task DepartureCleanupOnlyRemovesPendingNoticesOutsideTheExactActiveGuildScope()
    {
        FilterDefinition<RosterNotice>? filter = null;
        var collection = Stub<IMongoCollection<RosterNotice>>(
            (method, args) =>
            {
                Assert.Equal("DeleteManyAsync", method.Name);
                filter = (FilterDefinition<RosterNotice>)args![0]!;
                return Task.FromResult<DeleteResult>(new DeleteResult.Acknowledged(1));
            }
        );
        await new MongoRosterNoticeStore(collection).RemoveDepartedPendingAsync("50:mock", ["id:1"], TestContext.Current.CancellationToken);
        var rendered = Render(filter!);
        Assert.Equal("50:mock", rendered[Field<RosterNotice>(nameof(RosterNotice.ScopeId))].AsString);
        Assert.True(rendered[Field<RosterNotice>(nameof(RosterNotice.SentAtUtc))].IsBsonNull);
        Assert.Equal("id:1", rendered[Field<RosterNotice>(nameof(RosterNotice.CharacterKey))]["$nin"][0].AsString);
    }

    [Fact]
    public async Task SentStatusIsOnlyAppliedToThePendingNoticeAndIncludesAcknowledgedMessageId()
    {
        FilterDefinition<RosterNotice>? filter = null;
        UpdateDefinition<RosterNotice>? update = null;
        var collection = Stub<IMongoCollection<RosterNotice>>(
            (method, args) =>
            {
                Assert.Equal("UpdateOneAsync", method.Name);
                filter = (FilterDefinition<RosterNotice>)args![0]!;
                update = (UpdateDefinition<RosterNotice>)args[1]!;
                return Task.FromResult<UpdateResult>(new UpdateResult.Acknowledged(1, 1, null));
            }
        );
        await new MongoRosterNoticeStore(collection).MarkSentAsync("notice", DateTime.UtcNow, 123, TestContext.Current.CancellationToken);
        var rendered = Render(filter!);
        Assert.Equal("notice", rendered["_id"].AsString);
        Assert.True(rendered[Field<RosterNotice>(nameof(RosterNotice.SentAtUtc))].IsBsonNull);
        var changes = update!
            .Render(new RenderArgs<RosterNotice>(BsonSerializer.SerializerRegistry.GetSerializer<RosterNotice>(), BsonSerializer.SerializerRegistry))
            .AsBsonDocument["$set"];
        Assert.Equal(123, changes[Field<RosterNotice>(nameof(RosterNotice.DiscordMessageId))].AsInt64);
        Assert.Equal(BsonType.DateTime, changes[Field<RosterNotice>(nameof(RosterNotice.SentAtUtc))].BsonType);
    }

    private static BsonDocument Render<T>(FilterDefinition<T> filter) =>
        filter.Render(new RenderArgs<T>(BsonSerializer.SerializerRegistry.GetSerializer<T>(), BsonSerializer.SerializerRegistry));

    private static string Field<T>(string property) => BsonClassMap.LookupClassMap(typeof(T)).GetMemberMap(property).ElementName;
}
