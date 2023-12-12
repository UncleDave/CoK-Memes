namespace ChampionsOfKhazad.Bot;

public class NumberTriggerStrategy : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) => double.TryParse(notification.Message.CleanContent, out _);
}
