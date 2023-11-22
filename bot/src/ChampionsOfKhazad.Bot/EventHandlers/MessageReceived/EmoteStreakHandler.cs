using ChampionsOfKhazad.Bot.DiscordStats.StreakBreaks;
using Discord;
using MediatR;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class EmoteStreakHandler : INotificationHandler<MessageReceived>
{
    private readonly EmoteStreakHandlerOptions _options;
    private readonly BotContext _botContext;
    private readonly IGetStreakBreaks _streakBreakGetter;
    private readonly IPublisher _publisher;

    public EmoteStreakHandler(
        IOptions<EmoteStreakHandlerOptions> options,
        BotContext botContext,
        IGetStreakBreaks streakBreakGetter,
        IPublisher publisher
    )
    {
        _options = options.Value;
        _botContext = botContext;
        _streakBreakGetter = streakBreakGetter;
        _publisher = publisher;
    }

    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var emote =
            _botContext.Guild.Emotes.SingleOrDefault(x => x.Name == _options.EmoteName)
            ?? await _botContext.Guild.GetEmotesAsync().SingleAsync(x => x.Name == _options.EmoteName);

        var message = notification.Message;

        if (
            message.Channel is not ITextChannel textChannel
            || (textChannel.CategoryId != _options.ChannelId && textChannel.Id != _options.ChannelId)
            || message.Content == emote.ToString()
        )
            return;

        var streak = 0;
        ulong? previousAuthorId = null;

        await foreach (var previousMessage in message.GetPreviousMessagesAsync().WithCancellation(cancellationToken))
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
            var userStreakBreakCount = await _streakBreakGetter.GetStreakBreakCountByUserAsync(
                message.Author.Id,
                _options.EmoteName,
                cancellationToken
            );

            await message
                .Channel
                .SendMessageAsync(
                    $"Streak of {streak} {emote} broken by {message.Author.Mention}, shame on them. This is their {(userStreakBreakCount + 1).ToOrdinal()} streak break."
                );

            await _publisher.Publish(new StreakBroken(message.Author.Id, _options.EmoteName, message.Timestamp), cancellationToken);
        }
    }

    public override string ToString() => $"{nameof(EmoteStreakHandler)} - :{_options.EmoteName}: in {_options.ChannelId.ToString()}";
}
