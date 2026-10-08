using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public interface IGazetteGateway
{
    GazetteDestination? GetDestination();

    Task<GazetteChatBatch> ReadRecentAsync(ulong destinationId, DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken);

    Task<bool> VerifyAsync(ulong destinationId, IReadOnlyList<NotebookSource> sources, CancellationToken cancellationToken);

    Task<ulong> PublishAsync(ulong destinationId, string edition, CancellationToken cancellationToken);
}
