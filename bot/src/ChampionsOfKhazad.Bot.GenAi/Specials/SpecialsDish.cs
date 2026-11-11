namespace ChampionsOfKhazad.Bot.GenAi;

public sealed record SpecialsDish(
    int EventIndex,
    string Name,
    string Description,
    string Price,
    string EventSummary,
    IReadOnlyList<string> SourceUrls
);
