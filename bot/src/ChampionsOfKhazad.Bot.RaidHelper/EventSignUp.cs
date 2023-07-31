namespace ChampionsOfKhazad.Bot.RaidHelper;

public class EventSignUp
{
    public required string Name { get; init; }
    public required ulong Id { get; init; }
    public required string UserId { get; init; }
    public required string ClassName { get; init; }
    public required string SpecName { get; init; }
    public required ulong EntryTime { get; init; }
}
