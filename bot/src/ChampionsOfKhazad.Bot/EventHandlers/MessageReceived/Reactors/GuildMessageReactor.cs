using Discord;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public record GuildMessageReactorOptions(ulong UserId, IEnumerable<IEmote> ReactionEmojis);

public abstract class GuildMessageReactor : INotificationHandler<MessageReceived>
{
    private readonly GuildMessageReactorOptions _options;

    protected GuildMessageReactor(GuildMessageReactorOptions options)
    {
        _options = options;
    }

    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (message.Channel is ITextChannel && message.Author.Id == _options.UserId && ShouldReact(message))
        {
            await BeforeReactingAsync(message);
            await message.AddReactionsAsync(_options.ReactionEmojis);
            await AfterReactingAsync(message);
        }
    }

    protected abstract bool ShouldReact(IUserMessage message);

    protected virtual Task BeforeReactingAsync(IUserMessage message) => Task.CompletedTask;

    protected virtual Task AfterReactingAsync(IUserMessage message) => Task.CompletedTask;
}
