namespace ChampionsOfKhazad.Bot.EventLoop;

public class AllOfEligibilityStrategy(params IEnumerable<IEligibilityStrategy> strategies) : IEligibilityStrategy
{
    public async Task<bool> IsEligibleToFireAsync(CancellationToken cancellationToken) =>
        (await Task.WhenAll(strategies.Select(x => x.IsEligibleToFireAsync(cancellationToken)))).All(x => x);

    public void OnFired()
    {
        foreach (var strategy in strategies)
        {
            strategy.OnFired();
        }
    }
}
