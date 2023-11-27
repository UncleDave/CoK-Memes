using Discord;
using MediatR;
using OpenAI.ObjectModels.RequestModels;

namespace ChampionsOfKhazad.Bot;

public record FollowerTarget(ulong UserId, string UserName);

public record FollowerOptions(FollowerTarget Target, ulong IgnoreBotMentionsInChannelId, string Instructions);

public abstract class Follower(FollowerOptions options, Assistant assistant, BotContext botContext) : INotificationHandler<MessageReceived>
{
    protected abstract bool ShouldTrigger(IUserMessage message);

    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (
            message.Channel is not ITextChannel textChannel
            || message.Author.Id != options.Target.UserId
            || (message.MentionedUserIds.Contains(botContext.BotId) && message.Channel.Id == options.IgnoreBotMentionsInChannelId)
            || !ShouldTrigger(message)
        )
            return;

        using var typing = textChannel.EnterTypingState();

        var user = new User { Id = message.Author.Id, Name = options.Target.UserName };

        // Get the unbroken message chain from the same author within the last 60 seconds
        var recentUserMessages = await message
            .GetPreviousMessagesAsync()
            .TakeWhile(x => x.Author.Id == options.Target.UserId && DateTimeOffset.UtcNow - x.Timestamp < TimeSpan.FromSeconds(60))
            .Reverse()
            .Select(x => ChatMessage.FromUser(x.CleanContent, user.Name))
            .ToListAsync(cancellationToken: cancellationToken);

        var response = await assistant.RespondAsync(
            message.CleanContent,
            user,
            botContext.Guild.Emotes.Select(x => x.Name),
            recentUserMessages,
            instructions: options.Instructions
        );

        await textChannel.SendMessageAsync(response);
    }
}
