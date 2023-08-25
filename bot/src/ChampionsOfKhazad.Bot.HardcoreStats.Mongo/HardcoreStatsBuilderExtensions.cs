using ChampionsOfKhazad.Bot.HardcoreStats;
using ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;
using ChampionsOfKhazad.Bot.HardcoreStats.Mongo;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class HardcoreStatsBuilderExtensions
{
    public static HardcoreStatsBuilder AddMongoPersistence(this HardcoreStatsBuilder builder, string connectionString)
    {
        builder.Services
            .AddMongo(connectionString)
            .AddCollection<CharacterDeath>("characterDeaths")
            .AddSingleton<IStoreCharacterDeaths, MongoCharacterDeathStore>();

        return builder;
    }
}
