namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public record LoreEntrySnapshot(string Name, string Kind)
{
    public string Content { get; init; } = "";
    public string Pronouns { get; init; } = "Unknown";
    public string Nationality { get; init; } = "Unknown";
    public string MainCharacter { get; init; } = "Unknown";
    public string? Biography { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public IReadOnlyList<string> Roles { get; init; } = [];

    public static LoreEntrySnapshot FromLore(ILore lore) =>
        lore switch
        {
            IMemberLore member => new(member.Name, "member")
            {
                Pronouns = member.Pronouns,
                Nationality = member.Nationality,
                MainCharacter = member.MainCharacter,
                Biography = member.Biography,
                Aliases = member.Aliases,
                Roles = member.Roles,
            },
            IGuildLore guild => new(guild.Name, "guild") { Content = guild.Content },
            _ => throw new NotSupportedException("Unsupported lore type."),
        };
}
