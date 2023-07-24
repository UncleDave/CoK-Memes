using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChampionsOfKhazad.Bot;

public static class EventHandlerFactory
{
    public static IMessageReceivedEventHandler CreateMessageReceivedEventHandler<T>(
        IServiceProvider serviceProvider,
        IConfiguration config
    )
        where T : IMessageReceivedEventHandler
    {
        var eventHandler = ActivatorUtilities.CreateInstance<T>(serviceProvider);
        var channelId = config.GetValue<ulong?>("ChannelId");

        return channelId is not null
            ? new ChannelSpecificEventHandler(eventHandler, channelId.Value)
            : eventHandler;
    }
}
