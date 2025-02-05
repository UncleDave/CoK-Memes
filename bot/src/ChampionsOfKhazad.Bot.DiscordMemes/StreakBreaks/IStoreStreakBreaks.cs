namespace ChampionsOfKhazad.Bot.DiscordMemes.StreakBreaks;

public interface IStoreStreakBreaks
{
    Task InsertStreakBreakAsync(StreakBreak streakBreak);
}
