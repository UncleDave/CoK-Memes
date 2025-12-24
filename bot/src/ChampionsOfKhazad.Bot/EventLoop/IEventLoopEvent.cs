namespace ChampionsOfKhazad.Bot.EventLoop;

public interface IEventLoopEvent
{
    TimeSpan MeanTimeToHappen { get; }
    Task<bool> EligibleToFire();
    Task FireAsync(CancellationToken cancellationToken);
}
