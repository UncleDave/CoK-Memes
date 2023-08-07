using Discord.WebSocket;

namespace ChampionsOfKhazad.Bot;

public record RaidsSlashCommandExecuted(SocketSlashCommand Command) : SlashCommandExecuted(Command);
