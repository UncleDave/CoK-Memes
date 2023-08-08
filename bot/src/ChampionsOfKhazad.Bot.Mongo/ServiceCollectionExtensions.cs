using ChampionsOfKhazad.Bot.Mongo;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMongo(this IServiceCollection services, string connectionString)
    {
        services.TryAddSingleton(typeof(MongoCollectionProvider), _ => new MongoCollectionProvider(connectionString));

        var conventionPack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
            new IgnoreIfNullConvention(true),
        };

        ConventionRegistry.Register("ChampionsOfKhazad.Bot", conventionPack, _ => true);

        return services;
    }

    public static IServiceCollection AddCollection<T>(
        this IServiceCollection services,
        string name,
        Action<IMongoCollection<T>>? configureCollection = null
    ) =>
        services.AddSingleton(serviceProvider =>
        {
            var mongoCollectionProvider = serviceProvider.GetRequiredService<MongoCollectionProvider>();
            var collection = mongoCollectionProvider.GetCollection<T>(name);

            configureCollection?.Invoke(collection);

            return collection;
        });
}
