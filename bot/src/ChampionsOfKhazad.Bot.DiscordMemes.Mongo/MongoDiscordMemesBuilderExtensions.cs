using ChampionsOfKhazad.Bot.DiscordMemes.CharacterDeaths;
using ChampionsOfKhazad.Bot.DiscordMemes.Mongo;
using ChampionsOfKhazad.Bot.DiscordMemes.StreakBreaks;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class MongoDiscordMemesBuilderExtensions
{
    public static DiscordMemesBuilder AddMongoPersistence(this DiscordMemesBuilder builder)
    {
        builder
            .AddMongo()
            .AddCollection<StreakBreak>("streakBreaks")
            .AddCollection<CharacterDeath>("characterDeaths")
            .Services.AddSingleton<MongoStreakBreakStore>()
            .AddSingleton<IGetStreakBreaks>(sp => sp.GetRequiredService<MongoStreakBreakStore>())
            .AddSingleton<IStoreStreakBreaks>(sp => sp.GetRequiredService<MongoStreakBreakStore>())
            .AddSingleton<IStoreCharacterDeaths, MongoCharacterDeathStore>();

        return builder;
    }
}
