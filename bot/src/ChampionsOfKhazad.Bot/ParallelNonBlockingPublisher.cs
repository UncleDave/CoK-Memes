using MediatR;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public class ParallelNonBlockingPublisher : INotificationPublisher
{
    private readonly ILogger<ParallelNonBlockingPublisher> _logger;

    public ParallelNonBlockingPublisher(ILogger<ParallelNonBlockingPublisher> logger)
    {
        _logger = logger;
    }

    public Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        foreach (var handler in handlerExecutors)
        {
            Task.Run(
                async () =>
                {
                    try
                    {
                        await handler.HandlerCallback(notification, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(
                            e,
                            "Error handling notification {NotificationType} in handler {HandlerType}",
                            notification.GetType().Name,
                            handler.HandlerInstance.GetType().Name
                        );
                    }
                },
                cancellationToken
            );
        }

        return Task.CompletedTask;
    }
}
