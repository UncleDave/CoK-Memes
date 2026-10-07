namespace ChampionsOfKhazad.Bot.GenAi;

public record NotebookEvaluationAttempt(ulong UserId, DateTime AttemptedAtUtc)
{
    public NotebookOrigin Origin { get; init; }
}
