namespace ChampionsOfKhazad.Bot.Roster;

public record RosterObservation
{
    public required string GuildName { get; init; }
    public string? GuildId { get; init; }
    public DateTime ObservedAtUtc { get; init; }
    public bool Complete { get; init; }
    public RosterCharacter[] Characters { get; init; } = [];
}
