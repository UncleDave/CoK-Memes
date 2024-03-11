using Discord.WebSocket;

namespace ChampionsOfKhazad.Bot;

public record SummariseSlashCommandExecuted(SocketSlashCommand Command) : SlashCommandExecuted(Command);
