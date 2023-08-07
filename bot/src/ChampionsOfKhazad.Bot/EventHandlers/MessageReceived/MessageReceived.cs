using Discord;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public record MessageReceived(IUserMessage Message) : INotification;
