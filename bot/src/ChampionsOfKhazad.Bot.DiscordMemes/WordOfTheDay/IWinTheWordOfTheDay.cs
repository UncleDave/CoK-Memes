namespace ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;

public interface IWinTheWordOfTheDay
{
    Task<ushort> WinWordOfTheDayAsync(ulong userId);
}
