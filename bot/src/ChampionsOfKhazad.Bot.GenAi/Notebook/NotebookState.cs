namespace ChampionsOfKhazad.Bot.GenAi;

public record NotebookState
{
    public string Id { get; init; } = "lorekeeper";
    public long Revision { get; init; }

    // Changes to notes or write controls invalidate reviews; quota-only updates do not.
    public long ReviewRevision { get; init; }
    public bool Paused { get; init; }
    public IReadOnlyList<NotebookNote> Notes { get; init; } = [];
    public IReadOnlyList<NotebookEvaluationAttempt> EvaluationAttempts { get; init; } = [];
    public IReadOnlyList<NotebookWriteAttempt> WriteAttempts { get; init; } = [];
    public NotebookObserverState Observer { get; init; } = new();
}
