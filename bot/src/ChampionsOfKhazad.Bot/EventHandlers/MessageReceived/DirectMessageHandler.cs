using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;
using Discord;
using MediatR;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class DirectMessageHandler(
    IOptions<DirectMessageHandlerOptions> options,
    IGetTheWordOfTheDay wordOfTheDayGetter,
    PersonalityDirectMessageCommand personalityCommand,
    NotebookDirectMessageCommand notebookCommand,
    CooldownTracker<ulong> cooldowns,
    LoreDirectMessageCommand loreCommand
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
        + "notebook — status and notebook commands\n"
        + "notebook list/history [page] — review active notes or audit history\n"
        + "notebook show/discard <id> — inspect or remove a note\n"
        + "notebook pause/resume — stop or restart new notes\n\n"
        + "lore — DM lore editor help; explicit add/update requests save directly\n"
        + "lore list/show/history — browse entries and revisions\n"
        + "lore undo <name> (or undo) — reverse the latest edit\n"
        + "lore confirm <token> / lore cancel — approve or cancel a destructive proposal\n"
        + "lore reset — clear the short-lived editor conversation\n\n"
        + "Duration: whole minutes, hours, or days (e.g. 30m, 2h, 1d), up to 30 days; expires to baseline. "
        + "Without a duration, the personality stays active until changed. Example: personality furious 2h.\n\n"
        + "In guild chat: @Lorekeeper you've had a stroke. — cut that channel's conversation context here (admin only; confirmed with 🧠).";

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
                var response =
                    await personalityCommand.ExecuteAsync(message.Content, cancellationToken)
                    ?? await notebookCommand.ExecuteAsync(message.Content, cancellationToken)
                    ?? await loreCommand.ExecuteAsync(message.Author.Id, message.Content, cancellationToken);
                if (response is not null)
                    await message.Channel.SendMessageInChunksAsync(response, cancellationToken);
            }

            return;
        }

        if (!cooldowns.TryAcquire(message.Author.Id, TimeSpan.FromMinutes(5), refreshOnRejection: true))
            return;

        await message.Channel.SendMessageAsync(Message);
    }

    public override string ToString() => nameof(DirectMessageHandler);
}
