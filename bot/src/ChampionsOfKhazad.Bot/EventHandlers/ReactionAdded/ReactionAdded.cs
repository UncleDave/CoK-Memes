using Discord.WebSocket;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public record ReactionAdded(SocketReaction Reaction) : INotification;
