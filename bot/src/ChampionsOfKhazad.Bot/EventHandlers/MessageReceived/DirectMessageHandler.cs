using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;
using Discord;
using MediatR;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class DirectMessageHandler(
    IOptions<DirectMessageHandlerOptions> options,
    IGetTheWordOfTheDay wordOfTheDayGetter,
    PersonalityDirectMessageCommand personalityCommand
) : INotificationHandler<MessageReceived>
{
    private const string SourceUrl = $"{Constants.RepositoryUrl}/tree/main/bot";
    private const string Message = $"Hi! I'm a bot, if you want to know more you can find my juicy innards at {SourceUrl}";
    private const string AdminHelp =
        "Admin DM commands:\n"
        + "help — show this help\n"
        + "word — reveal today's word of the day\n"
        + "personality — show the current temperament and expiry\n"
        + "personality list — list the presets\n"
        + "personality <baseline|grouchy|furious> [duration] — switch temperament\n"
        + "personality reset — restore baseline and clear any expiry\n\n"
        + "Duration: whole minutes, hours, or days (e.g. 30m, 2h, 1d), up to 30 days; expires to baseline. "
        + "Without a duration, the personality stays active until changed. Example: personality furious 2h.";
    private static readonly Dictionary<ulong, DateTime> LastUserMessage = new();

    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (message.Channel is not IDMChannel)
            return;

        if (message.Author.IsBot)
            return;

        if (message.Author.Id == options.Value.AdminUserId)
        {
            if (message.Content.Trim().Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                await message.Channel.SendMessageAsync(AdminHelp);
            }
            else if (message.CleanContent.Equals("word", StringComparison.InvariantCultureIgnoreCase))
            {
                var wordOfTheDay = await wordOfTheDayGetter.GetWordOfTheDayAsync(cancellationToken);
                await message.Channel.SendMessageAsync(wordOfTheDay.Word);
            }
            else
            {
                var response = await personalityCommand.ExecuteAsync(message.Content, cancellationToken);
                if (response is not null)
                    await message.Channel.SendMessageAsync(response);
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
