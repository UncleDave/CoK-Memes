using Discord;

namespace ChampionsOfKhazad.Bot;

// TODO: Fix ToString.

public class ChannelSpecificEventHandler : IMessageReceivedEventHandler
{
    private readonly IMessageReceivedEventHandler _eventHandler;
    private readonly ulong _channelId;

    public ChannelSpecificEventHandler(IMessageReceivedEventHandler eventHandler, ulong channelId)
    {
        _eventHandler = eventHandler;
        _channelId = channelId;
    }

    public async Task HandleMessageAsync(IUserMessage message)
    {
        if (message.Channel.Id == _channelId || (message.Channel is ITextChannel textChannel && textChannel.CategoryId == _channelId))
            await _eventHandler.HandleMessageAsync(message);
    }
}
