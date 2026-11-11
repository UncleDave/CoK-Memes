namespace ChampionsOfKhazad.Bot.GenAi;

public sealed record SpecialsContext(SpecialsEvent Event, IReadOnlyList<NotebookSource> Sources);
