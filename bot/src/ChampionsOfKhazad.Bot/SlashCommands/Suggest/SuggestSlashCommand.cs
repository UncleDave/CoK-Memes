using Discord;
using MediatR;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class SuggestSlashCommand : INotificationHandler<SuggestSlashCommandExecuted>
{
    private readonly SuggestSlashCommandOptions _options;
    private readonly BotContext _botContext;

    public SuggestSlashCommand(IOptions<SuggestSlashCommandOptions> options, BotContext botContext)
    {
        _options = options.Value;
        _botContext = botContext;
    }

    public async Task Handle(SuggestSlashCommandExecuted notification, CancellationToken cancellationToken)
    {
        await notification.Command.RespondAsync("Thanks for your suggestion!", ephemeral: true);

        var targetUser = await _botContext.Client.GetUserAsync(_options.UserId);
        var suggestion = notification.Command.Data.Options.Single(x => x.Name == "suggestion").Value.ToString();

        await targetUser.SendMessageAsync(
            $"Suggestion from {notification.Command.User.GlobalName ?? notification.Command.User.Username}: {suggestion}"
        );
    }
}
