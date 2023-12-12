namespace ChampionsOfKhazad.Bot;

public interface IFollowerTriggerStrategy
{
    bool ShouldTrigger(MessageReceived notification);
}
