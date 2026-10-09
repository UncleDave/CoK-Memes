using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.GenAi.Mongo;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class MongoGazettePublishedEditionStoreTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task RecentQueryUsesScopedInclusiveApprovalBoundsStableNewestSortAndDatabaseLimit(int maximumEditions)
    {
        var since = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var until = since.AddDays(7);
        var edition = new GazettePublishedEdition("012345abcdef", 1, 8, "Unconfirmed approved text", until);
        var moves = 0;
        var queried = false;
        var cursor = Stub<IAsyncCursor<GazettePublishedEdition>>(
            (method, args) =>
                method.Name switch
                {
                    "get_Current" => new[] { edition },
                    "MoveNextAsync" => MoveNext(args!),
                    "Dispose" => null,
                    _ => throw new NotSupportedException(method.Name),
                }
        );
        Task<bool> MoveNext(object?[] args)
        {
            Assert.Equal(TestContext.Current.CancellationToken, args[0]);
            return Task.FromResult(++moves == 1);
        }
        var collection = Stub<IMongoCollection<GazettePublishedEdition>>(
            (method, args) =>
            {
                Assert.Equal("FindAsync", method.Name);
                queried = true;
                var render = new RenderArgs<GazettePublishedEdition>(
                    BsonSerializer.SerializerRegistry.GetSerializer<GazettePublishedEdition>(),
                    BsonSerializer.SerializerRegistry
                );
                var map = BsonClassMap.LookupClassMap(typeof(GazettePublishedEdition));
                string Member(string name) => map.GetMemberMap(name).ElementName;
                var filter = ((FilterDefinition<GazettePublishedEdition>)args![0]!).Render(render);
                Assert.Equal(
                    new BsonDocument
                    {
                        { Member(nameof(GazettePublishedEdition.GuildId)), new BsonInt64(1) },
                        { Member(nameof(GazettePublishedEdition.ChannelId)), new BsonInt64(8) },
                        {
                            Member(nameof(GazettePublishedEdition.ApprovedAtUtc)),
                            new BsonDocument { { "$gte", new BsonDateTime(since) }, { "$lte", new BsonDateTime(until) } }
                        },
                    },
                    filter
                );
                var options = Assert.IsAssignableFrom<FindOptions<GazettePublishedEdition, GazettePublishedEdition>>(args[1]);
                Assert.Equal(maximumEditions, options.Limit);
                Assert.Equal(
                    new BsonDocument { { Member(nameof(GazettePublishedEdition.ApprovedAtUtc)), -1 }, { "_id", -1 } },
                    options.Sort.Render(render)
                );
                Assert.Equal(TestContext.Current.CancellationToken, args[^1]);
                return Task.FromResult(cursor);
            }
        );

        var result = await new MongoGazettePublishedEditionStore(collection).GetRecentAsync(
            1,
            8,
            since,
            until,
            maximumEditions,
            TestContext.Current.CancellationToken
        );

        Assert.True(queried);
        Assert.Same(edition, Assert.Single(result));
        Assert.Null(result[0].MessageId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(6)]
    public async Task RecentQueryRejectsInvalidLimitsBeforeAccessingMongo(int maximumEditions)
    {
        var collection = Stub<IMongoCollection<GazettePublishedEdition>>((method, _) => throw new InvalidOperationException(method.Name));
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new MongoGazettePublishedEditionStore(collection).GetRecentAsync(
                1,
                8,
                DateTime.UtcNow.AddDays(-7),
                DateTime.UtcNow,
                maximumEditions,
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal("maximumEditions", exception.ParamName);
    }

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
