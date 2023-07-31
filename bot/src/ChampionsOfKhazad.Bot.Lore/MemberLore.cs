using System.Text;
using ChampionsOfKhazad.Bot.OpenAi.Embeddings;

namespace ChampionsOfKhazad.Bot.Lore;

public class MemberLore : IEmbeddable
{
    public required string Name { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    public required string Pronouns { get; init; }
    public required string Nationality { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public required string MainCharacter { get; init; }
    public string? Biography { get; init; }

    public TextEntry ToTextEntry()
    {
        var contentBuilder = new StringBuilder($"Guild Member\n\nName: {Name}\n");

        if (Aliases.Any())
            contentBuilder.Append($"Aliases: {string.Join(", ", Aliases)}\n");

        contentBuilder.Append($"Pronouns: {Pronouns}\nNationality: {Nationality}\n");

        if (Roles.Any())
            contentBuilder.Append($"Roles: {string.Join(", ", Roles)}\n");

        contentBuilder.Append($"Main Character: {MainCharacter}\n");

        if (Biography is not null)
            contentBuilder.Append($"Biography: {Biography}\n");

        return new TextEntry($"member-{Name.ToLowerInvariant()}", contentBuilder.ToString());
    }
}
