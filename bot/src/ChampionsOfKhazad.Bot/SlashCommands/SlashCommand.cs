using Discord;
using Discord.WebSocket;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public record SlashCommand(ApplicationCommandProperties Properties, Func<SocketSlashCommand, INotification> CreateNotification);
