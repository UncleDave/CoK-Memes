using Discord;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class EmoteStreakHandler : IMessageReceivedEventHandler
{
    private readonly EmoteStreakHandlerOptions _options;
    private readonly BotContext _botContext;

    public EmoteStreakHandler(IOptions<EmoteStreakHandlerOptions> options, BotContext botContext)
    {
        _options = options.Value;
        _botContext = botContext;
    }

    public async Task HandleMessageAsync(IUserMessage message)
    {
        var emote =
            _botContext.Guild.Emotes.SingleOrDefault(x => x.Name == _options.EmoteName)
            ?? await _botContext.Guild.GetEmotesAsync().SingleAsync(x => x.Name == _options.EmoteName);

        if (message.Content == emote.ToString())
            return;

        var streak = 0;
        ulong? previousAuthorId = null;

        await foreach (var previousMessage in message.GetPreviousMessagesAsync())
        {
            // Ignore messages that aren't from users or are from other bots
            // Messages from this bot should break the streak
            // Otherwise users can edit their messages after a streak is broken to continue it
            if (
                previousMessage is not IUserMessage previousUserMessage
                || (previousMessage.Author.IsBot && previousMessage.Author.Id != _botContext.BotId)
            )
                continue;

            // Streak is broken if the message isn't the emote - stop counting
            if (previousUserMessage.Content != emote.ToString())
                break;

            // Ignore repeat messages from the same user
            if (!_options.AllowSingleUserStreaks && previousUserMessage.Author.Id == previousAuthorId)
                continue;

            previousAuthorId = previousUserMessage.Author.Id;
            streak++;
        }

        if (streak > 1)
        {
            await message.Channel.SendMessageAsync($"Streak of {streak} {emote} broken by {message.Author.Mention}, shame on them.");
        }
    }

    public override string ToString() => $"{nameof(EmoteStreakHandler)} - :{_options.EmoteName}: in {_options.ChannelId.ToString()}";
}
