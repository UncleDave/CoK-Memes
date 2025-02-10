using ChampionsOfKhazad.Bot.DiscordMemes.CharacterDeaths;
using ChampionsOfKhazad.Bot.DiscordMemes.Mongo;
using ChampionsOfKhazad.Bot.DiscordMemes.StreakBreaks;
using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;
using ChampionsOfKhazad.Bot.Mongo;

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
            .AddCollection<WordOfTheDay>(
                "wordOfTheDay",
                collection =>
                {
                    collection.CreateUniqueIndex(x => x.Date, descending: true);
                }
            )
            .Services.AddSingleton<MongoStreakBreakStore>()
            .AddSingleton<IGetStreakBreaks>(sp => sp.GetRequiredService<MongoStreakBreakStore>())
            .AddSingleton<IStoreStreakBreaks>(sp => sp.GetRequiredService<MongoStreakBreakStore>())
            .AddSingleton<IStoreCharacterDeaths, MongoCharacterDeathStore>()
            .AddSingleton<IWordOfTheDayStore, MongoWordOfTheDayStore>();

        return builder;
    }
}
