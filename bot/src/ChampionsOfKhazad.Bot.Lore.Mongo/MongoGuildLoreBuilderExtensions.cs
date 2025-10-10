using ChampionsOfKhazad.Bot.Lore.Abstractions;
using ChampionsOfKhazad.Bot.Lore.Mongo;
using ChampionsOfKhazad.Bot.Mongo;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class MongoGuildLoreBuilderExtensions
{
    public static GuildLoreBuilder AddMongoPersistence(this GuildLoreBuilder builder)
    {
        builder
            .AddMongo()
            .AddCollection<LoreDocument>(
                Collections.Lore.Name,
                collection => collection.CreateUniqueIndex(Collections.Lore.UniqueIndex.Field, Collections.Lore.UniqueIndex.Collation)
            )
            .Services.AddSingleton<IStoreLore, MongoLoreStore>();

        return builder;
    }
}
