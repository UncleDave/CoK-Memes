using Discord.WebSocket;

namespace ChampionsOfKhazad.Bot;

public interface IReactionAddedEventHandler
{
    Task HandleReactionAsync(SocketReaction reaction);
}
