namespace ChampionsOfKhazad.Bot;

public class NumberFollowerTriggerStrategy(ushort minimumCount = 1) : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) => notification.Message.CleanContent.Count(char.IsDigit) >= minimumCount;
}
