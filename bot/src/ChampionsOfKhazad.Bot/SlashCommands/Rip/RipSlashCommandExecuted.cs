using Discord.WebSocket;

namespace ChampionsOfKhazad.Bot;

public record RipSlashCommandExecuted(SocketSlashCommand Command) : SlashCommandExecuted(Command);
