namespace ChampionsOfKhazad.Bot.EventLoop;

public interface IEventLoopEvent
{
    string Name { get; }
    TimeSpan MeanTimeToHappen { get; }
    Task<bool> EligibleToFire(CancellationToken cancellationToken);
    Task FireAsync(CancellationToken cancellationToken);
}
