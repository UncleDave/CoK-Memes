using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChampionsOfKhazad.Bot;

public static class EventHandlerFactory
{
    public static IEventHandler CreateEventHandler<T>(
        IServiceProvider serviceProvider,
        IConfiguration config
    )
        where T : IEventHandler
    {
        var eventHandler = ActivatorUtilities.CreateInstance<T>(serviceProvider);

        return ApplyChannelSpecificDecorator(eventHandler, config);
    }

    private static IEventHandler ApplyChannelSpecificDecorator(
        IEventHandler eventHandler,
        IConfiguration config
    )
    {
        var channelId = config.GetValue<ulong?>("ChannelId");

        return
            channelId is not null
            && eventHandler is IMessageReceivedEventHandler messageReceivedEventHandler
            ? new ChannelSpecificEventHandler(messageReceivedEventHandler, channelId.Value)
            : eventHandler;
    }
}
