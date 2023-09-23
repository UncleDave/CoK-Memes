using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.Mongo;
using MongoDB.Driver;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public class MongoBuilder : BotBuilder
{
    public MongoBuilder(IServiceCollection services, BotConfiguration botConfiguration)
        : base(services, botConfiguration) { }

    public MongoBuilder AddCollection<T>(string name, Action<IMongoCollection<T>>? configureCollection = null)
    {
        Services.AddSingleton(serviceProvider =>
        {
            var mongoCollectionProvider = serviceProvider.GetRequiredService<MongoCollectionProvider>();
            var collection = mongoCollectionProvider.GetCollection<T>(name);

            configureCollection?.Invoke(collection);

            return collection;
        });

        return this;
    }
}
