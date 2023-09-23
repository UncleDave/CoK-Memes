using ChampionsOfKhazad.Bot.Lore;
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
            .AddCollection<Lore>("lore", collection => collection.CreateUniqueIndex(lore => lore.Name))
            .AddCollection<MemberLore>("memberLore", collection => collection.CreateUniqueIndex(lore => lore.Name))
            .Services.AddSingleton<IStoreLore, MongoLoreStore>();

        return builder;
    }
}
