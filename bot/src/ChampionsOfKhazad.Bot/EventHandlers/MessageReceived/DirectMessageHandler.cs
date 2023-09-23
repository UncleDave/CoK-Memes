using Discord;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public class DirectMessageHandler : INotificationHandler<MessageReceived>
{
    private const string SourceUrl = $"{Constants.RepositoryUrl}/tree/main/bot";
    private const string Message = $"Hi! I'm a bot, if you want to know more you can find my juicy innards at {SourceUrl}";
    private static readonly Dictionary<ulong, DateTime> LastUserMessage = new();

    public Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (message.Channel is not IDMChannel)
            return Task.CompletedTask;

        var isOnCooldown = LastUserMessage.TryGetValue(message.Author.Id, out var lastMessage) && (DateTime.Now - lastMessage).TotalMinutes < 5;

        LastUserMessage[message.Author.Id] = DateTime.Now;

        return isOnCooldown ? Task.CompletedTask : message.Channel.SendMessageAsync(Message);
    }

    public override string ToString() => nameof(DirectMessageHandler);
}
