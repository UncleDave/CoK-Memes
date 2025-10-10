namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public interface IMemberLore : ILore
{
    string Pronouns { get; }
    string Nationality { get; }
    string MainCharacter { get; }
    string? Biography { get; }
    IReadOnlyList<string> Aliases { get; }
    IReadOnlyList<string> Roles { get; }
}
