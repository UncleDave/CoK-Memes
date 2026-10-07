namespace ChampionsOfKhazad.Bot.GenAi;

public enum NotebookWriteOutcome
{
    Saved,
    InvalidInput,
    InvalidSources,
    Duplicate,
    WriteBlocked,
    ReviewRejected,
    CommitRejected,
    ReviewDeliveryFailed,
    TimedOut,
    Cancelled,
    Failed,
}
