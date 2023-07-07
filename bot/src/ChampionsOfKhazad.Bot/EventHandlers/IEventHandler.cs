namespace ChampionsOfKhazad.Bot;

// TODO: Bot started status message / git hash

public interface IEventHandler
{
    Task StartAsync(BotContext context) => Task.CompletedTask;
}
