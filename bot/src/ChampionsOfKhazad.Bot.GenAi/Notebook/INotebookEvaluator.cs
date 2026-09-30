namespace ChampionsOfKhazad.Bot.GenAi;

public interface INotebookEvaluator
{
    Task<NotebookAssessment> EvaluateAsync(NotebookNote candidate, IReadOnlyList<NotebookNote> existing, CancellationToken cancellationToken);
}
