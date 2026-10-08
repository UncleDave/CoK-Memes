using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.GenAi.Mongo;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Tests;

public class GazetteMongoIntegrationTests
{
    public static bool HasLocalMongo => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COK_GAZETTE_MONGO_TEST_CONNECTION"));

    [Fact(Skip = "Set COK_GAZETTE_MONGO_TEST_CONNECTION to a disposable local Mongo instance.", SkipUnless = nameof(HasLocalMongo))]
    public async Task RealMongoSerializesCompetingIssueClaimsAndPreservesAcknowledgementsAndImageBudget()
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
            var budget = await Task.WhenAll(
                Enumerable
                    .Range(0, 5)
                    .Select(_ =>
                        new GazetteIssueService(new MongoGazetteIssueStore(collection), TimeProvider.System).TryReserveIllustrationAsync(
                            2,
                            cancellation
                        )
                    )
            );
            Assert.Equal(2, budget.Count(success => success));
            var restarted = new MongoGazetteIssueStore(collection);
            Assert.Equal(2, await new GazetteIssueService(restarted, TimeProvider.System).GetNextAsync(cancellation));
            var persisted = await restarted.GetAsync(cancellation);
            Assert.Equal(99UL, Assert.Single(persisted.Publications).MessageId);
            Assert.Equal(2, persisted.IllustrationAttempts.Count);
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
