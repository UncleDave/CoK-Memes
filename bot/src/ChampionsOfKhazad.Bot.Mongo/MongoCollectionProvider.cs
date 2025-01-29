using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Mongo;

internal class MongoCollectionProvider(string connectionString)
{
    private readonly Lazy<IMongoDatabase> _lazyMongoDatabase = new(
        () => new MongoClient(connectionString).GetDatabase(MongoUrl.Create(connectionString).DatabaseName)
    );

    public IMongoCollection<T> GetCollection<T>(string collectionName) => _lazyMongoDatabase.Value.GetCollection<T>(collectionName);
}
