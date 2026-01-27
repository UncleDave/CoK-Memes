using Discord.WebSocket;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public record UserLeft(SocketUser User) : INotification;
