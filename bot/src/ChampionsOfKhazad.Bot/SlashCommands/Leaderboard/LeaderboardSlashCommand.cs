using ChampionsOfKhazad.Bot.Roster;
using Discord;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public class LeaderboardSlashCommand(ILogger<LeaderboardSlashCommand> logger, GuildRosterQueries? roster = null)
    : INotificationHandler<LeaderboardSlashCommandExecuted>
{
    public async Task Handle(LeaderboardSlashCommandExecuted notification, CancellationToken cancellationToken)
    {
        var command = notification.Command;
        try
        {
            var pageValue = command.Data.Options.FirstOrDefault(option => option.Name == "page")?.Value as long? ?? 1;
            var profession = command.Data.Options.FirstOrDefault(option => option.Name == "profession")?.Value as string;
            var page =
                roster is null || command.GuildId is not { } guildId ? new RosterPage { Status = "unavailable" }
                : pageValue is < 1 or > int.MaxValue ? new RosterPage { Status = "invalid_query" }
                : await roster.QueryAsync(guildId, (int)pageValue, profession, cancellationToken: cancellationToken);
            foreach (var text in RosterText.Leaderboard(page))
                await command.FollowupAsync(
                    text,
                    allowedMentions: AllowedMentions.None,
                    options: new RequestOptions { CancelToken = cancellationToken }
                );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Leaderboard lookup failed");
            await command.FollowupAsync("The roster lookup failed. Try again later.", allowedMentions: AllowedMentions.None);
        }
    }
}
