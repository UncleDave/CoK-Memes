namespace ChampionsOfKhazad.Bot;

public class NumberTriggerStrategy : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) => notification.Message.CleanContent.Any(char.IsDigit);
}
