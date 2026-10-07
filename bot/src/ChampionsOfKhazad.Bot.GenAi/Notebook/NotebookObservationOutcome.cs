namespace ChampionsOfKhazad.Bot.GenAi;

public enum NotebookObservationOutcome
{
    Completed,
    NoMessages,
    ReadFailed,
    DiscoveryFailed,
    Paused,
    BudgetExhausted,
    Interrupted,
    TimedOut,
    Cancelled,
    Failed,
}
