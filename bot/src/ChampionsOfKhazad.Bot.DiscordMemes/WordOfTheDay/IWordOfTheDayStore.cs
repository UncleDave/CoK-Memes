namespace ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;

public interface IWordOfTheDayStore
{
    Task<WordOfTheDay?> GetWordOfTheDayAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task UpsertWordOfTheDayAsync(WordOfTheDay wordOfTheDay);
}
