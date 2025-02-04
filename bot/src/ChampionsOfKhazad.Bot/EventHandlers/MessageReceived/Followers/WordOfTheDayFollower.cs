using Bogus;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public class WordOfTheDayFollower(BotContext botContext, ILogger<WordOfTheDayFollower> logger) : Follower(0, botContext)
{
    // Statics are cringe but MediatR's service lifetime configuration option isn't working, and I'm too lazy to figure out why
    private static readonly Randomizer Randomizer = new();
    private static DateTime _lastWinnerAt;
    private static string _wordOfTheDay = string.Empty;
    private static DateTime _wordOfTheDaySetAt;
    private static readonly Lock Lock = new();

    protected override bool ShouldTrigger(MessageReceived notification)
    {
        if (_lastWinnerAt.Date == DateTime.UtcNow.Date)
            return false;

        var message = notification.Message.CleanContent.ToLowerInvariant();
        var wordOfTheDay = GetWordOfTheDay();

        if (!message.Contains(wordOfTheDay))
            return false;

        _lastWinnerAt = DateTime.UtcNow;
        return true;
    }

    protected override Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default) =>
        Task.FromResult($"Congratulations, {notification.Message.Author.Mention}! You found the word of the day! The word was \"{_wordOfTheDay}\".");

    private string GetWordOfTheDay()
    {
        using (Lock.EnterScope())
        {
            if (_wordOfTheDaySetAt.Date != DateTime.UtcNow.Date)
            {
                string newWordOfTheDay;

                do
                {
                    newWordOfTheDay = Randomizer.Word();
                    // Purge phrases, hyphenated words, and acronyms
                } while (newWordOfTheDay.Contains(' ') || newWordOfTheDay.Contains('-') || newWordOfTheDay == newWordOfTheDay.ToUpperInvariant());

                logger.LogInformation("Setting word of the day to \"{WordOfTheDay}\"", newWordOfTheDay);
                _wordOfTheDay = newWordOfTheDay.ToLowerInvariant();
                _wordOfTheDaySetAt = DateTime.UtcNow;
            }
        }

        return _wordOfTheDay;
    }
}
