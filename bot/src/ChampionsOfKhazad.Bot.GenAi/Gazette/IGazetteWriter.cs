namespace ChampionsOfKhazad.Bot.GenAi;

public interface IGazetteWriter
{
    Task<GazetteEdition> WriteAsync(
        IReadOnlyList<NotebookSource> sources,
        DateTimeOffset since,
        DateTimeOffset until,
        IReadOnlyList<GazettePublishedEdition> previousEditions,
        CancellationToken cancellationToken
    );
}
