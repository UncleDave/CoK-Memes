using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot.Tests;

internal sealed class MemoryGazetteIssueStore : IGazetteIssueStore
{
    public GazetteState State { get; private set; } = new();
    public int Saves { get; private set; }
    public bool FailWrites { get; set; }

    public Task<GazetteState> GetAsync(CancellationToken cancellationToken) => Task.FromResult(State);

    public Task<bool> TrySaveAsync(GazetteState state, CancellationToken cancellationToken)
    {
        if (FailWrites)
            throw new InvalidOperationException("Store writes unavailable.");
        if (state.Revision != State.Revision)
            return Task.FromResult(false);
        State = state with { Revision = state.Revision + 1 };
        Saves++;
        return Task.FromResult(true);
    }
}
