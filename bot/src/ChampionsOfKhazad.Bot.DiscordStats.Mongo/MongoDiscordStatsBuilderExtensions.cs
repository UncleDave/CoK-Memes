using ChampionsOfKhazad.Bot.DiscordStats.Mongo;
using ChampionsOfKhazad.Bot.DiscordStats.StreakBreaks;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class MongoDiscordStatsBuilderExtensions
{
    public static DiscordStatsBuilder AddMongoPersistence(this DiscordStatsBuilder builder)
    {
        builder.AddMongo().AddCollection<StreakBreak>("streakBreaks").Services.AddSingleton<IStoreStreakBreaks, MongoStreakBreakStore>();

        return builder;
    }
}
