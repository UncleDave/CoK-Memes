using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.GenAi.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class MongoGazettePublishedEditionStoreTests
{
    [Fact]
    public void SnapshotRoundTripsWithoutRawEvidenceOrPageBytes()
    {
        var edition = new GazettePublishedEdition(
            "012345abcdef",
            1,
            8,
            "Approved text and links",
            new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc)
        )
        {
            MessageId = 99,
        };
        var bson = edition.ToBsonDocument();
        Assert.Equal(edition, BsonSerializer.Deserialize<GazettePublishedEdition>(bson));
        Assert.Equal(edition.Id, bson["_id"].AsString);
        Assert.DoesNotContain("Png", bson.ToJson());
        Assert.DoesNotContain("ContentHash", bson.ToJson());
    }

    [Fact]
    public async Task SnapshotWritesInsertApprovedTextWithoutUpsertingOrReplacingOtherIssues()
    {
        var edition = new GazettePublishedEdition("012345abcdef", 1, 8, "Approved", DateTime.UtcNow);
        var collection = Stub<IMongoCollection<GazettePublishedEdition>>(
            (method, args) =>
            {
                Assert.Equal("InsertOneAsync", method.Name);
                Assert.Same(edition, args![0]);
                Assert.Equal(TestContext.Current.CancellationToken, args[^1]);
                return Task.CompletedTask;
            }
        );
        await new MongoGazettePublishedEditionStore(collection).SaveAsync(edition, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ConfirmationOnlyBindsUnconfirmedOrAlreadyMatchingSnapshotToItsMessage()
    {
        var collection = Stub<IMongoCollection<GazettePublishedEdition>>(
            (method, args) =>
            {
                Assert.Equal("UpdateOneAsync", method.Name);
                var render = new RenderArgs<GazettePublishedEdition>(
                    BsonSerializer.SerializerRegistry.GetSerializer<GazettePublishedEdition>(),
                    BsonSerializer.SerializerRegistry
                );
                var filter = ((FilterDefinition<GazettePublishedEdition>)args![0]!).Render(render);
                var conditions = filter.Contains("$and") ? filter["$and"].AsBsonArray.Select(value => value.AsBsonDocument).ToArray() : [filter];
                Assert.Equal("012345abcdef", conditions.Single(condition => condition.Contains("_id"))["_id"].AsString);
                Assert.Equal(2, conditions.Single(condition => condition.Contains("$or"))["$or"].AsBsonArray.Count);
                var update = ((UpdateDefinition<GazettePublishedEdition>)args[1]!).Render(render);
                var member = BsonClassMap
                    .LookupClassMap(typeof(GazettePublishedEdition))
                    .GetMemberMap(nameof(GazettePublishedEdition.MessageId))
                    .ElementName;
                Assert.Equal(99UL, (ulong)update["$set"][member].AsInt64);
                Assert.Equal(TestContext.Current.CancellationToken, args[^1]);
                return Task.FromResult<UpdateResult>(new UpdateResult.Acknowledged(1, 1, null));
            }
        );
        await new MongoGazettePublishedEditionStore(collection).ConfirmMessageAsync("012345abcdef", 99, TestContext.Current.CancellationToken);
    }
}
