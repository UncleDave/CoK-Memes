namespace ChampionsOfKhazad.Bot.GenAi;

public record CustomerDisciplineState
{
    public required string Id { get; init; }
    public long Revision { get; init; }
    public CustomerStrike[] Strikes { get; init; } = [];
    public ulong LastPardonMessageId { get; init; }
    public bool IsBarred => Strikes.Length >= 3;

    public static CustomerDisciplineState Create(ulong guildId, ulong userId) => new() { Id = $"{guildId}:{userId}" };
}
