using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.DiscordMemes.Mongo;

internal class MongoWordOfTheDayStore(IMongoCollection<WordOfTheDay.WordOfTheDay> wordOfTheDayCollection) : IWordOfTheDayStore
{
    public async Task<WordOfTheDay.WordOfTheDay?> GetWordOfTheDayAsync(DateOnly date, CancellationToken cancellationToken = default) =>
        await wordOfTheDayCollection.Find(x => x.Date == date).SingleOrDefaultAsync(cancellationToken);

    public async Task<WordOfTheDay.WordOfTheDay?> GetMostRecentlyWonWordOfTheDayAsync(CancellationToken cancellationToken = default) =>
        await wordOfTheDayCollection.Find(x => x.WinnerId.HasValue).SortByDescending(x => x.Date).FirstOrDefaultAsync(cancellationToken);

    public async Task<ushort> GetWinCountAsync(ulong userId, CancellationToken cancellationToken = default) =>
        (ushort)await wordOfTheDayCollection.Find(x => x.WinnerId == userId).CountDocumentsAsync(cancellationToken);

    public Task UpsertWordOfTheDayAsync(WordOfTheDay.WordOfTheDay wordOfTheDay) =>
        wordOfTheDayCollection.ReplaceOneAsync(x => x.Date == wordOfTheDay.Date, wordOfTheDay, new ReplaceOptions { IsUpsert = true });
}
