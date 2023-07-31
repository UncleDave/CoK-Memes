using Discord;

namespace ChampionsOfKhazad.Bot;

public class DirectMessageHandler : IMessageReceivedEventHandler
{
    private const string SourceUrl = "https://github.com/UncleDave/CoK-Memes/tree/main/bot";

    private const string Message = $"Hi! I'm a bot, if you want to know more you can find my juicy innards at {SourceUrl}";

    private static readonly Dictionary<ulong, DateTime> LastUserMessage = new();

    public Task HandleMessageAsync(IUserMessage message)
    {
        if (message.Channel is not IDMChannel)
            return Task.CompletedTask;

        var isOnCooldown = LastUserMessage.TryGetValue(message.Author.Id, out var lastMessage) && (DateTime.Now - lastMessage).TotalMinutes < 5;

        LastUserMessage[message.Author.Id] = DateTime.Now;

        return isOnCooldown ? Task.CompletedTask : message.Channel.SendMessageAsync(Message);
    }

    public override string ToString() => nameof(DirectMessageHandler);
}
