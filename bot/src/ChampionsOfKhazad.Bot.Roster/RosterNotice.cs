namespace ChampionsOfKhazad.Bot.Roster;

public record RosterNotice
{
    public required string Id { get; init; }
    public required string ScopeId { get; init; }
    public required string CharacterKey { get; init; }
    public required string CharacterName { get; init; }
    public required string TrackKey { get; init; }
    public string? ProfessionName { get; init; }
    public int Threshold { get; init; }
    public bool IsMock { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? SentAtUtc { get; init; }
    public ulong? DiscordMessageId { get; init; }
}
