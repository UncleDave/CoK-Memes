using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.EventLoop;

public class WordOfTheDayHintEvent(
    IOptions<WordOfTheDayHintEventOptions> options,
    BotContext botContext,
    ICompletionService completionService,
    IGetTheWordOfTheDay wordOfTheDayGetter
) : EventLoopEvent(TimeSpan.FromMinutes(options.Value.MeanTimeToHappenMinutes), "WordOfTheDayHint")
{
    private readonly TimeSpan _cooldown = TimeSpan.FromMinutes(options.Value.CooldownMinutes);
    private readonly TimeSpan _minimumTimeSinceLastWinner = TimeSpan.FromMinutes(options.Value.MinimumTimeSinceLastWinnerMinutes);
    private WordOfTheDay? _mostRecentlyWonWordOfTheDay;
    private TimeSpan? _timeSinceLastWin;
    private static DateTimeOffset _lastTriggeredAt = DateTimeOffset.MinValue;

    public override async Task<bool> EligibleToFire(CancellationToken cancellationToken)
    {
        if (DateTimeOffset.Now - _lastTriggeredAt < _cooldown)
            return false;

        var mostRecentlyWonWordOfTheDay = await wordOfTheDayGetter.GetMostRecentlyWonWordOfTheDayAsync(cancellationToken);

        if (mostRecentlyWonWordOfTheDay is null)
            return false;

        var timeSinceLastWin = DateTime.Now - mostRecentlyWonWordOfTheDay.Date.ToDateTime(TimeOnly.MinValue);

        if (timeSinceLastWin < _minimumTimeSinceLastWinner)
            return false;

        _mostRecentlyWonWordOfTheDay = mostRecentlyWonWordOfTheDay;
        _timeSinceLastWin = timeSinceLastWin;

        return true;
    }

    public override async Task FireAsync(CancellationToken cancellationToken)
    {
        if (_mostRecentlyWonWordOfTheDay is null || _timeSinceLastWin is null)
            throw new InvalidOperationException("Most recently won word of the day is null or time since last win is null");

        var wordOfTheDay = await wordOfTheDayGetter.GetWordOfTheDayAsync(cancellationToken);

        var firstLetter = wordOfTheDay.Word[0];
        var wordLength = wordOfTheDay.Word.Length;
        var daysSinceLastWin = (int)_timeSinceLastWin.Value.TotalDays;

        var requestOptions = new RequestOptions { CancelToken = cancellationToken };
        var mostRecentWinner = await botContext.Client.GetUserAsync(_mostRecentlyWonWordOfTheDay.WinnerId!.Value, requestOptions);

        var winnerName = mostRecentWinner.GetName();
        var winnerMention = mostRecentWinner.Mention;

        var systemMessage =
            "You are a Discord bot posting a single message in a text channel.\n"
            + "A word is randomly chosen each day as the 'word of the day' for users to guess. The first user to type the word in any text channel wins.\n"
            + $"No word of the day has been correctly guessed since {winnerName} guessed '{_mostRecentlyWonWordOfTheDay.Word}' {daysSinceLastWin} days ago.\n"
            + "Playfully tease the users about their inability to guess the word of the day by providing a hint that includes the first letter and the length of the word.\n"
            + $"The first letter of the word is '{firstLetter}' and the word has {wordLength} letters.\n"
            + $"You may optionally tag the last winner using this mention string: {winnerMention}.\n"
            + "Ensure that your message mentions that this is a hint for the word of the day.";

        var chatHistory = new ChatHistory(systemMessage);
        var message = completionService.InvokeAsync(chatHistory, cancellationToken);

        var textChannel = await botContext.Guild.GetTextChannelAsync(options.Value.TextChannelId, options: requestOptions);

        using var typing = textChannel.EnterTypingState();

        await textChannel.SendMessageAsync(await message);

        _lastTriggeredAt = DateTimeOffset.Now;
    }
}
