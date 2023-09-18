using ChampionsOfKhazad.Bot.HardcoreStats;
using ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;
using ChampionsOfKhazad.Bot.HardcoreStats.Mongo;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class HardcoreStatsBuilderExtensions
{
    public static HardcoreStatsBuilder AddMongoPersistence(this HardcoreStatsBuilder builder)
    {
        builder.Services
            .AddCollection<CharacterDeath>("characterDeaths")
            .AddSingleton<IStoreCharacterDeaths, MongoCharacterDeathStore>();

        return builder;
    }
}
