namespace ChampionsOfKhazad.Bot.EventLoop;

public abstract class EventLoopEvent(TimeSpan meanTimeToHappen, string name, IEligibilityStrategy eligibilityStrategy) : IEventLoopEvent
{
    public TimeSpan MeanTimeToHappen { get; } = meanTimeToHappen;

    public string Name => name;

    public virtual Task<bool> EligibleToFire(CancellationToken cancellationToken) => eligibilityStrategy.IsEligibleToFireAsync(cancellationToken);

    public virtual Task FireAsync(CancellationToken cancellationToken)
    {
        eligibilityStrategy.OnFired();
        return Task.CompletedTask;
    }
}
