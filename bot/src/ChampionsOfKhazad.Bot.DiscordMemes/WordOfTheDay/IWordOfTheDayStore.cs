namespace ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;

public interface IWordOfTheDayStore
{
    Task<WordOfTheDay?> GetWordOfTheDayAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<ushort> GetWinCountAsync(ulong userId, CancellationToken cancellationToken = default);
    Task UpsertWordOfTheDayAsync(WordOfTheDay wordOfTheDay);
}
