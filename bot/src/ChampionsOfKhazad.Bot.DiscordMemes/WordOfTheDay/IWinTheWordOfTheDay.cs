namespace ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;

public interface IWinTheWordOfTheDay
{
    Task WinWordOfTheDayAsync(ulong userId);
}
