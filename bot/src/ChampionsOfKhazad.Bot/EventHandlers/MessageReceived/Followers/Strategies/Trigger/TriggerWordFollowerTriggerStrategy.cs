namespace ChampionsOfKhazad.Bot;

public class TriggerWordFollowerTriggerStrategy(params IEnumerable<string> triggerWords) : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) =>
        triggerWords.Any(x => notification.Message.CleanContent.Contains(x, StringComparison.InvariantCultureIgnoreCase));
}
