namespace ChampionsOfKhazad.Bot.GenAi;

public interface INotebookStore
{
    Task<NotebookState> GetAsync(CancellationToken cancellationToken);

    // Atomically replace only when the persisted revision still matches state.Revision; increment Revision on success.
    // Callers advance ReviewRevision for note additions, discards and write-control changes, not quota/delivery updates.
    Task<bool> TrySaveAsync(NotebookState state, CancellationToken cancellationToken);
}
