namespace ChampionsOfKhazad.Bot.Roster;

public record ProfessionObservation
{
    public DateTime ObservedAtUtc { get; init; }
    public bool Complete { get; init; }
    public ProfessionSkill[] Skills { get; init; } = [];
}
