using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public sealed record SpecialsChatBatch(
    IReadOnlyList<NotebookSource> Sources,
    int EligibleChannels,
    int CompleteChannels,
    int FailedChannels,
    int LimitedChannels,
    int HistoryReads,
    int ExcludedMessages
);
