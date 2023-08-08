using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Mongo;

internal class MongoCollectionProvider
{
    private readonly Lazy<IMongoDatabase> _lazyMongoDatabase;

    public MongoCollectionProvider(string connectionString) =>
        _lazyMongoDatabase = new Lazy<IMongoDatabase>(
            () => new MongoClient(connectionString).GetDatabase(MongoUrl.Create(connectionString).DatabaseName)
        );

    public IMongoCollection<T> GetCollection<T>(string collectionName) => _lazyMongoDatabase.Value.GetCollection<T>(collectionName);
}
