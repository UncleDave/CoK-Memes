namespace ChampionsOfKhazad.Bot.GenAi;

public interface INotebookDiscoverer
{
    Task<IReadOnlyList<NotebookProposal>> DiscoverAsync(
        IReadOnlyList<NotebookSource> sources,
        int maximumCandidates,
        CancellationToken cancellationToken
    );
}
