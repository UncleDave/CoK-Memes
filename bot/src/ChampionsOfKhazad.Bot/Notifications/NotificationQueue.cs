using System.Threading.Channels;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class NotificationQueue
{
    private readonly Channel<INotification> _channel;
    private readonly ILogger<NotificationQueue> _logger;

    public NotificationQueue(IOptions<NotificationQueueOptions> options, ILogger<NotificationQueue> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<INotification>(
            new BoundedChannelOptions(options.Value.Capacity)
            {
                // TryWrite rejects a full queue in Wait mode; drop modes would silently accept/discard work.
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false,
            }
        );
    }

    // This is deliberately synchronous: gateway callbacks can submit work, never await its execution.
    public bool TryEnqueue(INotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (_channel.Writer.TryWrite(notification))
            return true;

        _logger.LogWarning("Rejected notification {NotificationType}: the queue is full or stopping", notification.GetType().Name);
        return false;
    }

    internal IAsyncEnumerable<INotification> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);

    internal void Complete() => _channel.Writer.TryComplete();
}
