using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.GenAi.Mongo;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Tests;

public class CustomerDisciplineMongoIntegrationTests
{
    public static bool HasLocalMongo => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COK_CUSTOMERS_MONGO_TEST_CONNECTION"));

    [Fact(Skip = "Set COK_CUSTOMERS_MONGO_TEST_CONNECTION to a disposable local Mongo instance.", SkipUnless = nameof(HasLocalMongo))]
    public async Task RealMongoCapsCompetingStrikesAndPreservesThemAcrossClientStoreAndServiceRecreation()
    {
        var connection = LocalConnection();
        var client = new MongoClient(connection);
        var databaseName = "customers_validation_" + Guid.NewGuid().ToString("N");
        var collection = client.GetDatabase(databaseName).GetCollection<CustomerDisciplineState>("customers");
        var cancellation = TestContext.Current.CancellationToken;
        try
        {
            var service = new CustomerDisciplineService(new MongoCustomerDisciplineStore(collection), TimeProvider.System);
            Assert.Empty((await service.GetAsync(1, 2, cancellation)).Strikes);
            Assert.Equal(0, await collection.CountDocumentsAsync(FilterDefinition<CustomerDisciplineState>.Empty, cancellationToken: cancellation));

            var results = await Task.WhenAll(
                Enumerable
                    .Range(1, 12)
                    .Select(index =>
                        new CustomerDisciplineService(new MongoCustomerDisciplineStore(collection), TimeProvider.System).IssueStrikeAsync(
                            1,
                            2,
                            (ulong)index,
                            $"Reason {index}",
                            cancellation
                        )
                    )
            );

            Assert.Equal(3, results.Count(result => result.Changed));
            Assert.All(results, result => Assert.Null(result.Error));
            Assert.All(results, result => Assert.NotNull(result.State));
            Assert.Equal(1, await collection.CountDocumentsAsync(FilterDefinition<CustomerDisciplineState>.Empty, cancellationToken: cancellation));
            var expected = await service.GetAsync(1, 2, cancellation);
            Assert.True(expected.IsBarred);
            Assert.Equal(3, expected.Revision);
            Assert.Equal(3, expected.Strikes.Length);
            Assert.Equal(3, expected.Strikes.Select(strike => strike.MessageId).Distinct().Count());
            Assert.All(expected.Strikes, strike => Assert.Equal($"Reason {strike.MessageId}", strike.Reason));
            foreach (var result in results.Where(result => result.Changed))
            {
                var strike = result.State!.Strikes[^1];
                var persisted = Assert.Single(expected.Strikes, item => item.MessageId == strike.MessageId);
                Assert.Equal(strike.Reason, persisted.Reason);
                Assert.Equal(new DateTimeOffset(strike.AtUtc).ToUnixTimeMilliseconds(), new DateTimeOffset(persisted.AtUtc).ToUnixTimeMilliseconds());
            }

            var restarted = RecreatedService(connection, databaseName);
            var durable = await restarted.GetAsync(1, 2, cancellation);
            AssertState(expected, durable);
            var capped = await restarted.IssueStrikeAsync(1, 2, 100, "Must not be recorded", cancellation);
            Assert.False(capped.Changed);
            Assert.Null(capped.Error);
            AssertState(expected, Assert.IsType<CustomerDisciplineState>(capped.State));
            Assert.True((await restarted.IssueStrikeAsync(1, 3, 100, "Other user", cancellation)).Changed);
            Assert.True((await restarted.IssueStrikeAsync(2, 2, 100, "Other guild", cancellation)).Changed);
            AssertState(expected, await RecreatedService(connection, databaseName).GetAsync(1, 2, cancellation));
            Assert.Equal(3, await collection.CountDocumentsAsync(FilterDefinition<CustomerDisciplineState>.Empty, cancellationToken: cancellation));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName, CancellationToken.None);
        }
    }

    [Fact(Skip = "Set COK_CUSTOMERS_MONGO_TEST_CONNECTION to a disposable local Mongo instance.", SkipUnless = nameof(HasLocalMongo))]
    public async Task RealMongoCompetingDuplicateMessagesRecordOneStrikeAndKeepTheOriginalReasonAfterRestart()
    {
        var connection = LocalConnection();
        var client = new MongoClient(connection);
        var databaseName = "customers_validation_" + Guid.NewGuid().ToString("N");
        var collection = client.GetDatabase(databaseName).GetCollection<CustomerDisciplineState>("customers");
        var cancellation = TestContext.Current.CancellationToken;
        try
        {
            var results = await Task.WhenAll(
                Enumerable
                    .Range(1, 8)
                    .Select(index =>
                        new CustomerDisciplineService(new MongoCustomerDisciplineStore(collection), TimeProvider.System).IssueStrikeAsync(
                            1,
                            2,
                            10,
                            $"Reason {index}",
                            cancellation
                        )
                    )
            );

            var winner = Assert.Single(results, result => result.Changed);
            Assert.All(results, result => Assert.Null(result.Error));
            Assert.All(results, result => Assert.Single(Assert.IsType<CustomerDisciplineState>(result.State).Strikes));
            var restarted = RecreatedService(connection, databaseName);
            var original = await restarted.GetAsync(1, 2, cancellation);
            Assert.Equal(1, original.Revision);
            Assert.Equal(10UL, Assert.Single(original.Strikes).MessageId);
            Assert.Equal(Assert.Single(winner.State!.Strikes).Reason, original.Strikes[0].Reason);
            var duplicate = await restarted.IssueStrikeAsync(1, 2, 10, "Replacement reason", cancellation);
            Assert.False(duplicate.Changed);
            Assert.Null(duplicate.Error);
            AssertState(original, Assert.IsType<CustomerDisciplineState>(duplicate.State));
            AssertState(original, await RecreatedService(connection, databaseName).GetAsync(1, 2, cancellation));
            Assert.Equal(1, await collection.CountDocumentsAsync(FilterDefinition<CustomerDisciplineState>.Empty, cancellationToken: cancellation));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName, CancellationToken.None);
        }
    }

    [Fact(Skip = "Set COK_CUSTOMERS_MONGO_TEST_CONNECTION to a disposable local Mongo instance.", SkipUnless = nameof(HasLocalMongo))]
    public async Task RealMongoPardonRetainsBarrierAcrossRestartAndRejectsStaleInFlightStrike()
    {
        var connection = LocalConnection();
        var client = new MongoClient(connection);
        var databaseName = "customers_validation_" + Guid.NewGuid().ToString("N");
        var collection = client.GetDatabase(databaseName).GetCollection<CustomerDisciplineState>("customers");
        var cancellation = TestContext.Current.CancellationToken;
        try
        {
            var store = new MongoCustomerDisciplineStore(collection);
            var service = new CustomerDisciplineService(store, TimeProvider.System);
            for (ulong messageId = 1; messageId <= 3; messageId++)
                Assert.True((await service.IssueStrikeAsync(1, 2, messageId, "Original reason", cancellation)).Changed);
            var beforePardon = await service.GetAsync(1, 2, cancellation);
            var pardon = await service.PardonAsync(1, 2, 100, cancellation);
            Assert.True(pardon.Changed);
            Assert.Null(pardon.Error);
            var pardonedState = Assert.IsType<CustomerDisciplineState>(pardon.State);
            Assert.Empty(pardonedState.Strikes);
            Assert.False(pardonedState.IsBarred);
            var staleWrite = beforePardon with
            {
                Strikes = [new CustomerStrike(99, "In-flight reason", new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc))],
            };

            Assert.False(await store.TrySaveAsync(staleWrite, cancellation));

            var restarted = RecreatedService(connection, databaseName);
            var cleared = await restarted.GetAsync(1, 2, cancellation);
            Assert.Empty(cleared.Strikes);
            Assert.False(cleared.IsBarred);
            Assert.Equal(100UL, cleared.LastPardonMessageId);
            Assert.Equal(4, cleared.Revision);
            Assert.Equal(1, await collection.CountDocumentsAsync(FilterDefinition<CustomerDisciplineState>.Empty, cancellationToken: cancellation));
            foreach (var messageId in new ulong[] { 1, 99, 100 })
            {
                var stale = await restarted.IssueStrikeAsync(1, 2, messageId, "Stale reason", cancellation);
                Assert.False(stale.Changed);
                Assert.Null(stale.Error);
                AssertState(cleared, Assert.IsType<CustomerDisciplineState>(stale.State));
            }
            var redundantPardon = await restarted.PardonAsync(1, 2, 100, cancellation);
            Assert.False(redundantPardon.Changed);
            AssertState(cleared, Assert.IsType<CustomerDisciplineState>(redundantPardon.State));
            Assert.True((await restarted.IssueStrikeAsync(1, 2, 101, "New reason", cancellation)).Changed);
            var fresh = await RecreatedService(connection, databaseName).GetAsync(1, 2, cancellation);
            Assert.Equal(101UL, Assert.Single(fresh.Strikes).MessageId);
            Assert.Equal(100UL, fresh.LastPardonMessageId);
            Assert.Equal(5, fresh.Revision);
            foreach (var messageId in new ulong[] { 50, 100 })
            {
                var stalePardon = await restarted.PardonAsync(1, 2, messageId, cancellation);
                Assert.False(stalePardon.Changed);
                Assert.Null(stalePardon.Error);
                AssertState(fresh, Assert.IsType<CustomerDisciplineState>(stalePardon.State));
            }
            var nextPardon = await restarted.PardonAsync(1, 2, 200, cancellation);
            Assert.True(nextPardon.Changed);
            var final = await RecreatedService(connection, databaseName).GetAsync(1, 2, cancellation);
            Assert.Empty(final.Strikes);
            Assert.Equal(200UL, final.LastPardonMessageId);
            Assert.Equal(6, final.Revision);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName, CancellationToken.None);
        }
    }

    private static string LocalConnection()
    {
        var connection = Environment.GetEnvironmentVariable("COK_CUSTOMERS_MONGO_TEST_CONNECTION")!;
        var servers = MongoUrl.Create(connection).Servers.ToArray();
        Assert.NotEmpty(servers);
        Assert.All(servers, server => Assert.Contains(server.Host, new[] { "localhost", "127.0.0.1", "::1" }));
        return connection;
    }

    [Fact(Skip = "Set COK_CUSTOMERS_MONGO_TEST_CONNECTION to a disposable local Mongo instance.", SkipUnless = nameof(HasLocalMongo))]
    public async Task RealMongoEmptyPardonPersistsBarrierAndStaleAppealsCannotClearLaterStrikes()
    {
        var connection = LocalConnection();
        var client = new MongoClient(connection);
        var databaseName = "customers_validation_" + Guid.NewGuid().ToString("N");
        var cancellation = TestContext.Current.CancellationToken;
        try
        {
            var service = RecreatedService(connection, databaseName);
            var pardon = await service.PardonAsync(1, 2, 100, cancellation);
            Assert.False(pardon.Changed);
            Assert.Null(pardon.Error);
            var restarted = RecreatedService(connection, databaseName);
            Assert.Equal(100UL, (await restarted.GetAsync(1, 2, cancellation)).LastPardonMessageId);
            Assert.False((await restarted.IssueStrikeAsync(1, 2, 99, "Stale cheek", cancellation)).Changed);
            Assert.True((await restarted.IssueStrikeAsync(1, 2, 150, "New cheek", cancellation)).Changed);
            Assert.False((await restarted.PardonAsync(1, 2, 125, cancellation)).Changed);
            var state = await RecreatedService(connection, databaseName).GetAsync(1, 2, cancellation);
            Assert.Equal(150UL, Assert.Single(state.Strikes).MessageId);
            Assert.Equal(100UL, state.LastPardonMessageId);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName, CancellationToken.None);
        }
    }

    private static CustomerDisciplineService RecreatedService(string connection, string databaseName) =>
        new(
            new MongoCustomerDisciplineStore(
                new MongoClient(connection).GetDatabase(databaseName).GetCollection<CustomerDisciplineState>("customers")
            ),
            TimeProvider.System
        );

    private static void AssertState(CustomerDisciplineState expected, CustomerDisciplineState actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Revision, actual.Revision);
        Assert.Equal(expected.LastPardonMessageId, actual.LastPardonMessageId);
        Assert.Equal(expected.Strikes, actual.Strikes);
        Assert.Equal(expected.IsBarred, actual.IsBarred);
    }
}
