using Discord;

namespace ChampionsOfKhazad.Bot;

public abstract class GuildMessageReactor : IMessageReceivedEventHandler
{
    private readonly Emoji[] _reactionEmojis;
    private readonly ulong _userId;

    protected GuildMessageReactor(ulong userId, params Emoji[] reactionEmojis)
    {
        _reactionEmojis = reactionEmojis;
        _userId = userId;
    }

    public async Task HandleMessageAsync(IUserMessage message)
    {
        if (message.Channel is ITextChannel && message.Author.Id == _userId && ShouldReact(message))
        {
            await message.AddReactionsAsync(_reactionEmojis);
            await AfterReactingAsync(message);
        }
    }

    protected abstract bool ShouldReact(IUserMessage message);

    protected virtual Task AfterReactingAsync(IUserMessage message) => Task.CompletedTask;
}
