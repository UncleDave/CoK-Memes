using Discord;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public abstract class GuildMessageReactor : INotificationHandler<MessageReceived>
{
    private readonly Emoji[] _reactionEmojis;
    private readonly ulong _userId;

    protected GuildMessageReactor(ulong userId, params Emoji[] reactionEmojis)
    {
        _reactionEmojis = reactionEmojis;
        _userId = userId;
    }

    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (message.Channel is ITextChannel && message.Author.Id == _userId && ShouldReact(message))
        {
            await BeforeReactingAsync(message);
            await message.AddReactionsAsync(_reactionEmojis);
            await AfterReactingAsync(message);
        }
    }

    protected abstract bool ShouldReact(IUserMessage message);

    protected virtual Task BeforeReactingAsync(IUserMessage message) => Task.CompletedTask;

    protected virtual Task AfterReactingAsync(IUserMessage message) => Task.CompletedTask;
}
