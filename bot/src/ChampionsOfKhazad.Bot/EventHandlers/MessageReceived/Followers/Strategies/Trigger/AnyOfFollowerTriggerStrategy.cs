namespace ChampionsOfKhazad.Bot;

public class AnyOfFollowerTriggerStrategy(params IEnumerable<IFollowerTriggerStrategy> strategies) : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) => strategies.Any(x => x.ShouldTrigger(notification));
}
