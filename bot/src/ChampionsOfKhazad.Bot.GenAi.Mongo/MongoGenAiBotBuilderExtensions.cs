using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.GenAi.Mongo;
using ChampionsOfKhazad.Bot.Mongo;
using MongoDB.Driver;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class MongoGenAiBotBuilderExtensions
{
    public static BotBuilder AddGenAiMongoPersistence(this BotBuilder builder)
    {
        builder
            .AddMongo()
            .AddCollection<GeneratedImage>(
                "generatedImages",
                collection =>
                {
                    collection.Indexes.CreateOne(new CreateIndexModel<GeneratedImage>(Builders<GeneratedImage>.IndexKeys.Ascending(x => x.UserId)));
                    collection.Indexes.CreateOne(
                        new CreateIndexModel<GeneratedImage>(Builders<GeneratedImage>.IndexKeys.Ascending(x => x.Timestamp))
                    );
                    collection.CreateUniqueIndex(x => x.Filename);
                }
            )
            .Services.AddSingleton<IGeneratedImageStore, MongoGeneratedImageStore>();

        return builder;
    }
}
