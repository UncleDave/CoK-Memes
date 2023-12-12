using Discord;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public abstract class Follower(
    ulong ignoreBotMentionsInChannelId,
    IFollowerTriggerStrategy triggerStrategy,
    IFollowerResponseStrategy responseStrategy,
    BotContext botContext
) : INotificationHandler<MessageReceived>
{
    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (
            message.Channel is not ITextChannel textChannel
            || (message.MentionedUserIds.Contains(botContext.BotId) && message.Channel.Id == ignoreBotMentionsInChannelId)
            || !triggerStrategy.ShouldTrigger(notification)
        )
            return;

        using var typing = textChannel.EnterTypingState();

        await textChannel.SendMessageAsync(await responseStrategy.GetResponseAsync(notification, cancellationToken));
    }
}
