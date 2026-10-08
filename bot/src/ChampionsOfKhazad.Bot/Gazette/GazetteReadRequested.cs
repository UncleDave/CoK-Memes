using Discord;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public sealed record GazetteReadRequested(IComponentInteraction Interaction) : INotification;
