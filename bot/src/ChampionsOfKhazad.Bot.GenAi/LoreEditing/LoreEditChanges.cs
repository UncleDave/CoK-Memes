namespace ChampionsOfKhazad.Bot.GenAi;

public record LoreEditChanges
{
    public string? Content { get; init; }
    public string? Pronouns { get; init; }
    public string? Nationality { get; init; }
    public string? MainCharacter { get; init; }
    public string? Biography { get; init; }
    public IReadOnlyList<string>? Aliases { get; init; }
    public IReadOnlyList<string>? Roles { get; init; }
}
