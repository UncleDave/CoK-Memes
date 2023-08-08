namespace ChampionsOfKhazad.Bot.DiscordStats.StreakBreaks;

public interface IStoreStreakBreaks
{
    Task<uint> GetStreakBreakCountByUserAsync(ulong userId, string emoteName, CancellationToken cancellationToken = default);
    Task InsertStreakBreakAsync(StreakBreak streakBreak);
}
