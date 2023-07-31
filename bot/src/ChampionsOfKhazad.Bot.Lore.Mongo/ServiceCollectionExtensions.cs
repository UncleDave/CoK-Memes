using System.Linq.Expressions;
using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Lore.Mongo;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static GuildLoreBuilder AddMongoPersistence(this GuildLoreBuilder builder, string connectionString)
    {
        var mongoCollectionProvider = new MongoCollectionProvider(connectionString);

        builder.Services
            .AddCollection<Lore>(mongoCollectionProvider, "lore", collection => CreateUniqueIndex(collection, lore => lore.Name))
            .AddCollection<MemberLore>(mongoCollectionProvider, "memberLore", collection => CreateUniqueIndex(collection, lore => lore.Name))
            .AddSingleton<IStoreLore, MongoLoreStore>();

        BsonClassMap.RegisterClassMap<Lore>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
        });

        BsonClassMap.RegisterClassMap<MemberLore>(map =>
        {
            map.AutoMap();
            map.SetIgnoreExtraElements(true);
        });

        return builder;
    }

    private static IServiceCollection AddCollection<T>(
        this IServiceCollection services,
        MongoCollectionProvider mongoCollectionProvider,
        string name,
        Action<IMongoCollection<T>> configureCollection
    ) =>
        services.AddSingleton(_ =>
        {
            var collection = mongoCollectionProvider.GetCollection<T>(name);

            configureCollection(collection);

            return collection;
        });

    private static void CreateUniqueIndex<T>(IMongoCollection<T> collection, Expression<Func<T, object>> field) =>
        collection.Indexes.CreateOne(new CreateIndexModel<T>(Builders<T>.IndexKeys.Ascending(field), new CreateIndexOptions { Unique = true }));
}
