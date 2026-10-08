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
                    collection.Indexes.CreateOne(new CreateIndexModel<GeneratedImage>(Builders<GeneratedImage>.IndexKeys.Text(x => x.Prompt)));
                }
            )
            .AddCollection<LorekeeperPersonalitySetting>("lorekeeperPersonality")
            .AddCollection<NotebookState>("lorekeeperNotebook")
            .AddCollection<GazetteState>("gazette")
            .AddCollection<GazettePublishedEdition>("gazettePublishedEditions")
            .Services.AddSingleton<IGeneratedImageStore, MongoGeneratedImageStore>()
            .AddSingleton<INotebookStore, MongoNotebookStore>()
            .AddSingleton<IGazetteIssueStore, MongoGazetteIssueStore>()
            .AddSingleton<IGazettePublishedEditionStore, MongoGazettePublishedEditionStore>()
            .AddSingleton<ILorekeeperPersonalityStore, MongoLorekeeperPersonalityStore>();

        return builder;
    }
}
