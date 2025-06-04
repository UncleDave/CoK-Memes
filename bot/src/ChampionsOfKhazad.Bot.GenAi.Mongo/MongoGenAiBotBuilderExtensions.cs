using ChampionsOfKhazad.Bot.GenAi;
using ChampionsOfKhazad.Bot.GenAi.Mongo;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class MongoGenAiBotBuilderExtensions
{
    public static BotBuilder AddGenAiMongoPersistence(this BotBuilder builder)
    {
        builder.AddMongo().AddCollection<GeneratedImage>("generatedImages").Services.AddSingleton<IGeneratedImageStore, MongoGeneratedImageStore>();

        return builder;
    }
}
