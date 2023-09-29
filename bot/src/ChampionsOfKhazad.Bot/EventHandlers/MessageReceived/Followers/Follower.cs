using Discord;
using MediatR;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace ChampionsOfKhazad.Bot;

public record FollowerTarget(ulong UserId, string UserName);

public record FollowerOptions(FollowerTarget Target, ulong IgnoreBotMentionsInChannelId, string Instructions);

public abstract class Follower : INotificationHandler<MessageReceived>
{
    private readonly FollowerOptions _options;
    private readonly Assistant _assistant;
    private readonly BotContext _botContext;

    protected Follower(FollowerOptions options, Assistant assistant, BotContext botContext)
    {
        _options = options;
        _assistant = assistant;
        _botContext = botContext;
    }

    protected abstract bool ShouldTrigger(IUserMessage message);

    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (
            message.Channel is not ITextChannel textChannel
            || message.Author.Id != _options.Target.UserId
            || (message.MentionedUserIds.Contains(_botContext.BotId) && message.Channel.Id == _options.IgnoreBotMentionsInChannelId)
            || !ShouldTrigger(message)
        )
            return;

        using var typing = textChannel.EnterTypingState();

        var user = new User { Id = message.Author.Id, Name = _options.Target.UserName };

        // Get the unbroken message chain from the same author within the last 60 seconds
        var recentUserMessages = await message
            .GetPreviousMessagesAsync()
            .TakeWhile(x => x.Author.Id == _options.Target.UserId && DateTimeOffset.UtcNow - x.Timestamp < TimeSpan.FromSeconds(60))
            .Reverse()
            .Select(x => ChatMessage.FromUser(x.CleanContent, user.Name))
            .ToListAsync(cancellationToken: cancellationToken);

        var response = await _assistant.RespondAsync(
            message.CleanContent,
            user,
            _botContext.Guild.Emotes.Select(x => x.Name),
            recentUserMessages,
            instructions: _options.Instructions,
            model: Models.Gpt_4
        );

        await textChannel.SendMessageAsync(response);
    }
}
