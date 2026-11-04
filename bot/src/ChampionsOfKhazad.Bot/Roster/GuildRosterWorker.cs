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
            await RefreshAndDeliverAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), clock, stoppingToken);
        }
    }

    internal async Task RefreshAndDeliverAsync(CancellationToken cancellationToken)
    {
        if (!context.IsReady || context.BotContext is null)
            return;
        try
        {
            await service.RefreshAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RosterSourceUnavailableException)
        {
            // The Discord Serilog sink receives Error+, not normal launch-time guild absence.
            logger.LogInformation("Guild roster endpoint returned HTTP 404; collection will retry");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Guild roster refresh failed");
        }
        if (!context.IsReady || context.BotContext is null)
            return;
        try
        {
            await service.DeliverAsync(sender, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Guild milestone delivery failed; pending notices retained");
        }
    }
}
