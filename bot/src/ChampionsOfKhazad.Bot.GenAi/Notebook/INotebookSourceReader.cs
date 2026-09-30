namespace ChampionsOfKhazad.Bot.GenAi;

public interface INotebookSourceReader
{
    // Filter guild/channel access without network reads. Message existence/content must still be verified afterwards.
    IReadOnlySet<string> GetAccessibleSourceUrls(IReadOnlyList<string> urls, IMessageContext messageContext);

    // Return complete sanitized text, never a truncated excerpt. Reject oversized, non-human or inaccessible sources.
    Task<IReadOnlyList<NotebookSource>?> ReadSourcesAsync(
        IReadOnlyList<string> urls,
        IMessageContext messageContext,
        CancellationToken cancellationToken
    );
}
