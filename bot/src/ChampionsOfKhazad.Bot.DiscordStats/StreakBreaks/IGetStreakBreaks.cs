namespace ChampionsOfKhazad.Bot.DiscordStats.StreakBreaks;

public interface IGetStreakBreaks
{
    Task<uint> GetStreakBreakCountByUserAsync(ulong userId, string emoteName, CancellationToken cancellationToken = default);
}
