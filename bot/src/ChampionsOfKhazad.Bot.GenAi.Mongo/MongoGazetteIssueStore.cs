using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.GenAi.Mongo;

internal sealed class MongoGazetteIssueStore(IMongoCollection<GazetteState> collection) : IGazetteIssueStore
{
    public async Task<GazetteState> GetAsync(CancellationToken cancellationToken) =>
        await collection.Find(state => state.Id == "khazad-gazette").FirstOrDefaultAsync(cancellationToken) ?? new();

    public async Task<bool> TrySaveAsync(GazetteState state, CancellationToken cancellationToken)
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
