namespace ChampionsOfKhazad.Bot.GenAi;

public record LorekeeperPersonalitySetting(LorekeeperTemperament Temperament, DateTime? ExpiresAtUtc = null)
{
    public string Id { get; init; } = "lorekeeper";
}
