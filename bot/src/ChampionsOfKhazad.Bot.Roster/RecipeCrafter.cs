namespace ChampionsOfKhazad.Bot.Roster;

public record RecipeCrafter
{
    public required string FullName { get; init; }
    public string? Id { get; init; }
    public required string ClassName { get; init; }
    public int Level { get; init; }
    public required string Profession { get; init; }
    public required string ProfessionId { get; init; }
    public string Tier { get; init; } = "";
    public int? Skill { get; init; }
    public DateTime ObservedAtUtc { get; init; }
    public DateTime? CheckedAtUtc { get; init; }
    public bool RefreshFailed { get; init; }
}
