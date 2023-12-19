﻿using System.Text;

namespace ChampionsOfKhazad.Bot.Lore;

public record MemberLore(string Name, string Pronouns, string Nationality, string MainCharacter, string? Biography) : Lore(Name)
{
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();

    public override string ToString() =>
        new StringBuilder($"Guild Member\n\nName: {Name}\n")
            .AppendIf(Aliases.Any(), $"Aliases: {string.Join(", ", Aliases)}\n")
            .Append($"Pronouns: {Pronouns}\nNationality: {Nationality}\n")
            .AppendIf(Roles.Any(), $"Roles: {string.Join(", ", Roles)}\n")
            .Append($"Main Character: {MainCharacter}\n")
            .AppendIf(Biography is not null, $"Biography: {Biography}\n")
            .ToString();
}
