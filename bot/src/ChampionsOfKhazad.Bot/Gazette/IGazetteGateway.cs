using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public interface IGazetteGateway
{
    string DestinationError { get; }

    GazetteDestination? GetDestination();

    string? GetPublicationError(ulong destinationId);

    Task<GazetteChatBatch> ReadRecentAsync(ulong destinationId, DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken);

    Task<bool> VerifyAsync(ulong destinationId, IReadOnlyList<NotebookSource> sources, CancellationToken cancellationToken);

    Task<ulong> PublishAsync(ulong destinationId, string edition, GazettePage page, CancellationToken cancellationToken);
}
