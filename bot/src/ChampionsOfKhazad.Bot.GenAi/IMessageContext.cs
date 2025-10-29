namespace ChampionsOfKhazad.Bot.GenAi;

public interface IMessageContext
{
    ulong UserId { get; }
    string UserName { get; }
    Task Reply(string message);
}
