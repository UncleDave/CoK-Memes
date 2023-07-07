using Discord;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class EmoteStreakHandler : IMessageReceivedEventHandler
{
    private readonly EmoteStreakHandlerOptions _options;
    private IEmote? _emote;
    private IGuildChannel? _channel;
    private ulong? _botId;

    public EmoteStreakHandler(IOptions<EmoteStreakHandlerOptions> options)
    {
        _options = options.Value;
    }

    public async Task StartAsync(BotContext context)
    {
        _emote = await context.Guild
            .GetEmotesAsync()
            .SingleAsync(x => x.Name == _options.EmoteName);

        _channel = await context.Guild.GetChannelAsync(_options.ChannelId);
        _botId = context.BotId;
    }

    public async Task HandleMessageAsync(IUserMessage message)
    {
        if (_emote is null)
            throw new InvalidOperationException(
                $"{nameof(EmoteStreakHandler)} has not been started"
            );

        if (message.Content == _emote.ToString())
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
                || (previousMessage.Author.IsBot && previousMessage.Author.Id != _botId)
            )
                continue;

            // Streak is broken if the message isn't the emote - stop counting
            if (previousUserMessage.Content != _emote.ToString())
                break;

            // Ignore repeat messages from the same user
            if (
                !_options.AllowSingleUserStreaks
                && previousUserMessage.Author.Id == previousAuthorId
            )
                continue;

            previousAuthorId = previousUserMessage.Author.Id;
            streak++;
        }

        if (streak > 1)
        {
            await message.Channel.SendMessageAsync(
                $"Streak of {streak} {_emote} broken by {message.Author.Mention}, shame on them."
            );
        }
    }

    public override string ToString() =>
        $"{nameof(EmoteStreakHandler)} - :{_options.EmoteName}: in #{_channel?.Name ?? _options.ChannelId.ToString()}";
}
