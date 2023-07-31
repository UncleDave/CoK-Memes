namespace ChampionsOfKhazad.Bot.RaidHelper;

public class EventsResponse
{
    public required ushort Pages { get; init; }
    public required ushort CurrentPage { get; init; }
    public required ushort EventCountOverall { get; init; }
    public required ushort EventCountTransmitted { get; init; }
}
