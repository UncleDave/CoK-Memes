using ChampionsOfKhazad.Bot.DiscordStats;
using ChampionsOfKhazad.Bot.DiscordStats.Mongo;
using ChampionsOfKhazad.Bot.DiscordStats.StreakBreaks;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DiscordStatsBuilderExtensions
{
    public static DiscordStatsBuilder AddMongoPersistence(this DiscordStatsBuilder builder, string connectionString)
    {
        builder.Services
            .AddMongo(connectionString)
            .AddCollection<StreakBreak>("streakBreaks")
            .AddSingleton<IStoreStreakBreaks, MongoStreakBreakStore>();

        return builder;
    }
}
