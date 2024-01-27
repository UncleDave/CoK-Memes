namespace ChampionsOfKhazad.Bot;

public class NumberTriggerStrategy : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) => notification.Message.CleanContent.Count(char.IsDigit) >= 3;
}
