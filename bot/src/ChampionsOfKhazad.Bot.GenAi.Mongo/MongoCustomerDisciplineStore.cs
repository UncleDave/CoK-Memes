using ChampionsOfKhazad.Bot.GenAi;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.GenAi.Mongo;

internal class MongoCustomerDisciplineStore(IMongoCollection<CustomerDisciplineState> collection) : ICustomerDisciplineStore
{
    public Task<CustomerDisciplineState?> GetAsync(string id, CancellationToken cancellationToken) =>
        collection.Find(state => state.Id == id).FirstOrDefaultAsync(cancellationToken)!;

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
