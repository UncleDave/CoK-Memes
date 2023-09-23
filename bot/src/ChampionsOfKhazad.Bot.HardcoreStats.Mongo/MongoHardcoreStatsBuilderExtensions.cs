using ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;
using ChampionsOfKhazad.Bot.HardcoreStats.Mongo;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class MongoHardcoreStatsBuilderExtensions
{
    public static HardcoreStatsBuilder AddMongoPersistence(this HardcoreStatsBuilder builder)
    {
        builder.AddMongo().AddCollection<CharacterDeath>("characterDeaths").Services.AddSingleton<IStoreCharacterDeaths, MongoCharacterDeathStore>();

        return builder;
    }
}
