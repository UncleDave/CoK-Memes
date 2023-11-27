namespace ChampionsOfKhazad.Bot.DiscordStats.StreakBreaks;

internal class StreakBreakService(IStoreStreakBreaks streakBreakStore) : IGetStreakBreaks
{
    public Task<uint> GetStreakBreakCountByUserAsync(ulong userId, string emoteName, CancellationToken cancellationToken = default) =>
        streakBreakStore.GetStreakBreakCountByUserAsync(userId, emoteName, cancellationToken);
}
