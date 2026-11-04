using ChampionsOfKhazad.Bot.Roster;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public class GuildRosterWorker(
    GuildRosterService service,
    IRosterNotificationSender sender,
    BotContextProvider context,
    TimeProvider clock,
    ILogger<GuildRosterWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (context.IsReady)
            {
                try
                {
                    await service.RefreshAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Guild roster refresh failed");
                }
                try
                {
                    await service.DeliverAsync(sender, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Guild milestone delivery failed; pending notices retained");
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(10), clock, stoppingToken);
        }
    }
}
