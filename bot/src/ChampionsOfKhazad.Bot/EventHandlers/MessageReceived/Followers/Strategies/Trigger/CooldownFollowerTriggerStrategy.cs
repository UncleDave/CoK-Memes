namespace ChampionsOfKhazad.Bot;

public class CooldownFollowerTriggerStrategy(string key, TimeSpan cooldown, CooldownTracker<string> cooldowns) : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) => cooldowns.TryAcquire(key, cooldown);
}
