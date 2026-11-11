using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

internal sealed record DiscordHistoryPage(IReadOnlyList<NotebookSource> Sources, ulong? Before, bool Complete, int RawMessages, int ExcludedMessages);
