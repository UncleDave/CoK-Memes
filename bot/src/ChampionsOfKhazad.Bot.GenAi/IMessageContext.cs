namespace ChampionsOfKhazad.Bot.GenAi;

public interface IMessageContext
{
    ulong UserId { get; }
    Task Reply(string message);
}
