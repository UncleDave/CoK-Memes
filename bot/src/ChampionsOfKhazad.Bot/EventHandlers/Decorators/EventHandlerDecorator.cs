namespace ChampionsOfKhazad.Bot;

public abstract class EventHandlerDecorator : IEventHandler
{
    private readonly IEventHandler _eventHandler;

    protected EventHandlerDecorator(IEventHandler eventHandler)
    {
        _eventHandler = eventHandler;
    }

    public Task StartAsync(BotContext context) => _eventHandler.StartAsync(context);
}
