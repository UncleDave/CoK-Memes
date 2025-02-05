namespace ChampionsOfKhazad.Bot.DiscordMemes.StreakBreaks;

public interface IGetStreakBreaks
{
    Task<uint> GetStreakBreakCountByUserAsync(ulong userId, string emoteName, CancellationToken cancellationToken = default);
}
