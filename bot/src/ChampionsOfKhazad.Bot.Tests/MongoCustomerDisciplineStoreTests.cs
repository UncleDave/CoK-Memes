using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.GenAi.Mongo;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class MongoCustomerDisciplineStoreTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 2)]
    public async Task ListingUsesAnExactGuildPrefixActiveStrikeFilterAndBoundedStablePaging(bool barredOnly, int strikeIndex)
    {
        FilterDefinition<CustomerDisciplineState>? captured = null;
        FindOptions<CustomerDisciplineState, CustomerDisciplineState>? options = null;
        var collection = Stub<IMongoCollection<CustomerDisciplineState>>(
            (method, args) =>
            {
                if (method.Name == "get_DocumentSerializer")
                    return BsonSerializer.SerializerRegistry.GetSerializer<CustomerDisciplineState>();
                if (method.Name == "get_Settings")
                    return new MongoCollectionSettings();
                Assert.Equal("FindAsync", method.Name);
                captured = (FilterDefinition<CustomerDisciplineState>)args![0]!;
                options = (FindOptions<CustomerDisciplineState, CustomerDisciplineState>)args[1]!;
                Assert.Equal(TestContext.Current.CancellationToken, args[^1]);
                return Task.FromResult<IAsyncCursor<CustomerDisciplineState>>(new EmptyCursor());
            }
        );

        var result = await new MongoCustomerDisciplineStore(collection).ListAsync(12, barredOnly, 25, 26, TestContext.Current.CancellationToken);

        var render = new RenderArgs<CustomerDisciplineState>(
            BsonSerializer.SerializerRegistry.GetSerializer<CustomerDisciplineState>(),
            BsonSerializer.SerializerRegistry
        );
        var filter = captured!.Render(render);
        Assert.Equal("^12:", filter["_id"].AsBsonRegularExpression.Pattern);
        var field = BsonClassMap.LookupClassMap(typeof(CustomerDisciplineState)).GetMemberMap(nameof(CustomerDisciplineState.Strikes)).ElementName;
        Assert.True(filter[$"{field}.{strikeIndex}"]["$exists"].AsBoolean);
        Assert.Equal(1, options!.Sort.Render(render)["_id"].AsInt32);
        Assert.Equal(25, options.Skip);
        Assert.Equal(26, options.Limit);
        Assert.Empty(result);
    }

    private sealed class EmptyCursor : IAsyncCursor<CustomerDisciplineState>
    {
        public IEnumerable<CustomerDisciplineState> Current => [];

        public bool MoveNext(CancellationToken cancellationToken = default) => false;

        public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public void Dispose() { }
    }
}
