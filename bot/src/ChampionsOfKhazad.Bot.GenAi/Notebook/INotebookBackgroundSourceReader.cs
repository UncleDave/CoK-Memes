namespace ChampionsOfKhazad.Bot.GenAi;

public interface INotebookBackgroundSourceReader
{
    IReadOnlyList<ulong> GetChannelIds();

    Task<NotebookObservationBatch?> ReadBatchAsync(ulong channelId, ulong afterMessageId, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotebookSource>?> ReadSourcesAsync(IReadOnlyList<string> urls, CancellationToken cancellationToken);
}
