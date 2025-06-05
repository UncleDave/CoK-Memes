using System.Text.RegularExpressions;
using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;

namespace ChampionsOfKhazad.Bot;

public class WordOfTheDayFollower(BotContext botContext, IGetTheWordOfTheDay wordOfTheDayGetter, IWinTheWordOfTheDay wordOfTheDayWinner)
    : Follower(0, botContext)
{
    private WordOfTheDay? _wordOfTheDay;

    protected override async Task<bool> ShouldTrigger(MessageReceived notification)
    {
        _wordOfTheDay = await wordOfTheDayGetter.GetWordOfTheDayAsync();

        if (_wordOfTheDay.WinnerId.HasValue)
            return false;

        var message = notification.Message.CleanContent.ToLowerInvariant();

        // Use regex to match the word as a whole word, ignoring punctuation
        if (!Regex.IsMatch(message, $@"\b{Regex.Escape(_wordOfTheDay.Word.ToLowerInvariant())}\b"))
            return false;

        await wordOfTheDayWinner.WinWordOfTheDayAsync(notification.Message.Author.Id);
        return true;
    }

    protected override Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default) =>
        Task.FromResult($"Congratulations, {notification.Message.Author.Mention}! You found the word of the day! The word was \"{_wordOfTheDay}\".");
}
