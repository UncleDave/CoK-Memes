namespace ChampionsOfKhazad.Bot.EventLoop;

public interface IEligibilityStrategy
{
    Task<bool> IsEligibleToFireAsync(CancellationToken cancellationToken);
    void OnFired();
}
