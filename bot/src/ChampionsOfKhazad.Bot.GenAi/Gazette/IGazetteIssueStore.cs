namespace ChampionsOfKhazad.Bot.GenAi;

public interface IGazetteIssueStore
{
    Task<GazetteState> GetAsync(CancellationToken cancellationToken);
    Task<bool> TrySaveAsync(GazetteState state, CancellationToken cancellationToken);
}
