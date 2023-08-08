using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Lore.Mongo;
using ChampionsOfKhazad.Bot.Mongo;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class GuildLoreBuilderExtensions
{
    public static GuildLoreBuilder AddMongoPersistence(this GuildLoreBuilder builder, string connectionString)
    {
        builder.Services
            .AddMongo(connectionString)
            .AddCollection<Lore>("lore", collection => collection.CreateUniqueIndex(lore => lore.Name))
            .AddCollection<MemberLore>("memberLore", collection => collection.CreateUniqueIndex(lore => lore.Name))
            .AddSingleton<IStoreLore, MongoLoreStore>();

        return builder;
    }
}
