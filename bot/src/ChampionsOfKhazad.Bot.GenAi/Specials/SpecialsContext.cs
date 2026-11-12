namespace ChampionsOfKhazad.Bot.GenAi;

public sealed record SpecialsContext(SpecialsEvent Event, IReadOnlyList<NotebookSource> Sources)
{
    public IReadOnlyList<SpecialsLoreReference> Lore { get; init; } = [];
}
