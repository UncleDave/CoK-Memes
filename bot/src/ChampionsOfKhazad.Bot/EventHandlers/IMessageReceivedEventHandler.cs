using Discord;

namespace ChampionsOfKhazad.Bot;

public interface IMessageReceivedEventHandler
{
    Task HandleMessageAsync(IUserMessage message);
}
