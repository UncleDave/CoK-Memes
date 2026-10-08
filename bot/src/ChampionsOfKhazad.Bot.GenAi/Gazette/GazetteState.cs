namespace ChampionsOfKhazad.Bot.GenAi;

public sealed record GazetteState
{
    public string Id { get; init; } = "khazad-gazette";
    public long Revision { get; init; }
    public long LastReservedIssue { get; init; }
    public IReadOnlyList<GazettePublication> Publications { get; init; } = [];

    // Legacy audit data, retained for safe deserialization/round-trips. It no longer limits Gazette images.
    public IReadOnlyList<DateTime> IllustrationAttempts { get; init; } = [];
}
