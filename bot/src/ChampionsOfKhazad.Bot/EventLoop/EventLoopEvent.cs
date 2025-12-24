namespace ChampionsOfKhazad.Bot.EventLoop;

public abstract class EventLoopEvent(TimeSpan meanTimeToHappen, string name) : IEventLoopEvent
{
    public string Name => name;

    public TimeSpan MeanTimeToHappen { get; } = meanTimeToHappen;

    public abstract Task<bool> EligibleToFire();

    public abstract Task FireAsync(CancellationToken cancellationToken);
}
