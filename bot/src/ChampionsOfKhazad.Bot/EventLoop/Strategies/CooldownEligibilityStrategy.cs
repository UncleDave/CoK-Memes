namespace ChampionsOfKhazad.Bot.EventLoop;

public class CooldownEligibilityStrategy(string key, TimeSpan cooldown) : IEligibilityStrategy
{
    private static readonly Dictionary<string, DateTimeOffset> LastFired = new();

    public Task<bool> IsEligibleToFireAsync(CancellationToken cancellationToken)
    {
        var hasPreviouslyFired = LastFired.TryGetValue(key, out var lastFired);

        return Task.FromResult(!hasPreviouslyFired || DateTimeOffset.Now - lastFired >= cooldown);
    }

    public void OnFired()
    {
        LastFired[key] = DateTimeOffset.Now;
    }
}
