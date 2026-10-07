namespace ChampionsOfKhazad.Bot.GenAi;

public record NotebookObservationScan(
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    NotebookObservationOutcome Outcome,
    int Messages,
    int Proposed,
    int Attempted,
    int Saved,
    int ReadFailures
);
