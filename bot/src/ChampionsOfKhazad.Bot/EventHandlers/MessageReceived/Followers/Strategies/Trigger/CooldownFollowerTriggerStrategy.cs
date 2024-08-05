using System.Collections.Concurrent;

namespace ChampionsOfKhazad.Bot;

public class CooldownFollowerTriggerStrategy(string key, TimeSpan cooldown) : IFollowerTriggerStrategy
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastTriggered = new();

    public bool ShouldTrigger(MessageReceived notification)
    {
        var hasPreviouslyTriggered = LastTriggered.TryGetValue(key, out var lastTriggered);

        if (hasPreviouslyTriggered && DateTimeOffset.UtcNow - lastTriggered < cooldown)
            return false;

        LastTriggered[key] = DateTimeOffset.UtcNow;
        return true;
    }
}
