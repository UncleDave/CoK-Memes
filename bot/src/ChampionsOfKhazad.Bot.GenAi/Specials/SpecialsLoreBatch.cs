namespace ChampionsOfKhazad.Bot.GenAi;

public sealed record SpecialsLoreBatch(IReadOnlyList<SpecialsContext> Contexts, int OmittedEntries);
