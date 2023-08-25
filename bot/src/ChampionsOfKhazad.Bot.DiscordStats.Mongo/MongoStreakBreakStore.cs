using ChampionsOfKhazad.Bot.DiscordStats.StreakBreaks;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.DiscordStats.Mongo;

internal class MongoStreakBreakStore : IStoreStreakBreaks
{
    private readonly IMongoCollection<StreakBreak> _streakBreakCollection;

    public MongoStreakBreakStore(IMongoCollection<StreakBreak> streakBreakCollection)
    {
        _streakBreakCollection = streakBreakCollection;
    }

    public async Task<uint> GetStreakBreakCountByUserAsync(ulong userId, string emoteName, CancellationToken cancellationToken = default)
    {
        var count = await _streakBreakCollection.CountDocumentsAsync(
            x => x.UserId == userId && x.EmoteName == emoteName,
            cancellationToken: cancellationToken
        );

        return (uint)count;
    }

    public Task InsertStreakBreakAsync(StreakBreak streakBreak) => _streakBreakCollection.InsertOneAsync(streakBreak);
}
