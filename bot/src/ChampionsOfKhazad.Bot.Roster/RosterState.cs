namespace ChampionsOfKhazad.Bot.Roster;

public record RosterState
{
    public required string Id { get; init; }
    public long Revision { get; init; }
    public string SourceId { get; init; } = "";
    public bool IsMock { get; init; }
    public string GuildName { get; init; } = "";
    public string? SourceGuildId { get; init; }
    public DateTime? ObservedAtUtc { get; init; }
    public DateTime? CheckedAtUtc { get; init; }
    public DateTime? LastSucceededAtUtc { get; init; }
    public bool LastRefreshFailed { get; init; }
    public RosterCharacter[] Characters { get; init; } = [];
    public RosterProgress[] Progress { get; init; } = [];
}
