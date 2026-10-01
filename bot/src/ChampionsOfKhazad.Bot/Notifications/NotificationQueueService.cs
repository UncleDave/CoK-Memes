using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class NotificationQueueService(
    NotificationQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationQueueOptions> options,
    ILogger<NotificationQueueService> logger
) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        // Each worker is scheduled independently, including handlers' synchronous work before their first await.
        Task.WhenAll(
            Enumerable
                .Range(0, options.Value.WorkerCount)
                .Select(_ => Task.Run(() => ProcessNotificationsAsync(stoppingToken), CancellationToken.None))
        );

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Complete();

        try
        {
            // Do not cancel the processing token until accepted work has drained or the host's deadline expires.
            if (ExecuteTask is not null)
                await ExecuteTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Notification queue did not drain before the shutdown deadline; cancelling remaining work");
        }
        finally
        {
            await base.StopAsync(cancellationToken);
        }
    }

    private async Task ProcessNotificationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var notification in queue.ReadAllAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
                    await publisher.Publish(notification, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Error publishing notification {NotificationType}", notification.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected when the host's shutdown deadline expires.
        }
    }
}
