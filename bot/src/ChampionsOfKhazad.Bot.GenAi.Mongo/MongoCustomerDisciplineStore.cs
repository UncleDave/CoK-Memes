using System.Globalization;
using ChampionsOfKhazad.Bot.GenAi;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.GenAi.Mongo;

internal class MongoCustomerDisciplineStore(IMongoCollection<CustomerDisciplineState> collection) : ICustomerDisciplineStore
{
    public Task<CustomerDisciplineState?> GetAsync(string id, CancellationToken cancellationToken) =>
        collection.Find(state => state.Id == id).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<CustomerDisciplineState>> ListAsync(
        ulong guildId,
        bool barredOnly,
        int offset,
        int limit,
        CancellationToken cancellationToken
    )
    {
        var prefix = guildId.ToString(CultureInfo.InvariantCulture) + ":";
        var filter = Builders<CustomerDisciplineState>.Filter;
        return await collection
            .Find(
                filter.Regex(state => state.Id, new BsonRegularExpression("^" + prefix)) & filter.SizeGte(state => state.Strikes, barredOnly ? 3 : 1)
            )
            .SortBy(state => state.Id)
            .Skip(offset)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TrySaveAsync(CustomerDisciplineState state, CancellationToken cancellationToken)
    {
        try
        {
            var result = await collection.ReplaceOneAsync(
                current => current.Id == state.Id && current.Revision == state.Revision,
                state with
                {
                    Revision = state.Revision + 1,
                },
                new ReplaceOptions { IsUpsert = state.Revision == 0 },
                cancellationToken
            );
            return result.ModifiedCount == 1 || result.UpsertedId is not null;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}
