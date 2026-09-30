namespace ChampionsOfKhazad.Bot.GenAi;

public interface INotebookReviewer
{
    Task<bool> NotifyAsync(NotebookNote note, CancellationToken cancellationToken);
}
