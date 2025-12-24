using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.EventLoop;

public class EventLoopService(IOptions<EventLoopOptions> options, IServiceProvider serviceProvider, ILogger<EventLoopService> logger)
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(options.Value.IntervalMinutes);
    private DateTimeOffset _lastRun = DateTimeOffset.Now;

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting Event Loop Service with interval {Interval} minutes", _interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;
            var deltaTime = now - _lastRun;

            await FireEventsAsync(deltaTime, stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task FireEventsAsync(TimeSpan deltaTime, CancellationToken cancellationToken)
    {
        logger.LogInformation("Firing events");

        _lastRun = DateTimeOffset.Now;

        using var scope = serviceProvider.CreateScope();
        var events = scope.ServiceProvider.GetServices<IEventLoopEvent>();

        foreach (var eventLoopEvent in events)
        {
            var eventName = eventLoopEvent.GetType().Name;
            var eligible = await eventLoopEvent.EligibleToFire();

            logger.LogInformation(
                "Checking event {EventLoopEvent} -> MeanTimeToHappen: {MeanTimeToHappen} | Eligible: {Eligible}",
                eventName,
                eventLoopEvent.MeanTimeToHappen,
                eligible
            );

            if (!eligible)
                continue;

            var probabilityToFire = deltaTime.TotalMilliseconds / eventLoopEvent.MeanTimeToHappen.TotalMilliseconds;
            var roll = Random.Shared.NextDouble();

            logger.LogInformation(
                "Rolling to fire event {EventLoopEvent} -> ProbabilityToFire: {ProbabilityToFire} | Roll: {Roll}",
                eventName,
                probabilityToFire * 100,
                roll * 100
            );

            if (roll < probabilityToFire)
            {
                await eventLoopEvent.FireAsync(cancellationToken);
            }
        }
    }
}
