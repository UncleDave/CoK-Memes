using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.GenAi.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class MongoGazetteIssueStoreTests
{
    [Fact]
    public void StateRoundTripsWithIssueJournalAndBudgetButNoChatOrArtwork()
    {
        var now = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        var state = new GazetteState
        {
            Revision = 3,
            LastReservedIssue = 2,
            Publications = [new(2, "approval", 8, now, 99)],
            IllustrationAttempts = [now],
        };
        var bson = state.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<GazetteState>(bson);
        Assert.Equal("khazad-gazette", bson["_id"].AsString);
        Assert.Equal(state.LastReservedIssue, restored.LastReservedIssue);
        Assert.Equal(state.Publications, restored.Publications);
        Assert.Equal(state.IllustrationAttempts, restored.IllustrationAttempts);
        Assert.DoesNotContain("Content", bson.ToJson());
        Assert.DoesNotContain("Png", bson.ToJson());
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(4, false)]
    public async Task WritesCompareIdentityAndRevisionAndOnlyUpsertTheInitialState(long revision, bool upsert)
    {
        FilterDefinition<GazetteState>? captured = null;
        GazetteState? saved = null;
        ReplaceOptions? options = null;
        var collection = Stub<IMongoCollection<GazetteState>>(
            (method, args) =>
            {
                Assert.Equal("ReplaceOneAsync", method.Name);
                captured = (FilterDefinition<GazetteState>)args![0]!;
                saved = (GazetteState)args[1]!;
                options = (ReplaceOptions)args[2]!;
                Assert.Equal(TestContext.Current.CancellationToken, args[3]);
                return Task.FromResult<ReplaceOneResult>(new ReplaceOneResult.Acknowledged(1, 1, null));
            }
        );
        Assert.True(
            await new MongoGazetteIssueStore(collection).TrySaveAsync(
                new() { Revision = revision, LastReservedIssue = 9 },
                TestContext.Current.CancellationToken
            )
        );
        var filter = captured!.Render(
            new RenderArgs<GazetteState>(BsonSerializer.SerializerRegistry.GetSerializer<GazetteState>(), BsonSerializer.SerializerRegistry)
        );
        Assert.Equal("khazad-gazette", filter["_id"].AsString);
        var revisionName = BsonClassMap.LookupClassMap(typeof(GazetteState)).GetMemberMap(nameof(GazetteState.Revision)).ElementName;
        Assert.Equal(revision, filter[revisionName].ToInt64());
        Assert.Equal(revision + 1, saved!.Revision);
        Assert.Equal(upsert, options!.IsUpsert);
    }
}
