using JetBrains.Annotations;

namespace ChampionsOfKhazad.Bot.Portal;

[method: UsedImplicitly]
public record UpdateMemberLoreContract(
    string Name,
    string Pronouns,
    string Nationality,
    string MainCharacter,
    string? Biography,
    IReadOnlyList<string>? Aliases,
    IReadOnlyList<string>? Roles
);
