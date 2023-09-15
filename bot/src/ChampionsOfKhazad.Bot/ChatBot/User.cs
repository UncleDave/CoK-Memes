namespace ChampionsOfKhazad.Bot;

public record User
{
    public required ulong Id { get; init; }

    public required string Name { get; init; }
}
