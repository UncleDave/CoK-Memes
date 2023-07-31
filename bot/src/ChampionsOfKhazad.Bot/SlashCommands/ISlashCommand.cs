using Discord.WebSocket;

namespace ChampionsOfKhazad.Bot;

public interface ISlashCommand
{
    Task ExecuteAsync(SocketSlashCommand command);
}
