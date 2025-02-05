using Discord;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public abstract class Follower(ulong ignoreBotMentionsInChannelId, BotContext botContext) : INotificationHandler<MessageReceived>
{
    protected abstract Task<bool> ShouldTrigger(MessageReceived notification);

    protected abstract Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default);

    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (
            message.Channel is not ITextChannel textChannel
            || (message.MentionedUserIds.Contains(botContext.BotId) && message.Channel.Id == ignoreBotMentionsInChannelId)
            || !await ShouldTrigger(notification)
        )
            return;

        using var typing = textChannel.EnterTypingState();

        await textChannel.SendMessageAsync(await GetResponseAsync(notification, cancellationToken));
    }
}
