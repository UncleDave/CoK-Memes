namespace ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;

public interface IGetTheWordOfTheDay
{
    Task<WordOfTheDay> GetWordOfTheDayAsync(CancellationToken cancellationToken = default);
}
