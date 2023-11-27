using Discord;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public record GuildMessageReactorOptions(ulong UserId, IEnumerable<IEmote> ReactionEmojis);

public abstract class GuildMessageReactor(GuildMessageReactorOptions options) : INotificationHandler<MessageReceived>
{
    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (message.Channel is ITextChannel && message.Author.Id == options.UserId && ShouldReact(message))
        {
            await BeforeReactingAsync(message);
            await message.AddReactionsAsync(options.ReactionEmojis);
            await AfterReactingAsync(message);
        }
    }

    protected abstract bool ShouldReact(IUserMessage message);

    protected virtual Task BeforeReactingAsync(IUserMessage message) => Task.CompletedTask;

    protected virtual Task AfterReactingAsync(IUserMessage message) => Task.CompletedTask;
}
