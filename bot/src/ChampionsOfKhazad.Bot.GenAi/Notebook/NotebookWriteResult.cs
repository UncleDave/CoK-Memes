namespace ChampionsOfKhazad.Bot.GenAi;

public record NotebookWriteResult(string Message, NotebookWriteOutcome Outcome, NotebookRejectionCategory? RejectionCategory = null);
