namespace ChampionsOfKhazad.Bot.Roster;

public record ProfessionSkill
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Tier { get; init; } = "";
    public int? Skill { get; init; }
    public int? MaxSkill { get; init; }
    public DateTime ObservedAtUtc { get; init; }
    public string TrackKey => $"profession:{Id.Length}:{Id}:{Tier}";
}
