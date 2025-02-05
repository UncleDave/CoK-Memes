using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.DiscordMemes.Mongo;

internal class MongoWordOfTheDayStore(IMongoCollection<WordOfTheDay.WordOfTheDay> wordOfTheDayCollection) : IWordOfTheDayStore
{
    public async Task<WordOfTheDay.WordOfTheDay?> GetWordOfTheDayAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        await wordOfTheDayCollection.Find(x => x.Date == date).SingleOrDefaultAsync(cancellationToken);

    public Task UpsertWordOfTheDayAsync(WordOfTheDay.WordOfTheDay wordOfTheDay) =>
        wordOfTheDayCollection.ReplaceOneAsync(x => x.Date == wordOfTheDay.Date, wordOfTheDay, new ReplaceOptions { IsUpsert = true });
}
