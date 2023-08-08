namespace ChampionsOfKhazad.Bot.DiscordStats.StreakBreaks;

internal class StreakBreakService : IGetStreakBreaks
{
    private readonly IStoreStreakBreaks _streakBreakStore;

    public StreakBreakService(IStoreStreakBreaks streakBreakStore)
    {
        _streakBreakStore = streakBreakStore;
    }

    public Task<uint> GetStreakBreakCountByUserAsync(ulong userId, string emoteName, CancellationToken cancellationToken = default) =>
        _streakBreakStore.GetStreakBreakCountByUserAsync(userId, emoteName, cancellationToken);
}
