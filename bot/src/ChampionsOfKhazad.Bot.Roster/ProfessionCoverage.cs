namespace ChampionsOfKhazad.Bot.Roster;

public record ProfessionCoverage
{
    public bool NameFiltered { get; init; }
    public int TotalCharacters { get; init; }
    public int AffectedCharacters { get; init; }
    public int UnavailableCharacters { get; init; }
    public int PartialCharacters { get; init; }
    public int FailedRefreshCharacters { get; init; }
    public int UnknownRankCharacters { get; init; }
}
