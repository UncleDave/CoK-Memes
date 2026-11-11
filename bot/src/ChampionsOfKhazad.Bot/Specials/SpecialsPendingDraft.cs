using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public sealed record SpecialsPendingDraft(
    string Token,
    SpecialsDestination Destination,
    string Menu,
    string Evidence,
    string Coverage,
    IReadOnlyList<NotebookSource> Sources,
    DateTimeOffset ExpiresAtUtc
);
