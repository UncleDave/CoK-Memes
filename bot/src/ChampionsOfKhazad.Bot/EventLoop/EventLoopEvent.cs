namespace ChampionsOfKhazad.Bot.EventLoop;

public abstract class EventLoopEvent(TimeSpan meanTimeToHappen) : IEventLoopEvent
{
    public TimeSpan MeanTimeToHappen { get; } = meanTimeToHappen;

    public abstract Task<bool> EligibleToFire();

    public abstract Task FireAsync(CancellationToken cancellationToken);
}
