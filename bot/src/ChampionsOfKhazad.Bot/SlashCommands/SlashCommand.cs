using Discord;
using Discord.WebSocket;
using MediatR;

namespace ChampionsOfKhazad.Bot;

// The gateway acknowledges before enqueueing; notification handlers must not send another initial response/defer.
public record SlashCommand(
    ApplicationCommandProperties Properties,
    Func<SocketSlashCommand, INotification> CreateNotification,
    Func<ISlashCommandInteraction, Task> AcknowledgeAsync
);
