using ChampionsOfKhazad.Bot.GenAi;
using Discord;

namespace ChampionsOfKhazad.Bot;

public interface ISpecialsGateway
{
    SpecialsDestination? GetDestination();
    string DestinationError { get; }
    string? GetPublicationError(ulong destinationId);
    Task<SpecialsChatBatch> ReadWeekAsync(ulong destinationId, DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotebookSource>> ResolveNamesAsync(IReadOnlyList<NotebookSource> sources, CancellationToken cancellationToken);
    Task<bool> VerifyAsync(ulong destinationId, IReadOnlyList<NotebookSource> sources, CancellationToken cancellationToken);
    Task<ulong> PublishAsync(ulong destinationId, Embed menu, CancellationToken cancellationToken);
}
