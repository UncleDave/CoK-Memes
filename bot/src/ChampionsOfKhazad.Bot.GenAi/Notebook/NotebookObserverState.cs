namespace ChampionsOfKhazad.Bot.GenAi;

public record NotebookObserverState
{
    public string? ActiveScanId { get; init; }
    public DateTime? LeaseExpiresAtUtc { get; init; }
    public DateTime? LastStartedAtUtc { get; init; }
    public DateTime? LastCompletedAtUtc { get; init; }
    public IReadOnlyList<NotebookChannelAttempt> ChannelAttempts { get; init; } = [];
    public IReadOnlyList<NotebookChannelCheckpoint> Checkpoints { get; init; } = [];
    public IReadOnlyList<DateTime> DiscoveryAttempts { get; init; } = [];
    public IReadOnlyList<NotebookObservationScan> Scans { get; init; } = [];
}
