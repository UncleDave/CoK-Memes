namespace ChampionsOfKhazad.Bot.EventLoop;

public interface IEventLoopEvent
{
    string Name { get; }
    TimeSpan MeanTimeToHappen { get; }
    Task<bool> EligibleToFire();
    Task FireAsync(CancellationToken cancellationToken);
}
