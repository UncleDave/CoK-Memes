namespace ChampionsOfKhazad.Bot.Roster;

public record RosterPage
{
    public string Status { get; init; } = "ok";
    public bool IsMock { get; init; }
    public string GuildName { get; init; } = "";
    public DateTime? ObservedAtUtc { get; init; }
    public DateTime? CheckedAtUtc { get; init; }
    public bool LastRefreshFailed { get; init; }
    public bool IncompleteProfessionCoverage { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public string? Profession { get; init; }
    public string[] ProfessionChoices { get; init; } = [];
    public RosterEntry[] Rows { get; init; } = [];
}
