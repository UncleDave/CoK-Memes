using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class NotebookDirectMessageCommand(
    NotebookService notebook,
    TimeProvider clock,
    ILogger<NotebookDirectMessageCommand> logger,
    IOptions<NotebookObserverOptions>? observerOptions = null
)
{
    private readonly NotebookObserverOptions _observerOptions = observerOptions?.Value ?? new();
    private const string Help =
        "Notebook commands: `notebook`, `notebook list [page]`, `notebook history [page]`, `notebook show <id>`, `notebook discard <id>`, `notebook pause`, `notebook resume`.";

    public async Task<string?> ExecuteAsync(string content, CancellationToken cancellationToken)
    {
        var parts = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].Equals("notebook", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            return await ExecuteCommandAsync(parts, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Notebook admin command failed");
            return "Notebook command failed; its effect could not be confirmed. Do not assume writes are paused or a note was discarded. Check `notebook` when persistence is available.";
        }
    }

    private async Task<string> ExecuteCommandAsync(string[] parts, CancellationToken cancellationToken)
    {
        if (
            parts.Length == 2
            && (parts[1].Equals("pause", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("resume", StringComparison.OrdinalIgnoreCase))
        )
        {
            var paused = parts[1].Equals("pause", StringComparison.OrdinalIgnoreCase);
            await notebook.SetPausedAsync(paused, cancellationToken);
            return paused ? "Notebook writes paused. Existing notes remain searchable." : "Notebook writes resumed.";
        }
        if (parts.Length == 3 && parts[1].Equals("discard", StringComparison.OrdinalIgnoreCase))
            return await notebook.DiscardAsync(parts[2].ToLowerInvariant(), cancellationToken)
                ? "Note discarded. It will no longer appear in notebook searches. Its audit record and daily budget remain."
                : "No note with that ID was found.";

        var state = await notebook.GetAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (parts.Length == 1)
        {
            var evaluations = state.EvaluationAttempts.Count(attempt => attempt.AttemptedAtUtc > now.AddDays(-1));
            var attempts = state.WriteAttempts.Where(attempt => attempt.CompletedAtUtc > now.AddDays(-1)).ToArray();
            var outcomes = attempts
                .GroupBy(attempt => (attempt.Origin, attempt.Outcome, attempt.RejectionCategory))
                .OrderBy(group => group.Key.Origin)
                .ThenBy(group => group.Key.Outcome)
                .ThenBy(group => group.Key.RejectionCategory)
                .Select(group =>
                    $"{group.Key.Origin}/{group.Key.Outcome}{(group.Key.RejectionCategory is { } category ? $"/{category}" : "")}: {group.Count()}"
                );
            return $"Notebook writes: {(state.Paused ? "paused" : "enabled")}. {state.Notes.Count(note => note.IsActive(now))}/{NotebookService.MaximumActiveNotes} active notes. "
                + $"Expiry: {NotebookService.LifetimeDays} days. Rolling daily saved-note limits: {NotebookService.DailyGuildLimit}/guild, {NotebookService.DailyMemberLimit}/member. "
                + $"Evaluation budget used in the last 24 hours: {evaluations}/{NotebookService.DailyGuildEvaluationLimit} guild limit; {NotebookService.DailyMemberEvaluationLimit}/member (rejections and failures count).\n"
                + $"Recent completed proposals (last 24 hours, newest {NotebookService.MaximumDiagnosticAttempts} at most): {attempts.Length}. "
                + (attempts.Length == 0 ? "No diagnostic outcomes recorded." : string.Join(", ", outcomes))
                + $"\n{FormatObserver(state, now)}"
                + $"\n{Help}";
        }
        if (parts.Length == 3 && parts[1].Equals("show", StringComparison.OrdinalIgnoreCase))
        {
            var note = state.Notes.SingleOrDefault(note => note.Id.Equals(parts[2], StringComparison.OrdinalIgnoreCase));
            return note is null ? "No note with that ID was found." : NotebookReview.Format(note, now);
        }
        if (
            parts.Length is 2 or 3
            && (parts[1].Equals("list", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("history", StringComparison.OrdinalIgnoreCase))
        )
        {
            var page = 1;
            if (parts.Length == 3 && (!int.TryParse(parts[2], out page) || page is < 1 or > 100))
                return Help;
            var history = parts[1].Equals("history", StringComparison.OrdinalIgnoreCase);
            var notes = state.Notes.Where(note => history || note.IsActive(now)).OrderByDescending(note => note.CreatedAtUtc).ToArray();
            var rows = notes
                .Skip((page - 1) * 10)
                .Take(10)
                .Select(note => $"`{note.Id}` {NotebookReview.DisplayText(note.Subject, singleLine: true)} ({NotebookReview.Status(note, now)})");
            return $"Notebook {(history ? "audit history" : "active notes")} — page {page}/{Math.Max(1, (notes.Length + 9) / 10)}\n"
                + (rows.Any() ? string.Join('\n', rows) : "No notes on this page.")
                + "\nInspect with `notebook show <id>`.";
        }
        return Help;
    }

    private string FormatObserver(NotebookState state, DateTime now)
    {
        var observer = state.Observer;
        var discovery = observer.DiscoveryAttempts.Count(attempt => attempt > now.AddDays(-1));
        var reviews = state.EvaluationAttempts.Count(attempt =>
            attempt.Origin == NotebookOrigin.Background && attempt.AttemptedAtUtc > now.AddDays(-1)
        );
        var notes = state.Notes.Count(note => note.Origin == NotebookOrigin.Background && note.CreatedAtUtc > now.AddDays(-1));
        var latest = observer.Scans.MaxBy(scan => scan.CompletedAtUtc);
        var active = observer.ActiveScanId is not null && observer.LeaseExpiresAtUtc > now;
        return $"Background observer: {(!_observerOptions.Enabled ? "disabled" : state.Paused ? "paused" : "enabled")}. "
            + $"MTTH {_observerOptions.MeanTimeToHappenMinutes}m; cooldown {_observerOptions.CooldownMinutes}m; lookback {_observerOptions.LookbackMinutes}m. "
            + $"Rolling 24-hour usage: discovery {discovery}/{_observerOptions.DailyDiscoveryLimit}, reviews {reviews}/{_observerOptions.DailyReviewLimit}, saved notes {notes}/{_observerOptions.DailyNoteLimit} (reviews/notes share guild limits). "
            + $"{observer.Checkpoints.Count} channel checkpoints. Scan {(active ? "in progress" : "idle")}.\n"
            + (
                latest is null
                    ? "No completed observer scans recorded."
                    : $"Last observer scan: {latest.CompletedAtUtc:u}, {latest.Outcome}; messages {latest.Messages}, proposed {latest.Proposed}, attempted {latest.Attempted}, saved {latest.Saved}, read failures {latest.ReadFailures}."
            );
    }
}
