using Discord;
using Discord.WebSocket;

namespace ChampionsOfKhazad.Bot;

public interface IReactionAddedEventHandler : IEventHandler
{
    Task HandleReactionAsync(SocketReaction reaction);
}
