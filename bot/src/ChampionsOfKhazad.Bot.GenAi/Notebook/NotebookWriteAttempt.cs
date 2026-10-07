namespace ChampionsOfKhazad.Bot.GenAi;

public record NotebookWriteAttempt(DateTime CompletedAtUtc, NotebookWriteOutcome Outcome, NotebookRejectionCategory? RejectionCategory = null)
{
    public NotebookOrigin Origin { get; init; }
}
