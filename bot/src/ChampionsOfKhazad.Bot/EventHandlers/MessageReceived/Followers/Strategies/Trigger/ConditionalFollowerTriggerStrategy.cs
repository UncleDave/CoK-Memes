namespace ChampionsOfKhazad.Bot;

public class ConditionalFollowerTriggerStrategy(
    IFollowerTriggerStrategy condition,
    IFollowerTriggerStrategy whenTrue,
    IFollowerTriggerStrategy whenFalse
) : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) =>
        (condition.ShouldTrigger(notification) ? whenTrue : whenFalse).ShouldTrigger(notification);
}
