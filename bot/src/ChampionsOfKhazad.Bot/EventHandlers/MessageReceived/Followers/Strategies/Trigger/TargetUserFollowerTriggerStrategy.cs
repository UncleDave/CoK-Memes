namespace ChampionsOfKhazad.Bot;

public class TargetUserFollowerTriggerStrategy(ulong userId) : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) => notification.Message.Author.Id == userId;
}
