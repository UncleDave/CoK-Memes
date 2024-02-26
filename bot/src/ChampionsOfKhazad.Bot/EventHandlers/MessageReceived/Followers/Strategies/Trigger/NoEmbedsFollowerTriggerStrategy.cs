namespace ChampionsOfKhazad.Bot;

public class NoEmbedsFollowerTriggerStrategy : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) => notification.Message.Embeds.Count == 0;
}
