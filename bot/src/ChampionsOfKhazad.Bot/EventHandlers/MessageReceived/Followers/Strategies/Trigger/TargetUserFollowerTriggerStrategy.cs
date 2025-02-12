namespace ChampionsOfKhazad.Bot;

public class TargetUserFollowerTriggerStrategy(params IEnumerable<ulong> userIds) : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) => userIds.Contains(notification.Message.Author.Id);
}
