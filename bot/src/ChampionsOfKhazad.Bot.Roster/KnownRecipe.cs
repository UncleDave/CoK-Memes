namespace ChampionsOfKhazad.Bot.Roster;

public record KnownRecipe
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}
