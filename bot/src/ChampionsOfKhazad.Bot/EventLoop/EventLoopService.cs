using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.EventLoop;

public class EventLoopService(
    IOptions<EventLoopOptions> options,
    IServiceProvider serviceProvider,
    ILogger<EventLoopService> logger,
    BotContextProvider contextProvider,
    TimeProvider clock
) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(options.Value.IntervalMinutes);
    private DateTimeOffset _lastRun = clock.GetUtcNow();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Event Loop Service with interval {Interval} minutes", _interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_interval, clock, stoppingToken);

            var now = clock.GetUtcNow();
            var deltaTime = now - _lastRun;

            await FireEventsAsync(deltaTime, stoppingToken);
        }
    }

    internal async Task FireEventsAsync(TimeSpan deltaTime, CancellationToken cancellationToken)
    {
        _lastRun = clock.GetUtcNow();
        if (!contextProvider.IsReady)
            return;
        logger.LogInformation("Firing events");

        await using var scope = serviceProvider.CreateAsyncScope();
        var events = scope.ServiceProvider.GetServices<IEventLoopEvent>();

        foreach (var eventLoopEvent in events)
        {
            try
            {
                var eligible = await eventLoopEvent.EligibleToFire(cancellationToken);

                logger.LogInformation(
                    "Checking event {EventLoopEvent} -> MeanTimeToHappen: {MeanTimeToHappen} | Eligible: {Eligible}",
                    eventLoopEvent.Name,
                    eventLoopEvent.MeanTimeToHappen,
                    eligible
                );

                if (!eligible)
                    continue;

                var probabilityToFire = deltaTime.TotalMilliseconds / eventLoopEvent.MeanTimeToHappen.TotalMilliseconds;
                var roll = Random.Shared.NextDouble();

                logger.LogInformation(
                    "Rolling to fire event {EventLoopEvent} -> ProbabilityToFire: {ProbabilityToFire} | Roll: {Roll}",
                    eventLoopEvent.Name,
                    probabilityToFire * 100,
                    roll * 100
                );

                if (roll < probabilityToFire)
                {
                    logger.LogInformation("Firing event {EventLoopEvent}", eventLoopEvent.Name);
                    await eventLoopEvent.FireAsync(cancellationToken);
                    logger.LogInformation("Event {EventLoopEvent} completed", eventLoopEvent.Name);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Event {EventLoopEvent} failed", eventLoopEvent.Name);
            }
        }
    }
}
