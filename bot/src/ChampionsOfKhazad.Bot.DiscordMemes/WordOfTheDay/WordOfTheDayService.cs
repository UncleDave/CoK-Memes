using CrypticWizard.RandomWordGenerator;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;

internal class WordOfTheDayService(IWordOfTheDayStore wordOfTheDayStore, ILogger<WordOfTheDayService> logger)
    : IGetTheWordOfTheDay,
        IWinTheWordOfTheDay
{
    private readonly WordGenerator _wordGenerator = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<WordOfTheDay> GetWordOfTheDayAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        try
        {
            await _lock.WaitAsync(cancellationToken);

            var wordOfTheDay = await wordOfTheDayStore.GetWordOfTheDayAsync(today, cancellationToken);

            if (wordOfTheDay is null)
            {
                var newWordOfTheDay = GenerateWordOfTheDay();
                wordOfTheDay = new WordOfTheDay(newWordOfTheDay, today);
                await wordOfTheDayStore.UpsertWordOfTheDayAsync(wordOfTheDay);
            }

            return wordOfTheDay;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ushort> WinWordOfTheDayAsync(ulong userId)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        try
        {
            await _lock.WaitAsync();

            var wordOfTheDay = await wordOfTheDayStore.GetWordOfTheDayAsync(today);

            if (wordOfTheDay is null)
                throw new InvalidOperationException("Cannot win the word of the day when there is no word of the day.");

            if (wordOfTheDay.WinnerId.HasValue)
                throw new InvalidOperationException("The word of the day has already been won.");

            var wonWordOfTheDay = wordOfTheDay with { WinnerId = userId };
            await wordOfTheDayStore.UpsertWordOfTheDayAsync(wonWordOfTheDay);

            return await wordOfTheDayStore.GetWinCountAsync(userId);
        }
        finally
        {
            _lock.Release();
        }
    }

    private string GenerateWordOfTheDay()
    {
        string newWordOfTheDay;

        do
        {
            newWordOfTheDay = _wordGenerator.GetWord();
            // Purge phrases, hyphenated words, and acronyms
        } while (newWordOfTheDay.Contains(' ') || newWordOfTheDay.Contains('-') || newWordOfTheDay == newWordOfTheDay.ToUpperInvariant());

        logger.LogInformation("Generated new word of the day - \"{WordOfTheDay}\"", newWordOfTheDay);
        return newWordOfTheDay.ToLowerInvariant();
    }
}
