using Discord.WebSocket;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public record SlashCommandExecuted(SocketSlashCommand Command) : INotification;
