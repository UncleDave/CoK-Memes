namespace ChampionsOfKhazad.Bot.GenAi;

public record NotebookAssessment(bool Accept, string Reason)
{
    public NotebookRejectionCategory RejectionCategory { get; init; }
}
