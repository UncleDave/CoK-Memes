using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public sealed record GazettePendingDraft(
    string Token,
    GazetteDestination Destination,
    string Edition,
    IReadOnlyList<NotebookSource> Sources,
    DateTimeOffset ExpiresAtUtc,
    long IssueNumber,
    GazettePrintEdition PrintEdition
)
{
    public string IllustrationStatus { get; init; } = "Illustration: no status recorded.";
}
