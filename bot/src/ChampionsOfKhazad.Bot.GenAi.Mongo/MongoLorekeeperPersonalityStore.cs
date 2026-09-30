using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.GenAi.Mongo;

internal class MongoLorekeeperPersonalityStore(IMongoCollection<LorekeeperPersonalitySetting> collection) : ILorekeeperPersonalityStore
{
    public Task<LorekeeperPersonalitySetting?> GetAsync(CancellationToken cancellationToken = default) =>
        collection.Find(x => x.Id == "lorekeeper").FirstOrDefaultAsync(cancellationToken)!;

    public Task SaveAsync(LorekeeperPersonalitySetting setting, CancellationToken cancellationToken = default) =>
        collection.ReplaceOneAsync(x => x.Id == "lorekeeper", setting, new ReplaceOptions { IsUpsert = true }, cancellationToken);
}
