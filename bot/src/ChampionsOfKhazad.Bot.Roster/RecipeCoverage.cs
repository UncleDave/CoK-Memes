namespace ChampionsOfKhazad.Bot.Roster;

public record RecipeCoverage
{
    public int TotalCharacters { get; init; }
    public int AffectedCharacters { get; init; }
    public int UnavailableCharacters { get; init; }
    public int PartialCharacters { get; init; }
    public int FailedRefreshCharacters { get; init; }
}
