using ChampionsOfKhazad.Bot.GenAi;
using Discord;

namespace ChampionsOfKhazad.Bot;

public sealed record SpecialsPendingDraft(
    string Token,
    SpecialsDestination Destination,
    Embed Menu,
    string Evidence,
    string Coverage,
    IReadOnlyList<NotebookSource> Sources,
    DateTimeOffset ExpiresAtUtc
)
{
    public IReadOnlyList<SpecialsLoreReference> Lore { get; init; } = [];
}
