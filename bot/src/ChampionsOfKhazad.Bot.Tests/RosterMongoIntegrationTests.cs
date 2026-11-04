using ChampionsOfKhazad.Bot.Roster;
using ChampionsOfKhazad.Bot.Roster.Mongo;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Tests;

public class RosterMongoIntegrationTests
{
    public static bool HasLocalMongo => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COK_ROSTER_MONGO_TEST_CONNECTION"));

    [Fact(Skip = "Set COK_ROSTER_MONGO_TEST_CONNECTION to a disposable local Mongo instance.", SkipUnless = nameof(HasLocalMongo))]
    public async Task RealMongoPersistsRosterAndUniquePendingSentNoticesAcrossStoreRecreation()
    {
        var connection = Environment.GetEnvironmentVariable("COK_ROSTER_MONGO_TEST_CONNECTION")!;
        var servers = MongoUrl.Create(connection).Servers.ToArray();
        Assert.NotEmpty(servers);
        Assert.All(servers, server => Assert.Contains(server.Host, new[] { "localhost", "127.0.0.1", "::1" }));
        var client = new MongoClient(connection);
        var databaseName = "roster_validation_" + Guid.NewGuid().ToString("N");
        var token = TestContext.Current.CancellationToken;
        try
        {
            var database = client.GetDatabase(databaseName);
            var snapshots = database.GetCollection<RosterState>("rosters");
            var collection = database.GetCollection<RosterNotice>("notices");
            var store = new MongoRosterStore(snapshots);
            var notices = new MongoRosterNoticeStore(collection);
            using var fixture = new RosterFixture();
            using var service = new GuildRosterService(fixture.Options, fixture.Source, store, notices, fixture.Clock);
            await service.RefreshAsync(token, force: true);
            var initial = (await store.GetAsync(service.ScopeId, token))!;
            Assert.Equal(19, Assert.Single(initial.Characters).Level);
            fixture.Advance();
            fixture.SetMembers(RosterFixture.Character(level: 20));
            await service.RefreshAsync(token, force: true);
            var notice = Assert.Single(await notices.GetPendingAsync(service.ScopeId, token));
            await notices.AddPendingAsync(notice, token);
            Assert.Single(await notices.GetPendingAsync(service.ScopeId, token));
            Assert.False(await store.TrySaveAsync(initial, token));

            var restartedDatabase = new MongoClient(connection).GetDatabase(databaseName);
            var restarted = new MongoRosterStore(restartedDatabase.GetCollection<RosterState>("rosters"));
            var restartedNotices = new MongoRosterNoticeStore(restartedDatabase.GetCollection<RosterNotice>("notices"));
            Assert.Equal(20, Assert.Single((await restarted.GetAsync(service.ScopeId, token))!.Characters).Level);
            Assert.Single(await restartedNotices.GetPendingAsync(service.ScopeId, token));
            await restartedNotices.MarkSentAsync(notice.Id, DateTime.UtcNow, 123, token);
            Assert.Empty(await restartedNotices.GetPendingAsync(service.ScopeId, token));
            var sent = await collection.Find(item => item.Id == notice.Id).SingleAsync(token);
            Assert.NotNull(sent.SentAtUtc);
            Assert.Equal(123UL, sent.DiscordMessageId);

            await notices.AddPendingAsync(notice with { Id = "pending-departed", CharacterKey = "id:departed" }, token);
            await notices.AddPendingAsync(notice with { Id = "other-guild", ScopeId = "other-scope" }, token);
            await notices.RemoveDepartedPendingAsync(service.ScopeId, [], token);
            Assert.Equal(2, await collection.CountDocumentsAsync(FilterDefinition<RosterNotice>.Empty, cancellationToken: token));
            Assert.Single(await notices.GetPendingAsync("other-scope", token));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName, CancellationToken.None);
        }
    }
}
