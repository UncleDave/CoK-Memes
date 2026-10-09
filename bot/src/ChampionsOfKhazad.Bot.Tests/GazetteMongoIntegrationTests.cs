using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.GenAi.Mongo;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Tests;

public class GazetteMongoIntegrationTests
{
    public static bool HasLocalMongo => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COK_GAZETTE_MONGO_TEST_CONNECTION"));

    [Fact(Skip = "Set COK_GAZETTE_MONGO_TEST_CONNECTION to a disposable local Mongo instance.", SkipUnless = nameof(HasLocalMongo))]
    public async Task RealMongoRecentEditionsRespectScopeInclusiveBoundsOrderingAndLimitAcrossStoreRecreation()
    {
        var connection = Environment.GetEnvironmentVariable("COK_GAZETTE_MONGO_TEST_CONNECTION")!;
        Assert.All(MongoUrl.Create(connection).Servers, server => Assert.Contains(server.Host, new[] { "localhost", "127.0.0.1", "::1" }));
        var client = new MongoClient(connection);
        var databaseName = "gazette_validation_" + Guid.NewGuid().ToString("N");
        var collection = client.GetDatabase(databaseName).GetCollection<GazettePublishedEdition>("publishedEditions");
        var cancellation = TestContext.Current.CancellationToken;
        var since = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var until = since.AddDays(7);
        try
        {
            GazettePublishedEdition Edition(string id, DateTime approvedAt) => new(id, 1, 8, "Approved text for " + id, approvedAt);
            GazettePublishedEdition[] editions =
            [
                Edition("lower", since),
                Edition("older", since.AddDays(1)),
                Edition("middle", since.AddDays(2)),
                Edition("tie-a", since.AddDays(3)),
                Edition("tie-z", since.AddDays(3)),
                Edition("upper", until),
                Edition("before", since.AddMilliseconds(-1)),
                Edition("after", until.AddMilliseconds(1)),
                Edition("other-guild", until) with
                {
                    GuildId = 2,
                },
                Edition("other-channel", until) with
                {
                    ChannelId = 9,
                },
            ];
            var store = new MongoGazettePublishedEditionStore(collection);
            foreach (var edition in editions)
                await store.SaveAsync(edition, cancellation);
            await store.ConfirmMessageAsync("upper", 99, cancellation);
            var restarted = new MongoGazettePublishedEditionStore(
                new MongoClient(connection).GetDatabase(databaseName).GetCollection<GazettePublishedEdition>("publishedEditions")
            );

            var recent = await restarted.GetRecentAsync(1, 8, since, until, 5, cancellation);
            Assert.Equal(new[] { "upper", "tie-z", "tie-a", "middle", "older" }, recent.Select(edition => edition.Id));
            Assert.Equal(99UL, recent[0].MessageId);
            Assert.All(recent.Skip(1), edition => Assert.Null(edition.MessageId));
            Assert.All(recent, edition => Assert.Equal("Approved text for " + edition.Id, edition.Text));
            var lowerWindow = await restarted.GetRecentAsync(1, 8, since, since.AddDays(1), 5, cancellation);
            Assert.Equal(new[] { "older", "lower" }, lowerWindow.Select(edition => edition.Id));
            Assert.Equal("upper", Assert.Single(await restarted.GetRecentAsync(1, 8, since, until, 1, cancellation)).Id);
            Assert.Empty(await restarted.GetRecentAsync(3, 8, since, until, 5, cancellation));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName, CancellationToken.None);
        }
    }

    [Fact(Skip = "Set COK_GAZETTE_MONGO_TEST_CONNECTION to a disposable local Mongo instance.", SkipUnless = nameof(HasLocalMongo))]
    public async Task RealMongoSerializesCompetingIssueClaimsAndPreservesAcknowledgementsAndPublishedText()
    {
        var connection = Environment.GetEnvironmentVariable("COK_GAZETTE_MONGO_TEST_CONNECTION")!;
        Assert.All(MongoUrl.Create(connection).Servers, server => Assert.Contains(server.Host, new[] { "localhost", "127.0.0.1", "::1" }));
        var client = new MongoClient(connection);
        var databaseName = "gazette_validation_" + Guid.NewGuid().ToString("N");
        var collection = client.GetDatabase(databaseName).GetCollection<GazetteState>("gazette");
        var cancellation = TestContext.Current.CancellationToken;
        try
        {
            var store = new MongoGazetteIssueStore(collection);
            var service = new GazetteIssueService(store, TimeProvider.System);
            Assert.Equal(1, await service.GetNextAsync(cancellation));
            Assert.Equal(0, await collection.CountDocumentsAsync(FilterDefinition<GazetteState>.Empty, cancellationToken: cancellation));
            var claims = await Task.WhenAll(
                Enumerable
                    .Range(0, 10)
                    .Select(index =>
                        new GazetteIssueService(new MongoGazetteIssueStore(collection), TimeProvider.System).TryReserveAsync(
                            1,
                            "claim-" + index,
                            8,
                            cancellation
                        )
                    )
            );
            Assert.Equal(1, claims.Count(success => success));
            Assert.Equal(1, await collection.CountDocumentsAsync(FilterDefinition<GazetteState>.Empty, cancellationToken: cancellation));
            var reservation = Assert.Single((await store.GetAsync(cancellation)).Publications);
            await service.MarkPublishedAsync(1, reservation.Token, 99, cancellation);
            var restarted = new MongoGazetteIssueStore(collection);
            Assert.Equal(2, await new GazetteIssueService(restarted, TimeProvider.System).GetNextAsync(cancellation));
            var persisted = await restarted.GetAsync(cancellation);
            Assert.Equal(99UL, Assert.Single(persisted.Publications).MessageId);
            Assert.Empty(persisted.IllustrationAttempts);
            var snapshots = client.GetDatabase(databaseName).GetCollection<GazettePublishedEdition>("publishedEditions");
            var approved = new GazettePublishedEdition(
                "012345abcdef",
                1,
                8,
                "Exact approved text with source links",
                new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc)
            );
            var archive = new MongoGazettePublishedEditionStore(snapshots);
            await archive.SaveAsync(approved, cancellation);
            Assert.Equal(approved, await new MongoGazettePublishedEditionStore(snapshots).GetAsync(approved.Id, cancellation));
            await archive.ConfirmMessageAsync(approved.Id, 99, cancellation);
            Assert.Equal(99UL, (await archive.GetAsync(approved.Id, cancellation))!.MessageId);
            await Assert.ThrowsAsync<InvalidOperationException>(() => archive.ConfirmMessageAsync(approved.Id, 100, cancellation));
            await Assert.ThrowsAsync<MongoWriteException>(() =>
                archive.SaveAsync(approved with { Text = "Must not overwrite approved text" }, cancellation)
            );
            Assert.Equal(approved.Text, (await archive.GetAsync(approved.Id, cancellation))!.Text);
        }
        finally
        {
            // Only the uniquely named database created by this test is removed, never existing guild data.
            await client.DropDatabaseAsync(databaseName, CancellationToken.None);
        }
    }
}
