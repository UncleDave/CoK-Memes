namespace ChampionsOfKhazad.Bot.RaidHelper;

public class PostedEvent
{
    public required string Id { get; init; }
    public required string ChannelId { get; init; }
    public required string LeaderId { get; init; }
    public required string LeaderName { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required ulong StartTime { get; init; }
    public required ulong EndTime { get; init; }
    public required ulong ClosingTime { get; init; }
    public required string TemplateId { get; init; }
    public required string Color { get; init; }
    public required string ImageUrl { get; init; }
    public required string SoftresId { get; init; }
    public required IEnumerable<EventSignUp> SignUps { get; init; }
}
