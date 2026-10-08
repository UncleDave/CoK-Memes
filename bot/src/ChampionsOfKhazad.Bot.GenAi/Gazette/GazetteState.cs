namespace ChampionsOfKhazad.Bot.GenAi;

public sealed record GazetteState
{
    public string Id { get; init; } = "khazad-gazette";
    public long Revision { get; init; }
    public long LastReservedIssue { get; init; }
    public IReadOnlyList<GazettePublication> Publications { get; init; } = [];
    public IReadOnlyList<DateTime> IllustrationAttempts { get; init; } = [];
}
