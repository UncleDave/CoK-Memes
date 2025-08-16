using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;
using Discord;
using MediatR;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class DirectMessageHandler(IOptions<DirectMessageHandlerOptions> options, IGetTheWordOfTheDay wordOfTheDayGetter)
    : INotificationHandler<MessageReceived>
{
    private const string SourceUrl = $"{Constants.RepositoryUrl}/tree/main/bot";
    private const string Message = $"Hi! I'm a bot, if you want to know more you can find my juicy innards at {SourceUrl}";
    private static readonly Dictionary<ulong, DateTime> LastUserMessage = new();

    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (message.Channel is not IDMChannel)
            return;

        if (message.Author.Id == options.Value.AdminUserId)
        {
            if (message.CleanContent.Equals("word", StringComparison.InvariantCultureIgnoreCase))
            {
                var wordOfTheDay = await wordOfTheDayGetter.GetWordOfTheDayAsync(cancellationToken);
                await message.Channel.SendMessageAsync(wordOfTheDay.Word);
            }

            return;
        }

        var isOnCooldown = LastUserMessage.TryGetValue(message.Author.Id, out var lastMessage) && (DateTime.Now - lastMessage).TotalMinutes < 5;

        LastUserMessage[message.Author.Id] = DateTime.Now;

        if (isOnCooldown)
            return;

        await message.Channel.SendMessageAsync(Message);
    }

    public override string ToString() => nameof(DirectMessageHandler);
}
