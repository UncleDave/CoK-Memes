using Discord.WebSocket;

namespace ChampionsOfKhazad.Bot;

public record LeaderboardSlashCommandExecuted(SocketSlashCommand Command) : SlashCommandExecuted(Command);
