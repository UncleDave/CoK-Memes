namespace ChampionsOfKhazad.Bot;

public sealed class CooldownTracker<TKey>(TimeProvider clock)
    where TKey : notnull
{
    private readonly Lock _lock = new();
    private readonly Dictionary<TKey, DateTimeOffset> _lastAcquired = [];

    public bool IsOnCooldown(TKey key, TimeSpan cooldown)
    {
        lock (_lock)
            return _lastAcquired.TryGetValue(key, out var lastAcquired) && clock.GetUtcNow() - lastAcquired < cooldown;
    }

    public bool TryAcquire(TKey key, TimeSpan cooldown, bool refreshOnRejection = false)
    {
        lock (_lock)
        {
            var now = clock.GetUtcNow();
            var allowed = !_lastAcquired.TryGetValue(key, out var lastAcquired) || now - lastAcquired >= cooldown;
            if (allowed || refreshOnRejection)
                _lastAcquired[key] = now;
            return allowed;
        }
    }
}
