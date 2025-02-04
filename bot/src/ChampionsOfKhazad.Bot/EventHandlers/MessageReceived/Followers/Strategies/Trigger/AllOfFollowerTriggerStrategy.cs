namespace ChampionsOfKhazad.Bot;

public class AllOfFollowerTriggerStrategy(params IEnumerable<IFollowerTriggerStrategy> strategies) : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) => strategies.All(x => x.ShouldTrigger(notification));
}
