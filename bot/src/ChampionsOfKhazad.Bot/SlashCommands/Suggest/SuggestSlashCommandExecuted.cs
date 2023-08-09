using Discord.WebSocket;

namespace ChampionsOfKhazad.Bot;

public record SuggestSlashCommandExecuted(SocketSlashCommand Command) : SlashCommandExecuted(Command);
