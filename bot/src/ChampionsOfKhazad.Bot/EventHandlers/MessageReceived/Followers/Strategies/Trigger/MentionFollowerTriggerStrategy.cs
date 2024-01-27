namespace ChampionsOfKhazad.Bot;

public class MentionFollowerTriggerStrategy(ulong userId) : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) => notification.Message.MentionedUserIds.Contains(userId);
}
