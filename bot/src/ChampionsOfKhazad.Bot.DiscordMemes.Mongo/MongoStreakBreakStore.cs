using ChampionsOfKhazad.Bot.DiscordMemes.StreakBreaks;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.DiscordMemes.Mongo;

internal class MongoStreakBreakStore(IMongoCollection<StreakBreak> streakBreakCollection) : IGetStreakBreaks, IStoreStreakBreaks
{
    public async Task<uint> GetStreakBreakCountByUserAsync(ulong userId, string emoteName, CancellationToken cancellationToken = default)
    {
        var count = await streakBreakCollection.CountDocumentsAsync(
            x => x.UserId == userId && x.EmoteName == emoteName,
            cancellationToken: cancellationToken
        );

        return (uint)count;
    }

    public Task InsertStreakBreakAsync(StreakBreak streakBreak) => streakBreakCollection.InsertOneAsync(streakBreak);
}
