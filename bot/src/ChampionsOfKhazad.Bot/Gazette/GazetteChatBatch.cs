using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public sealed record GazetteChatBatch(IReadOnlyList<NotebookSource> Sources, int ChannelsRead, int AvailableChannels, int ReadFailures);
